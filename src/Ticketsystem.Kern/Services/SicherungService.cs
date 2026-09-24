using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public sealed class DatenOptions
{
    public string? SicherungsOrdner { get; set; }

    public string? ZweitSicherungsOrdner { get; set; }

    public int AufbewahrteSicherungen { get; set; } = 10;

    public bool SicherungBeimStart { get; set; } = true;

    // Vier Stunden heißen höchstens einen halben Arbeitstag Verlust und zwei
    // bis drei Stände am Tag, passend zur Aufbewahrung von zehn. Null schaltet
    // den Rhythmus ab.
    public int SicherungIntervallStunden { get; set; } = 4;
}

// Was die automatische Sicherung dieses Laufs zuletzt getan hat, damit die
// Datenseite einen Fehlschlag zeigt und nicht nur das Protokoll. Lebt nur
// im Arbeitsspeicher: Nach einem Neustart ist die Liste der Sicherungen
// die Wahrheit.
public sealed class Sicherungsstand
{
    private readonly object _schloss = new();

    public DateTime? LetzterErfolg { get; private set; }

    public string? LetzterPfad { get; private set; }

    public string? LetzterFehler { get; private set; }

    public bool LetzterVersuchErfolgreich { get; private set; } = true;

    // Getrennt vom Hauptergebnis: Ein fehlender Stick entwertet die
    // Hauptsicherung nicht.
    public string? ZweitHinweis { get; private set; }

    public void ZweitGelungen()
    {
        lock (_schloss)
        {
            ZweitHinweis = null;
        }
    }

    public void ZweitGescheitert(string grund)
    {
        lock (_schloss)
        {
            ZweitHinweis = grund;
        }
    }

    public void Gelungen(Sicherung sicherung)
    {
        lock (_schloss)
        {
            LetzterErfolg = sicherung.Erstellt;
            LetzterPfad = sicherung.Pfad;
            LetzterFehler = null;
            LetzterVersuchErfolgreich = true;
        }
    }

    public void Gescheitert(string grund)
    {
        lock (_schloss)
        {
            LetzterFehler = grund;
            LetzterVersuchErfolgreich = false;
        }
    }
}

public sealed record Sicherung(string Pfad, DateTime Erstellt, long Bytes)
{
    public string Dateiname => Path.GetFileName(Pfad);

    public string GroesseAnzeige => Bytes < 1024 * 1024
        ? $"{Bytes / 1024.0:0.#} KB"
        : $"{Bytes / (1024.0 * 1024.0):0.#} MB";
}

public sealed record Bestand(
    int Tickets,
    int Historie,
    int Kommentare,
    int Kontakte,
    int Artikel,
    int Ruhezeiten,
    int Vorschlaege,
    int Kategorien,
    int Tags);

// Sicherung und Wiederherstellung der SQLite-Datei, dazu Aufräumen alter
// Stände und Spiegelkopie auf ein zweites Ziel. Beide Richtungen laufen
// über die Online-Backup-Schnittstelle von SQLite statt über File.Copy:
// Die Anwendung hält die Datei offen, und eine Kopie mitten in einer
// Schreibtransaktion ergäbe eine beschädigte Sicherung.
public sealed class SicherungService(
    TicketsystemContext db,
    IOptions<DatenOptions> options,
    ILogger<SicherungService> log,
    // Optional, damit Tests den Dienst ohne Stand bauen; im Betrieb kommt er
    // als Singleton aus dem Dienstverzeichnis.
    Sicherungsstand? stand = null)
{
    private readonly Sicherungsstand _stand = stand ?? new Sicherungsstand();

    // Die ersten 15 Bytes jeder SQLite-Datei. Die Prüfung fängt ab, dass
    // jemand ein Bild als Sicherung einspielt.
    private static readonly byte[] SqliteKennung = "SQLite format 3"u8.ToArray();

    private const string VorsatzSicherung = "app-";

    private const string VorsatzVorRueckspielen = "vor-wiederherstellung-";

    private const string Zeitstempel = "yyyy-MM-dd-HHmm-ss";

    public string DatenbankDatei => db.Database.GetDbConnection().DataSource;

    public string SicherungsOrdner => string.IsNullOrWhiteSpace(options.Value.SicherungsOrdner)
        ? Datenablage.SicherungsOrdnerFuer(DatenbankDatei)
        : Path.GetFullPath(options.Value.SicherungsOrdner);

    public string? ZweitSicherungsOrdner => string.IsNullOrWhiteSpace(options.Value.ZweitSicherungsOrdner)
        ? null
        : Path.GetFullPath(options.Value.ZweitSicherungsOrdner);

    public async Task<Bestand> BestandAsync() => new(
        await db.Tickets.CountAsync(),
        await db.TicketHistory.CountAsync(),
        await db.TicketComments.CountAsync(),
        await db.Kontakte.CountAsync(),
        await db.KbArticles.CountAsync(),
        await db.Ruhezeiten.CountAsync(),
        await db.KbVorschlaege.CountAsync(),
        await db.KbKategorien.CountAsync(),
        await db.KbTags.CountAsync());

    public Sicherung Erstellen(Akteur akteur, string? zielOrdner = null)
    {
        RequireAdministration(akteur);
        var ordner = Ordner(zielOrdner);
        var sicherung = Schreiben(ordner, VorsatzSicherung);
        Aufraeumen(ordner);
        Spiegeln(sicherung);
        return sicherung;
    }

    public IReadOnlyList<Sicherung> Liste(Akteur akteur, string? ordner = null)
    {
        RequireAdministration(akteur);
        return Staende(Ordner(ordner));
    }

    private static IReadOnlyList<Sicherung> Staende(string ordner)
    {
        if (!Directory.Exists(ordner))
        {
            return [];
        }

        return new DirectoryInfo(ordner).GetFiles("*.db")
            .OrderByDescending(datei => datei.LastWriteTimeUtc)
            .ThenByDescending(datei => datei.Name, StringComparer.Ordinal)
            .Select(datei => new Sicherung(datei.FullName, datei.LastWriteTimeUtc, datei.Length))
            .ToList();
    }

    public async Task<Bestand> WiederherstellenAsync(Akteur akteur, string pfad)
    {
        RequireAdministration(akteur);

        var quellPfad = Path.GetFullPath(pfad);
        Pruefen(quellPfad);

        // Sicherheitsnetz vor dem Überschreiben: Wer den falschen Stand einspielt,
        // hat sonst keinen Weg zurück.
        var vorher = Schreiben(SicherungsOrdner, VorsatzVorRueckspielen);
        log.LogInformation("Stand vor der Wiederherstellung gesichert: {Datei}", vorher.Dateiname);

        var ziel = Verbindung();
        var warOffen = ziel.State == ConnectionState.Open;
        if (!warOffen)
        {
            ziel.Open();
        }

        try
        {
            using var quelle = new SqliteConnection(Einmalverbindung(quellPfad, nurLesen: true));
            quelle.Open();
            quelle.BackupDatabase(ziel);
        }
        finally
        {
            if (!warOffen)
            {
                ziel.Close();
            }
        }

        // Eine ältere Sicherung kann einen älteren Schemastand tragen; ohne diesen
        // Schritt liefe die Anwendung gegen fehlende Spalten. Der Verfolger kennt
        // danach noch Zeilen des alten Standes, deshalb wird er geleert.
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        return await BestandAsync();
    }

    // Ein leerer Bestand wird übersprungen, hier wie im Rhythmus: Seine
    // Sicherung verdrängte in der Aufbewahrung nur eine, die etwas enthält.
    public async Task<Sicherung?> BeimStartSichernAsync()
    {
        if (!options.Value.SicherungBeimStart || DatenbankDatei is ":memory:" or "")
        {
            return null;
        }

        if (!await db.Tickets.AnyAsync())
        {
            return null;
        }

        return Sichern("Sicherung beim Start");
    }

    public int IntervallStunden => options.Value.SicherungIntervallStunden;

    // Der Abstand wird am jüngsten Stand im Ordner gemessen, nicht an einem
    // Merker: So zählt die Startsicherung mit, und ein Neustart beginnt nicht
    // bei null. jetzt kommt von außen, damit der Rhythmus ohne Warten auf die
    // Uhr prüfbar ist.
    public async Task<Sicherung?> FaelligeSicherungAsync(DateTime jetzt)
    {
        var intervall = options.Value.SicherungIntervallStunden;
        if (intervall <= 0 || DatenbankDatei is ":memory:" or "")
        {
            return null;
        }

        if (!await db.Tickets.AnyAsync())
        {
            return null;
        }

        var juengster = Staende(SicherungsOrdner).MaxBy(s => s.Erstellt);
        if (juengster is not null && jetzt - juengster.Erstellt < TimeSpan.FromHours(intervall))
        {
            return null;
        }

        return Sichern("Laufende Sicherung");
    }

    // Der gemeinsame Kern der automatischen Wege. Sie dürfen den Betrieb nicht
    // anhalten: Ein Helpdesk ohne Anwendung ist schlimmer als ein Lauf ohne
    // frische Sicherung, also landet ein Fehlschlag im Protokoll und im Stand.
    private Sicherung? Sichern(string anlass)
    {
        try
        {
            var sicherung = Schreiben(SicherungsOrdner, VorsatzSicherung);
            Aufraeumen(SicherungsOrdner);
            log.LogInformation("{Anlass} abgelegt: {Datei}", anlass, sicherung.Pfad);
            _stand.Gelungen(sicherung);
            Spiegeln(sicherung);
            return sicherung;
        }
        catch (Exception ex) when (ex is IOException or SqliteException or UnauthorizedAccessException)
        {
            log.LogError(ex, "{Anlass} fehlgeschlagen, Ordner {Ordner}", anlass, SicherungsOrdner);
            _stand.Gescheitert($"{anlass} in {SicherungsOrdner} fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    // File.Copy genügt hier, denn die Quelle ist schon ein konsistenter Stand.
    // Ein Fehlschlag (Stick fehlt) entwertet die Hauptsicherung nicht und
    // wirft deshalb nicht; er wird als eigener Hinweis im Stand vermerkt.
    private void Spiegeln(Sicherung sicherung)
    {
        var zweitOrdner = options.Value.ZweitSicherungsOrdner;
        if (string.IsNullOrWhiteSpace(zweitOrdner))
        {
            return;
        }

        var ordner = Path.GetFullPath(zweitOrdner);
        try
        {
            Directory.CreateDirectory(ordner);
            File.Copy(sicherung.Pfad, Path.Combine(ordner, sicherung.Dateiname), overwrite: true);
            Aufraeumen(ordner);
            _stand.ZweitGelungen();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogError(ex, "Spiegelkopie in das zweite Sicherungsziel {Ordner} fehlgeschlagen", ordner);
            _stand.ZweitGescheitert(
                $"Die Spiegelkopie nach {ordner} ist fehlgeschlagen: {ex.Message} " +
                "Die Sicherung selbst ist gelungen.");
        }
    }

    // Über alle Stände im Ordner statt je Vorsatz: Eine je Art gezählte
    // Obergrenze wäre keine Obergrenze für den Plattenplatz. Nach Änderungszeit
    // und nicht nach Name, denn der Zusatz gegen Namenskollisionen (-2.db)
    // sortiert sich als Text falsch ein.
    public int Aufraeumen(string? ordner = null)
    {
        var ziel = Ordner(ordner);
        var behalten = Math.Max(1, options.Value.AufbewahrteSicherungen);
        if (!Directory.Exists(ziel))
        {
            return 0;
        }

        var alte = new DirectoryInfo(ziel).GetFiles("*.db")
            .OrderByDescending(datei => datei.LastWriteTimeUtc)
            .ThenByDescending(datei => datei.Name, StringComparer.Ordinal)
            .Skip(behalten)
            .ToList();

        foreach (var datei in alte)
        {
            datei.Delete();
        }

        return alte.Count;
    }

    private static string Einmalverbindung(string pfad, bool nurLesen = false) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = pfad,
            Mode = nurLesen ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            // Ohne Pooling: Der Treiber hält gepoolte Handles bis zum Prozessende.
            // Eine aufgeräumte Sicherung bliebe als Handle zurück, ein gleich
            // benannter Stand landete mit „disk I/O error" in der gelöschten Datei.
            Pooling = false
        }.ToString();

    private string Ordner(string? gewuenscht) =>
        string.IsNullOrWhiteSpace(gewuenscht) ? SicherungsOrdner : Path.GetFullPath(gewuenscht);

    private SqliteConnection Verbindung() =>
        db.Database.GetDbConnection() as SqliteConnection
        ?? throw new InvalidOperationException("Sicherungen gibt es nur für SQLite-Datenbanken.");

    private Sicherung Schreiben(string ordner, string vorsatz)
    {
        Directory.CreateDirectory(ordner);
        var stempel = DateTime.UtcNow;
        var pfad = FreierPfad(ordner, vorsatz, stempel);

        var quelle = Verbindung();
        var warOffen = quelle.State == ConnectionState.Open;
        if (!warOffen)
        {
            quelle.Open();
        }

        try
        {
            using var ziel = new SqliteConnection(Einmalverbindung(pfad));
            ziel.Open();
            quelle.BackupDatabase(ziel);
        }
        finally
        {
            if (!warOffen)
            {
                quelle.Close();
            }
        }

        return new Sicherung(pfad, stempel, new FileInfo(pfad).Length);
    }

    private static string FreierPfad(string ordner, string vorsatz, DateTime stempel)
    {
        var basis = vorsatz + stempel.ToString(Zeitstempel, CultureInfo.InvariantCulture);
        var pfad = Path.Combine(ordner, basis + ".db");
        var zaehler = 2;
        // Zwei Sicherungen in derselben Sekunde sind selten, aber ein still
        // überschriebener Stand wäre genau der Verlust, gegen den das gebaut ist.
        while (File.Exists(pfad))
        {
            pfad = Path.Combine(ordner, $"{basis}-{zaehler++}.db");
        }

        return pfad;
    }

    private static void Pruefen(string pfad)
    {
        if (!File.Exists(pfad))
        {
            throw new InvalidOperationException($"Die Datei „{Path.GetFileName(pfad)}“ gibt es nicht.");
        }

        var kopf = new byte[SqliteKennung.Length];
        using (var datei = File.OpenRead(pfad))
        {
            if (datei.Read(kopf, 0, kopf.Length) < kopf.Length || !kopf.SequenceEqual(SqliteKennung))
            {
                throw new InvalidOperationException(
                    "Diese Datei ist keine SQLite-Datenbank. Der Bestand wurde nicht angefasst.");
            }
        }

        using var verbindung = new SqliteConnection(Einmalverbindung(pfad, nurLesen: true));
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Tickets'";
        if (Convert.ToInt64(befehl.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
        {
            throw new InvalidOperationException(
                "Diese Datenbank stammt nicht aus dem Ticketsystem, die Tabelle „Tickets“ fehlt. " +
                "Der Bestand wurde nicht angefasst.");
        }
    }

    // Administration und nicht Teamleitung, weil Sicherung und
    // Wiederherstellung den gesamten Bestand aller Personen umfassen.
    private static void RequireAdministration(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Administration))
        {
            throw new InvalidOperationException("Sichern und Wiederherstellen darf nur die Administration.");
        }
    }
}
