using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Prozessstart allein reicht als Auslöser der Sicherung nicht: Wer die
// Anwendung eine Woche laufen lässt, hätte eine Woche Arbeit ausschließlich
// in app.db, und wer mit leerer Datenbank startet, bekäme gar keine. Jeder
// Test stellt seine Ausgangslage ausdrücklich fest, bevor er etwas behauptet.
public sealed class SicherungsrhythmusTests : IDisposable
{
    private readonly string _ordner;
    private readonly string _sicherungsOrdner;
    private readonly TicketsystemContext _db;
    private readonly TicketService _tickets;

    public SicherungsrhythmusTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"rhythmus-{Guid.NewGuid():N}");
        _sicherungsOrdner = Path.Combine(_ordner, "Sicherungen");
        Directory.CreateDirectory(_ordner);

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={Path.Combine(_ordner, "app.db")};Pooling=false")
            .Options;
        _db = new TicketsystemContext(options);
        _db.Database.Migrate();
        _tickets = new TicketService(_db);
    }

    private SicherungService Dienst(int intervallStunden = 4) => new(
        _db,
        Options.Create(new DatenOptions
        {
            SicherungsOrdner = _sicherungsOrdner,
            AufbewahrteSicherungen = 10,
            SicherungBeimStart = true,
            SicherungIntervallStunden = intervallStunden
        }),
        NullLogger<SicherungService>.Instance);

    [Fact]
    public async Task Ohne_Ticket_wird_beim_Start_nichts_gesichert()
    {
        Assert.Empty(await _db.Tickets.ToListAsync());

        Assert.Null(await Dienst().BeimStartSichernAsync());
        Assert.Empty(Dateien());
    }

    [Fact]
    public async Task Die_erste_Sicherung_kommt_auch_ohne_Neustart()
    {
        var dienst = Dienst();
        Assert.Null(await dienst.BeimStartSichernAsync());

        await TicketAnlegenAsync();
        var faellig = await dienst.FaelligeSicherungAsync(DateTime.UtcNow);

        Assert.NotNull(faellig);
        Assert.Single(Dateien());
    }

    [Fact]
    public async Task Innerhalb_des_Intervalls_wird_nicht_noch_einmal_gesichert()
    {
        var dienst = Dienst(intervallStunden: 4);
        await TicketAnlegenAsync();
        var jetzt = DateTime.UtcNow;

        Assert.NotNull(await dienst.FaelligeSicherungAsync(jetzt));
        Assert.Null(await dienst.FaelligeSicherungAsync(jetzt.AddHours(1)));
        Assert.Single(Dateien());
    }

    [Fact]
    public async Task Nach_dem_Intervall_entsteht_der_naechste_Stand()
    {
        var dienst = Dienst(intervallStunden: 4);
        await TicketAnlegenAsync();
        var jetzt = DateTime.UtcNow;

        Assert.NotNull(await dienst.FaelligeSicherungAsync(jetzt));
        Assert.NotNull(await dienst.FaelligeSicherungAsync(jetzt.AddHours(5)));

        Assert.Equal(2, Dateien().Length);
    }

    [Fact]
    public async Task Ein_Intervall_von_null_schaltet_die_laufende_Sicherung_ab()
    {
        var dienst = Dienst(intervallStunden: 0);
        await TicketAnlegenAsync();

        Assert.Null(await dienst.FaelligeSicherungAsync(DateTime.UtcNow));
        Assert.Empty(Dateien());
    }

    // Ein Fehler nur im Protokoll reicht nicht: Die Datenseite sagte sonst
    // beruhigend „Beim nächsten Start entsteht die erste automatisch".
    [Fact]
    public async Task Ein_Fehlschlag_beim_Start_bleibt_sichtbar()
    {
        await TicketAnlegenAsync();
        var stand = new Sicherungsstand();
        var dienst = new SicherungService(
            _db,
            Options.Create(new DatenOptions
            {
                // Ein Pfad, der keine Datei aufnehmen kann: Der Ordnername ist eine
                // vorhandene Datei.
                SicherungsOrdner = await BlockierterOrdnerAsync(),
                SicherungBeimStart = true
            }),
            NullLogger<SicherungService>.Instance,
            stand);

        Assert.Null(await dienst.BeimStartSichernAsync());

        Assert.False(stand.LetzterVersuchErfolgreich);
        Assert.NotNull(stand.LetzterFehler);
    }

    [Fact]
    public async Task Ein_gelungener_Lauf_haelt_den_Stand_fest()
    {
        await TicketAnlegenAsync();
        var stand = new Sicherungsstand();
        var dienst = new SicherungService(
            _db,
            Options.Create(new DatenOptions { SicherungsOrdner = _sicherungsOrdner, SicherungBeimStart = true }),
            NullLogger<SicherungService>.Instance,
            stand);

        await dienst.BeimStartSichernAsync();

        Assert.True(stand.LetzterVersuchErfolgreich);
        Assert.Null(stand.LetzterFehler);
        Assert.NotNull(stand.LetzterErfolg);
    }

    private async Task<string> BlockierterOrdnerAsync()
    {
        var pfad = Path.Combine(_ordner, "keinordner");
        await File.WriteAllTextAsync(pfad, "Das ist eine Datei, kein Ordner.");
        return pfad;
    }

    private string[] Dateien() =>
        Directory.Exists(_sicherungsOrdner) ? Directory.GetFiles(_sicherungsOrdner, "*.db") : [];

    private Task TicketAnlegenAsync() => _tickets.CreateAsync(
        "Etwas zu sichern", "Inhalt", TicketPriority.Medium, "Test", TicketSource.Phone);

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_ordner, recursive: true);
    }
}
