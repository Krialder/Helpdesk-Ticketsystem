using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

// Pflegt Ferien und Betriebsruhe, die bei der Fristberechnung nicht
// mitzählen. Anlegen und Löschen rechnen die offenen Fälligkeiten neu;
// gerissene Fristen bleiben gerissen, weil eine rückwirkende Schönung
// die SLA-Auswertung entwertete.
public class RuhezeitService(TicketsystemContext db, IOptions<SlaOptions>? slaOptions = null)
{
    private readonly SlaOptions _sla = slaOptions?.Value ?? new SlaOptions();

    public Task<List<Ruhezeit>> ListAsync() =>
        db.Ruhezeiten.OrderByDescending(r => r.Von).ToListAsync();

    public async Task<Ruhezeit> AnlegenAsync(string bezeichnung, DateTime von, DateTime bis, Akteur akteur)
    {
        RequireTeamleitung(akteur);

        if (bis <= von)
        {
            throw new InvalidOperationException("Das Ende der Ruhezeit muss nach ihrem Beginn liegen.");
        }

        var ruhezeit = new Ruhezeit
        {
            Bezeichnung = (bezeichnung ?? string.Empty).Trim(),
            Von = DateTime.SpecifyKind(von, DateTimeKind.Utc),
            Bis = DateTime.SpecifyKind(bis, DateTimeKind.Utc),
            AngelegtVon = akteur.Name,
            CreatedAt = DateTime.UtcNow
        };

        db.Ruhezeiten.Add(ruhezeit);
        await db.SaveChangesAsync();
        await FristenNeuRechnenAsync(akteur);
        return ruhezeit;
    }

    public async Task<bool> LoeschenAsync(int id, Akteur akteur)
    {
        RequireTeamleitung(akteur);

        var ruhezeit = await db.Ruhezeiten.SingleOrDefaultAsync(r => r.Id == id);
        if (ruhezeit is null)
        {
            return false;
        }

        db.Ruhezeiten.Remove(ruhezeit);
        await db.SaveChangesAsync();
        await FristenNeuRechnenAsync(akteur);
        return true;
    }

    public async Task<int> FristenNeuRechnenAsync(Akteur akteur)
    {
        RequireTeamleitung(akteur);

        var jetzt = DateTime.UtcNow;
        var fenster = (await db.Ruhezeiten.Select(r => new { r.Von, r.Bis }).ToListAsync())
            .Select(r => new Ruhefenster(r.Von, r.Bis))
            .ToList();

        var offene = await db.Tickets
            .Where(t => t.Status != TicketStatus.Closed)
            .ToListAsync();

        var geaendert = 0;
        foreach (var ticket in offene)
        {
            var window = _sla.For(ticket.Priority);
            var beruehrt = false;

            // Angefasst wird nur, was weder erfüllt noch gerissen ist; gerechnet
            // wird ab Erstellung, weil die Uhr des Kunden seit dem Eingang läuft.
            if (ticket.FirstReactionAt is null && ticket.ReactionDueAt > jetzt)
            {
                var neu = SlaRechner.Faelligkeit(ticket.CreatedAt, window.ReactionHours, fenster);
                if (neu != ticket.ReactionDueAt)
                {
                    ticket.ReactionDueAt = neu;
                    beruehrt = true;
                }
            }

            if (ticket.ResolvedAt is null && ticket.ResolutionDueAt > jetzt)
            {
                var neu = SlaRechner.Faelligkeit(ticket.CreatedAt, window.ResolutionHours, fenster);
                if (neu != ticket.ResolutionDueAt)
                {
                    ticket.ResolutionDueAt = neu;
                    beruehrt = true;
                }
            }

            if (beruehrt)
            {
                geaendert++;
            }
        }

        await db.SaveChangesAsync();
        return geaendert;
    }

    private static void RequireTeamleitung(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Teamleitung))
        {
            throw new InvalidOperationException("Ruhezeiten pflegen dürfen Teamleitung und Administration.");
        }
    }
}
