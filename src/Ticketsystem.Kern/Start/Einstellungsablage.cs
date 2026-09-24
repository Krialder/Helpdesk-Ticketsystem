using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Start;

// Die Einstellungsdatei im Datenordner: Sie wird nie von selbst angelegt
// und entsteht nur als Vorlage über die Verwaltung, mit den gerade
// geltenden Werten. Die Schichtenfolge (Datei, Umgebung, Befehlszeile)
// liegt hier im Kern, damit sie ohne Fenster prüfbar ist.
public sealed class Einstellungsablage(string pfad, IOptions<DatenOptions> daten, IConfiguration konfiguration)
{
    public const string Dateiname = "einstellungen.json";

    public string Pfad { get; } = Path.GetFullPath(pfad);

    public bool Vorhanden => File.Exists(Pfad);

    public static string Vorgabepfad(string profilOrdner, string benutzerOrdner, string programmOrdner) =>
        Path.Combine(Datenablage.Vorgabeordner(profilOrdner, benutzerOrdner, programmOrdner), Dateiname);

    public static string Vorgabepfad() => Vorgabepfad(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        AppContext.BaseDirectory);

    public static IConfigurationBuilder Anhaengen(IConfigurationBuilder konfiguration, string pfad, string[] args)
    {
        konfiguration.AddJsonFile(pfad, optional: true, reloadOnChange: false);
        konfiguration.AddEnvironmentVariables();
        if (args.Length > 0)
        {
            konfiguration.AddCommandLine(args);
        }

        return konfiguration;
    }

    public string Vorlage()
    {
        var werte = daten.Value;
        var wurzel = new Dictionary<string, object?>
        {
            ["ConnectionStrings"] = new Dictionary<string, object?>
            {
                ["Default"] = konfiguration.GetConnectionString("Default") ?? ""
            },
            ["Daten"] = new Dictionary<string, object?>
            {
                ["SicherungsOrdner"] = werte.SicherungsOrdner ?? "",
                ["ZweitSicherungsOrdner"] = werte.ZweitSicherungsOrdner ?? "",
                ["AufbewahrteSicherungen"] = werte.AufbewahrteSicherungen,
                ["SicherungBeimStart"] = werte.SicherungBeimStart,
                ["SicherungIntervallStunden"] = werte.SicherungIntervallStunden
            },
            ["Desktop"] = new Dictionary<string, object?>
            {
                ["ProtokollTage"] = konfiguration.GetValue("Desktop:ProtokollTage", Protokolldatei.VorgabeTage)
            }
        };

        return JsonSerializer.Serialize(wurzel, new JsonSerializerOptions
        {
            WriteIndented = true,
            // Ohne diesen Encoder stünden Umlaute und der Backslash der Windows-Pfade
            // als \u-Folgen in der Datei, die ein Mensch bearbeiten soll.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public bool Anlegen()
    {
        if (Vorhanden)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Pfad)!);
        File.WriteAllText(Pfad, Vorlage());
        return true;
    }
}
