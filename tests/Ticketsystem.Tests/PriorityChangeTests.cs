using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ohne Umstufung liefen E-Mail-Tickets für immer mit den Mittel-Fristen, und
// die Priorisierung war für den automatischen Eingangsweg nur auf dem Papier
// erfüllt.
public sealed class PriorityChangeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public PriorityChangeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
    }

    [Fact]
    public async Task Umstufung_berechnet_die_Fristen_ab_Erstellung_neu()
    {
        var ticket = await _service.CreateAsync(
            "Server down", "Alles steht", TicketPriority.Medium, "kunde@example.net", TicketSource.Email);

        await _service.ChangePriorityAsync(ticket.Id, TicketPriority.Critical, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketPriority.Critical, gespeichert.Priority);
        // Kritisch-Fristen (1 und 4 Stunden) rechnen ab Erstellung, nicht ab
        // Umstufung: Die Uhr des Kunden läuft seit Eingang.
        Assert.Equal(gespeichert.CreatedAt.AddHours(1), gespeichert.ReactionDueAt);
        Assert.Equal(gespeichert.CreatedAt.AddHours(4), gespeichert.ResolutionDueAt);
        var eintrag = Assert.Single(gespeichert.History);
        Assert.Equal("Priorität", eintrag.Field);
        Assert.Equal("Mittel", eintrag.OldValue);
        Assert.Equal("Kritisch", eintrag.NewValue);
    }

    [Fact]
    public async Task Unveraenderte_Prioritaet_erzeugt_keinen_Historieneintrag()
    {
        var ticket = await _service.CreateAsync(
            "Titel", "Text", TicketPriority.Medium, "kunde@example.net", TicketSource.Web);

        await _service.ChangePriorityAsync(ticket.Id, TicketPriority.Medium, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        Assert.Empty(gespeichert.History);
    }

    [Fact]
    public async Task Geschlossenes_Ticket_wird_nicht_mehr_umgestuft()
    {
        var ticket = await _service.CreateAsync(
            "Titel", "Text", TicketPriority.Medium, "kunde@example.net", TicketSource.Web);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ChangePriorityAsync(ticket.Id, TicketPriority.Critical, TestDaten.Teamleitung));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
