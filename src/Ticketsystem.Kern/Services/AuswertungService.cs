using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public sealed record AuswertungsZeile(string Schluessel, int Anzahl);

public sealed record WochenZeile(DateTime Beginn, int Anzahl);

public sealed record TagZeile(DateTime Tag, int Eingegangen, int Erledigt)
{
    // Formatiert hier, weil kein Fenster einen Zeitpunkt selbst formatiert;
    // ein Wächtertest hält das fest.
    public string Beschriftung => Tag.ToString("dd.MM.", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record Auswertung(
    int Gesamt,
    int OhneRiss,
    IReadOnlyList<WochenZeile> JeWoche,
    IReadOnlyList<AuswertungsZeile> JeRaum,
    IReadOnlyList<AuswertungsZeile> JePrioritaet,
    IReadOnlyList<AuswertungsZeile> JeZustand,
    IReadOnlyList<TagZeile> JeTag);

// Die Auswertungsseite der Teamleitung: zählt die Vorgänge eines Zeitraums
// nach Woche, Raum, Priorität, SLA-Zustand und Tag. Die SLA-Zustände sind
// nirgends gespeichert; sie werden hier aus Fristen und Uhr gerechnet.
public sealed class AuswertungService(TicketsystemContext db)
{
    public async Task<Auswertung> ErstellenAsync(Akteur akteur, int wochen, DateTime jetzt)
    {
        if (!akteur.IstMindestens(RoleLevel.Teamleitung))
        {
            throw new InvalidOperationException("Die Auswertung ist der Teamleitung vorbehalten.");
        }

        var beginn = jetzt.AddDays(-7 * wochen);
        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= beginn)
            .ToListAsync();

        var zustaende = tickets
            .Select(t => SlaEvaluation.Schlechtester(t, jetzt))
            .ToList();

        // Erledigt zählt über alle Vorgänge, auch die vor dem Zeitraum angelegten:
        // Ein alter Vorgang, der heute gelöst wird, ist heute erledigt. Jeder Tag
        // des Zeitraums bekommt eine Zeile, auch ohne Vorgang.
        var tage = 7 * wochen;
        var ersterTag = jetzt.Date.AddDays(-(tage - 1));
        var erledigte = await db.Tickets.AsNoTracking()
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= ersterTag)
            .Select(t => t.ResolvedAt!.Value)
            .ToListAsync();
        var jeTag = Enumerable.Range(0, tage)
            .Select(i => ersterTag.AddDays(i))
            .Select(tag => new TagZeile(tag,
                tickets.Count(t => t.CreatedAt.Date == tag),
                erledigte.Count(e => e.Date == tag)))
            .ToList();

        return new Auswertung(
            tickets.Count,
            zustaende.Count(z => z is not (SlaState.Ueberfaellig or SlaState.VerspaetetErfuellt)),
            tickets
                .GroupBy(t => Wochenbeginn(t.CreatedAt))
                .OrderByDescending(g => g.Key)
                .Select(g => new WochenZeile(g.Key, g.Count()))
                .ToList(),
            tickets
                .GroupBy(t => t.Address ?? "ohne Adresse")
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new AuswertungsZeile(g.Key, g.Count()))
                .ToList(),
            tickets
                .GroupBy(t => t.Priority)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => new AuswertungsZeile(g.Key.Anzeige(), g.Count()))
                .ToList(),
            zustaende
                .GroupBy(z => z)
                .OrderByDescending(g => g.Count())
                .Select(g => new AuswertungsZeile(g.Key.Anzeige(), g.Count()))
                .ToList(),
            jeTag);
    }

    // Montag als Wochenbeginn: Gefragt ist „wie viele je Kalenderwoche", nicht
    // „je sieben Tage ab heute".
    private static DateTime Wochenbeginn(DateTime zeitpunkt) =>
        zeitpunkt.Date.AddDays(-(((int)zeitpunkt.DayOfWeek + 6) % 7));
}
