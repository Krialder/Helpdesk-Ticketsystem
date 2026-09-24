using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Zwei Änderungen am selben Vorgang, ohne voneinander zu wissen; der
// Alltagsauslöser ist ein Doppelklick. Die Tests laufen gegen eine echte
// Datenbankdatei, weil zwei Kontexte denselben Bestand sehen müssen, und der
// Reihe nach statt parallel: Zwei gleichzeitige Schreiber auf einer
// SQLite-Datei enden je nach Zeitlage in einer Sperrmeldung statt in dem
// Fehler, um den es hier geht.
public sealed class NebenlaeufigkeitTests : IDisposable
{
    private readonly string _ordner;
    private readonly List<TicketsystemContext> _kontexte = [];

    public NebenlaeufigkeitTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"nebenlaeufig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_ordner);
    }

    private TicketsystemContext Kontext()
    {
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={Path.Combine(_ordner, "app.db")};Pooling=false")
            .Options;
        var db = new TicketsystemContext(options);
        db.Database.Migrate();
        _kontexte.Add(db);
        return db;
    }

    [Fact]
    public async Task Der_zweite_Statuswechsel_auf_denselben_Stand_scheitert()
    {
        var (id, ersterDienst, zweiterDienst) = await ZweiSichtenAufDenselbenVorgangAsync();

        // Beide Wege sind aus „Neu" erlaubt; der Übergang ist nicht der Grund für
        // das Scheitern.
        await ersterDienst.ChangeStatusAsync(id, TicketStatus.Assigned, TestDaten.Administration);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => zweiterDienst.ChangeStatusAsync(id, TicketStatus.Closed, TestDaten.Administration));
    }

    // Der eigentliche Schaden war nicht der doppelte Schreibvorgang, sondern die
    // Akte danach: zwei Einträge über denselben Übergang.
    [Fact]
    public async Task Nach_dem_gescheiterten_Wechsel_steht_genau_ein_Historieneintrag()
    {
        var (id, ersterDienst, zweiterDienst) = await ZweiSichtenAufDenselbenVorgangAsync();

        await ersterDienst.ChangeStatusAsync(id, TicketStatus.Assigned, TestDaten.Administration);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => zweiterDienst.ChangeStatusAsync(id, TicketStatus.Closed, TestDaten.Administration));

        await using var pruefer = Kontext();
        var eintraege = await pruefer.TicketHistory
            .AsNoTracking()
            .Where(h => h.TicketId == id && h.Field == "Status")
            .ToListAsync();

        Assert.Single(eintraege);
        Assert.Equal(TicketStatus.Assigned, (await pruefer.Tickets.AsNoTracking().SingleAsync(t => t.Id == id)).Status);
    }

    [Fact]
    public async Task Auch_ein_zweites_Nachtragen_auf_denselben_Stand_scheitert()
    {
        var (id, ersterDienst, zweiterDienst) = await ZweiSichtenAufDenselbenVorgangAsync();

        await ersterDienst.AngabenNachtragenAsync(
            id, kundenname: null, adresse: "B-2", rueckrufnummer: null, TestDaten.Administration);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => zweiterDienst.AngabenNachtragenAsync(
                id, kundenname: null, adresse: "C-3", rueckrufnummer: null, TestDaten.Administration));
    }

    [Fact]
    public async Task Ein_neu_geladener_Stand_laesst_sich_wieder_aendern()
    {
        var (id, ersterDienst, _) = await ZweiSichtenAufDenselbenVorgangAsync();

        await ersterDienst.ChangeStatusAsync(id, TicketStatus.Assigned, TestDaten.Administration);

        await using var frisch = Kontext();
        await new TicketService(frisch).ChangeStatusAsync(id, TicketStatus.InProgress, TestDaten.Administration);

        await using var pruefer = Kontext();
        Assert.Equal(
            TicketStatus.InProgress,
            (await pruefer.Tickets.AsNoTracking().SingleAsync(t => t.Id == id)).Status);
    }

    // Das Lesen vorab ist der Kern: Danach kennt jeder Kontext den Vorgang, wie
    // er vor der Änderung des anderen war.
    private async Task<(int Id, TicketService Erster, TicketService Zweiter)> ZweiSichtenAufDenselbenVorgangAsync()
    {
        await using var aufbau = Kontext();
        var ticket = await new TicketService(aufbau).CreateAsync(
            "Doppelklick", "Zweimal abgeschickt", TicketPriority.Medium, "Test", TicketSource.Phone);

        var ersterKontext = Kontext();
        var zweiterKontext = Kontext();
        await ersterKontext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        await zweiterKontext.Tickets.SingleAsync(t => t.Id == ticket.Id);

        return (ticket.Id, new TicketService(ersterKontext), new TicketService(zweiterKontext));
    }

    public void Dispose()
    {
        foreach (var db in _kontexte)
        {
            db.Dispose();
        }

        Directory.Delete(_ordner, recursive: true);
    }
}
