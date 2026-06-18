using System.Text.Json;
using System.Text.Json.Serialization;

namespace cwApp.Models;

/// <summary>
/// Opciones de conexión a ITGlue. La API key vive SERVER-SIDE (variable de entorno),
/// nunca en el navegador / localStorage. Tenant US = https://api.itglue.com,
/// tenant EU = https://api.eu.itglue.com.
/// </summary>
public class ItGlueOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.itglue.com";

    /// <summary>Base web del tenant para deep-links, ej. https://intwo.itglue.com (opcional).</summary>
    public string? WebUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Envoltura JSON:API genérica de ITGlue (lista de recursos).</summary>
public class ItGlueListResponse
{
    [JsonPropertyName("data")]
    public List<ItGlueResource> Data { get; set; } = new();
}

/// <summary>
/// Recurso JSON:API. Los atributos se guardan como diccionario de JsonElement
/// para tolerar los nombres con guiones de ITGlue (ej. "configuration-type-name")
/// sin acoplar el modelo a campos específicos del tenant.
/// </summary>
public class ItGlueResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; set; } = new();

    /// <summary>Lee el primer atributo presente de la lista de candidatos (string o número).</summary>
    public string? Attr(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (Attributes.TryGetValue(key, out var el))
            {
                if (el.ValueKind == JsonValueKind.String) return el.GetString();
                if (el.ValueKind == JsonValueKind.Number) return el.ToString();
            }
        }
        return null;
    }
}

/// <summary>Organización de ITGlue resuelta para una company de ConnectWise.</summary>
public class ItGlueOrg
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? WebLink { get; set; }
}

/// <summary>Un activo/recurso de ITGlue relacionado con un ticket.</summary>
public class ItGlueItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Subtitle { get; set; }
    public string ResourceType { get; set; } = "";
    public string? WebLink { get; set; }
    public int Score { get; set; }
}

/// <summary>Contexto de ITGlue relacionado con un ticket (resultado del bridge).</summary>
public class TicketContext
{
    public ItGlueOrg? Organization { get; set; }
    public List<ItGlueItem> RelatedItems { get; set; } = new();

    /// <summary>Mensaje informativo cuando no hay organización/datos (ej. sin coincidencia).</summary>
    public string? Message { get; set; }
}
