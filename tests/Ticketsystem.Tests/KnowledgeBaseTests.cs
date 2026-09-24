using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Regeln der Wissensdatenbank: Kategorien, Tags, Freigabe-Workflow,
// Prüfzyklus und Fassungen. Die Geschichte eines Artikels wird nie
// umgeschrieben, nur verlängert.
public sealed class KnowledgeBaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly KnowledgeBaseService _kb;

    public KnowledgeBaseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _kb = new KnowledgeBaseService(_db);
    }

    private async Task<(KbArticle Anleitung, KbArticle Technik, KbArticle Entwurf)> BestandAsync()
    {
        var kategorie = await _kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        var anleitung = await _kb.SpeichernAsync(null, "Drucker einrichten",
            "Schritt für Schritt zum Netzwerkdrucker.", kategorie.Id,
            published: true, 180, [], TestDaten.Teamleitung);
        var technik = await _kb.SpeichernAsync(null, "Druckserver neu starten",
            "Dienst spooler durchstarten, vorher Queue leeren.", kategorie.Id,
            published: true, 180, [], TestDaten.Teamleitung);
        var entwurf = await _kb.SpeichernAsync(null, "VPN-Leitfaden (Entwurf)",
            "Noch unvollständig.", null,
            published: false, 180, [], TestDaten.Teamleitung);
        return (anleitung, technik, entwurf);
    }

    // Wer Entwürfe ausblendete, versteckte Arbeit vor genau den Leuten, die sie
    // fertig machen sollen.
    [Fact]
    public async Task Die_Liste_zeigt_auch_Entwuerfe()
    {
        await BestandAsync();

        Assert.Equal(3, (await _kb.ListAsync()).Count);
    }

    [Fact]
    public async Task Die_Suche_trifft_Titel_und_Inhalt()
    {
        await BestandAsync();

        Assert.Single(await _kb.ListAsync(suche: "spooler"));
        Assert.Empty(await _kb.ListAsync(suche: "gibtesnicht"));
    }

    [Fact]
    public async Task Nur_Teamleitung_legt_Kategorien_und_Tags_an()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.KategorieAnlegenAsync("Netzwerk", TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.TagAnlegenAsync("vpn", TestDaten.Bearbeiter1));

        Assert.NotNull(await _kb.KategorieAnlegenAsync("Netzwerk", TestDaten.Teamleitung));
        Assert.NotNull(await _kb.TagAnlegenAsync("vpn", TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Gleichnamige_Kategorien_und_Tags_entstehen_nicht_doppelt()
    {
        var a = await _kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        var b = await _kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        var t1 = await _kb.TagAnlegenAsync("VPN", TestDaten.Teamleitung);
        var t2 = await _kb.TagAnlegenAsync("vpn", TestDaten.Teamleitung);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(t1.Id, t2.Id);
    }

    [Fact]
    public async Task Nach_Tag_und_Kategorie_laesst_sich_filtern()
    {
        var kategorie = await _kb.KategorieAnlegenAsync("Netzwerk", TestDaten.Teamleitung);
        var tag = await _kb.TagAnlegenAsync("vpn", TestDaten.Teamleitung);
        var passend = await _kb.SpeichernAsync(null, "VPN einrichten", "Text", kategorie.Id,
            published: true, 180, [tag.Id], TestDaten.Teamleitung);
        await _kb.SpeichernAsync(null, "Anderes", "Text", null,
            published: true, 180, [], TestDaten.Teamleitung);

        Assert.Equal(passend.Id, Assert.Single(await _kb.ListAsync(kategorieId: kategorie.Id)).Id);
        Assert.Equal(passend.Id, Assert.Single(await _kb.ListAsync(tagId: tag.Id)).Id);
    }

    [Fact]
    public async Task Bearbeiter_speichert_nicht_direkt_sondern_reicht_ein()
    {
        var bestand = await BestandAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.SpeichernAsync(bestand.Anleitung.Id, "Neuer Titel", "Text", null,
                true, 180, [], TestDaten.Bearbeiter1));

        await _kb.VorschlagEinreichenAsync(bestand.Anleitung.Id, "Neuer Titel", "Besserer Text",
            null, TestDaten.Bearbeiter1);

        Assert.Equal("Drucker einrichten",
            (await _db.KbArticles.SingleAsync(a => a.Id == bestand.Anleitung.Id)).Title);
        Assert.Single(await _kb.VorschlaegeAsync(TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Teamleitung_uebernimmt_den_Vorschlag()
    {
        var bestand = await BestandAsync();
        var vorschlag = await _kb.VorschlagEinreichenAsync(bestand.Anleitung.Id, "Drucker einrichten (neu)",
            "Besserer Text", null, TestDaten.Bearbeiter1);

        await _kb.VorschlagUebernehmenAsync(vorschlag.Id, TestDaten.Teamleitung);

        var artikel = await _db.KbArticles.SingleAsync(a => a.Id == bestand.Anleitung.Id);
        Assert.Equal("Drucker einrichten (neu)", artikel.Title);
        Assert.Equal(TestDaten.Teamleitung.Name, artikel.Owner);
        Assert.Empty(await _kb.VorschlaegeAsync(TestDaten.Teamleitung));
    }

    // Übernehmen heißt „Text ist angekommen", nicht „steht sofort im
    // Nachschlagewerk".
    [Fact]
    public async Task Neuer_Vorschlag_wird_unveroeffentlicht_uebernommen()
    {
        var vorschlag = await _kb.VorschlagEinreichenAsync(null, "Neues Thema", "Text",
            null, TestDaten.Bearbeiter1);

        await _kb.VorschlagUebernehmenAsync(vorschlag.Id, TestDaten.Teamleitung);

        var artikel = Assert.Single(await _db.KbArticles.ToListAsync());
        Assert.False(artikel.Published);
    }

    [Fact]
    public async Task Bearbeiter_sieht_keine_offenen_Vorschlaege()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.VorschlaegeAsync(TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Ohne_Rolle_reicht_man_nichts_ein()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.VorschlagEinreichenAsync(null, "Titel", "Text", null, TestDaten.OhneRolle));
    }

    [Fact]
    public async Task Abgelaufener_Pruefzyklus_wird_erkannt_und_zuruecksetzbar()
    {
        var artikel = await _kb.SpeichernAsync(null, "Alt", "Text", null,
            published: true, reviewIntervallTage: 30, [], TestDaten.Teamleitung);

        var gespeichert = await _db.KbArticles.SingleAsync(a => a.Id == artikel.Id);
        gespeichert.UpdatedAt = DateTime.UtcNow.AddDays(-31);
        await _db.SaveChangesAsync();

        Assert.True(gespeichert.PruefungFaellig(DateTime.UtcNow));
        Assert.Single(await _kb.ListAsync(nurPruefungFaellig: true));

        await _kb.AlsGeprueftMarkierenAsync(artikel.Id, TestDaten.Teamleitung);

        Assert.Empty(await _kb.ListAsync(nurPruefungFaellig: true));
    }

    private async Task<KbArticle> ArtikelAsync(string titel = "Drucker einrichten", string inhalt = "Erster Stand.") =>
        await _kb.SpeichernAsync(null, titel, inhalt, null, published: true, 180, [], TestDaten.Teamleitung);

    private Task<List<KbArtikelFassung>> FassungenAsync(int id) =>
        _db.KbFassungen.AsNoTracking().Where(f => f.ArticleId == id).OrderBy(f => f.Nummer).ToListAsync();

    // Sonst begänne die Geschichte erst beim zweiten Speichern, und der erste
    // Text wäre der einzige, den niemand mehr nachlesen kann.
    [Fact]
    public async Task Anlegen_ist_die_Fassung_1()
    {
        var artikel = await ArtikelAsync();

        var fassung = Assert.Single(await FassungenAsync(artikel.Id));
        Assert.Equal(1, fassung.Nummer);
        Assert.Equal("Drucker einrichten", fassung.Title);
        Assert.Equal("Erster Stand.", fassung.Content);
        Assert.Equal(Fassungsanlass.Angelegt, fassung.Anlass);
        Assert.Equal(TestDaten.Teamleitung.Name, fassung.GespeichertVon);
        Assert.Equal(TestDaten.Teamleitung.Id, fassung.GespeichertVonId);
    }

    [Fact]
    public async Task Bearbeiten_legt_die_naechste_Fassung_an_und_laesst_die_alte_stehen()
    {
        var artikel = await ArtikelAsync();
        var kategorie = await _kb.KategorieAnlegenAsync("Drucker", TestDaten.Teamleitung);
        var tag = await _kb.TagAnlegenAsync("netzwerk", TestDaten.Teamleitung);

        await _kb.SpeichernAsync(artikel.Id, "Drucker einrichten", "Zweiter Stand.", kategorie.Id,
            published: true, 180, [tag.Id], TestDaten.Teamleitung);

        var fassungen = await FassungenAsync(artikel.Id);
        Assert.Equal(2, fassungen.Count);
        Assert.Equal("Erster Stand.", fassungen[0].Content);
        Assert.Equal("Zweiter Stand.", fassungen[1].Content);
        Assert.Equal(Fassungsanlass.Bearbeitet, fassungen[1].Anlass);
        // Kategorie und Tags stehen als Namen in der Fassung, nicht als Verweise:
        // Beides wird umbenannt und gelöscht, und eine Fassung, die dann ins Leere
        // zeigt, sagt nichts mehr.
        Assert.Equal("Drucker", fassungen[1].Kategorie);
        Assert.Equal("netzwerk", fassungen[1].Tags);
    }

    // Wie ein leerer Commit: Wer nachsieht, was sich geändert hat, findet
    // nichts und misstraut der Liste.
    [Fact]
    public async Task Speichern_ohne_Aenderung_legt_keine_Fassung_an()
    {
        var artikel = await ArtikelAsync();

        await _kb.SpeichernAsync(artikel.Id, "Drucker einrichten", "Erster Stand.", null,
            published: true, 180, [], TestDaten.Teamleitung);

        Assert.Single(await FassungenAsync(artikel.Id));
    }

    [Fact]
    public async Task Vorschlag_uebernehmen_legt_eine_Fassung_an_und_nennt_den_Urheber()
    {
        var artikel = await ArtikelAsync();
        var vorschlag = await _kb.VorschlagEinreichenAsync(artikel.Id, "Drucker einrichten", "Vom Bearbeiter.",
            null, TestDaten.Bearbeiter1);

        await _kb.VorschlagUebernehmenAsync(vorschlag.Id, TestDaten.Teamleitung);

        var juengste = (await FassungenAsync(artikel.Id)).Last();
        Assert.Equal("Vom Bearbeiter.", juengste.Content);
        // Gespeichert hat die Teamleitung, geschrieben der Bearbeiter; die Person
        // verantwortet den Stand, der Anlass sagt, woher er kam.
        Assert.Equal(TestDaten.Teamleitung.Name, juengste.GespeichertVon);
        Assert.Equal(Fassungsanlass.VorschlagUebernommen(TestDaten.Bearbeiter1.Name), juengste.Anlass);
    }

    // Zurückgehen löscht nicht Fassung 2, sondern legt Fassung 3 an, die wieder
    // sagt, was Fassung 1 sagte; so bleibt der Umweg nachlesbar.
    [Fact]
    public async Task Zurueck_auf_eine_Fassung_ist_selbst_eine_neue_Fassung()
    {
        var artikel = await ArtikelAsync();
        await _kb.SpeichernAsync(artikel.Id, "Drucker einrichten", "Zweiter Stand.", null,
            published: true, 180, [], TestDaten.Teamleitung);

        await _kb.ZurueckAufFassungAsync(artikel.Id, 1, TestDaten.Teamleitung);

        var fassungen = await FassungenAsync(artikel.Id);
        Assert.Equal(3, fassungen.Count);
        Assert.Equal("Zweiter Stand.", fassungen[1].Content);
        Assert.Equal("Erster Stand.", fassungen[2].Content);
        Assert.Equal(Fassungsanlass.Zurueck(1), fassungen[2].Anlass);
        Assert.Equal("Erster Stand.", (await _db.KbArticles.AsNoTracking().SingleAsync(a => a.Id == artikel.Id)).Content);
    }

    // Ob ein Artikel im Nachschlagewerk steht, ist eine eigene Entscheidung und
    // kein Teil des Textes.
    [Fact]
    public async Task Zurueck_laesst_den_Veroeffentlichungsstand_unberuehrt()
    {
        var artikel = await _kb.SpeichernAsync(null, "Alt", "Entwurfstext.", null,
            published: false, 180, [], TestDaten.Teamleitung);
        await _kb.SpeichernAsync(artikel.Id, "Alt", "Freigegebener Text.", null,
            published: true, 180, [], TestDaten.Teamleitung);

        await _kb.ZurueckAufFassungAsync(artikel.Id, 1, TestDaten.Teamleitung);

        var frisch = await _db.KbArticles.AsNoTracking().SingleAsync(a => a.Id == artikel.Id);
        Assert.Equal("Entwurfstext.", frisch.Content);
        Assert.True(frisch.Published);
    }

    [Fact]
    public async Task Zurueck_auf_die_aktuelle_Fassung_wird_abgewiesen()
    {
        var artikel = await ArtikelAsync();

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.ZurueckAufFassungAsync(artikel.Id, 1, TestDaten.Teamleitung));

        Assert.Contains("aktuelle Stand", fehler.Message);
        Assert.Single(await FassungenAsync(artikel.Id));
    }

    [Fact]
    public async Task Fassungen_sieht_und_setzt_nur_die_Teamleitung()
    {
        var artikel = await ArtikelAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.FassungenAsync(artikel.Id, TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _kb.ZurueckAufFassungAsync(artikel.Id, 1, TestDaten.Bearbeiter1));
        Assert.Single(await _kb.FassungenAsync(artikel.Id, TestDaten.Teamleitung));
    }

    [Fact]
    public async Task Loeschen_eines_Artikels_nimmt_seine_Fassungen_mit()
    {
        var artikel = await ArtikelAsync();

        await _kb.ArtikelLoeschenAsync(artikel.Id, TestDaten.Teamleitung);

        Assert.Empty(await _db.KbFassungen.ToListAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
