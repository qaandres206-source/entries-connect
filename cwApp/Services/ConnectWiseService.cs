using cwApp.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    /// Devuelve tickets abiertos según el filtro:
    /// - includeUnassigned = false → solo donde el miembro es Resource (resources contains MemberId),
    ///   que es como aparecen tus tickets en el Service Board (antes usábamos owner/identifier, que
    ///   solo trae los tickets de los que eres dueño).
    /// - companyId != null → acota a esa company (permite ver tickets de una company aunque no sean tuyos).
    /// </summary>
    public async Task<List<TicketSummary>> GetTicketsAsync(
        ConnectWiseConfig config, int? companyId = null, bool includeUnassigned = false,
        string? keyword = null, int pageSize = 100)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";

        var conds = new List<string> { "closedFlag=false" };
        if (!includeUnassigned)
            conds.Add($"resources contains \"{config.MemberId}\"");
        if (companyId is not null)
            conds.Add($"company/id={companyId}");
        if (!string.IsNullOrWhiteSpace(keyword))
            conds.Add($"summary contains \"{keyword.Replace("\"", "").Trim()}\"");

        var conditions = Uri.EscapeDataString(string.Join(" AND ", conds));
        var url = $"{baseUrl}/service/tickets?conditions={conditions}&orderBy=id desc&pageSize={pageSize}";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        return await response.Content.ReadFromJsonAsync<List<TicketSummary>>() ?? new();
    }

    /// <summary>Devuelve las companies (para el desplegable de filtro).</summary>
    public async Task<List<CompanyItem>> GetCompaniesAsync(ConnectWiseConfig config, int pageSize = 1000)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
        var conditions = Uri.EscapeDataString("deletedFlag=false");
        var url = $"{baseUrl}/company/companies?conditions={conditions}&orderBy=name asc&pageSize={pageSize}&fields=id,identifier,name";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        return await response.Content.ReadFromJsonAsync<List<CompanyItem>>() ?? new();
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

    /// <summary>
    /// Devuelve el hilo COMPLETO del ticket: las notas propias del ticket (ServiceNote)
    /// MÁS las notas que viven dentro de entradas de tiempo, fusionadas, sin duplicados
    /// y ordenadas cronológicamente.
    ///
    /// Por qué: una nota creada desde esta app se escribe como parte de un time entry
    /// (POST /time/entries con addTo*Flag). ConnectWise la muestra en las pestañas
    /// Discussion/Internal/Resolution del ticket, pero NO la devuelve
    /// GET /service/tickets/{id}/notes — por eso antes "desaparecían" del hilo.
    /// </summary>
    public async Task<List<TicketNote>> GetTicketThreadAsync(ConnectWiseConfig config, string ticketId)
    {
        var notes = await GetTicketNotesAsync(config, ticketId);

        List<TicketNote> fromTimeEntries;
        try
        {
            fromTimeEntries = await GetTimeEntryNotesAsync(config, ticketId);
        }
        catch
        {
            // Best-effort: si la consulta de time entries falla, el hilo sigue mostrando
            // al menos las notas propias del ticket en vez de romperse por completo.
            fromTimeEntries = new();
        }

        return MergeThread(notes, fromTimeEntries);
    }

    /// <summary>
    /// Fusiona las notas del ticket con las de entradas de tiempo, descarta duplicados
    /// y ordena cronológicamente. Estático y público para poder probarlo sin red.
    /// Ante un duplicado gana la nota del ticket (llega primero al HashSet).
    /// </summary>
    public static List<TicketNote> MergeThread(List<TicketNote> notes, List<TicketNote> fromTimeEntries)
    {
        var merged = new List<TicketNote>(notes);
        var keys = new HashSet<string>(notes.Select(DedupeKey));

        foreach (var n in fromTimeEntries)
        {
            if (keys.Add(DedupeKey(n)))
                merged.Add(n);
        }

        return merged.OrderBy(n => n.DateCreated ?? DateTime.MinValue).ToList();
    }

    /// <summary>
    /// Notas provenientes de las entradas de tiempo cargadas a un ticket, normalizadas
    /// al mismo modelo que las notas del ticket para poder mostrarlas en el mismo hilo.
    /// </summary>
    public async Task<List<TicketNote>> GetTimeEntryNotesAsync(ConnectWiseConfig config, string ticketId, int pageSize = 200)
    {
        var baseUrl = $"https://{config.SiteUrl}/v4_6_release/apis/3.0";
        var conditions = Uri.EscapeDataString($"chargeToId={ticketId} AND chargeToType=\"ServiceTicket\"");
        var url = $"{baseUrl}/time/entries?conditions={conditions}&orderBy=dateEntered asc&pageSize={pageSize}";

        var client = CreateClient(config);
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildErrorMessageAsync(response));

        var entries = await response.Content.ReadFromJsonAsync<List<TimeEntryItem>>() ?? new();

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Notes))
            .Select(e => new TicketNote
            {
                Id = e.Id,
                Text = e.Notes!,
                DetailDescriptionFlag = e.AddToDetailDescriptionFlag,
                InternalAnalysisFlag = e.AddToInternalAnalysisFlag,
                ResolutionFlag = e.AddToResolutionFlag,
                Member = e.Member,
                DateCreated = e.TimeStart ?? e.DateEntered,
                Source = "tiempo"
            })
            .ToList();
    }

    /// <summary>
    /// Clave de deduplicación: mismo texto + mismo autor + mismo minuto ⇒ es la misma nota.
    /// Deliberadamente conservadora (incluye el minuto) para no ocultar notas legítimas
    /// que repitan el texto en momentos distintos.
    /// </summary>
    private static string DedupeKey(TicketNote n)
    {
        var text = Regex.Replace(n.Text ?? "", @"\s+", " ").Trim().ToLowerInvariant();
        var author = (n.Member?.Name ?? n.CreatedBy ?? "").Trim().ToLowerInvariant();
        var when = n.DateCreated?.ToString("yyyy-MM-dd HH:mm") ?? "";
        return $"{text}|{author}|{when}";
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
