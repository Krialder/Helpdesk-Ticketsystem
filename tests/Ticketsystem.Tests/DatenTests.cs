using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Sicherung, Wiederherstellung, Export und Import. Die Tests laufen gegen
// eine echte Datenbankdatei und nicht gegen :memory:, denn der Umgang mit
// der Datei ist der Prüfgegenstand: Eine Sicherung, die es nur im
// Arbeitsspeicher gibt, ist keine.
public sealed class DatenTests : IDisposable
{
    private readonly string _ordner;
    private readonly string _sicherungsOrdner;
    private readonly TicketsystemContext _db;
    private readonly SicherungService _sicherung;
    private readonly AustauschService _austausch;
    private readonly TicketService _tickets;
    private readonly List<TicketsystemContext> _weitere = [];

    public DatenTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"daten-tests-{Guid.NewGuid():N}");
        _sicherungsOrdner = Path.Combine(_ordner, "Sicherungen");
        Directory.CreateDirectory(_ordner);

        _db = Kontext(Path.Combine(_ordner, "app.db"));
        _sicherung = new SicherungService(_db, Optionen(), NullLogger<SicherungService>.Instance);
        _austausch = new AustauschService(_db, NullLogger<AustauschService>.Instance);
        _tickets = new TicketService(_db);
    }

    // Migrate statt EnsureCreated: Die Wiederherstellung hebt eine ältere
    // Sicherung auf den aktuellen Schemastand und braucht dafür die
    // Migrationstabelle, die EnsureCreated nicht anlegt.
    private TicketsystemContext Kontext(string pfad)
    {
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={pfad};Pooling=false")
            .Options;
        var db = new TicketsystemContext(options);
        db.Database.Migrate();
        return db;
    }

    private IOptions<DatenOptions> Optionen(int aufbewahrt = 10) => Options.Create(new DatenOptions
    {
        SicherungsOrdner = _sicherungsOrdner,
        AufbewahrteSicherungen = aufbewahrt
    });

    // Ein Bestand mit genau den Fallen, an denen Export und Import scheitern:
    // Umlaute, deutsche Anführungszeichen, ein Semikolon (der CSV-Trenner), ein
    // Zeilenumbruch mitten im Text, dazu ein Schlagwort und eine Kategorie ohne
    // Artikel und ein offener Änderungsvorschlag, die ein Import still
    // verlieren kann.
    private async Task<Ticket> BestandAufbauenAsync()
    {
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);

        var ticket = await _tickets.CreateAsync(
            "Drucker „Müller“ zieht Papier ein",
            "Zeile eins\nZeile zwei; mit Semikolon",
            TicketPriority.High,
            "Müller, Anna",
            TicketSource.Phone,
            customerEmail: "anna.mueller@example.net",
            createdById: TestDaten.Bearbeiter1.Id,
            createdByName: TestDaten.Bearbeiter1.Name,
            address: "A-101");

        await _tickets.AddCommentAsync(ticket.Id, TestDaten.Bearbeiter1, "Rückruf für 14 Uhr vereinbart");
        await _tickets.AssignAsync(
            ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Bearbeiter1);
        await _tickets.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Bearbeiter1);

        _db.Kontakte.Add(new Kontakt
        {
            Name = "Müller, Anna",
            NameNormalisiert = "müller, anna",
            LetzteAdresse = "A-101",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LetzteRufnummer = "0221123456"
        });

        var kategorie = new KbKategorie { Name = "Drucker" };
        var artikel = new KbArticle
        {
            Title = "Papierstau lösen",
            Content = "Klappe öffnen, Blatt gerade herausziehen.",
            Kategorie = kategorie,
            Tags = [new KbTag { Name = "Drucker", NameNormalisiert = "drucker" }],
            Published = true,
            Owner = TestDaten.Teamleitung.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.KbArticles.Add(artikel);

        // Der Vorschlag braucht die Nummer des Artikels, also erst speichern.
        await _db.SaveChangesAsync();

        _db.KbKategorien.Add(new KbKategorie { Name = "Netzwerk" });
        _db.KbTags.Add(new KbTag { Name = "VPN", NameNormalisiert = "vpn" });
        _db.KbVorschlaege.Add(new KbAenderungsvorschlag
        {
            ArticleId = artikel.Id,
            Title = "Papierstau lösen",
            Content = "Zusatz: Bei Modell X zuerst die hintere Klappe öffnen.",
            VorgeschlagenVon = TestDaten.Bearbeiter1.Name,
            VorgeschlagenAm = DateTime.UtcNow
        });

        _db.Ruhezeiten.Add(new Ruhezeit
        {
            Bezeichnung = "Betriebsruhe",
            Von = new DateTime(2026, 12, 24),
            Bis = new DateTime(2027, 1, 2),
            AngelegtVon = TestDaten.Administration.Name,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task Sicherung_und_Rueckspielen_stellen_denselben_Bestand_her()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();

        var stand = _sicherung.Erstellen(TestDaten.Administration);

        await _db.TicketComments.ExecuteDeleteAsync();
        await _db.TicketHistory.ExecuteDeleteAsync();
        await _db.Tickets.ExecuteDeleteAsync();
        await _db.Kontakte.ExecuteDeleteAsync();
        _db.ChangeTracker.Clear();
        Assert.Equal(0, await _db.Tickets.CountAsync());

        var danach = await _sicherung.WiederherstellenAsync(TestDaten.Administration, stand.Pfad);

        Assert.Equal(vorher, danach);
    }

    [Fact]
    public async Task Wiederherstellen_bringt_den_Bestand_auf_eine_leere_Datenbank()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();
        var stand = _sicherung.Erstellen(TestDaten.Administration);

        var neuerLaptop = Kontext(Path.Combine(_ordner, "neuer-laptop.db"));
        _weitere.Add(neuerLaptop);
        var dortigeSicherung = new SicherungService(
            neuerLaptop, Optionen(), NullLogger<SicherungService>.Instance);

        var danach = await dortigeSicherung.WiederherstellenAsync(TestDaten.Administration, stand.Pfad);

        Assert.Equal(vorher, danach);
        Assert.Equal("Drucker „Müller“ zieht Papier ein", (await neuerLaptop.Tickets.FirstAsync()).Title);
    }

    [Fact]
    public async Task Eine_Datei_die_keine_Datenbank_ist_wird_abgewiesen()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();
        var fremd = Path.Combine(_ordner, "notiz.txt");
        await File.WriteAllTextAsync(fremd, "Das ist eine Notiz und keine Datenbank.");

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sicherung.WiederherstellenAsync(TestDaten.Administration, fremd));

        Assert.Contains("keine SQLite-Datenbank", fehler.Message);
        Assert.Equal(vorher, await _sicherung.BestandAsync());
    }

    [Fact]
    public async Task Eine_fremde_Datenbank_ohne_Tickets_wird_abgewiesen()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();
        var fremd = Path.Combine(_ordner, "adressbuch.db");
        using (var verbindung = new SqliteConnection($"Data Source={fremd};Pooling=false"))
        {
            verbindung.Open();
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "CREATE TABLE Adressen (Id INTEGER PRIMARY KEY, Name TEXT)";
            befehl.ExecuteNonQuery();
        }

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sicherung.WiederherstellenAsync(TestDaten.Administration, fremd));

        Assert.Contains("nicht aus dem Ticketsystem", fehler.Message);
        Assert.Equal(vorher, await _sicherung.BestandAsync());
    }

    [Fact]
    public async Task Sichern_und_Exportieren_darf_nur_die_Administration()
    {
        Assert.Throws<InvalidOperationException>(() => _sicherung.Erstellen(TestDaten.Teamleitung));
        Assert.Throws<InvalidOperationException>(() => _sicherung.Liste(TestDaten.Bearbeiter1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _austausch.ExportJsonAsync(TestDaten.Teamleitung));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _austausch.ImportJsonAsync(TestDaten.Bearbeiter1, new MemoryStream()));
    }

    [Fact]
    public async Task Export_und_Import_erhalten_Nummer_Umlaute_und_Zeilenumbrueche()
    {
        var ticket = await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();

        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);
        var danach = await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        Assert.Equal(vorher, danach);

        var zurueck = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Comments)
            .Include(t => t.History)
            .SingleAsync();

        // Die Ticketnummer steht in Mails und auf Notizzetteln; ein Import, der neu
        // nummeriert, entwertet beides.
        Assert.Equal(ticket.Id, zurueck.Id);
        Assert.Equal("Drucker „Müller“ zieht Papier ein", zurueck.Title);
        Assert.Contains("\n", zurueck.Description);
        Assert.Equal("A-101", zurueck.Address);
        Assert.Single(zurueck.Comments);
        Assert.Contains(zurueck.History, h => h.Field == "Status");
    }

    [Fact]
    public async Task Import_ersetzt_den_Bestand_statt_ihn_zu_verdoppeln()
    {
        await BestandAufbauenAsync();
        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);

        await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));
        var danach = await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        Assert.Equal(1, danach.Tickets);
        Assert.Equal(1, danach.Artikel);
        Assert.Equal(1, danach.Kontakte);
    }

    [Fact]
    public async Task Eine_fremde_JSON_Datei_scheitert_ohne_Datenverlust()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();
        var fremd = new MemoryStream(Encoding.UTF8.GetBytes("""{"anwendung":"Adressbuch","version":1}"""));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _austausch.ImportJsonAsync(TestDaten.Administration, fremd));

        Assert.Contains("nicht aus dem Ticketsystem", fehler.Message);
        Assert.Equal(vorher, await _sicherung.BestandAsync());
    }

    [Fact]
    public async Task Eine_beschaedigte_Datei_scheitert_verstaendlich()
    {
        await BestandAufbauenAsync();
        var vorher = await _sicherung.BestandAsync();
        var kaputt = new MemoryStream(Encoding.UTF8.GetBytes("{ das ist kein JSON"));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _austausch.ImportJsonAsync(TestDaten.Administration, kaputt));

        Assert.Contains("keine gültige JSON-Datei", fehler.Message);
        Assert.Equal(vorher, await _sicherung.BestandAsync());
    }

    [Fact]
    public async Task Eine_aeltere_Formatversion_wird_abgewiesen()
    {
        await BestandAufbauenAsync();
        var kuenftig = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"anwendung":"Ticketsystem","version":99,"tickets":[]}"""));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _austausch.ImportJsonAsync(TestDaten.Administration, kuenftig));

        Assert.Contains("Format-Version 99", fehler.Message);
        Assert.Equal(1, (await _sicherung.BestandAsync()).Tickets);
    }

    [Fact]
    public async Task Der_Export_enthaelt_keine_Passwoerter()
    {
        await BestandAufbauenAsync();
        var konto = await _db.Users.FirstAsync();
        konto.PasswordHash = "HASH-DER-NICHT-RAUS-DARF";
        await _db.SaveChangesAsync();

        var datei = Encoding.UTF8.GetString(await _austausch.ExportJsonAsync(TestDaten.Administration));

        Assert.DoesNotContain("HASH-DER-NICHT-RAUS-DARF", datei);
    }

    [Fact]
    public async Task Die_Tabellen_Ausgabe_liefert_alle_Dateien_und_haelt_Sonderzeichen()
    {
        await BestandAufbauenAsync();

        var zip = await _austausch.ExportCsvAsync(TestDaten.Administration);

        using var archiv = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.Equal(
            ["LIESMICH.txt", "historie.csv", "kommentare.csv", "kontakte.csv", "tickets.csv", "wissen.csv"],
            archiv.Entries.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal));

        var roh = Bytes(archiv, "tickets.csv");
        // Ohne Byte-Order-Mark liest Excel die Datei als Windows-1252 und macht aus
        // „Müller" ein „MÃ¼ller".
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, roh.Take(3));

        var text = Encoding.UTF8.GetString(roh);
        Assert.Contains("Müller", text);
        // Semikolon und Zeilenumbruch im Text müssen in Anführungszeichen stehen,
        // sonst zerfällt die Zeile in zwei Spalten.
        Assert.Contains("\"Zeile eins\nZeile zwei; mit Semikolon\"", text);
    }

    [Fact]
    public async Task Formelzeichen_werden_in_der_Tabelle_entschaerft()
    {
        await _tickets.CreateAsync("=1+1", "harmlos", TicketPriority.Low, "Test", TicketSource.Web);

        var zip = await _austausch.ExportCsvAsync(TestDaten.Administration);

        using var archiv = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.Contains("'=1+1", Encoding.UTF8.GetString(Bytes(archiv, "tickets.csv")));
    }

    // Die Spalte trägt auch den Eingang händisch erfasster Mails; „Anrufzeit"
    // wäre für die Hälfte der Zeilen die falsche Beschriftung.
    [Fact]
    public async Task Die_Tabellenausgabe_nennt_die_Spalte_Eingang()
    {
        await BestandAufbauenAsync();

        var zip = await _austausch.ExportCsvAsync(TestDaten.Administration);

        using var archiv = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var kopf = Encoding.UTF8.GetString(Bytes(archiv, "tickets.csv")).Split('\n')[0];
        Assert.Contains("Eingang (UTC)", kopf);
        Assert.DoesNotContain("Anrufzeit", kopf);
    }

    [Fact]
    public async Task Export_und_Import_erhalten_offene_Aenderungsvorschlaege()
    {
        await BestandAufbauenAsync();

        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);
        await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        var vorschlag = await _db.KbVorschlaege.AsNoTracking().SingleAsync();
        Assert.Equal("Papierstau lösen", vorschlag.Title);
        Assert.Contains("hintere Klappe", vorschlag.Content);
        Assert.Equal(TestDaten.Bearbeiter1.Name, vorschlag.VorgeschlagenVon);

        // Der Artikel bekommt beim Import eine neue Nummer; die Zuordnung muss
        // trotzdem stehen, sonst ist der Vorschlag ein Zettel ohne Adressat.
        var artikel = await _db.KbArticles.AsNoTracking().SingleAsync();
        Assert.Equal(artikel.Id, vorschlag.ArticleId);
    }

    [Fact]
    public async Task Export_und_Import_erhalten_unbenutzte_Kategorien_und_Schlagworte()
    {
        await BestandAufbauenAsync();

        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);
        await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        Assert.Contains(await _db.KbKategorien.AsNoTracking().ToListAsync(), k => k.Name == "Netzwerk");
        Assert.Contains(await _db.KbTags.AsNoTracking().ToListAsync(), t => t.Name == "VPN");

        Assert.Single(await _db.KbKategorien.AsNoTracking().Where(k => k.Name == "Drucker").ToListAsync());
        Assert.Single(await _db.KbTags.AsNoTracking().Where(t => t.NameNormalisiert == "drucker").ToListAsync());
    }

    [Fact]
    public async Task Das_Mengengeruest_nennt_auch_Vorschlaege_und_Vokabular()
    {
        await BestandAufbauenAsync();

        var vorher = await _sicherung.BestandAsync();
        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);
        var danach = await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        Assert.Equal(1, vorher.Vorschlaege);
        Assert.Equal(2, vorher.Kategorien);
        Assert.Equal(2, vorher.Tags);
        Assert.Equal(vorher, danach);
    }

    [Fact]
    public async Task Eine_Datei_der_Vorversion_wird_eingelesen()
    {
        await BestandAufbauenAsync();
        var alt = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {"anwendung":"Ticketsystem","version":1,"tickets":[
              {"id":9001,"titel":"Aus der Vorversion","beschreibung":"x","kundeName":"Test",
               "erstellt":"2026-01-01T08:00:00Z","geaendert":"2026-01-01T08:00:00Z"}]}
            """));

        var danach = await _austausch.ImportJsonAsync(TestDaten.Administration, alt);

        Assert.Equal(1, danach.Tickets);
        Assert.Equal(0, danach.Vorschlaege);
        Assert.Equal(9001, (await _db.Tickets.AsNoTracking().SingleAsync()).Id);
    }

    // Bis Version 2 trug ein Kontakt eine Liste von Nummern, jetzt eine
    // einzelne. Es gilt dieselbe Wahl wie in der Migration: die zuletzt
    // genannte, nicht die zuletzt notierte. Die Reihenfolge in der Datei ist
    // deshalb absichtlich nicht chronologisch.
    [Fact]
    public async Task Eine_Datei_der_Version_2_bringt_von_mehreren_Rufnummern_die_juengste_mit()
    {
        await BestandAufbauenAsync();
        var alt = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {"anwendung":"Ticketsystem","version":2,
             "tickets":[{"id":9002,"titel":"Alt","beschreibung":"x","kundeName":"Test",
               "erstellt":"2026-01-01T08:00:00Z","geaendert":"2026-01-01T08:00:00Z"}],
             "kontakte":[{"name":"Frodo","letzteAdresse":"B-120",
               "erstellt":"2026-01-01T08:00:00Z","geaendert":"2026-03-01T08:00:00Z",
               "rufnummern":[
                 {"nummer":"0151231412","art":3,"erstellt":"2026-01-01T08:00:00Z"},
                 {"nummer":"0151222333","art":3,"erstellt":"2026-03-01T08:00:00Z"},
                 {"nummer":"0151231222","art":3,"erstellt":"2026-02-01T08:00:00Z"}]}]}
            """));

        await _austausch.ImportJsonAsync(TestDaten.Administration, alt);

        var frodo = await _db.Kontakte.AsNoTracking().SingleAsync(k => k.Name == "Frodo");
        Assert.Equal("0151222333", frodo.LetzteRufnummer);
        Assert.Equal("B-120", frodo.LetzteAdresse);
    }

    [Fact]
    public async Task Export_und_Import_erhalten_die_Fassungen_eines_Artikels()
    {
        await BestandAufbauenAsync();
        var kb = new KnowledgeBaseService(_db);
        var artikel = await kb.SpeichernAsync(null, "Mit Geschichte", "Erster Stand.", null, true, 180, [], TestDaten.Teamleitung);
        await kb.SpeichernAsync(artikel.Id, "Mit Geschichte", "Zweiter Stand.", null, true, 180, [], TestDaten.Teamleitung);

        var datei = await _austausch.ExportJsonAsync(TestDaten.Administration);
        await _austausch.ImportJsonAsync(TestDaten.Administration, new MemoryStream(datei));

        var zurueck = await _db.KbArticles.AsNoTracking().Include(a => a.Fassungen)
            .SingleAsync(a => a.Title == "Mit Geschichte");
        Assert.Equal(2, zurueck.Fassungen.Count);
        var fassungen = zurueck.Fassungen.OrderBy(f => f.Nummer).ToList();
        Assert.Equal("Erster Stand.", fassungen[0].Content);
        Assert.Equal(Fassungsanlass.Angelegt, fassungen[0].Anlass);
        Assert.Equal("Zweiter Stand.", fassungen[1].Content);
        Assert.Equal(TestDaten.Teamleitung.Name, fassungen[1].GespeichertVon);
    }

    [Fact]
    public async Task Eine_alte_Datei_mit_Sichtbarkeit_wird_ohne_sie_eingelesen()
    {
        await BestandAufbauenAsync();
        var alt = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {"anwendung":"Ticketsystem","version":2,
             "tickets":[{"id":9003,"titel":"Alt","beschreibung":"x","kundeName":"Test",
               "erstellt":"2026-01-01T08:00:00Z","geaendert":"2026-01-01T08:00:00Z"}],
             "wissensartikel":[{"titel":"Aus alter Datei","inhalt":"Text","sichtbarkeit":"Public",
               "veroeffentlicht":true,"pruefzyklusTage":180,
               "erstellt":"2026-01-01T08:00:00Z","geaendert":"2026-01-01T08:00:00Z"}]}
            """));

        var danach = await _austausch.ImportJsonAsync(TestDaten.Administration, alt);

        Assert.Equal(1, danach.Artikel);
        var artikel = await _db.KbArticles.AsNoTracking().Include(a => a.Fassungen).SingleAsync();
        Assert.Equal("Aus alter Datei", artikel.Title);
        Assert.True(artikel.Published);
        // Eine Datei ohne Fassungen: Der Artikel bekommt seinen Stand als Fassung
        // 1 mit einem Anlass, der sagt, woher er kommt; eine leere Geschichte sähe
        // aus wie ein Fehler.
        var fassung = Assert.Single(artikel.Fassungen);
        Assert.Equal(1, fassung.Nummer);
        Assert.Equal(Fassungsanlass.AusSicherung, fassung.Anlass);
        Assert.Equal("Text", fassung.Content);
    }

    // Die Entschärfung trifft auch Texte, die keine Formel sind, und bleibt
    // trotzdem vollständig; dafür erklärt LIESMICH.txt im Archiv das Hochkomma.
    [Fact]
    public async Task Ein_Aufzaehlungsstrich_bleibt_lesbar_und_eine_Formel_bleibt_entschaerft()
    {
        await _tickets.CreateAsync("- Drucker piept", "=1+1", TicketPriority.Low, "Test", TicketSource.Web);

        var zip = await _austausch.ExportCsvAsync(TestDaten.Administration);

        using var archiv = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var tabelle = Encoding.UTF8.GetString(Bytes(archiv, "tickets.csv"));
        Assert.Contains("'- Drucker piept", tabelle);
        Assert.Contains("'=1+1", tabelle);

        var hinweis = Encoding.UTF8.GetString(Bytes(archiv, "LIESMICH.txt"));
        Assert.Contains("Hochkomma", hinweis);
        Assert.Contains("Export als JSON", hinweis);
    }

    [Fact]
    public async Task Aufraeumen_behaelt_nur_die_vereinbarte_Zahl_an_Staenden()
    {
        await BestandAufbauenAsync();
        var knapp = new SicherungService(_db, Optionen(aufbewahrt: 3), NullLogger<SicherungService>.Instance);

        Sicherung? letzte = null;
        for (var lauf = 0; lauf < 5; lauf++)
        {
            letzte = knapp.Erstellen(TestDaten.Administration);
        }

        Assert.Equal(3, Directory.GetFiles(_sicherungsOrdner, "*.db").Length);
        Assert.True(File.Exists(letzte!.Pfad), "Der zuletzt erstellte Stand darf nie der gelöschte sein.");
    }

    [Fact]
    public async Task Die_Sicherung_beim_Start_ueberspringt_einen_leeren_Bestand()
    {
        Assert.Null(await _sicherung.BeimStartSichernAsync());

        await BestandAufbauenAsync();

        Assert.NotNull(await _sicherung.BeimStartSichernAsync());
    }

    private static byte[] Bytes(ZipArchive archiv, string name)
    {
        using var strom = archiv.GetEntry(name)!.Open();
        using var speicher = new MemoryStream();
        strom.CopyTo(speicher);
        return speicher.ToArray();
    }

    public void Dispose()
    {
        _db.Dispose();
        foreach (var weiterer in _weitere)
        {
            weiterer.Dispose();
        }

        // Ohne das Leeren der Verbindungspools hält SQLite die Dateien unter
        // Windows offen, und das Aufräumen scheitert.
        try
        {
            Directory.Delete(_ordner, recursive: true);
        }
        // Ein liegengebliebener Testordner ist kein Grund für einen roten Lauf.
        catch (IOException)
        {
        }
    }
}
