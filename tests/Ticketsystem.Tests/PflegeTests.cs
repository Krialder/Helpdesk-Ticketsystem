using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Bestände lassen sich berichtigen, nicht nur anlegen: Ein Tippfehler im
// Vokabular war dauerhaft, ein versehentlich angelegter Artikel blieb für
// immer im Bestand, und das Löschen von Stammdaten gab es nur als SQL.
public sealed class PflegeTests : IDisposable
{
    private readonly SqliteConnection _verbindung;
    private readonly TicketsystemContext _db;
    private readonly KnowledgeBaseService Kb;
    private readonly StammdatenService Stammdaten;

    public PflegeTests()
    {
        _verbindung = new SqliteConnection("DataSource=:memory:");
        _verbindung.Open();
        var options = new DbContextOptionsBuilder<TicketsystemContext>().UseSqlite(_verbindung).Options;
        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        Kb = new KnowledgeBaseService(_db);
        Stammdaten = new StammdatenService(_db);
    }

    [Fact]
    public async Task Eine_Kategorie_laesst_sich_umbenennen()
    {
        var kategorie = await Kb.KategorieAnlegenAsync("Drucekr", TestDaten.Teamleitung);

        await Kb.KategorieUmbenennenAsync(kategorie.Id, "Drucker", TestDaten.Teamleitung);

        Assert.Equal("Drucker", (await Kb.KategorienAsync()).Single().Name);
    }

    // Die Normalform ist der Suchschlüssel; bliebe sie stehen, fände die Suche
    // den neuen Namen nicht.
    [Fact]
    public async Task Ein_Schlagwort_laesst_sich_umbenennen_samt_Normalform()
    {
        var tag = await Kb.TagAnlegenAsync("drucekr", TestDaten.Teamleitung);

        await Kb.TagUmbenennenAsync(tag.Id, "Drucker", TestDaten.Teamleitung);

        var neu = (await Kb.TagsAsync()).Single();
        Assert.Equal("Drucker", neu.Name);
        Assert.Equal("drucker", neu.NameNormalisiert);
    }

    [Fact]
    public async Task Eine_unbenutzte_Kategorie_laesst_sich_loeschen()
    {
        var kategorie = await Kb.KategorieAnlegenAsync("Versehen", TestDaten.Teamleitung);

        await Kb.KategorieLoeschenAsync(kategorie.Id, TestDaten.Teamleitung);

        Assert.Empty(await Kb.KategorienAsync());
    }

    // Verweigern statt still leeren: Ein Artikel, der plötzlich ohne Kategorie
    // dasteht, ist ein Verlust, den niemand bemerkt.
    [Fact]
    public async Task Eine_benutzte_Kategorie_wird_nicht_geloescht()
    {
        var kategorie = await Kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        await Kb.SpeichernAsync(null, "Papierstau", "Klappe öffnen.", kategorie.Id,
            true, 180, [], TestDaten.Teamleitung);

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.KategorieLoeschenAsync(kategorie.Id, TestDaten.Teamleitung));

        Assert.Contains("1 Artikel", fehler.Message);
        Assert.Single(await Kb.KategorienAsync());
    }

    [Fact]
    public async Task Ein_benutztes_Schlagwort_wird_nicht_geloescht()
    {
        var tag = await Kb.TagAnlegenAsync("Drucker", TestDaten.Teamleitung);
        await Kb.SpeichernAsync(null, "Papierstau", "Klappe öffnen.", null,
            true, 180, [tag.Id], TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.TagLoeschenAsync(tag.Id, TestDaten.Teamleitung));

        Assert.Single(await Kb.TagsAsync());
    }

    [Fact]
    public async Task Nur_ab_Teamleitung_wird_das_Vokabular_gepflegt()
    {
        var kategorie = await Kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        var tag = await Kb.TagAnlegenAsync("Papier", TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.KategorieUmbenennenAsync(kategorie.Id, "Neu", TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.KategorieLoeschenAsync(kategorie.Id, TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.TagUmbenennenAsync(tag.Id, "Neu", TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.TagLoeschenAsync(tag.Id, TestDaten.Bearbeiter1));
    }

    // Vorgangsakten werden nie gelöscht; für Wissensartikel gilt das
    // ausdrücklich nicht, denn sie sind lebende Doku.
    [Fact]
    public async Task Die_Teamleitung_loescht_einen_Artikel_samt_offener_Vorschlaege()
    {
        var artikel = await Kb.SpeichernAsync(null, "Versehentlich", "Inhalt", null,
            false, 180, [], TestDaten.Teamleitung);
        await Kb.VorschlagEinreichenAsync(artikel.Id, "Versehentlich", "Besser so", null,
            TestDaten.Bearbeiter1);

        await Kb.ArtikelLoeschenAsync(artikel.Id, TestDaten.Teamleitung);

        Assert.Empty(await Kb.ListAsync());
        Assert.Empty(await Kb.VorschlaegeAsync(TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Ein_Bearbeiter_loescht_keinen_Artikel()
    {
        var artikel = await Kb.SpeichernAsync(null, "Bleibt", "Inhalt", null,
            false, 180, [], TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Kb.ArtikelLoeschenAsync(artikel.Id, TestDaten.Bearbeiter1));

        Assert.Single(await Kb.ListAsync());
    }

    [Fact]
    public async Task Die_Administration_sieht_die_Stammdaten_als_Liste()
    {
        await Stammdaten.ErfasseAsync("Weber, Sabine", "0221333444", "A-101", TestDaten.Bearbeiter1);

        var liste = await Stammdaten.KontakteAsync(TestDaten.Administration);

        Assert.Equal("Weber, Sabine", liste.Single().Name);
        Assert.Equal("0221333444", liste.Single().LetzteRufnummer);
    }

    [Fact]
    public async Task Nur_die_Administration_sieht_die_Stammdatenliste()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Stammdaten.KontakteAsync(TestDaten.Teamleitung));
    }

    // 180 stand in KbArticle, im KnowledgeBaseService und im AustauschService;
    // wer den Wert ändert, ändert ihn an einer Stelle und übersieht zwei.
    [Fact]
    public void Der_Standard_Pruefzyklus_steht_an_einer_Stelle()
    {
        var quellen = Directory.GetFiles(
            Path.Combine(Quellordner(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(datei => !datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();

        // Wortgrenzen, sonst zählt „RFC 4180" im AustauschService mit.
        var zahl = new System.Text.RegularExpressions.Regex(@"\b180\b");
        var treffer = quellen
            .Where(datei => !datei.Contains("Migrations", StringComparison.Ordinal))
            .Where(datei => zahl.IsMatch(File.ReadAllText(datei)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["KbArticle.cs"], treffer);
    }

    private static string Quellordner()
    {
        var ordner = AppContext.BaseDirectory;
        while (ordner is not null && !File.Exists(Path.Combine(ordner, "Ticketsystem.sln")))
        {
            ordner = Directory.GetParent(ordner)?.FullName;
        }

        return ordner ?? throw new InvalidOperationException("Die Projektwurzel wurde nicht gefunden.");
    }

    public void Dispose()
    {
        _db.Dispose();
        _verbindung.Dispose();
    }
}
