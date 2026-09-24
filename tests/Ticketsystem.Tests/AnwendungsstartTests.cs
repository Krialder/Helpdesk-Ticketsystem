using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Start;

namespace Ticketsystem.Tests;

// Der Bestand liegt an einem absoluten, benennbaren Ort, und das Protokoll
// räumt sich auf. Den Einzelstart sichert der Mutex im Desktop-Programm;
// der ist net8.0-windows-Code und auf dem Linux-Prüfrechner nicht
// ausführbar, deshalb fehlt er hier.
public sealed class AnwendungsstartTests : IDisposable
{
    private readonly string _ordner =
        Path.Combine(Path.GetTempPath(), $"start-tests-{Guid.NewGuid():N}");

    public AnwendungsstartTests() => Directory.CreateDirectory(_ordner);

    // In der eigenständigen Einzeldatei kam LocalApplicationData leer zurück.
    // Ein relativer Pfad hinge am Arbeitsverzeichnis, und zwei Startwege
    // ergäben zwei Datenbanken.
    [Fact]
    public void Ohne_Benutzerprofil_entsteht_trotzdem_ein_absoluter_Pfad()
    {
        var verbindung = Datenablage.Verbindung(
            konfiguriert: null, profilOrdner: string.Empty,
            benutzerOrdner: _ordner, programmOrdner: "/programm");

        var datei = Datenablage.DateiAus(verbindung);
        Assert.True(Path.IsPathRooted(datei), $"Pfad ist nicht absolut: {datei}");
        Assert.StartsWith(_ordner, datei);
    }

    [Fact]
    public void Faellt_alles_aus_landet_der_Bestand_neben_der_Programmdatei()
    {
        var verbindung = Datenablage.Verbindung(
            konfiguriert: null, profilOrdner: string.Empty,
            benutzerOrdner: string.Empty, programmOrdner: _ordner);

        Assert.StartsWith(_ordner, Datenablage.DateiAus(verbindung));
    }

    [Theory]
    [InlineData("Data Source=vorgegeben.db", "", "", "Konfiguration")]
    [InlineData(null, "/profil", "/heim", "Benutzerprofil")]
    [InlineData(null, "", "/heim", "Benutzerordner")]
    [InlineData(null, "", "", "Programmordner")]
    public void Die_Herkunft_des_Ablageorts_ist_benennbar(
        string? konfiguriert, string profil, string heim, string erwartet) =>
        Assert.Equal(erwartet, Datenablage.Herkunft(konfiguriert, profil, heim));

    [Fact]
    public void Das_Protokoll_raeumt_alte_Tage_ab_und_laesst_junge_stehen()
    {
        var alt = Path.Combine(_ordner, "ticketsystem-2020-01-01.log");
        var jung = Path.Combine(_ordner, "ticketsystem-heute.log");
        File.WriteAllText(alt, "alt");
        File.WriteAllText(jung, "jung");
        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow.AddDays(-30));

        var geloescht = Protokolldatei.Aufraeumen(_ordner, aufbewahrtTage: 14);

        Assert.Equal(1, geloescht);
        Assert.False(File.Exists(alt));
        Assert.True(File.Exists(jung));
    }

    public void Dispose()
    {
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
