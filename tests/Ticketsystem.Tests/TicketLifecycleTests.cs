using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Übergangstabelle der Status. Die Tests sind nach den fachlichen
// Festlegungen benannt, damit ein roter Test direkt sagt, welche Zusage
// verletzt ist.
public sealed class TicketLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public TicketLifecycleTests()
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

    private Task<Ticket> NeuesTicketAsync() =>
        _service.CreateAsync("Titel", "Beschreibung", TicketPriority.Medium, "A. Beispiel", TicketSource.Web);

    [Fact]
    public async Task Erlaubter_Uebergang_aendert_Status_und_schreibt_Historie()
    {
        var ticket = await NeuesTicketAsync();

        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.Assigned, gespeichert.Status);
        var eintrag = Assert.Single(gespeichert.History);
        Assert.Equal("Status", eintrag.Field);
        Assert.Equal("Neu", eintrag.OldValue);
        Assert.Equal("Zugewiesen", eintrag.NewValue);
    }

    [Fact]
    public async Task Verbotener_Uebergang_wird_abgewiesen_und_aendert_nichts()
    {
        var ticket = await NeuesTicketAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, TestDaten.Teamleitung));

        var gespeichert = await _db.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.New, gespeichert.Status);
        Assert.Empty(gespeichert.History);
    }

    [Fact]
    public async Task Geloest_ist_wiedereroeffenbar()
    {
        var ticket = await NeuesTicketAsync();
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, TestDaten.Teamleitung);

        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.InProgress, gespeichert.Status);
    }

    [Fact]
    public async Task Geschlossen_ist_endgueltig()
    {
        var ticket = await NeuesTicketAsync();
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.ChangeStatusAsync(ticket.Id, status, TestDaten.Teamleitung));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Direktschluss_ist_aus_jedem_offenen_Status_erlaubt()
    {
        var ticket = await NeuesTicketAsync();
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);

        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.Closed, gespeichert.Status);
    }

    [Fact]
    public async Task Zuweisung_setzt_Agent_und_hebt_neues_Ticket_auf_Zugewiesen()
    {
        var ticket = await NeuesTicketAsync();

        await _service.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung);

        var gespeichert = await _db.Tickets.Include(t => t.History).SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TestDaten.Bearbeiter1.Name, gespeichert.AgentName);
        Assert.Equal(TicketStatus.Assigned, gespeichert.Status);
        Assert.Equal(2, gespeichert.History.Count);
    }

    [Fact]
    public async Task Telefon_Ticket_speichert_Anrufdaten()
    {
        var anrufzeit = new DateTime(2026, 6, 11, 14, 30, 0, DateTimeKind.Utc);

        var ticket = await _service.CreatePhoneAsync(
            "Kein Netzwerk", "Beschreibung", TicketPriority.Critical,
            "A. Beispiel", "0151 2345678", anrufzeit, "Ruft nach 15 Uhr zurück.");

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketSource.Phone, gespeichert.Source);
        Assert.Equal(TicketStatus.New, gespeichert.Status);
        Assert.Equal("0151 2345678", gespeichert.CallbackNumber);
        Assert.Equal(anrufzeit, gespeichert.CallTime);
        Assert.Equal("Ruft nach 15 Uhr zurück.", gespeichert.CallNote);
    }

    [Fact]
    public async Task Filter_nur_offene_blendet_Geloeste_und_Geschlossene_aus()
    {
        var offen = await NeuesTicketAsync();
        var geloest = await NeuesTicketAsync();
        await _service.ChangeStatusAsync(geloest.Id, TicketStatus.Assigned, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(geloest.Id, TicketStatus.InProgress, TestDaten.Teamleitung);
        await _service.ChangeStatusAsync(geloest.Id, TicketStatus.Resolved, TestDaten.Teamleitung);
        var geschlossen = await NeuesTicketAsync();
        await _service.ChangeStatusAsync(geschlossen.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, nurOffene: true)).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(offen.Id, einziges.Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
