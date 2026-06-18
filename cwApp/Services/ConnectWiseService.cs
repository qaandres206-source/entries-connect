using cwApp.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace cwApp.Services;

public class ConnectWiseService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ConnectWiseService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiResult> PostTimeEntryAsync(
        ConnectWiseConfig config,
        string ticketId,
        double hours,
        string description,
        DateTime date,
        double startHour,
        string billableOption,
        bool addToDetail,
        bool addToInternal,
        bool addToResolution,
        bool emailResource,
        bool emailContact,
        bool emailCc)
    {
        try
        {
            var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
            var url = $"{baseUrl}/time/entries";

            double endHour = startHour + hours;

            var startTime = new DateTime(date.Year, date.Month, date.Day,
                (int)startHour, (int)((startHour % 1) * 60), 0);
            startTime = startTime.AddHours(-config.TimezoneOffset);

            var endTime = new DateTime(date.Year, date.Month, date.Day,
                (int)endHour, (int)((endHour % 1) * 60), 0);
            endTime = endTime.AddHours(-config.TimezoneOffset);

            var timeStart = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var timeEnd = endTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var payload = new
            {
                company = new { identifier = config.CompanyId },
                chargeToId = int.Parse(ticketId),
                chargeToType = "ServiceTicket",
                member = new { identifier = config.MemberId },
                actualHours = hours,
                billableOption = billableOption,
                workType = new { name = config.WorkType },
                notes = description,
                timeStart = timeStart,
                timeEnd = timeEnd,
                addToDetailDescriptionFlag = addToDetail,
                addToInternalAnalysisFlag = addToInternal,
                addToResolutionFlag = addToResolution,
                emailResourceFlag = emailResource,
                emailContactFlag = emailContact,
                emailCcFlag = emailCc
            };

            var client = CreateClient(config);
            var response = await client.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
                return new ApiResult { Success = true, Message = "Entrada creada exitosamente" };

            return new ApiResult { Success = false, Message = await BuildErrorMessageAsync(response) };
        }
        catch (Exception ex)
        {
            return new ApiResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Devuelve los tickets abiertos asignados al miembro configurado.
    /// El filtro usa owner/identifier; según el flujo de la instancia puede
    /// que el campo correcto sea "resources" — ajustar tras validar con datos reales.
    /// </summary>
    public async Task<List<TicketSummary>> GetMyTicketsAsync(ConnectWiseConfig config, int pageSize = 50)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
        var conditions = Uri.EscapeDataString($"closedFlag=false AND owner/identifier=\"{config.MemberId}\"");
        var url = $"{baseUrl}/service/tickets?conditions={conditions}&orderBy=id desc&pageSize={pageSize}";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        return await response.Content.ReadFromJsonAsync<List<TicketSummary>>() ?? new();
    }

    /// <summary>Devuelve el detalle de un ticket por su id.</summary>
    public async Task<TicketSummary?> GetTicketAsync(ConnectWiseConfig config, string ticketId)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
        var url = $"{baseUrl}/service/tickets/{ticketId}";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        return await response.Content.ReadFromJsonAsync<TicketSummary>();
    }

    /// <summary>Devuelve el hilo de notas (discussion / internal / resolution) de un ticket.</summary>
    public async Task<List<TicketNote>> GetTicketNotesAsync(ConnectWiseConfig config, string ticketId, int pageSize = 100)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
        var url = $"{baseUrl}/service/tickets/{ticketId}/notes?orderBy=dateCreated asc&pageSize={pageSize}";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        return await response.Content.ReadFromJsonAsync<List<TicketNote>>() ?? new();
    }

    /// <summary>Crea un HttpClient con la autenticación Basic + clientId de ConnectWise.</summary>
    private HttpClient CreateClient(ConnectWiseConfig config)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {config.GetAuthHeader()}");
        client.DefaultRequestHeaders.Add("clientId", config.ClientId);
        return client;
    }

    /// <summary>Extrae un mensaje de error legible de una respuesta fallida de la API.</summary>
    private static async Task<string> BuildErrorMessageAsync(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        string errorMsg = $"Error {(int)response.StatusCode}";
        try
        {
            var errorJson = JsonDocument.Parse(errorContent);
            if (errorJson.RootElement.TryGetProperty("message", out var msgEl))
                errorMsg += $": {msgEl.GetString()}";
            else
                errorMsg += $": {errorContent}";
        }
        catch { errorMsg += $": {errorContent}"; }
        return errorMsg;
    }
}
