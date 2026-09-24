using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Verweise zeigen nur auf Sichtbares, und die Sammelansichten nach Person
// und Raum zeigen nichts, was einzeln verborgen wäre.
public sealed class VerweisUndHistorieTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public VerweisUndHistorieTests()
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
    }

    private Task<Ticket> TicketAsync(string kunde, string adresse) =>
        _service.CreateAsync("Titel", "Text", TicketPriority.Medium, kunde,
            TicketSource.Web, address: adresse);

    [Fact]
    public async Task Verweis_wird_am_Ticket_gespeichert()
    {
        var alt = await TicketAsync("A. Meier", "A-101");

        var neu = await _service.CreateAsync("Rückläufer", "Text", TicketPriority.Medium,
            "A. Meier", TicketSource.Web, address: "A-101", relatedTicketId: alt.Id);

        Assert.Equal(alt.Id, (await _db.Tickets.SingleAsync(t => t.Id == neu.Id)).RelatedTicketId);
    }

    [Fact]
    public async Task Sichtbarkeitspruefung_verraet_kein_fremdes_Ticket()
    {
        var fremdes = await TicketAsync("A. Meier", "A-101");
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        Assert.False(await _service.IstSichtbarAsync(fremdes.Id, TestDaten.Bearbeiter1));
        Assert.True(await _service.IstSichtbarAsync(fremdes.Id, TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Personen_Historie_zeigt_alle_Vorgaenge_derselben_Person()
    {
        await TicketAsync("A. Meier", "A-101");
        await TicketAsync("A. Meier", "B-7");
        await TicketAsync("B. Schulz", "A-101");

        var liste = await _service.ListByPersonAsync("A. Meier", TestDaten.Teamleitung);

        Assert.Equal(2, liste.Count);
        Assert.All(liste, t => Assert.Equal("A. Meier", t.CustomerName));
    }

    [Fact]
    public async Task Raum_Historie_zeigt_alle_Vorgaenge_desselben_Raums()
    {
        await TicketAsync("A. Meier", "A-101");
        await TicketAsync("B. Schulz", "A-101");
        await TicketAsync("C. Klein", "B-7");

        var liste = await _service.ListByRoomAsync("A-101", TestDaten.Teamleitung);

        Assert.Equal(2, liste.Count);
        Assert.All(liste, t => Assert.Equal("A-101", t.Address));
    }

    // Beide Tickets gehören derselben Person und demselben Raum; nur das
    // zugewiesene ist für Bearbeiter1 verborgen.
    [Fact]
    public async Task Historien_zeigen_Bearbeitern_nichts_zusaetzlich_Sichtbares()
    {
        var offen = await TicketAsync("A. Meier", "A-101");
        var fremdes = await TicketAsync("A. Meier", "A-101");
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var person = await _service.ListByPersonAsync("A. Meier", TestDaten.Bearbeiter1);
        var raum = await _service.ListByRoomAsync("A-101", TestDaten.Bearbeiter1);

        Assert.Equal(offen.Id, Assert.Single(person).Id);
        Assert.Equal(offen.Id, Assert.Single(raum).Id);
    }

    [Fact]
    public async Task Ohne_Rolle_erreicht_die_Historien_nicht()
    {
        await TicketAsync("A. Meier", "A-101");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ListByPersonAsync("A. Meier", TestDaten.OhneRolle));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ListByRoomAsync("A-101", TestDaten.OhneRolle));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
