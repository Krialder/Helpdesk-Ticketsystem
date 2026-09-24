using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Eine Typo-, Geometrie- und Zeitleiter nach Windows, denn die Nutzer
// verbringen ihre übrige Rechnerzeit in anderen Windows-Programmen. Windows
// codiert mit dem Radius eine Bedeutung: 8 für alles, was über anderem
// liegt, 4 für alles im Fluss. Ein-Punkt-Sprünge (13 gegen 14) lesen sich
// nicht als Stufe, sondern als Unfall.
public sealed class LeiternTests : IDisposable
{
    private readonly KernWirt _factory = new();

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

    private static IEnumerable<(string Datei, string Inhalt)> Fensterbeschreibungen() =>
        Directory.GetFiles(AppOrdner(), "*.axaml", SearchOption.AllDirectories)
            .Where(d => Path.GetFileName(d) != "App.axaml")
            .Select(d => (Path.GetFileName(d), File.ReadAllText(d)));

    // Eine nackte FontSize im Fenster umgeht die Leiter; so entstanden 11, 13,
    // 14, 18 und 30 px nebeneinander, ohne dass eine eine Stufe der anderen war.
    [Fact]
    public void Kein_Fenster_setzt_eine_eigene_Schriftgroesse()
    {
        var verstoesse = Fensterbeschreibungen()
            .Where(d => d.Inhalt.Contains("FontSize=", StringComparison.Ordinal))
            .Select(d => d.Datei)
            .ToList();

        Assert.True(verstoesse.Count == 0,
            "Diese Oberflächenbeschreibungen setzen Schriftgrößen an der Leiter vorbei: "
            + string.Join(", ", verstoesse));
    }

    [Fact]
    public void Es_gibt_nur_die_beiden_Windows_Radien_und_die_eine_Ausnahme()
    {
        // Die Pill mit 99 ist die eine bewusste Ausnahme, weil ihre Form die
        // Bedeutung trägt.
        var erlaubt = new[] { "4", "8", "99" };
        var muster = new Regex("CornerRadius=\"([0-9,]+)\"|<Setter Property=\"CornerRadius\" Value=\"([0-9,]+)\"");
        var gefunden = Directory.GetFiles(AppOrdner(), "*.axaml", SearchOption.AllDirectories)
            .SelectMany(d => muster.Matches(File.ReadAllText(d))
                .Select(m => (Datei: Path.GetFileName(d), Wert: m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)))
            .Where(t => !erlaubt.Contains(t.Wert))
            .ToList();

        Assert.True(gefunden.Count == 0,
            "Radien außerhalb der Windows-Leiter: "
            + string.Join(", ", gefunden.Select(t => $"{t.Datei}:{t.Wert}")));
    }

    // Gemessen vor der Behebung: fünf Elemente mit drei Höhen (35, 32, 30) ohne
    // gemeinsame Kante, und bei 1240 px schoben sich die rechten Knöpfe über
    // den Kontonamen.
    [AvaloniaFact]
    public async Task Die_Kopfzeile_steht_auf_einer_Grundlinie_und_laeuft_nicht_ueber()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.LadenAsync();

        Control[] reihe =
        [
            fenster.Suche, fenster.Ansicht, fenster.Prioritaet, fenster.NurMeine,
            fenster.NeuesTicket, fenster.Wissen, fenster.Einstellungen
        ];

        // Die gesetzte Width des Fensters gewinnt gegen jedes Arrange; sie muss
        // vor dem Messen stehen, sonst misst der Test immer die Wunschbreite.
        void Auslegen(double breite)
        {
            fenster.Width = breite;
            fenster.Measure(new Size(breite, 760));
            fenster.Arrange(new Rect(0, 0, breite, 760));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            fenster.UpdateLayout();
        }

        double RechteKante() =>
            reihe.Max(c => (c.TranslatePoint(new Point(0, 0), fenster)?.X ?? 0) + c.Bounds.Width);
        double LinkeKante() =>
            reihe.Min(c => c.TranslatePoint(new Point(0, 0), fenster)?.X ?? 0);

        Auslegen(1240);
        var hoehen = reihe.Select(c => Math.Round(c.Bounds.Height)).Distinct().ToList();
        Assert.True(hoehen.Count == 1,
            "Die Kopfzeile trägt mehrere Elementhöhen: " + string.Join(", ", hoehen));
        Assert.Equal(32, hoehen[0]);
        Assert.True(RechteKante() <= 1240,
            $"Die Kopfzeile reicht bis x={RechteKante():F0} und damit über den Fensterrand hinaus.");

        // Eine Kopfzeile, die nur bei der Wunschbreite passt, bricht beim ersten
        // Ziehen am Fensterrand. Ein negativer linker Rand ist das Warnzeichen:
        // Dann läuft die Zeile aus dem Fenster heraus, statt zu schrumpfen.
        Auslegen(fenster.MinWidth);
        Assert.True(LinkeKante() >= 0,
            $"Bei der Mindestbreite beginnt die Kopfzeile bei x={LinkeKante():F0}, läuft also links hinaus.");
        Assert.True(RechteKante() <= fenster.MinWidth,
            $"Bei der Mindestbreite {fenster.MinWidth:F0} px reicht die Kopfzeile bis x={RechteKante():F0}.");

        fenster.Close();
    }

    // Die Überschriften standen auf 13 px und der Inhalt auf 14: Die Hierarchie
    // kippte, die Karte las sich von unten nach oben.
    [AvaloniaFact]
    public async Task Eine_Kartenueberschrift_ist_nicht_kleiner_als_ihr_Inhalt()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Weber, Sabine", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, 1);
        fenster.Show();
        await fenster.LadenAsync();
        fenster.Measure(new Size(1180, 760));
        fenster.Arrange(new Rect(0, 0, 1180, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();

        var ueberschriften = fenster.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("kartenkopf"))
            .ToList();

        Assert.NotEmpty(ueberschriften);
        foreach (var kopf in ueberschriften)
        {
            Assert.Equal(14, kopf.FontSize);
            Assert.Equal(Avalonia.Media.FontWeight.SemiBold, kopf.FontWeight);
        }

        // Der leise Text ist die kleinste Stufe und liegt bei 12, nicht bei 11:
        // Darunter ist auf einem 125-Prozent-Bildschirm nichts mehr zu lesen.
        var leise = fenster.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("leise")).ToList();
        Assert.NotEmpty(leise);
        Assert.All(leise, t => Assert.True(t.FontSize >= 12, $"„{t.Text}“ steht auf {t.FontSize} px."));
        fenster.Close();
    }

    // Feste 40-px-Zeile mit Trennlinie statt Karten: Aus rund neun sichtbaren
    // Vorgängen werden rund sechzehn, und 40 px ist die Zeilenhöhe, die Windows
    // für Listen vorsieht.
    [AvaloniaFact]
    public async Task Eine_Vorgangszeile_ist_vierzig_Pixel_hoch()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            for (var i = 0; i < 3; i++)
            {
                await tickets.CreatePhoneAsync($"Drucker {i} zieht kein Papier ein", "Papierstau.",
                    TicketPriority.Medium, "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
                    TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
            }
        }

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.LadenAsync();
        fenster.Measure(new Size(1240, 760));
        fenster.Arrange(new Rect(0, 0, 1240, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();

        var erste = (Control)fenster.Liste.ContainerFromIndex(0)!;
        var zweite = (Control)fenster.Liste.ContainerFromIndex(1)!;
        var raster = (zweite.TranslatePoint(new Point(0, 0), fenster)?.Y ?? 0)
                     - (erste.TranslatePoint(new Point(0, 0), fenster)?.Y ?? 0);

        Assert.Equal(40, Math.Round(raster));
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
