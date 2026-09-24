using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Rechtematrix im Dienst; welche Zeilen noch keinen Fall haben, steht
// im Entwicklerhandbuch unter den offenen Punkten. Die Feinrechte liegen im
// Dienst, die Fenster prüfen nur das Rollenlevel; deshalb werden sie hier
// belegt.
public sealed class RollenRechteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public RollenRechteTests()
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
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter2);
        TestDaten.KontoAnlegen(_db, TestDaten.Teamleitung);
    }

    private Task<Ticket> TicketAsync(Akteur ersteller) =>
        _service.CreateAsync("Titel", "Text", TicketPriority.Medium, ersteller.Name,
            TicketSource.Web,
            createdById: ersteller.Id, createdByName: ersteller.Name);

    [Fact]
    public async Task Bearbeiter_sieht_eigene_zugewiesene_und_unzugewiesene_Tickets()
    {
        var eigenes = await TicketAsync(TestDaten.Bearbeiter1);
        var unzugewiesen = await TicketAsync(TestDaten.OhneRolle);
        var fremdes = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Bearbeiter1)).Zeilen;

        Assert.Equal(
            new[] { eigenes.Id, unzugewiesen.Id }.OrderBy(x => x),
            liste.Select(t => t.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task Teamleitung_sieht_alle_Tickets()
    {
        await TicketAsync(TestDaten.OhneRolle);
        var fremdes = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Teamleitung)).Zeilen;

        Assert.Equal(2, liste.Count);
    }

    [Fact]
    public async Task Bearbeiter_weist_unzugewiesenes_Ticket_sich_selbst_zu()
    {
        var ticket = await TicketAsync(TestDaten.OhneRolle);

        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Bearbeiter1);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TestDaten.Bearbeiter1.Id, gespeichert.AgentId);
    }

    [Fact]
    public async Task Bearbeiter_weist_niemand_anderen_zu()
    {
        var ticket = await TicketAsync(TestDaten.OhneRolle);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Bearbeiter_uebernimmt_kein_bereits_zugewiesenes_Ticket()
    {
        var ticket = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Teamleitung_weist_beliebige_Person_zu()
    {
        var ticket = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung);

        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TestDaten.Bearbeiter2.Id, gespeichert.AgentId);
    }

    [Fact]
    public async Task Pausiertes_Konto_wird_nicht_zugewiesen()
    {
        var pausiert = new Akteur("pausiert-1", "pausiert@example.org", RoleLevel.Bearbeiter);
        TestDaten.KontoAnlegen(_db, pausiert, pausiertBis: DateTime.UtcNow.AddDays(14));
        var ticket = await TicketAsync(TestDaten.OhneRolle);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignAsync(ticket.Id, pausiert.Id, pausiert.Name, TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Bearbeiter_aendert_nur_eigene_Tickets()
    {
        var fremdes = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ChangeStatusAsync(fremdes.Id, TicketStatus.InProgress, TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ChangePriorityAsync(fremdes.Id, TicketPriority.Critical, TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Ohne_Rolle_wird_vom_Dienst_fuer_Mitarbeiter_Aktionen_abgewiesen()
    {
        var ticket = await TicketAsync(TestDaten.OhneRolle);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.OhneRolle));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignAsync(ticket.Id, TestDaten.OhneRolle.Id, TestDaten.OhneRolle.Name, TestDaten.OhneRolle));
    }

    [Fact]
    public async Task Ansicht_pausierter_Zuweisungen_ist_der_Teamleitung_vorbehalten()
    {
        var pausiert = new Akteur("pausiert-2", "pausiert2@example.org", RoleLevel.Bearbeiter);
        TestDaten.KontoAnlegen(_db, pausiert);
        var ticket = await TicketAsync(TestDaten.OhneRolle);
        await _service.AssignAsync(ticket.Id, pausiert.Id, pausiert.Name, TestDaten.Teamleitung);
        var konto = await _db.Users.SingleAsync(u => u.Id == pausiert.Id);
        konto.PausedUntil = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, nurPausierteZuweisungen: true)).Zeilen;
        var einziges = Assert.Single(liste);
        Assert.Equal(ticket.Id, einziges.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ListAsync(TestDaten.Bearbeiter1, nurPausierteZuweisungen: true));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
