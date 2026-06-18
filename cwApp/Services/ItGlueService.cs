using cwApp.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace cwApp.Services;

/// <summary>
/// Cliente de SOLO LECTURA para la API de ITGlue (JSON:API). La key se inyecta
/// server-side vía <see cref="ItGlueOptions"/>. Por diseño NO consulta /passwords:
/// ningún secreto de clientes sale del backend.
/// </summary>
public class ItGlueService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ItGlueOptions _options;

    public ItGlueService(IHttpClientFactory httpClientFactory, ItGlueOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>Resuelve la organización cuyo nombre coincide (exacto, o la primera) con el dado.</summary>
    public async Task<ItGlueOrg?> FindOrganizationAsync(string? companyName)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(companyName)) return null;

        var url = $"{_options.BaseUrl}/organizations?filter[name]={Uri.EscapeDataString(companyName)}&page[size]=10";
        var data = await GetAsync(url);
        if (data.Count == 0) return null;

        var match = data.FirstOrDefault(r =>
                        string.Equals(r.Attr("name"), companyName, StringComparison.OrdinalIgnoreCase))
                    ?? data[0];

        return new ItGlueOrg
        {
            Id = match.Id,
            Name = match.Attr("name") ?? companyName,
            WebLink = OrgLink(match.Id)
        };
    }

    /// <summary>Devuelve las configuraciones (servidores, equipos, firewalls, etc.) de una organización.</summary>
    public async Task<List<ItGlueItem>> GetConfigurationsAsync(string orgId, int pageSize = 100)
    {
        if (!IsConfigured) return new();

        var url = $"{_options.BaseUrl}/organizations/{orgId}/relationships/configurations?page[size]={pageSize}";
        var data = await GetAsync(url);

        return data.Select(r => new ItGlueItem
        {
            Id = r.Id,
            Name = r.Attr("name") ?? $"Config #{r.Id}",
            Subtitle = r.Attr("configuration-type-name", "hostname", "primary-ip"),
            ResourceType = "Configuration",
            WebLink = ResourceLink(orgId, "configs", r.Id)
        }).ToList();
    }

    private async Task<List<ItGlueResource>> GetAsync(string url)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"ITGlue error {(int)response.StatusCode}: {body}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<ItGlueListResponse>();
        return parsed?.Data ?? new();
    }

    private string? OrgLink(string orgId) =>
        string.IsNullOrWhiteSpace(_options.WebUrl)
            ? null
            : $"{_options.WebUrl!.TrimEnd('/')}/{orgId}";

    private string? ResourceLink(string orgId, string segment, string id) =>
        string.IsNullOrWhiteSpace(_options.WebUrl)
            ? null
            : $"{_options.WebUrl!.TrimEnd('/')}/{orgId}/{segment}/{id}";
}
