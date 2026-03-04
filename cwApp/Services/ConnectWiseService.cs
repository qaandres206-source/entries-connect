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

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {config.GetAuthHeader()}");
            client.DefaultRequestHeaders.Add("clientId", config.ClientId);

            var response = await client.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                return new ApiResult { Success = true, Message = "Entrada creada exitosamente" };
            }
            else
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

                return new ApiResult { Success = false, Message = errorMsg };
            }
        }
        catch (Exception ex)
        {
            return new ApiResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}
