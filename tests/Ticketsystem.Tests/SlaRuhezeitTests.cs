using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Das sechsstufige Fristmodell, die reine Fristberechnung mit Ruhezeiten
// und die Neuberechnung offener Fristen, wenn eine Ruhezeit dazukommt.
public sealed class SlaRuhezeitTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _tickets;
    private readonly RuhezeitService _ruhezeiten;

    public SlaRuhezeitTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _tickets = new TicketService(_db);
        _ruhezeiten = new RuhezeitService(_db);
    }

    private static readonly DateTime Start = new(2026, 8, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Frist_ohne_Ruhezeit_ist_die_schlichte_Summe() =>
        Assert.Equal(Start.AddHours(8), SlaRechner.Faelligkeit(Start, 8, []));

    [Fact]
    public void Ruhezeit_im_Fristzeitraum_verschiebt_um_ihre_Dauer()
    {
        var ruhe = new Ruhefenster(Start.AddHours(2), Start.AddHours(4));

        Assert.Equal(Start.AddHours(10), SlaRechner.Faelligkeit(Start, 8, [ruhe]));
    }

    [Fact]
    public void Ruhezeit_vor_dem_Start_wirkt_nicht()
    {
        var ruhe = new Ruhefenster(Start.AddHours(-5), Start.AddHours(-1));

        Assert.Equal(Start.AddHours(8), SlaRechner.Faelligkeit(Start, 8, [ruhe]));
    }

    [Fact]
    public void Ticket_das_in_die_Ruhe_faellt_rechnet_ab_Ruheende()
    {
        var ruhe = new Ruhefenster(Start.AddHours(2), Start.AddHours(26));

        Assert.Equal(Start.AddHours(32), SlaRechner.Faelligkeit(Start, 8, [ruhe]));
    }

    [Fact]
    public void Zustandsmodell_unterscheidet_erfuellt_und_verspaetet_erfuellt()
    {
        var ticket = new Ticket
        {
            CreatedAt = Start,
            ReactionDueAt = Start.AddHours(4),
            ResolutionDueAt = Start.AddHours(8),
            Status = TicketStatus.Resolved,
            FirstReactionAt = Start.AddHours(1),
            ResolvedAt = Start.AddHours(9)
        };

        Assert.Equal(SlaState.Erfuellt, SlaEvaluation.ReactionState(ticket, Start.AddHours(10)));
        Assert.Equal(SlaState.VerspaetetErfuellt, SlaEvaluation.ResolutionState(ticket, Start.AddHours(10)));
        // Die Listenspalte zeigt den dringlicheren der beiden Zustände.
        Assert.Equal(SlaState.VerspaetetErfuellt, SlaEvaluation.Schlechtester(ticket, Start.AddHours(10)));
    }

    [Fact]
    public void Offene_gerissene_Frist_heisst_ueberfaellig()
    {
        var ticket = new Ticket
        {
            CreatedAt = Start,
            ReactionDueAt = Start.AddHours(4),
            ResolutionDueAt = Start.AddHours(8),
            Status = TicketStatus.New
        };

        Assert.Equal(SlaState.Ueberfaellig, SlaEvaluation.ReactionState(ticket, Start.AddHours(5)));
    }

    [Fact]
    public async Task Neue_Ruhezeit_verschiebt_offene_Fristen_und_laesst_gerissene_stehen()
    {
        var offen = await _tickets.CreateAsync("Offen", "Text", TicketPriority.Low,
            "A. Beispiel", TicketSource.Web);
        var gerissen = await _tickets.CreateAsync("Gerissen", "Text", TicketPriority.Low,
            "A. Beispiel", TicketSource.Web);

        // Das zweite Ticket altert künstlich, damit seine Frist schon gerissen ist.
        var alt = await _db.Tickets.SingleAsync(t => t.Id == gerissen.Id);
        var alteFrist = DateTime.UtcNow.AddHours(-1);
        alt.ReactionDueAt = alteFrist;
        await _db.SaveChangesAsync();

        var vorher = (await _db.Tickets.SingleAsync(t => t.Id == offen.Id)).ReactionDueAt;
        await _ruhezeiten.AnlegenAsync("Betriebsruhe", DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(5), TestDaten.Teamleitung);

        var nachher = await _db.Tickets.SingleAsync(t => t.Id == offen.Id);
        var unveraendert = await _db.Tickets.SingleAsync(t => t.Id == gerissen.Id);
        Assert.True(nachher.ReactionDueAt > vorher);
        Assert.Equal(alteFrist, unveraendert.ReactionDueAt);
    }

    [Fact]
    public async Task Bearbeiter_pflegt_keine_Ruhezeiten()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _ruhezeiten.AnlegenAsync("Ferien", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Ruhezeit_mit_Ende_vor_Beginn_wird_abgewiesen()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _ruhezeiten.AnlegenAsync("Unsinn", DateTime.UtcNow.AddDays(1), DateTime.UtcNow, TestDaten.Teamleitung));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
