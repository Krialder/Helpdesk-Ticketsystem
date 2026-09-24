using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Der Weg einer bestehenden Datenbank auf den aktuellen Stand, geprüft an
// einer echten Alt-Datenbank: Eine frische hat nichts zu übernehmen, und
// genau die Übernahme ist der Prüfgegenstand. Die Datenbank wird erst auf
// einen alten Stand gebracht, mit Altdaten gefüllt und dann fertig
// migriert.
public sealed class MigrationTests : IDisposable
{
    // Der letzte Stand mit Freitext-Kategorien am Artikel.
    private const string StandVorKategorietabelle = "20260819091341_AddRuhezeitenUndSlaMelder";

    // Der letzte Stand, in dem ein Kontakt beliebig viele Rufnummern in einer
    // eigenen Tabelle hatte.
    private const string StandVorEinerRufnummer = "20260901032634_AddHistorieKontokennung";

    private readonly string _ordner;
    private readonly string _datei;

    public MigrationTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_ordner);
        _datei = Path.Combine(_ordner, "app.db");
    }

    [Fact]
    public async Task Ein_Altbestand_behaelt_seine_Kategorien_und_meldet_keine_Warnung()
    {
        var warnungen = new List<string>();

        await using (var alt = Kontext())
        {
            await alt.GetService<IMigrator>().MigrateAsync(StandVorKategorietabelle);
        }

        AltartikelEinfuegen();

        await using (var neu = Kontext(warnungen))
        {
            await neu.Database.MigrateAsync();
        }

        await using var pruefer = Kontext();
        var kategorien = await pruefer.KbKategorien.AsNoTracking().OrderBy(k => k.Id).ToListAsync();
        Assert.Equal(["Drucker", "Netzwerk"], kategorien.Select(k => k.Name));

        var artikel = await pruefer.KbArticles.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, artikel.Count);
        Assert.All(artikel, a => Assert.NotNull(a.KategorieId));
        Assert.Equal(kategorien[0].Id, artikel[0].KategorieId);
        Assert.Equal(kategorien[1].Id, artikel[1].KategorieId);

        // Die Alt-Datenbank hatte die Sichtbarkeitsspalte (Visibility = 1 in den
        // INSERTs); ihr Abriss darf den Inhalt nicht mitnehmen.
        Assert.DoesNotContain("Visibility", await SpaltenAsync(pruefer, "KbArticles"));
        Assert.DoesNotContain("Visibility", await SpaltenAsync(pruefer, "KbVorschlaege"));

        // Jeder Altartikel bekommt seinen Stand als Fassung 1 mit dem Namen seiner
        // Kategorie; ein Artikel ohne Fassung wäre in der Liste ein Artikel ohne
        // Vergangenheit, obwohl er Monate alt sein kann.
        var fassungen = await pruefer.KbFassungen.AsNoTracking().OrderBy(f => f.ArticleId).ToListAsync();
        Assert.Equal(2, fassungen.Count);
        Assert.All(fassungen, f => Assert.Equal(1, f.Nummer));
        Assert.All(fassungen, f => Assert.Equal(Fassungsanlass.Altbestand, f.Anlass));
        Assert.Equal("Altartikel", fassungen[0].Title);
        Assert.Equal("Drucker", fassungen[0].Kategorie);
        Assert.Equal("Netzwerk", fassungen[1].Kategorie);

        // Kein Hinweis auf einen Tabellenumbau im Protokoll: Die Warnung wäre kein
        // Fehler, aber wer bei jedem Start eine Warnung liest, liest bald keine
        // mehr.
        Assert.DoesNotContain(warnungen, w => w.Contains("rebuild of table", StringComparison.Ordinal));
    }

    // Die Migration nimmt die zuletzt genannte Nummer, also die mit dem
    // jüngsten Zeitstempel, nicht die zuletzt eingefügte Zeile. Die Reihenfolge
    // der INSERTs ist absichtlich nicht chronologisch, sonst wäre der Test
    // blind für genau diesen Unterschied.
    [Fact]
    public async Task Ein_Altbestand_behaelt_von_mehreren_Rufnummern_die_juengste()
    {
        await using (var alt = Kontext())
        {
            await alt.GetService<IMigrator>().MigrateAsync(StandVorEinerRufnummer);
        }

        AltkontaktEinfuegen();

        await using (var neu = Kontext())
        {
            await neu.Database.MigrateAsync();
        }

        await using var pruefer = Kontext();
        var kontakte = await pruefer.Kontakte.AsNoTracking().OrderBy(k => k.Id).ToListAsync();
        Assert.Equal(2, kontakte.Count);
        var frodo = kontakte[0];
        Assert.Equal("Frodo", frodo.Name);
        Assert.Equal("0151222333", frodo.LetzteRufnummer);
        Assert.Equal("B-120", frodo.LetzteAdresse);

        var ohne = await pruefer.Kontakte.AsNoTracking().SingleOrDefaultAsync(k => k.Name == "Hans");
        Assert.NotNull(ohne);
        Assert.Null(ohne.LetzteRufnummer);
    }

    private TicketsystemContext Kontext(List<string>? warnungen = null)
    {
        var bauer = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={_datei};Pooling=false");

        if (warnungen is not null)
        {
            bauer = bauer.UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new Sammler(warnungen))));
        }

        return new TicketsystemContext(bauer.Options);
    }

    private void AltartikelEinfuegen()
    {
        using var verbindung = new SqliteConnection($"Data Source={_datei};Pooling=false");
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        // Das führende und schließende Leerzeichen um „ Drucker " ist Absicht: Die
        // Übernahme schneidet es ab.
        befehl.CommandText =
            """
            INSERT INTO KbArticles (Title, Content, Category, Visibility, Published, CreatedAt, UpdatedAt)
            VALUES ('Altartikel', 'Inhalt', ' Drucker ', 1, 1, '2026-01-01 08:00:00', '2026-01-01 08:00:00'),
                   ('Zweiter Altartikel', 'Inhalt', 'Netzwerk', 1, 1, '2026-01-01 08:00:00', '2026-01-01 08:00:00');
            """;
        befehl.ExecuteNonQuery();
    }

    private static async Task<List<string>> SpaltenAsync(TicketsystemContext db, string tabelle)
    {
        var spalten = new List<string>();
        var verbindung = db.Database.GetDbConnection();
        await verbindung.OpenAsync();
        await using var befehl = verbindung.CreateCommand();
        befehl.CommandText = $"PRAGMA table_info({tabelle});";
        await using var leser = await befehl.ExecuteReaderAsync();
        while (await leser.ReadAsync())
        {
            spalten.Add(leser.GetString(1));
        }

        return spalten;
    }

    private void AltkontaktEinfuegen()
    {
        using var verbindung = new SqliteConnection($"Data Source={_datei};Pooling=false");
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText =
            """
            INSERT INTO Kontakte (Id, Name, NameNormalisiert, LetzteAdresse, CreatedAt, UpdatedAt)
            VALUES (1, 'Frodo', 'frodo', 'B-120', '2026-01-01 08:00:00', '2026-03-01 08:00:00'),
                   (2, 'Hans', 'hans', 'A-101', '2026-01-01 08:00:00', '2026-01-01 08:00:00');
            INSERT INTO KontaktRufnummern (KontaktId, Nummer, Art, CreatedAt)
            VALUES (1, '0151231412', 3, '2026-01-01 08:00:00'),
                   (1, '0151222333', 3, '2026-03-01 08:00:00'),
                   (1, '0151231222', 3, '2026-02-01 08:00:00');
            """;
        befehl.ExecuteNonQuery();
    }

    // Ein Protokollanbieter, der nur mitschreibt; kürzer als jedes fertige
    // Paket und ohne weitere Abhängigkeit.
    private sealed class Sammler(List<string> ziel) : ILoggerProvider
    {
        public ILogger CreateLogger(string kategorie) => new Schreiber(ziel);

        public void Dispose()
        {
        }

        private sealed class Schreiber(List<string> ziel) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    lock (ziel)
                    {
                        ziel.Add(formatter(state, exception));
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        Directory.Delete(_ordner, recursive: true);
    }
}
