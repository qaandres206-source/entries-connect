using System.Text.Json.Serialization;

namespace cwApp.Models;

/// <summary>
/// Resumen de un ticket de servicio de ConnectWise, tal como lo devuelve
/// GET /service/tickets. Los nombres de propiedad mapean a camelCase
/// automáticamente (ReadFromJsonAsync usa JsonSerializerDefaults.Web).
/// </summary>
public class TicketSummary
{
    public int Id { get; set; }
    public string Summary { get; set; } = "";
    public TicketStatus? Status { get; set; }
    public TicketCompany? Company { get; set; }
    public TicketBoard? Board { get; set; }
    public TicketPriority? Priority { get; set; }

    [JsonPropertyName("_info")]
    public TicketInfo? Info { get; set; }
}

public class TicketStatus
{
    public string Name { get; set; } = "";
}

public class TicketCompany
{
    public int Id { get; set; }
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TicketBoard
{
    public string Name { get; set; } = "";
}

public class TicketPriority
{
    public string Name { get; set; } = "";
}

public class TicketInfo
{
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Una nota del hilo de un ticket (GET /service/tickets/{id}/notes).
/// Los flags indican en qué sección de ConnectWise vive la nota.
/// </summary>
public class TicketNote
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public bool DetailDescriptionFlag { get; set; }
    public bool InternalAnalysisFlag { get; set; }
    public bool ResolutionFlag { get; set; }
    public TicketMember? Member { get; set; }
    public DateTime? DateCreated { get; set; }
    public string? CreatedBy { get; set; }
}

public class TicketMember
{
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Company de ConnectWise (para el desplegable de filtro de tickets).</summary>
public class CompanyItem
{
    public int Id { get; set; }
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
}
