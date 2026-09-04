using System.Text.RegularExpressions;

namespace cwApp.Services;

public enum NoteFragmentKind { Text, Image, Link }

/// <summary>Un trozo de una nota ya clasificado para poder pintarlo (texto, imagen o enlace).</summary>
public sealed class NoteFragment
{
    public NoteFragmentKind Kind { get; init; }

    /// <summary>Texto visible: el contenido para Text, el alt para Image, la etiqueta para Link.</summary>
    public string Text { get; init; } = "";

    /// <summary>URL original y absoluta (solo para Image y Link).</summary>
    public string Url { get; init; } = "";
}

/// <summary>
/// Convierte el cuerpo de una nota de ConnectWise en trozos pintables.
///
/// Las notas llegan con marcado tipo Markdown: ConnectWise inserta las capturas
/// pegadas en un ticket como ![alt](https://.../api/newinlineimages/...), y esa
/// URL exige autenticación, por eso las imágenes se sirven a través del proxy
/// (ver NoteImageProxy) en vez de apuntar directo a ConnectWise.
///
/// No genera HTML: devuelve datos que Razor escapa al pintarlos. El cuerpo de una
/// nota es contenido que puede venir de fuera (un cliente que escribe por correo al
/// ticket), así que nunca se interpola como marcado.
/// </summary>
public static class NoteContentParser
{
    // 1) ![alt](url)  2) [texto](url)  3) http(s)://... suelto
    // El alt y la etiqueta admiten escapes (\[ \]) porque ConnectWise escribe ![\[image\]](...).
    private static readonly Regex Pattern = new(
        @"!\[(?<alt>(?:\\.|[^\]\\])*)\]\((?<iurl>[^)\s]+)\)"
        + @"|\[(?<label>(?:\\.|[^\]\\])*)\]\((?<lurl>[^)\s]+)\)"
        + @"|(?<bare>https?://[^\s<>""'\)]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Puntuación que suele quedar pegada al final de una URL suelta dentro de una frase.
    private static readonly char[] TrailingJunk = { '.', ',', ';', ':', '!', '?', '"', '\'' };

    public static List<NoteFragment> Parse(string? body)
    {
        var result = new List<NoteFragment>();
        if (string.IsNullOrEmpty(body)) return result;

        var cursor = 0;

        foreach (Match m in Pattern.Matches(body))
        {
            if (m.Index > cursor)
                AddText(result, body[cursor..m.Index]);

            if (m.Groups["iurl"].Success)
            {
                result.Add(new NoteFragment
                {
                    Kind = NoteFragmentKind.Image,
                    Url = m.Groups["iurl"].Value,
                    Text = Unescape(m.Groups["alt"].Value) is { Length: > 0 } alt ? alt : "Imagen de la nota"
                });
            }
            else if (m.Groups["lurl"].Success)
            {
                var label = Unescape(m.Groups["label"].Value);
                result.Add(new NoteFragment
                {
                    Kind = NoteFragmentKind.Link,
                    Url = m.Groups["lurl"].Value,
                    Text = label.Length > 0 ? label : m.Groups["lurl"].Value
                });
            }
            else
            {
                var url = m.Groups["bare"].Value.TrimEnd(TrailingJunk);
                if (url.Length == 0) { AddText(result, m.Value); cursor = m.Index + m.Length; continue; }

                result.Add(new NoteFragment { Kind = NoteFragmentKind.Link, Url = url, Text = url });

                // Lo que se recortó de la URL sigue siendo texto de la frase.
                var trimmed = m.Value[url.Length..];
                if (trimmed.Length > 0) AddText(result, trimmed);
            }

            cursor = m.Index + m.Length;
        }

        if (cursor < body.Length)
            AddText(result, body[cursor..]);

        return result;
    }

    /// <summary>true si la nota trae al menos una imagen (para decidir si vale la pena registrar tokens).</summary>
    public static bool HasImages(string? body) =>
        Parse(body).Any(f => f.Kind == NoteFragmentKind.Image);

    private static void AddText(List<NoteFragment> result, string text)
    {
        if (text.Length == 0) return;
        result.Add(new NoteFragment { Kind = NoteFragmentKind.Text, Text = text });
    }

    /// <summary>Quita los escapes de Markdown (\[ → [) del texto visible.</summary>
    private static string Unescape(string s) =>
        s.Contains('\\') ? Regex.Replace(s, @"\\(.)", "$1") : s;
}
