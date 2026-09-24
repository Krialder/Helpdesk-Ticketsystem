using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Jede Text-auf-Fläche-Paarung, die die Oberfläche wirklich benutzt, hält
// in beiden Schemata das WCAG-AA-Verhältnis von 4,5:1. Der Test rechnet
// statt zu meinen: Eine Palette, die nur im Screenshot gut aussieht, fällt
// am hellen Fenster neben dem Laptop durch.
public sealed class PaletteTests
{
    private static void MussLesbarSein(Avalonia.Media.Color text, Avalonia.Media.Color flaeche, string wo)
    {
        var kontrast = Palette.Kontrast(text, flaeche);
        Assert.True(kontrast >= 4.5,
            $"{wo}: Kontrast {kontrast:F2} liegt unter 4,5:1.");
    }

    // Die Tests lesen aus dem Schema selbst und nicht aus Palette.X, damit sie
    // nicht davon abhängen, welches Schema gerade angewendet ist.
    public static IEnumerable<object[]> Schemata => Farbschema.Alle.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Schemata))]
    public void Haupt_und_Nebentext_sind_auf_allen_Grundflaechen_lesbar(Farbschema schema)
    {
        foreach (var (flaeche, name) in new[]
                 {
                     (schema.Grund, "Grund"), (schema.Karte, "Karte"), (schema.Erhaben, "Erhaben")
                 })
        {
            MussLesbarSein(schema.Text, flaeche, $"{schema.Name}: Text auf {name}");
            MussLesbarSein(schema.TextLeise, flaeche, $"{schema.Name}: Leiser Text auf {name}");
        }
    }

    // Über die echten Klassen aus dem Kern, nicht über die Farbfelder: So reißt
    // der Test auch, wenn BadgeKlasse eine Klasse liefert, die die Palette noch
    // nicht kennt und die deshalb neutral fiele.
    [Theory]
    [MemberData(nameof(Schemata))]
    public void Jede_Badge_Klasse_der_Ampel_ist_in_ihrer_Pill_lesbar(Farbschema schema)
    {
        foreach (var zustand in Enum.GetValues<SlaState>())
        {
            var klasse = zustand.BadgeKlasse();
            var (text, flaeche) = schema.Badge(klasse);
            MussLesbarSein(text, flaeche, $"{schema.Name}: Pill {klasse}");
        }
    }

    [Theory]
    [MemberData(nameof(Schemata))]
    public void Prioritaetsfarben_sind_als_Text_auf_Karten_lesbar(Farbschema schema)
    {
        foreach (var prio in Enum.GetValues<TicketPriority>())
        {
            MussLesbarSein(schema.Prioritaet(prio.PrioKlasse()), schema.Karte, $"{schema.Name}: Priorität {prio}");
        }
    }

    // Das Zeichen steht frei in der Listenzeile, also auf dem Fenstergrund und
    // nicht auf der Pill-Fläche. Dieselbe Farbe auf anderem Untergrund ist genau
    // der Umzug, bei dem ein Kontrast still verloren geht.
    [Theory]
    [MemberData(nameof(Schemata))]
    public void Das_Fristzeichen_ist_auch_ausserhalb_seiner_Pill_lesbar(Farbschema schema)
    {
        foreach (var zustand in Enum.GetValues<SlaState>())
        {
            var (text, _) = schema.Badge(zustand.BadgeKlasse());
            MussLesbarSein(text, schema.Grund, $"{schema.Name}: Fristzeichen {zustand} auf dem Fenstergrund");
            MussLesbarSein(text, schema.Erhaben, $"{schema.Name}: Fristzeichen {zustand} auf der gewählten Zeile");
        }
    }

    [Theory]
    [MemberData(nameof(Schemata))]
    public void Der_Primaerknopf_ist_in_beiden_Zustaenden_lesbar(Farbschema schema)
    {
        MussLesbarSein(schema.AkzentText, schema.Akzent, $"{schema.Name}: Primärknopf");
        MussLesbarSein(schema.AkzentText, schema.AkzentHell, $"{schema.Name}: Primärknopf (Zeiger)");
    }

    [Theory]
    [MemberData(nameof(Schemata))]
    public void Der_Gefahrknopf_ist_in_beiden_Formen_lesbar(Farbschema schema)
    {
        MussLesbarSein(schema.GefahrKnopfText, schema.GefahrKnopf, $"{schema.Name}: Gefahrknopf");
        MussLesbarSein(schema.GefahrKnopfText, schema.GefahrKnopfZeiger, $"{schema.Name}: Gefahrknopf (Zeiger)");
        foreach (var (flaeche, name) in new[] { (schema.Grund, "Grund"), (schema.Karte, "Karte"), (schema.Erhaben, "Erhaben") })
        {
            MussLesbarSein(schema.GefahrText, flaeche, $"{schema.Name}: Umrandeter Gefahrknopf auf {name}");
        }
    }

    [Fact]
    public void Kein_Fenster_traegt_eigene_Farbtoene_in_der_Oberflaechenbeschreibung()
    {
        var dateien = Directory.GetFiles(AppOrdner(), "*.axaml", SearchOption.AllDirectories);
        Assert.NotEmpty(dateien);

        var muster = new System.Text.RegularExpressions.Regex("\"#[0-9a-fA-F]{6,8}\"");
        var verstoesse = dateien
            .Select(datei => (Datei: Path.GetFileName(datei), Inhalt: File.ReadAllText(datei)))
            .Where(d => muster.IsMatch(d.Inhalt))
            .Select(d => d.Datei)
            .ToList();

        Assert.True(verstoesse.Count == 0,
            "Diese Oberflächenbeschreibungen setzen Farbtöne an der Palette vorbei: "
            + string.Join(", ", verstoesse));
    }

    // Farbschema und Palette sind ausgenommen: Das eine nennt die Töne, das
    // andere leitet mit Stufe() welche daraus ab und braucht dafür FromRgb.
    [Fact]
    public void Kein_Fenster_mischt_eigene_Farbwerte_in_den_Code()
    {
        var muster = new System.Text.RegularExpressions.Regex(
            "\"#[0-9a-fA-F]{6,8}\"|Color\\.FromArgb|Color\\.FromRgb");
        var verstoesse = Directory
            .GetFiles(AppOrdner(), "*.cs", SearchOption.AllDirectories)
            .Where(datei => Path.GetFileName(datei) is not ("Farbschema.cs" or "Palette.cs"))
            .Where(datei => !datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                            && !datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(datei => muster.IsMatch(File.ReadAllText(datei)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(verstoesse.Count == 0,
            "Diese Dateien setzen Farbtöne an der Palette vorbei: " + string.Join(", ", verstoesse));
    }

    private static string AppOrdner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var kandidat = Path.Combine(ordner.FullName, "src", "Ticketsystem.App");
            if (Directory.Exists(kandidat))
            {
                return kandidat;
            }

            ordner = ordner.Parent;
        }

        throw new DirectoryNotFoundException("Der App-Ordner wurde nicht gefunden.");
    }
}
