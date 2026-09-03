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

    /// <summary>
    /// Momento de la nota YA CONVERTIDO a la zona horaria del usuario
    /// (ConnectWise entrega todo en UTC; el servicio aplica TimezoneOffset al leer).
    /// Para notas del ticket es cuándo se escribió; para notas de una entrada de
    /// tiempo es el inicio del trabajo (timeStart), que es lo que muestra ConnectWise.
    /// </summary>
    public DateTime? DateCreated { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Fin del trabajo, solo para notas que vienen de una entrada de tiempo.
    /// Permite mostrar el rango "08:00–08:15" igual que ConnectWise. No viene de la API.
    /// </summary>
    [JsonIgnore]
    public DateTime? DateEnd { get; set; }

    /// <summary>
    /// Origen de la nota dentro del hilo: "ticket" (nota propia del ticket / ServiceNote)
    /// o "tiempo" (nota que vive dentro de una entrada de tiempo). No viene de la API.
    /// </summary>
    [JsonIgnore]
    public string Source { get; set; } = "ticket";

    /// <summary>Etiqueta de fecha/hora para la UI y los reportes, en hora del usuario.</summary>
    [JsonIgnore]
    public string WhenLabel =>
        DateCreated is null ? ""
        : DateEnd is DateTime end && end > DateCreated
            ? $"{DateCreated:yyyy-MM-dd HH:mm}–{end:HH:mm}"
            : $"{DateCreated:yyyy-MM-dd HH:mm}";
}

/// <summary>
/// Entrada de tiempo de ConnectWise. Sus notas también se muestran en el hilo del
/// ticket (pestañas Discussion/Internal/Resolution), pero NO las devuelve el endpoint
/// /service/tickets/{id}/notes, por eso hay que leerlas aparte y fusionarlas.
/// </summary>
public class TimeEntryItem
{
    public int Id { get; set; }
    public string? Notes { get; set; }
    public bool AddToDetailDescriptionFlag { get; set; }
    public bool AddToInternalAnalysisFlag { get; set; }
    public bool AddToResolutionFlag { get; set; }
    public TicketMember? Member { get; set; }
    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public DateTime? DateEntered { get; set; }
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
