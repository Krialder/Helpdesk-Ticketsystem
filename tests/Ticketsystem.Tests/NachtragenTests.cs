using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Wer unter Zeitdruck keine verlässliche Adresse, keinen Namen oder keine
// Rückrufnummer bekommt, trägt sie später nach, statt zu raten: Ein
// geratener Raum ist von einem echten nicht zu unterscheiden und vergiftet
// die Raum-Historie.
public sealed class NachtragenTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public NachtragenTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter2);
    }

    private Task<Ticket> AnrufAsync(string adresse = "A-101") =>
        _service.CreatePhoneAsync(
            "Drucker klemmt", "Text", TicketPriority.Medium, "Müller, Anna",
            "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name,
            address: adresse);

    private async Task<Ticket> FrischAsync(int id) =>
        await _db.Tickets.AsNoTracking().Include(t => t.History).SingleAsync(t => t.Id == id);

    [Fact]
    public async Task Ein_nachgetragener_Raum_steht_danach_am_Ticket_und_in_der_Historie()
    {
        var ticket = await AnrufAsync();

        var geaendert = await _service.AngabenNachtragenAsync(
            ticket.Id, kundenname: null, adresse: "b-12", rueckrufnummer: null, TestDaten.Bearbeiter1);

        Assert.Equal(1, geaendert);
        var frisch = await FrischAsync(ticket.Id);
        // Aufgeräumt gespeichert wie bei der Erfassung, sonst führte die
        // Raum-Historie „B-12" und „b-12" als zwei Räume.
        Assert.Equal("B-12", frisch.Address);
        var eintrag = Assert.Single(frisch.History, h => h.Field == "Adresse");
        Assert.Equal("A-101", eintrag.OldValue);
        Assert.Equal("B-12", eintrag.NewValue);
        Assert.Equal(TestDaten.Bearbeiter1.Name, eintrag.ChangedBy);
    }

    // Das Formular schickt immer alle Felder mit; sonst stünde nach jedem
    // Öffnen und Speichern eine Änderung in der Akte, die keine war.
    [Fact]
    public async Task Wer_nichts_aendert_erzeugt_keinen_Historieneintrag()
    {
        var ticket = await AnrufAsync();

        var geaendert = await _service.AngabenNachtragenAsync(
            ticket.Id, "Müller, Anna", "A-101", "0221123456", TestDaten.Bearbeiter1);

        Assert.Equal(0, geaendert);
        Assert.DoesNotContain((await FrischAsync(ticket.Id)).History, h => h.Field == "Adresse");
    }

    [Fact]
    public async Task Eine_unsinnige_Adresse_wird_abgewiesen_und_nichts_gespeichert()
    {
        var ticket = await AnrufAsync();

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, null, "Raum 5", null, TestDaten.Bearbeiter1));

        Assert.Contains("Gebäude-Raum", fehler.Message);
        Assert.Equal("A-101", (await FrischAsync(ticket.Id)).Address);
    }

    [Fact]
    public async Task Eine_unsinnige_Rufnummer_wird_abgewiesen()
    {
        var ticket = await AnrufAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, null, null, "0", TestDaten.Bearbeiter1));

        Assert.Equal("0221123456", (await FrischAsync(ticket.Id)).CallbackNumber);
    }

    [Fact]
    public async Task Eine_geleerte_Rufnummer_wird_entfernt_und_das_steht_in_der_Historie()
    {
        var ticket = await AnrufAsync();

        await _service.AngabenNachtragenAsync(ticket.Id, null, null, string.Empty, TestDaten.Bearbeiter1);

        var frisch = await FrischAsync(ticket.Id);
        Assert.Null(frisch.CallbackNumber);
        Assert.Single(frisch.History, h => h.Field == "Rückrufnummer" && h.NewValue is null);
    }

    [Fact]
    public async Task Ein_leerer_Name_wird_abgewiesen()
    {
        var ticket = await AnrufAsync();

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, "   ", null, null, TestDaten.Bearbeiter1));

        Assert.Contains("Ohne Namen", fehler.Message);
    }

    // Dort ist das Konto die Wahrheit; den Namen zu ändern hieße, den Vorgang
    // in der Personen-Historie einer anderen Person zuzuschlagen. Neue Tickets
    // bekommen keine CustomerId mehr, der Vertrag lebt für Altdaten weiter.
    [Fact]
    public async Task Bei_einem_Ticket_mit_Kundenkonto_bleibt_der_Name_unantastbar()
    {
        var ticket = await _service.CreateAsync("Web-Ticket", "Text", TicketPriority.Medium,
            "kunde-1@example.net", TicketSource.Web);
        ticket.CustomerId = TestDaten.OhneRolle.Id;
        await _db.SaveChangesAsync();

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, "Jemand anderes", null, null, TestDaten.Teamleitung));

        Assert.Contains("Kundenkonto", fehler.Message);
    }

    [Fact]
    public async Task Ein_geschlossenes_Ticket_nimmt_keine_Nachtraege_mehr_an()
    {
        var ticket = await AnrufAsync();
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Bearbeiter1);

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, null, "B-12", null, TestDaten.Bearbeiter1));

        Assert.Contains("endgültig", fehler.Message);
    }

    [Fact]
    public async Task Ein_Kunde_darf_nichts_nachtragen()
    {
        var ticket = await AnrufAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, null, "B-12", null, TestDaten.OhneRolle));
    }

    [Fact]
    public async Task Ein_Bearbeiter_darf_nur_an_eigenen_Vorgaengen_nachtragen()
    {
        var ticket = await AnrufAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AngabenNachtragenAsync(ticket.Id, null, "B-12", null, TestDaten.Bearbeiter2));

        Assert.Equal("A-101", (await FrischAsync(ticket.Id)).Address);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
