namespace cwApp.Models;

public class ConnectWiseConfig
{
    public string CompanyId { get; set; } = "Intwo";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string SiteUrl { get; set; } = "connect.intwo.cloud";
    public string MemberId { get; set; } = "";
    public string WorkType { get; set; } = "Remote-Standard";
    public string BillableOption { get; set; } = "DoNotBill";
    public string ClientId { get; set; } = "4332716b-7270-470d-b7c6-9c036f760e6f";
    public double TimezoneOffset { get; set; } = -4.0;

    public bool IsComplete() =>
        !string.IsNullOrWhiteSpace(CompanyId) &&
        !string.IsNullOrWhiteSpace(PublicKey) &&
        !string.IsNullOrWhiteSpace(PrivateKey) &&
        !string.IsNullOrWhiteSpace(MemberId) &&
        !string.IsNullOrWhiteSpace(ClientId);

    public string GetAuthHeader()
    {
        var authString = $"{CompanyId}+{PublicKey}:{PrivateKey}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));
    }
}

public class DateEntry
{
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public string StartTime { get; set; } = "08:00";
    public double Hours { get; set; } = 1.0;
    public Guid Id { get; set; } = Guid.NewGuid();
}

public class TimeEntryPayload
{
    public string TicketId { get; set; } = "";
    public string Description { get; set; } = "";
    public string BillableOption { get; set; } = "DoNotBill";
    public bool AddToDetail { get; set; } = false;
    public bool AddToInternal { get; set; } = true;
    public bool AddToResolution { get; set; } = false;
    public bool EmailResource { get; set; } = false;
    public bool EmailContact { get; set; } = false;
    public bool EmailCc { get; set; } = false;
    public List<DateEntry> DateEntries { get; set; } = new();
}

public class SessionLogEntry
{
    public string Message { get; set; } = "";
    public bool IsError { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
