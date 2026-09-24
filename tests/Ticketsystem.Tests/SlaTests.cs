using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Fristen kommen aus der Priorität bei Erstellung. Reaktion heißt
// Verlassen von Neu, Lösung heißt Gelöst erreicht, und eine Wiedereröffnung
// löscht den Lösungszeitpunkt.
public sealed class SlaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public SlaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
    }

    [Fact]
    public async Task Fristen_kommen_aus_der_Prioritaet_bei_Erstellung()
    {
        var ticket = await _service.CreateAsync(
            "Server down", "Nichts geht mehr", TicketPriority.Critical, "A. Beispiel", TicketSource.Web);

        // Kritisch: 1 Stunde Reaktion, 4 Stunden Lösung.
        Assert.Equal(ticket.CreatedAt.AddHours(1), ticket.ReactionDueAt);
        Assert.Equal(ticket.CreatedAt.AddHours(4), ticket.ResolutionDueAt);
        Assert.Null(ticket.FirstReactionAt);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public async Task Reaktion_wird_beim_Verlassen_von_Neu_einmalig_festgehalten()
    {
        var ticket = await _service.CreateAsync(
            "Titel", "Text", TicketPriority.Medium, "A. Beispiel", TicketSource.Web);

        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung);
        var nachZuweisung = (await _db.Tickets.SingleAsync(t => t.Id == ticket.Id)).FirstReactionAt;
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.NotNull(nachZuweisung);
        Assert.Equal(nachZuweisung, gespeichert.FirstReactionAt);
    }

    [Fact]
    public async Task Wiedereroeffnung_loescht_den_Loesungszeitpunkt()
    {
        var ticket = await _service.CreateAsync(
            "Titel", "Text", TicketPriority.Medium, "A. Beispiel", TicketSource.Web);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, TestDaten.Teamleitung);
        Assert.NotNull((await _db.Tickets.SingleAsync(t => t.Id == ticket.Id)).ResolvedAt);

        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);

        Assert.Null((await _db.Tickets.SingleAsync(t => t.Id == ticket.Id)).ResolvedAt);
    }

    [Fact]
    public void Zustandslogik_deckt_alle_Faelle_ab()
    {
        var erstellt = new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc);
        var ticket = new Ticket
        {
            CreatedAt = erstellt,
            ReactionDueAt = erstellt.AddHours(4),
            Status = TicketStatus.New
        };

        // Bald fällig heißt weniger als ein Viertel der Frist übrig; erfüllt und
        // verspätet erfüllt messen am Zeitpunkt der Reaktion, nicht am Jetzt;
        // entfällt heißt geschlossen ohne Erfüllung.
        Assert.Equal(SlaState.Laeuft, SlaEvaluation.ReactionState(ticket, erstellt.AddHours(1)));
        Assert.Equal(SlaState.BaldFaellig, SlaEvaluation.ReactionState(ticket, erstellt.AddMinutes(3 * 60 + 30)));
        Assert.Equal(SlaState.Ueberfaellig, SlaEvaluation.ReactionState(ticket, erstellt.AddHours(5)));

        ticket.FirstReactionAt = erstellt.AddHours(2);
        Assert.Equal(SlaState.Erfuellt, SlaEvaluation.ReactionState(ticket, erstellt.AddHours(10)));
        ticket.FirstReactionAt = erstellt.AddHours(6);
        Assert.Equal(SlaState.VerspaetetErfuellt, SlaEvaluation.ReactionState(ticket, erstellt.AddHours(10)));

        ticket.FirstReactionAt = null;
        ticket.Status = TicketStatus.Closed;
        Assert.Equal(SlaState.Entfaellt, SlaEvaluation.ReactionState(ticket, erstellt.AddHours(10)));
    }

    [Fact]
    public async Task Ueberfaellig_Filter_findet_nur_gerissene_Fristen()
    {
        var frisch = await _service.CreateAsync(
            "Frisch", "Text", TicketPriority.Low, "A. Beispiel", TicketSource.Web);
        var ueberfaellig = await _service.CreateAsync(
            "Alt", "Text", TicketPriority.Critical, "A. Beispiel", TicketSource.Web);

        // Die Uhr lässt sich im Test nicht drehen, also wandern die Fristen in die
        // Vergangenheit.
        var alt = await _db.Tickets.SingleAsync(t => t.Id == ueberfaellig.Id);
        alt.ReactionDueAt = DateTime.UtcNow.AddHours(-2);
        alt.ResolutionDueAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, nurUeberfaellige: true)).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(ueberfaellig.Id, einziges.Id);
        Assert.NotEqual(frisch.Id, einziges.Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
