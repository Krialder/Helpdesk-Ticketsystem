using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;

namespace Ticketsystem.Kern.Services;

// Meldet gerissene SLA-Fristen per Mail an Teamleitung und Administration.
// Jedes Ticket wird höchstens einmal gemeldet; der Merker dafür ist
// SlaBreachNotifiedAt am Ticket. Pausierte Konten bekommen nichts.
public class SlaMelder(TicketsystemContext db, UserManager<AppUser> users, ITicketMailer mailer)
{
    public async Task<int> MeldeVerletzungenAsync(DateTime jetzt, CancellationToken ct = default)
    {
        var gerissen = await db.Tickets
            .Where(t => t.SlaBreachNotifiedAt == null
                && t.Status != TicketStatus.Closed
                && ((t.FirstReactionAt == null && t.ReactionDueAt < jetzt)
                    || (t.ResolvedAt == null && t.ResolutionDueAt < jetzt)))
            .ToListAsync(ct);

        if (gerissen.Count == 0)
        {
            return 0;
        }

        var empfaenger = await EmpfaengerAsync(jetzt);
        foreach (var ticket in gerissen)
        {
            var text = Nachricht(ticket, jetzt);
            foreach (var adresse in empfaenger)
            {
                await mailer.SendAsync(
                    adresse,
                    $"[Ticket #{ticket.Id}] SLA-Frist gerissen: {ticket.Title}",
                    text,
                    ct);
            }

            // Auch ohne Empfänger als gemeldet markieren: Sonst sammelt sich eine
            // Warteschlange an, die beim ersten Teamleitungskonto auf einen Schlag
            // herausgeht.
            ticket.SlaBreachNotifiedAt = jetzt;
        }

        await db.SaveChangesAsync(ct);
        return gerissen.Count;
    }

    private static string Nachricht(Ticket ticket, DateTime jetzt)
    {
        var gerissen = new List<string>();
        if (ticket.FirstReactionAt is null && ticket.ReactionDueAt < jetzt)
        {
            gerissen.Add($"Reaktionsfrist, abgelaufen am {ticket.ReactionDueAt:dd.MM.yyyy HH:mm} UTC ({Seit(ticket.ReactionDueAt, jetzt)})");
        }

        if (ticket.ResolvedAt is null && ticket.ResolutionDueAt < jetzt)
        {
            gerissen.Add($"Lösungsfrist, abgelaufen am {ticket.ResolutionDueAt:dd.MM.yyyy HH:mm} UTC ({Seit(ticket.ResolutionDueAt, jetzt)})");
        }

        return $"""
            Bei Ticket #{ticket.Id} ist eine Frist abgelaufen.

            {string.Join(Environment.NewLine, gerissen.Select(z => "- " + z))}

            Titel: {ticket.Title}
            Priorität: {ticket.Priority.Anzeige()}
            Status: {ticket.Status.Anzeige()}
            Zugewiesen an: {ticket.AgentName ?? "niemanden"}

            Hinweis: Maßgeblich ist der Zeitpunkt des Ablaufs oben, nicht der Zeitpunkt dieser Mail.
            Die Anwendung prüft die Fristen nur, solange sie läuft; zwischen dem
            Ablauf und dieser Meldung kann deshalb eine Nacht oder ein
            Wochenende liegen.
            """;
    }

    private static string Seit(DateTime faellig, DateTime jetzt)
    {
        var dauer = jetzt - faellig;
        if (dauer.TotalHours < 1)
        {
            return $"vor {Math.Max(1, (int)dauer.TotalMinutes)} Minuten";
        }

        return dauer.TotalHours < 48
            ? $"vor {(int)dauer.TotalHours} Stunden"
            : $"vor {(int)dauer.TotalDays} Tagen";
    }

    private async Task<IReadOnlyList<string>> EmpfaengerAsync(DateTime jetzt)
    {
        var kandidaten = new List<AppUser>();
        kandidaten.AddRange(await users.GetUsersInRoleAsync(Rollen.TeamLead));
        kandidaten.AddRange(await users.GetUsersInRoleAsync(Rollen.Admin));

        return kandidaten
            .DistinctBy(u => u.Id)
            .Where(u => !u.IstPausiert(jetzt) && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email!)
            .ToList();
    }
}

// Prüft im Hintergrund regelmäßig auf gerissene Fristen: Eine Frist reißt
// auch, wenn niemand hinschaut. Der erste Durchlauf steht vor der ersten
// Wartezeit, denn beim Start ist der Rückstand am größten.
public class SlaMelderService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaOptions> optionen,
    ILogger<SlaMelderService> logger)
    : BackgroundService
{
    private TimeSpan Intervall => TimeSpan.FromMinutes(Math.Max(1, optionen.Value.PruefIntervallMinuten));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var melder = scope.ServiceProvider.GetRequiredService<SlaMelder>();
                var anzahl = await melder.MeldeVerletzungenAsync(DateTime.UtcNow, stoppingToken);
                if (anzahl > 0)
                {
                    logger.LogInformation("{Anzahl} gerissene SLA-Fristen gemeldet.", anzahl);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA-Prüfung fehlgeschlagen.");
            }

            await Task.Delay(Intervall, stoppingToken);
        }
    }
}
