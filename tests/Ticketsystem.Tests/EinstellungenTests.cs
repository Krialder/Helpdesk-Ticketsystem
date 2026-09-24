using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Services;
using Ticketsystem.Kern.Start;

namespace Ticketsystem.Tests;

// Die Einstellungen wohnen beim Bestand, nicht neben der Programmdatei: Ein
// Update durfte den Programmordner ersetzen, und genau dort lag die Datei
// mit den Einstellungen des Hauses. Drei Schichten, jede überschreibt die
// darunter: Vorgaben im Code, eine optionale Datei im Datenordner,
// Umgebungsvariablen. Die Datei legt die Anwendung nie von selbst an, weil
// sie die damaligen Vorgaben einfröre; sie entsteht auf Knopfdruck.
public sealed class EinstellungenTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), $"einstellungen-{Guid.NewGuid():N}");

    public EinstellungenTests() => Directory.CreateDirectory(_ordner);

    // Dieselbe Kette wie bei der Datenbank, aber ohne die Konfiguration: Die
    // Datei ist die Konfiguration und kann nicht von ihr abhängen.
    [Fact]
    public void Die_Datei_liegt_im_Datenordner_und_folgt_derselben_Kette_wie_die_Datenbank()
    {
        var profil = Path.Combine(_ordner, "profil");
        var benutzer = Path.Combine(_ordner, "benutzer");
        var programm = Path.Combine(_ordner, "programm");

        var pfad = Einstellungsablage.Vorgabepfad(profil, benutzer, programm);
        Assert.Equal(Path.Combine(profil, "Ticketsystem", "einstellungen.json"), pfad);

        var ohneProfil = Einstellungsablage.Vorgabepfad("", benutzer, programm);
        Assert.Equal(Path.Combine(benutzer, "Ticketsystem", "einstellungen.json"), ohneProfil);
    }

    // Protokolliert EF Core ohne Datei auf Information, liegt die eine
    // Fehlerzeile unter Hunderten SQL-Zeilen.
    [Fact]
    public void Ohne_Datei_gelten_dieselben_Werte_wie_mit_der_frueher_ausgelieferten()
    {
        var (konfiguration, dienste) = Aufbauen(Path.Combine(_ordner, "gibt-es-nicht.json"));

        var daten = dienste.GetRequiredService<IOptions<DatenOptions>>().Value;
        Assert.Equal(10, daten.AufbewahrteSicherungen);
        Assert.True(daten.SicherungBeimStart);
        Assert.Equal(4, daten.SicherungIntervallStunden);
        Assert.Equal(14, konfiguration.GetValue("Desktop:ProtokollTage", Protokolldatei.VorgabeTage));

        var regeln = dienste.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules;
        Assert.Contains(regeln, r =>
            r.CategoryName == "Microsoft.EntityFrameworkCore" && r.LogLevel == LogLevel.Warning);
    }

    // Der Host hängt die Umgebung vor unserer Datei an, deshalb wird sie danach
    // erneut angehängt; sonst gewänne die Datei über die Umgebung, und die
    // Proben der CI liefen gegen die Datei des Läufers. Die Variable wird im
    // finally entfernt, damit sie nicht in andere Tests leckt.
    [Fact]
    public void Die_Datei_ueberschreibt_die_Vorgabe_und_die_Umgebung_ueberschreibt_die_Datei()
    {
        var datei = Path.Combine(_ordner, "einstellungen.json");
        File.WriteAllText(datei, """{ "Daten": { "AufbewahrteSicherungen": 3, "SicherungIntervallStunden": 7 } }""");
        Environment.SetEnvironmentVariable("Daten__SicherungIntervallStunden", "9");
        try
        {
            var (_, dienste) = Aufbauen(datei);
            var daten = dienste.GetRequiredService<IOptions<DatenOptions>>().Value;

            Assert.Equal(3, daten.AufbewahrteSicherungen);
            Assert.Equal(9, daten.SicherungIntervallStunden);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Daten__SicherungIntervallStunden", null);
        }
    }

    [Fact]
    public void Die_Vorlage_nennt_jeden_Schluessel_mit_dem_geltenden_Wert_und_ist_selbst_gueltig()
    {
        var (_, dienste) = Aufbauen(Path.Combine(_ordner, "leer.json"));
        var ablage = dienste.GetRequiredService<Einstellungsablage>();

        var vorlage = ablage.Vorlage();
        foreach (var schluessel in new[]
                 {
                     "ConnectionStrings", "Default", "SicherungsOrdner", "ZweitSicherungsOrdner",
                     "AufbewahrteSicherungen", "SicherungBeimStart", "SicherungIntervallStunden", "ProtokollTage"
                 })
        {
            Assert.Contains($"\"{schluessel}\"", vorlage);
        }

        var zurueck = Path.Combine(_ordner, "zurueck.json");
        File.WriteAllText(zurueck, vorlage);
        var (_, erneut) = Aufbauen(zurueck);
        var daten = erneut.GetRequiredService<IOptions<DatenOptions>>().Value;
        Assert.Equal(10, daten.AufbewahrteSicherungen);
        Assert.True(daten.SicherungBeimStart);
        Assert.Equal(4, daten.SicherungIntervallStunden);
        Assert.Null(daten.ZweitSicherungsOrdner is { Length: 0 } ? null : daten.ZweitSicherungsOrdner);
    }

    // Eine vorhandene Datei ist die Arbeit eines Menschen; überschriebe der
    // Knopf sie, löschte sich das Haus damit seine eigenen Einstellungen.
    [Fact]
    public void Anlegen_schreibt_die_Vorlage_einmal_und_ueberschreibt_nichts()
    {
        var datei = Path.Combine(_ordner, "einstellungen.json");
        var (_, dienste) = Aufbauen(datei);
        var ablage = dienste.GetRequiredService<Einstellungsablage>();

        Assert.False(ablage.Vorhanden);
        Assert.True(ablage.Anlegen());
        Assert.True(ablage.Vorhanden);

        File.WriteAllText(datei, """{ "Daten": { "AufbewahrteSicherungen": 99 } }""");
        Assert.False(ablage.Anlegen());
        Assert.Contains("99", File.ReadAllText(datei));
    }

    [AvaloniaFact]
    public async Task Der_Datenreiter_nennt_den_Ort_und_legt_die_Datei_auf_Knopfdruck_an()
    {
        using var wirt = new KernWirt();
        var fenster = new VerwaltungsFenster(wirt.Services, TestDaten.Administration);
        fenster.Show();
        await fenster.DatenLadenAsync();

        var ablage = wirt.Services.GetRequiredService<Einstellungsablage>();
        Assert.Contains("Einstellungen: " + ablage.Pfad, fenster.DatenStand.Text);
        Assert.Contains("nicht vorhanden", fenster.DatenStand.Text);

        fenster.EinstellungenAnlegen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 20 && !File.Exists(ablage.Pfad); i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(File.Exists(ablage.Pfad));
        Assert.Contains(ablage.Pfad, fenster.DatenMeldung.Text);
        Assert.DoesNotContain("nicht vorhanden", fenster.DatenStand.Text);
        fenster.Close();
    }

    // Derselbe Aufbau wie in Programm.Hochfahren, nur ohne Fenster.
    private static (IConfiguration Konfiguration, IServiceProvider Dienste) Aufbauen(string einstellungsdatei)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        Einstellungsablage.Anhaengen(builder.Configuration, einstellungsdatei, []);
        Protokollstufen.Anwenden(builder.Logging);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Nach den Quellen, damit kein Test ins Profil schreibt; die Verbindung
            // ist nur Pflichtangabe, geöffnet wird sie nicht.
            ["ConnectionStrings:Default"] = "Data Source=:memory:"
        });
        KernDienste.Registrieren(builder.Services, builder.Configuration, einstellungsdatei);
        var host = builder.Build();
        return (builder.Configuration, host.Services);
    }

    public void Dispose()
    {
        if (Directory.Exists(_ordner))
        {
            Directory.Delete(_ordner, recursive: true);
        }
    }
}
