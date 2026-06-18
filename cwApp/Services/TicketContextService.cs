using cwApp.Models;
using System.Text.RegularExpressions;

namespace cwApp.Services;

/// <summary>
/// El "bridge": relaciona un ticket de ConnectWise con datos de ITGlue de forma
/// DETERMINÍSTICA (sin IA). Resuelve la organización por nombre de company y
/// rankea los recursos de ITGlue por coincidencia con las palabras clave del
/// ticket (summary + hilo de notas). Es provider-agnostic: una futura capa de
/// IA (Fase 4) puede consumir este mismo contexto sin reescribir nada.
/// </summary>
public class TicketContextService
{
    private readonly ItGlueService _itGlue;

    public TicketContextService(ItGlueService itGlue) => _itGlue = itGlue;

    /// <summary>True solo si ITGlue está configurado server-side.</summary>
    public bool IsEnabled => _itGlue.IsConfigured;

    public async Task<TicketContext> GetContextAsync(
        TicketSummary ticket, IEnumerable<TicketNote> notes, int maxItems = 8)
    {
        var companyName = ticket.Company?.Name ?? ticket.Company?.Identifier;
        var org = await _itGlue.FindOrganizationAsync(companyName);
        if (org is null)
            return new TicketContext { Message = "No se encontró una organización equivalente en ITGlue." };

        var items = await _itGlue.GetConfigurationsAsync(org.Id);

        var keywords = ExtractKeywords(ticket, notes);
        foreach (var item in items)
            item.Score = ScoreItem(item, keywords);

        var ranked = items
            .OrderByDescending(i => i.Score)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();

        return new TicketContext { Organization = org, RelatedItems = ranked };
    }

    private static HashSet<string> ExtractKeywords(TicketSummary ticket, IEnumerable<TicketNote> notes)
    {
        var text = ticket.Summary + " " + string.Join(" ", notes.Select(n => n.Text));
        var tokens = Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9\-\.]{3,}")
            .Select(m => m.Value)
            .Where(t => !Stopwords.Contains(t));
        return new HashSet<string>(tokens);
    }

    private static int ScoreItem(ItGlueItem item, HashSet<string> keywords)
    {
        var hay = $"{item.Name} {item.Subtitle}".ToLowerInvariant();
        return keywords.Count(k => hay.Contains(k));
    }

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "have", "not", "you", "are", "was",
        "los", "las", "que", "con", "por", "para", "una", "del", "como", "esta", "este", "ticket"
    };
}
