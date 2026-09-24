using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;

namespace Ticketsystem.Kern.Services;

// Schickt dem Kunden die Eingangsbestätigung zu einem neu erfassten Ticket.
// Eigener Dienst statt Teil von TicketService, weil ein gescheiterter
// Versand die Erfassung nicht kosten darf: Der Fehler landet nur im
// Protokoll, der Rückgabewert sagt, ob etwas hinausging.
public sealed class BestaetigungsMelder(ITicketMailer mailer, ILogger<BestaetigungsMelder> log)
{
    public async Task<bool> VersendenAsync(Ticket ticket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticket.CustomerEmail))
        {
            return false;
        }

        try
        {
            await mailer.SendAsync(
                ticket.CustomerEmail,
                $"[Ticket #{ticket.Id}] Eingangsbestätigung: {ticket.Title}",
                $"""
                Guten Tag,

                Ihre Anfrage ist bei uns eingegangen und hat die Ticketnummer #{ticket.Id} erhalten.
                Wir melden uns, sobald jemand die Bearbeitung übernimmt.

                Titel: {ticket.Title}
                Aufgenommen am: {ticket.CreatedAt:dd.MM.yyyy HH:mm} UTC

                Bitte nennen Sie die Ticketnummer, wenn Sie sich zu diesem Vorgang melden.

                Ihr Support-Team
                """,
                ct);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Eingangsbestätigung für Ticket #{Id} konnte nicht versandt werden.", ticket.Id);
            return false;
        }
    }
}
