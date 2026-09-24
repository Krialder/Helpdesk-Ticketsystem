using System.Text.RegularExpressions;

namespace Ticketsystem.Tests;

// Hält die Kommentarregeln des Entwicklerhandbuchs (docs/entwicklung/entwicklerhandbuch.md, Beitragen):
// keine XML-Dokumentation im Bestand, und je Projekt bleibt der Anteil der
// Kommentarzeilen unter einem Fünftel. Die erste Fassung hatte den Kern auf
// fast die Hälfte Kommentarzeilen gebracht; dieser Wächter verhindert, dass
// das unbemerkt wiederkommt.
public sealed class KommentarTests
{
    [Fact]
    public void Kein_XML_Kommentar_im_Bestand()
    {
        var treffer = Quelldateien("src", "tests")
            .Where(datei => File.ReadLines(datei).Any(z => z.TrimStart().StartsWith("///", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(treffer.Count == 0, "XML-Kommentare in: " + string.Join(", ", treffer));
    }

    [Theory]
    [InlineData("src/Ticketsystem.Kern")]
    [InlineData("src/Ticketsystem.App")]
    [InlineData("tests")]
    public void Der_Kommentaranteil_je_Projekt_bleibt_unter_einem_Fuenftel(string projekt)
    {
        var kommentar = 0;
        var code = 0;
        foreach (var datei in Quelldateien(projekt))
        {
            foreach (var zeile in File.ReadLines(datei).Select(z => z.Trim()).Where(z => z.Length > 0))
            {
                if (zeile.StartsWith("//", StringComparison.Ordinal))
                {
                    kommentar++;
                }
                else
                {
                    code++;
                }
            }
        }

        var anteil = (double)kommentar / (kommentar + code);
        Assert.True(anteil < 0.2, $"{projekt}: {kommentar} Kommentar- zu {code} Codezeilen, Anteil {anteil:P0}.");
    }

    // Migrationen sind erzeugter Code und zählen nicht.
    private static IEnumerable<string> Quelldateien(params string[] ordner) =>
        ordner.SelectMany(o => Directory.GetFiles(Path.Combine(Wurzel(), o), "*.cs", SearchOption.AllDirectories))
            .Where(datei => !datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !datei.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            if (Directory.Exists(Path.Combine(ordner.FullName, "src", "Ticketsystem.Kern")))
            {
                return ordner.FullName;
            }

            ordner = ordner.Parent;
        }

        throw new DirectoryNotFoundException("Die Repository-Wurzel wurde oberhalb des Testverzeichnisses nicht gefunden.");
    }
}
