using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Erfassungsablauf ist die einzige Fassung der Prüfungen und ihrer
// Reihenfolge; das Fenster reicht nur Werte hinein und Fehler heraus.
public sealed class ErfassungsAblaufTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly ErfassungsAblauf _ablauf;
    private readonly TicketService _tickets;

    public ErfassungsAblaufTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _tickets = new TicketService(_db);
        _ablauf = new ErfassungsAblauf(
            _tickets,
            new StammdatenService(_db),
            new BestaetigungsMelder(new LoggingTicketMailer(NullLogger<LoggingTicketMailer>.Instance),
                NullLogger<BestaetigungsMelder>.Instance));
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
    }

    private static ErfassungsEingabe Telefon(string? nummer = "0221123456", string? adresse = "A-1") => new(
        TicketSource.Phone, "Weber, Sabine", "Drucker klemmt", "Papierstau.", adresse,
        TicketPriority.Medium, nummer, null, null, null, null, MirZuweisen: false);

    [Fact]
    public async Task Ein_Telefon_Ticket_entsteht_samt_Stammdaten()
    {
        var ergebnis = await _ablauf.AnlegenAsync(Telefon(), TestDaten.Bearbeiter1);

        Assert.True(ergebnis.Gelungen);
        Assert.Contains("wurde angelegt", ergebnis.Meldung);
        var ticket = Assert.Single(await _db.Tickets.ToListAsync());
        Assert.Equal(TicketSource.Phone, ticket.Source);
        Assert.Equal("0221123456", ticket.CallbackNumber);
        Assert.Single(await _db.Kontakte.ToListAsync());
    }

    [Fact]
    public async Task Eine_Mail_braucht_den_Absender_und_traegt_den_Eingang()
    {
        var ohneAbsender = await _ablauf.AnlegenAsync(Telefon() with
        {
            Quelle = TicketSource.Email, Rueckrufnummer = null, KundenEmail = null
        }, TestDaten.Bearbeiter1);
        Assert.Contains(ohneAbsender.Fehler, f => f.Feld == Erfassungsfeld.Absender && f.Text.Contains("Absenderadresse"));

        var eingang = DateTime.UtcNow.AddHours(-14);
        var gelungen = await _ablauf.AnlegenAsync(Telefon() with
        {
            Quelle = TicketSource.Email, Rueckrufnummer = null,
            KundenEmail = "s.weber@example.com", ZeitpunktUtc = eingang
        }, TestDaten.Bearbeiter1);

        Assert.True(gelungen.Gelungen);
        var ticket = Assert.Single(await _db.Tickets.ToListAsync());
        Assert.Equal(TicketSource.Email, ticket.Source);
        Assert.Equal(eingang, ticket.CallTime);
    }

    [Fact]
    public async Task Die_Quellen_Wache_weist_Web_ab()
    {
        var ergebnis = await _ablauf.AnlegenAsync(Telefon() with { Quelle = TicketSource.Web }, TestDaten.Bearbeiter1);

        Assert.False(ergebnis.Gelungen);
        Assert.Contains(ergebnis.Fehler, f => f.Feld == Erfassungsfeld.Quelle && f.Text.Contains("kein Eingangsweg"));
        Assert.Empty(await _db.Tickets.ToListAsync());
    }

    [Fact]
    public async Task Telefon_ohne_gueltige_Nummer_wird_abgewiesen()
    {
        var leer = await _ablauf.AnlegenAsync(Telefon(nummer: null), TestDaten.Bearbeiter1);
        var falsch = await _ablauf.AnlegenAsync(Telefon(nummer: "1"), TestDaten.Bearbeiter1);

        Assert.Contains(leer.Fehler, f => f.Feld == Erfassungsfeld.Nummer && f.Text.Contains("Rückrufnummer"));
        Assert.Contains(falsch.Fehler, f => f.Feld == Erfassungsfeld.Nummer && f.Text.Contains("Durchwahl"));
        Assert.Empty(await _db.Tickets.ToListAsync());
    }

    // Ein stilles TryParse machte aus einem vertippten Verweis („12a") einen
    // fehlenden, und das Ticket entstand ohne Verweis und ohne Meldung.
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData("  123 ", 123, null)]
    [InlineData("#123", 123, null)]
    [InlineData("12a", null, "Ticketnummer")]
    [InlineData("abc", null, "Ticketnummer")]
    public void Der_Verweis_wird_geparst_statt_still_verschluckt(string? text, int? erwarteteId, string? fehlerteil)
    {
        var (id, fehler) = Erfassung.VerweisParsen(text);

        Assert.Equal(erwarteteId, id);
        if (fehlerteil is null)
        {
            Assert.Null(fehler);
        }
        else
        {
            Assert.Contains(fehlerteil, fehler);
        }
    }

    [Fact]
    public async Task Ein_Verweis_auf_ein_unsichtbares_Ticket_ist_ein_Tippfehler()
    {
        var ergebnis = await _ablauf.AnlegenAsync(Telefon() with { VerweisId = 999 }, TestDaten.Bearbeiter1);

        Assert.Contains(ergebnis.Fehler, f => f.Feld == Erfassungsfeld.Verweis && f.Text.Contains("#999"));
    }

    [Fact]
    public async Task Mir_zuweisen_wirkt_sofort()
    {
        var ergebnis = await _ablauf.AnlegenAsync(Telefon() with { MirZuweisen = true }, TestDaten.Bearbeiter1);

        Assert.True(ergebnis.Gelungen);
        var ticket = await _db.Tickets.SingleAsync();
        Assert.Equal(TestDaten.Bearbeiter1.Id, ticket.AgentId);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
