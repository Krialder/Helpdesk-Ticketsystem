using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein Nachfassdatum mit Grund am Ticket, gezählt von der Fristenanzeige beim
// Hinschauen. Warten auf Externe ist im Helpdesk der Normalfall und braucht
// einen Platz in der Akte. Bewusst kein eigener Wartestatus und kein
// Hintergrunddienst: Der Status hätte Übergangstabelle und SLA-Logik
// angefasst, und die Fristenleiste rechnet ohnehin bei jedem Hinschauen.
public sealed class WiedervorlageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public WiedervorlageTests()
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

    private Task<Ticket> TicketAsync(Akteur? ersteller = null)
    {
        var wer = ersteller ?? TestDaten.Bearbeiter1;
        return _service.CreatePhoneAsync("Netzteil defekt", "Ersatz bestellt.",
            TicketPriority.Medium, "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: wer.Id, createdByName: wer.Name);
    }

    [Fact]
    public async Task Setzen_speichert_Datum_und_Grund_und_schreibt_Historie()
    {
        var ticket = await TicketAsync();
        var donnerstag = DateTime.UtcNow.AddDays(2);

        await _service.WiedervorlageSetzenAsync(ticket.Id, donnerstag, "Netzteil da? Einbau", TestDaten.Bearbeiter1);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(donnerstag, gespeichert.FollowUpAt);
        Assert.Equal("Netzteil da? Einbau", gespeichert.FollowUpNote);
        var eintrag = Assert.Single(await _db.TicketHistory
            .Where(h => h.TicketId == ticket.Id && h.Field == "Wiedervorlage").ToListAsync());
        Assert.Contains("Netzteil da? Einbau", eintrag.NewValue);
    }

    // Eine Wiedervorlage, die nach dem Nachfassen stehen bleibt, wird zum
    // Daueralarm, den niemand mehr liest.
    [Fact]
    public async Task Entfernen_leert_beide_Felder_und_schreibt_Historie()
    {
        var ticket = await TicketAsync();
        await _service.WiedervorlageSetzenAsync(ticket.Id, DateTime.UtcNow.AddDays(2), "Netzteil da?", TestDaten.Bearbeiter1);

        await _service.WiedervorlageSetzenAsync(ticket.Id, null, null, TestDaten.Bearbeiter1);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Null(gespeichert.FollowUpAt);
        Assert.Null(gespeichert.FollowUpNote);
        Assert.Equal(2, await _db.TicketHistory
            .CountAsync(h => h.TicketId == ticket.Id && h.Field == "Wiedervorlage"));
    }

    [Fact]
    public async Task Ein_geschlossenes_Ticket_nimmt_keine_Wiedervorlage_an()
    {
        var ticket = await TicketAsync();
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.WiedervorlageSetzenAsync(ticket.Id, DateTime.UtcNow.AddDays(1), "zu spät", TestDaten.Teamleitung));
    }

    // Fremd heißt: von einem anderen erstellt und einem anderen zugewiesen,
    // sonst greift einer der drei Blicke des Bearbeiters.
    [Fact]
    public async Task Ein_Bearbeiter_setzt_auf_fremden_Tickets_keine_Wiedervorlage()
    {
        var fremdes = await TicketAsync(TestDaten.Bearbeiter2);
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.WiedervorlageSetzenAsync(fremdes.Id, DateTime.UtcNow.AddDays(1), "fremd", TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Der_Fristenstand_zaehlt_faellige_Wiedervorlagen()
    {
        var faellig = await TicketAsync();
        await _service.WiedervorlageSetzenAsync(faellig.Id, DateTime.UtcNow.AddMinutes(-5), "Netzteil da?", TestDaten.Bearbeiter1);
        var spaeter = await TicketAsync();
        await _service.WiedervorlageSetzenAsync(spaeter.Id, DateTime.UtcNow.AddDays(3), "erst Freitag", TestDaten.Bearbeiter1);

        var stand = await _service.FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);

        // Eine Anzeige, die auch künftige meldet, wäre eine Liste aller
        // Wartezustände und kein Handlungssignal.
        Assert.Equal(1, stand.WiedervorlagenFaellig);
        Assert.True(stand.Handlungsbedarf);
    }

    [Fact]
    public async Task Die_Filteransicht_zeigt_nur_faellige_Wiedervorlagen()
    {
        var faellig = await TicketAsync();
        await _service.WiedervorlageSetzenAsync(faellig.Id, DateTime.UtcNow.AddMinutes(-5), "Netzteil da?", TestDaten.Bearbeiter1);
        var spaeter = await TicketAsync();
        await _service.WiedervorlageSetzenAsync(spaeter.Id, DateTime.UtcNow.AddDays(3), "erst Freitag", TestDaten.Bearbeiter1);
        await TicketAsync();

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, nurWiedervorlagen: true)).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(faellig.Id, einziges.Id);
    }

    // Ein Feld, das der Austausch nicht kennt, ginge bei jedem Umzug still
    // verloren.
    [Fact]
    public async Task Export_und_Import_erhalten_die_Wiedervorlage()
    {
        var ticket = await TicketAsync();
        var termin = DateTime.UtcNow.AddDays(2);
        await _service.WiedervorlageSetzenAsync(ticket.Id, termin, "Netzteil da? Einbau", TestDaten.Bearbeiter1);
        var austausch = new AustauschService(_db, NullLogger<AustauschService>.Instance);

        var datei = await austausch.ExportJsonAsync(TestDaten.Administration);
        await austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        var importiert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(termin, importiert.FollowUpAt);
        Assert.Equal("Netzteil da? Einbau", importiert.FollowUpNote);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
