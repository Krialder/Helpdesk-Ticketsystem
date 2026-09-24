using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketsystem.Kern.Start;

namespace Ticketsystem.Tests;

// Baut den Generic Host mit derselben Dienstverdrahtung wie die
// Desktop-Anwendung über einer eigenen Temp-Datenbank, samt Migrationen und
// Seed. Tests kennen nur Services und MitEinstellungen, nicht den Aufbau.
public sealed class KernWirt : IDisposable
{
    // Ein eigenes Verzeichnis je Wirt, nicht nur ein eigener Dateiname: Der
    // Erfassungsentwurf liegt neben der Datenbank, und in einem gemeinsamen
    // Temp-Ordner sähe ein Test den Entwurf eines anderen.
    private readonly string _verzeichnis =
        Path.Combine(Path.GetTempPath(), $"kernwirt-{Guid.NewGuid():N}");

    private readonly Dictionary<string, string?> _einstellungen = [];
    private IHost? _host;

    public KernWirt() => Directory.CreateDirectory(_verzeichnis);

    private string DbPfad => Path.Combine(_verzeichnis, "ticketsystem.db");
    // Vor dem ersten Zugriff auf Services aufrufen; der Wirt entsteht bei diesem
    // Zugriff, danach ist die Konfiguration eingefroren.
    public void MitEinstellungen(Dictionary<string, string> einstellungen)
    {
        foreach (var (schluessel, wert) in einstellungen)
        {
            _einstellungen[schluessel] = wert;
        }
    }

    public IServiceProvider Services => (_host ??= Bauen()).Services;

    private IHost Bauen()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={DbPfad};Pooling=false",
            ["Daten:SicherungBeimStart"] = "false"
        });
        builder.Configuration.AddInMemoryCollection(_einstellungen);

        // Ohne eigenen Pfad nähme die Verdrahtung die Einstellungsdatei des
        // Benutzerprofils, und ein Test schriebe ins echte Profil des Prüfrechners.
        KernDienste.Registrieren(builder.Services, builder.Configuration,
            Path.Combine(_verzeichnis, "einstellungen.json"));

        var host = builder.Build();
        // Der Host wird nicht gestartet, damit keine Hintergrunddienste in Tests
        // takten; Migrationen und Seed laufen trotzdem wie beim echten Start.
        Startablauf.DatenbankVorbereitenAsync(host.Services, builder.Configuration, NullLogger.Instance)
            .GetAwaiter().GetResult();
        return host;
    }

    public void Dispose()
    {
        _host?.Dispose();
        // Kein ClearAllPools: Das leert den prozessweiten Pool und reißt parallel
        // laufenden Testklassen die Verbindung weg (ObjectDisposedException auf
        // sqlite3). Ohne Pooling hält niemand die Datei nach Dispose offen.
        if (Directory.Exists(_verzeichnis))
        {
            Directory.Delete(_verzeichnis, recursive: true);
        }
    }
}
