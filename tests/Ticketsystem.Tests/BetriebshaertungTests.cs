using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;
using Ticketsystem.Kern.Start;

namespace Ticketsystem.Tests;

// Das Startkonto hat kein bekanntes Passwort mehr: „Agent123!" stand
// wörtlich in der README, und jeder Bearbeiter konnte sich damit als
// Administration anmelden. Jeder Wächter wird in beide Richtungen geprüft:
// dass er sperrt, wenn er sperren soll, und dass er den gewollten Weg offen
// lässt; „nie aktiviert" und „abgewiesen" sind zwei verschiedene Dinge.
public sealed class SeedKontoTests : IDisposable
{
    private const string SeedEmail = "agent@ticketsystem.local";

    private KernWirt? _factory;

    [Fact]
    public async Task Ohne_Konfiguration_gilt_das_alte_Standardpasswort_nicht_mehr()
    {
        _factory = new KernWirt();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var konto = await users.FindByEmailAsync(SeedEmail);

        Assert.NotNull(konto);
        Assert.False(await users.CheckPasswordAsync(konto!, "Agent123!"));
    }

    [Fact]
    public async Task Ein_konfiguriertes_Passwort_bleibt_wirksam()
    {
        _factory = new KernWirt();
        _factory.MitEinstellungen(new() { ["SeedAgent:Password"] = "Eigenes-Start1!" });
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var konto = await users.FindByEmailAsync(SeedEmail);

        Assert.NotNull(konto);
        Assert.True(await users.CheckPasswordAsync(konto!, "Eigenes-Start1!"));
    }

    // Die Administration legt ihr eigenes Konto an, löscht das Startkonto, und
    // nach dem nächsten Start stand es wieder da. Der Start seedet deshalb nur,
    // wenn es überhaupt keine Administration gibt.
    [Fact]
    public async Task Das_Startkonto_kehrt_nicht_zurueck_solange_eine_Administration_existiert()
    {
        _factory = new KernWirt();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var eigene = new AppUser { UserName = "chef@example.org", Email = "chef@example.org", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(eigene, "Eigenes-Chef1!")).Succeeded);
        await users.AddToRoleAsync(eigene, Rollen.Admin);
        await users.DeleteAsync((await users.FindByEmailAsync(SeedEmail))!);

        await IdentitySeed.EnsureRolesAndAdminAsync(
            scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IConfiguration>());

        Assert.Null(await users.FindByEmailAsync(SeedEmail));
    }

    [Fact]
    public async Task Ohne_jede_Administration_kehrt_das_Startkonto_zurueck()
    {
        _factory = new KernWirt();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await users.DeleteAsync((await users.FindByEmailAsync(SeedEmail))!);

        await IdentitySeed.EnsureRolesAndAdminAsync(
            scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IConfiguration>());

        Assert.NotNull(await users.FindByEmailAsync(SeedEmail));
    }

    // 16 Zeichen aus allen vier Klassen, damit es an keiner Identity-Vorgabe
    // scheitert.
    [Fact]
    public void Das_Zufallspasswort_erfuellt_die_Kontoregeln_und_wiederholt_sich_nicht()
    {
        var erstes = IdentitySeed.Zufallspasswort();
        var zweites = IdentitySeed.Zufallspasswort();

        Assert.Equal(16, erstes.Length);
        Assert.Contains(erstes, char.IsUpper);
        Assert.Contains(erstes, char.IsLower);
        Assert.Contains(erstes, char.IsDigit);
        Assert.Contains(erstes, z => !char.IsLetterOrDigit(z));
        Assert.NotEqual(erstes, zweites);
    }

    public void Dispose() => _factory?.Dispose();
}

// Die Spiegelkopie auf ein zweites Sicherungsziel: Datenbank, Sicherungen
// und Protokoll teilen sich sonst eine Festplatte, und bei Diebstahl oder
// Plattendefekt ist alles gleichzeitig weg. Kopiert wird die fertige
// Sicherungsdatei, also ein konsistenter Stand, keine offene Datenbank.
public sealed class SpiegelSicherungTests : IDisposable
{
    private readonly string _ordner;
    private readonly string _zweitOrdner;
    private readonly TicketsystemContext _db;
    private readonly Sicherungsstand _stand = new();

    public SpiegelSicherungTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"spiegel-{Guid.NewGuid():N}");
        _zweitOrdner = Path.Combine(_ordner, "Stick");
        Directory.CreateDirectory(_ordner);

        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={Path.Combine(_ordner, "app.db")};Pooling=false")
            .Options);
        _db.Database.Migrate();
    }

    private SicherungService Dienst(string? zweitOrdner, int aufbewahrt = 10) => new(
        _db,
        Options.Create(new DatenOptions
        {
            SicherungsOrdner = Path.Combine(_ordner, "Sicherungen"),
            ZweitSicherungsOrdner = zweitOrdner,
            AufbewahrteSicherungen = aufbewahrt
        }),
        NullLogger<SicherungService>.Instance,
        _stand);

    [Fact]
    public void Nach_der_Sicherung_liegt_dieselbe_Datei_auch_im_Zweitordner()
    {
        var sicherung = Dienst(_zweitOrdner).Erstellen(TestDaten.Administration);

        var kopie = Path.Combine(_zweitOrdner, sicherung.Dateiname);
        Assert.True(File.Exists(kopie), $"Die Spiegelkopie {kopie} fehlt.");
        Assert.Equal(new FileInfo(sicherung.Pfad).Length, new FileInfo(kopie).Length);
        Assert.Null(_stand.ZweitHinweis);
    }

    // Der Alltag dieses Features: Der Stick steckt nicht.
    [Fact]
    public void Ein_unerreichbares_Zweitziel_entwertet_die_Hauptsicherung_nicht()
    {
        var sperre = Path.Combine(_ordner, "sperre");
        File.WriteAllText(sperre, "eine Datei, kein Ordner");

        var sicherung = Dienst(Path.Combine(sperre, "unmoeglich")).Erstellen(TestDaten.Administration);

        Assert.True(File.Exists(sicherung.Pfad));
        Assert.NotNull(_stand.ZweitHinweis);
    }

    // Sonst stünde „Stick fehlt" für immer da, und ein Dauerhinweis wird
    // ignoriert wie ein Daueralarm.
    [Fact]
    public void Ein_wieder_erreichbares_Zweitziel_loescht_den_Hinweis()
    {
        var sperre = Path.Combine(_ordner, "sperre");
        File.WriteAllText(sperre, "eine Datei, kein Ordner");
        Dienst(Path.Combine(sperre, "unmoeglich")).Erstellen(TestDaten.Administration);
        Assert.NotNull(_stand.ZweitHinweis);

        Dienst(_zweitOrdner).Erstellen(TestDaten.Administration);

        Assert.Null(_stand.ZweitHinweis);
    }

    [Fact]
    public void Auch_der_Zweitordner_haelt_die_Obergrenze()
    {
        var dienst = Dienst(_zweitOrdner, aufbewahrt: 3);
        for (var i = 0; i < 5; i++)
        {
            dienst.Erstellen(TestDaten.Administration);
        }

        Assert.True(Directory.GetFiles(_zweitOrdner, "*.db").Length <= 3,
            "Der Zweitordner hält die Obergrenze der Aufbewahrung nicht.");
    }

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_ordner, recursive: true);
    }
}

// Die Zeitachse des Protokolls: Der Dateiname trägt nur das Startdatum, die
// Kopfzeile den Startzeitpunkt und den Zeitbezug, damit niemand Ortszeit in
// die UTC-Stempel hineinliest.
public sealed class ProtokollZeitTests
{
    [Fact]
    public void Die_Protokolldatei_beginnt_jeden_Lauf_mit_einer_UTC_Kopfzeile()
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"protokoll-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        var alterOut = Console.Out;
        var alterError = Console.Error;

        try
        {
            var pfad = Protokolldatei.Einrichten(Path.Combine(ordner, "app.db"), aufbewahrtTage: 14);
            Console.Out.Flush();

            using var lesen = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var leser = new StreamReader(lesen);
            var ersteZeile = leser.ReadLine();

            Assert.NotNull(ersteZeile);
            Assert.Contains("UTC", ersteZeile);
        }
        finally
        {
            Console.SetOut(alterOut);
            Console.SetError(alterError);
        }
    }

    // Quelltext-Wache statt Laufzeitprobe: Die Formatierung übernimmt der
    // SimpleConsole-Formatierer von .NET, und dessen Verhalten zu testen wäre
    // ein Test fremden Codes. Ohne TimestampFormat schreibt .NET 8 gar keine
    // Zeit.
    [Fact]
    public void Die_Anwendung_stempelt_jede_Protokollzeile_mit_UTC_Zeit()
    {
        var quelle = File.ReadAllText(Path.Combine(Quellordner(),
            "src", "Ticketsystem.App", "Programm.cs"));

        Assert.Contains("TimestampFormat", quelle);
        Assert.Contains("UseUtcTimestamp = true", quelle);
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
}
