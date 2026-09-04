using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using cwApp.Models;

namespace cwApp.Services;

/// <summary>
/// Sirve las imágenes incrustadas en las notas de ConnectWise.
///
/// Por qué hace falta: esas URLs (…/api/newinlineimages/…) exigen autenticación, así
/// que un &lt;img src="https://connect.intwo.cloud/…"&gt; en el navegador no cargaría nada.
/// El navegador pide /note-image/{token} a esta app, y el servidor —que ya tiene las
/// credenciales del usuario en memoria durante su sesión— hace la petición autenticada.
///
/// SEGURIDAD — el cuerpo de una nota es contenido no confiable: un cliente puede escribir
/// al ticket por correo y su texto acaba siendo una nota. Si aceptáramos cualquier URL,
/// una nota con ![x](https://servidor-del-atacante/) haría que el servidor enviara la
/// Basic auth del usuario a ese servidor. Por eso <see cref="TryRegister"/> solo admite
/// URLs https del mismo host de ConnectWise que el usuario tiene configurado; cualquier
/// otra se pinta como enlace y nunca se carga sola.
/// </summary>
public sealed class NoteImageProxy
{
    private sealed record Entry(string Url, string AuthHeader, string ClientId, DateTimeOffset Expires);

    private readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.Ordinal);

    /// <summary>Secreto por proceso: hace que los tokens no se puedan adivinar ni fabricar desde fuera.</summary>
    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);

    private const int MaxEntries = 2_000;
    private const long MaxImageBytes = 20L * 1024 * 1024;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    /// <summary>
    /// Registra una imagen y devuelve su token, o null si la URL no es del ConnectWise
    /// del usuario (en ese caso la UI la muestra como enlace, sin cargarla).
    /// El token es determinista para el mismo usuario+imagen, así el navegador puede
    /// cachearla entre recargas del hilo en vez de volver a pedirla.
    /// </summary>
    public string? TryRegister(string imageUrl, ConnectWiseConfig config)
    {
        if (!IsOwnConnectWiseUrl(imageUrl, config.SiteUrl)) return null;

        var auth = $"Basic {config.GetAuthHeader()}";
        var token = DeriveToken(imageUrl, auth);

        _store[token] = new Entry(imageUrl, auth, config.ClientId, DateTimeOffset.UtcNow.Add(Ttl));
        if (_store.Count > MaxEntries) Evict();

        return token;
    }

    /// <summary>
    /// Descarga la imagen del token. Devuelve null si el token no existe o caducó
    /// (p. ej. tras un reinicio del contenedor en Render: basta con recargar el hilo).
    /// </summary>
    public async Task<(byte[] Bytes, string ContentType)?> FetchAsync(
        string token, IHttpClientFactory factory, CancellationToken ct)
    {
        if (!_store.TryGetValue(token, out var entry)) return null;
        if (entry.Expires < DateTimeOffset.UtcNow) { _store.TryRemove(token, out _); return null; }

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        using var request = new HttpRequestMessage(HttpMethod.Get, entry.Url);
        request.Headers.TryAddWithoutValidation("Authorization", entry.AuthHeader);
        request.Headers.TryAddWithoutValidation("clientId", entry.ClientId);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return null;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return null;   // ConnectWise devolvió un login o un error, no una imagen.

        if (response.Content.Headers.ContentLength > MaxImageBytes) return null;

        var bytes = await ReadCappedAsync(response, ct);
        return bytes is null ? null : (bytes, contentType);
    }

    /// <summary>
    /// Solo se aceptan URLs https del mismo host de ConnectWise que el usuario configuró.
    /// Es lo que impide que una nota manipulada haga viajar sus credenciales a otro servidor.
    /// </summary>
    public static bool IsOwnConnectWiseUrl(string imageUrl, string siteUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(siteUrl)) return false;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;   // https://algo@host/… despista al leerlo
        if (!uri.IsDefaultPort) return false;

        // SiteUrl se guarda como host pelado ("connect.intwo.cloud"), pero toleramos
        // que alguien lo haya escrito con esquema o con una barra al final.
        var configured = siteUrl.Trim();
        if (!Uri.TryCreate(configured.Contains("://") ? configured : $"https://{configured}",
                           UriKind.Absolute, out var siteUri)) return false;

        return string.Equals(uri.Host, siteUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private string DeriveToken(string url, string auth)
    {
        var mac = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes($"{url}\n{auth}"));
        return Convert.ToBase64String(mac).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Descarta lo caducado y, si aún sobra, lo más próximo a caducar.</summary>
    private void Evict()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (k, v) in _store)
            if (v.Expires < now) _store.TryRemove(k, out _);

        if (_store.Count <= MaxEntries) return;

        foreach (var k in _store.OrderBy(kv => kv.Value.Expires)
                                .Take(_store.Count - MaxEntries)
                                .Select(kv => kv.Key).ToList())
            _store.TryRemove(k, out _);
    }

    /// <summary>Lee el cuerpo sin pasar del tope, por si la respuesta no declara Content-Length.</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();

        var chunk = new byte[81_920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxImageBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
