using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Stammdaten führen den Stand des letzten Anrufs, kein Archiv: eine
// aktuelle Nummer und Adresse je Person, nur gültige Nummern, keine Ausgabe
// ohne ausreichenden Suchbegriff.
public sealed class StammdatenTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly StammdatenService _service;

    public StammdatenTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _service = new StammdatenService(_db);
    }

    [Fact]
    public async Task Ohne_Rolle_erreicht_die_Stammdaten_nicht()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SucheKontakteAsync("Meier", TestDaten.OhneRolle));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SucheAdressenAsync("A", TestDaten.OhneRolle));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ErfasseAsync("Meier", "4711", "A-101", TestDaten.OhneRolle));
    }

    [Fact]
    public async Task Suche_liefert_ohne_ausreichenden_Begriff_nichts()
    {
        await _service.ErfasseAsync("Meier", "4711", "A-101", TestDaten.Bearbeiter1);

        Assert.Empty(await _service.SucheKontakteAsync("Me", TestDaten.Bearbeiter1));
        Assert.Single(await _service.SucheKontakteAsync("Mei", TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Eine_neue_Nummer_ersetzt_die_alte()
    {
        await _service.ErfasseAsync("A. Meier", "4711", "A-101", TestDaten.Bearbeiter1);
        await _service.ErfasseAsync("a. meier ", "0151 2345678", "B-7", TestDaten.Bearbeiter1);

        var kontakt = Assert.Single(await _db.Kontakte.ToListAsync());
        Assert.Equal("A. Meier", kontakt.Name);
        Assert.Equal("01512345678", kontakt.LetzteRufnummer);
        Assert.Equal("B-7", kontakt.LetzteAdresse);
    }

    [Fact]
    public async Task Dieselbe_Nummer_in_anderer_Schreibweise_bleibt_dieselbe()
    {
        await _service.ErfasseAsync("A. Meier", "0521 12345", null, TestDaten.Bearbeiter1);
        await _service.ErfasseAsync("A. Meier", "0521/12345", null, TestDaten.Bearbeiter1);

        var kontakt = Assert.Single(await _db.Kontakte.ToListAsync());
        Assert.Equal("052112345", kontakt.LetzteRufnummer);
    }

    [Fact]
    public async Task Ungueltige_Nummer_landet_nicht_im_Bestand()
    {
        await _service.ErfasseAsync("A. Meier", "4", null, TestDaten.Bearbeiter1);

        var kontakt = Assert.Single(await _db.Kontakte.ToListAsync());
        Assert.Null(kontakt.LetzteRufnummer);
    }

    // Ersetzt wird nur durch etwas Brauchbares, sonst löschte ein Vertipper beim
    // zweiten Anruf die Nummer, die beim ersten stimmte.
    [Fact]
    public async Task Eine_ungueltige_Nummer_verdraengt_keine_gueltige()
    {
        await _service.ErfasseAsync("A. Meier", "4711", null, TestDaten.Bearbeiter1);
        await _service.ErfasseAsync("A. Meier", "4", null, TestDaten.Bearbeiter1);

        var kontakt = Assert.Single(await _db.Kontakte.ToListAsync());
        Assert.Equal("4711", kontakt.LetzteRufnummer);
    }

    [Fact]
    public async Task Adressvorschlaege_kommen_aus_den_erfassten_Tickets()
    {
        var tickets = new TicketService(_db);
        await tickets.CreateAsync("T1", "Text", TicketPriority.Medium, "A. Beispiel", TicketSource.Web, address: "A-101");
        await tickets.CreateAsync("T2", "Text", TicketPriority.Medium, "A. Beispiel", TicketSource.Web, address: "B-7");

        var treffer = await _service.SucheAdressenAsync("A", TestDaten.Bearbeiter1);

        Assert.Equal(["A-101"], treffer);
    }

    [Fact]
    public async Task Nur_die_Administration_loescht_Stammdaten()
    {
        await _service.ErfasseAsync("A. Meier", "4711", null, TestDaten.Bearbeiter1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoescheKontaktAsync("A. Meier", TestDaten.Teamleitung));

        Assert.True(await _service.LoescheKontaktAsync("A. Meier", TestDaten.Administration));
        Assert.Empty(await _db.Kontakte.ToListAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
