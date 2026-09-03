using ClosedXML.Excel;
using cwApp.Models;

namespace cwApp.Services;

/// <summary>
/// Genera reportes .xlsx a partir de los datos del panel. El archivo se produce
/// en memoria y se entrega al navegador como descarga (sin envío por email).
/// </summary>
public class ReportService
{
    /// <summary>Construye un .xlsx con el listado de tickets indicado.</summary>
    public byte[] BuildTicketsReport(IEnumerable<TicketSummary> tickets, string memberId)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Mis Tickets");

        // Encabezado informativo
        ws.Cell(1, 1).Value = "Reporte de tickets";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Miembro: {memberId}";
        ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}";

        // Cabecera de la tabla
        const int headerRow = 5;
        string[] headers = { "Ticket", "Resumen", "Estado", "Compañía", "Board", "Prioridad", "Última actualización" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EDFF");
        }

        int row = headerRow + 1;
        foreach (var t in tickets)
        {
            ws.Cell(row, 1).Value = t.Id;
            ws.Cell(row, 2).Value = t.Summary;
            ws.Cell(row, 3).Value = t.Status?.Name ?? "";
            ws.Cell(row, 4).Value = t.Company?.Name ?? t.Company?.Identifier ?? "";
            ws.Cell(row, 5).Value = t.Board?.Name ?? "";
            ws.Cell(row, 6).Value = t.Priority?.Name ?? "";
            ws.Cell(row, 7).Value = t.Info?.LastUpdated?.ToString("yyyy-MM-dd HH:mm") ?? "";
            row++;
        }

        if (row > headerRow + 1)
            ws.Range(headerRow, 1, row - 1, headers.Length).SetAutoFilter();

        ws.Column(2).Width = 60;
        ws.Columns(1, headers.Length).AdjustToContents(1, headerRow + 1, 60);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Construye un .xlsx con el detalle de un ticket y su hilo de notas.</summary>
    public byte[] BuildTicketDetailReport(TicketSummary ticket, IEnumerable<TicketNote> notes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet($"Ticket {ticket.Id}");

        ws.Cell(1, 1).Value = $"Ticket #{ticket.Id}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = ticket.Summary;
        ws.Cell(3, 1).Value = $"Estado: {ticket.Status?.Name} | Compañía: {ticket.Company?.Name ?? ticket.Company?.Identifier}";
        ws.Cell(4, 1).Value = $"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}";

        const int headerRow = 6;
        string[] headers = { "Fecha", "Tipo", "Autor", "Nota" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EDFF");
        }

        int row = headerRow + 1;
        foreach (var n in notes)
        {
            ws.Cell(row, 1).Value = n.WhenLabel;
            ws.Cell(row, 2).Value = n.ResolutionFlag ? "Resolution"
                                  : n.InternalAnalysisFlag ? "Internal"
                                  : n.DetailDescriptionFlag ? "Discussion" : "Nota";
            ws.Cell(row, 3).Value = n.Member?.Name ?? n.CreatedBy ?? "";
            ws.Cell(row, 4).Value = n.Text;
            ws.Cell(row, 4).Style.Alignment.WrapText = true;
            row++;
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 24;
        ws.Column(4).Width = 90;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
