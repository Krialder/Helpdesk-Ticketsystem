using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Lesefläche setzt die Blöcke des Kerns um, an drei Stellen dieselbe,
// und der Bearbeiten-Dialog zeigt beim Tippen die Vorschau. Geprüft wird
// die Verdrahtung (welcher Block wird was), nicht der Parser; der hat die
// WissenstextTests.
public sealed class WissenstextAnzeigeTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Die Lesefläche braucht ein Fenster, damit Vorlagen und Stile greifen.
    private static (Window Fenster, Wissenstextanzeige Anzeige) Aufgebaut(string text)
    {
        var anzeige = new Wissenstextanzeige { Text = text };
        var fenster = new Window { Content = anzeige };
        Messen.Auslegen(fenster, 800, 600);
        return (fenster, anzeige);
    }

    private static List<SelectableTextBlock> Lesetexte(Wissenstextanzeige anzeige) =>
        anzeige.GetVisualDescendants().OfType<SelectableTextBlock>().ToList();

    // Aus einem Artikel muss sich ein Befehl oder Pfad kopieren lassen; genau
    // dafür werden Artikel geschrieben.
    [AvaloniaFact]
    public void Ein_Text_ohne_Auszeichnung_ist_ein_markierbarer_Absatz_mit_seinen_Zeilen()
    {
        var (fenster, anzeige) = Aufgebaut("sf\nasfrw33");

        var absatz = Assert.Single(Lesetexte(anzeige));
        Assert.Collection(absatz.Inlines!,
            i => Assert.Equal("sf", Assert.IsType<Run>(i).Text),
            i => Assert.IsType<LineBreak>(i),
            i => Assert.Equal("asfrw33", Assert.IsType<Run>(i).Text));
        Assert.Equal(14, absatz.FontSize);
        Assert.Equal("sf\nasfrw33", anzeige.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public void Fett_wird_halbfett_und_ein_Befehl_steht_in_Festbreite()
    {
        var (fenster, anzeige) = Aufgebaut("Vorher **Sicherung** anlegen: `cmd`");

        var laeufe = Assert.Single(Lesetexte(anzeige)).Inlines!.OfType<Run>().ToList();
        var fett = Assert.Single(laeufe, r => r.Text == "Sicherung");
        // SemiBold wie die Kartenköpfe: zwei Gewichte in der Anwendung, nicht drei.
        Assert.Equal(FontWeight.SemiBold, fett.FontWeight);
        var befehl = Assert.Single(laeufe, r => r.Text == "cmd");
        Assert.Equal(Schriften.Fest, befehl.FontFamily);
        Assert.Equal(FontWeight.Normal, befehl.FontWeight);
        Assert.Equal(FontWeight.Normal, Assert.Single(laeufe, r => r.Text == "Vorher ").FontWeight);
        fenster.Close();
    }

    [AvaloniaFact]
    public void Schritte_zaehlen_ab_dem_Beginn_und_Punkte_bekommen_einen_Punkt()
    {
        var (fenster, anzeige) = Aufgebaut("3. Weiter\n4. Fertig\n\n- Kabel");

        var marken = anzeige.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t is not SelectableTextBlock)
            .Select(t => t.Text)
            .ToList();
        Assert.Equal(["3.", "4.", "•"], marken);
        Assert.Equal(["Weiter", "Fertig", "Kabel"], Lesetexte(anzeige).Select(t => t.Inlines!.Text));
        fenster.Close();
    }

    [AvaloniaFact]
    public void Eine_Ueberschrift_hat_die_Stufe_der_Kartenkoepfe()
    {
        var (fenster, anzeige) = Aufgebaut("## Vorgehen\nText");

        var kopf = Lesetexte(anzeige).Single(t => t.Classes.Contains("kartenkopf"));
        Assert.Equal("Vorgehen", kopf.Inlines!.Text);
        Assert.Equal(14, kopf.FontSize);
        Assert.Equal(FontWeight.SemiBold, kopf.FontWeight);
        fenster.Close();
    }

    [AvaloniaFact]
    public void Ein_Befehlsblock_liegt_auf_der_erhabenen_Flaeche_und_bleibt_wortgetreu()
    {
        var (fenster, anzeige) = Aufgebaut("```\n  net use \\\\srv\\share **x**\n```");

        var flaeche = Assert.Single(anzeige.GetVisualDescendants().OfType<Border>(),
            b => b.Child is SelectableTextBlock);
        Assert.Equal(Palette.Erhaben, Assert.IsType<SolidColorBrush>(flaeche.Background).Color);
        var text = Assert.IsType<SelectableTextBlock>(flaeche.Child);
        Assert.Equal("  net use \\\\srv\\share **x**", text.Inlines!.Text);
        Assert.Equal(Schriften.Fest, text.FontFamily);
        fenster.Close();
    }

    [AvaloniaFact]
    public void Gedaempft_nimmt_die_leise_Textfarbe()
    {
        var (fenster, anzeige) = Aufgebaut("Alt.");
        Assert.Equal(Palette.Text, Assert.IsType<SolidColorBrush>(Assert.Single(Lesetexte(anzeige)).Foreground).Color);

        anzeige.Gedaempft = true;
        Assert.Equal(Palette.TextLeise, Assert.IsType<SolidColorBrush>(Assert.Single(Lesetexte(anzeige)).Foreground).Color);
        fenster.Close();
    }

    // Ohne Vorschau speichert man, um zu sehen, wie es aussieht, und jedes
    // Speichern ist eine Fassung.
    [AvaloniaFact]
    public async Task Der_Bearbeiten_Dialog_zeigt_die_Vorschau_beim_Tippen_und_nennt_die_Schreibweisen()
    {
        var dialog = new WissensBearbeitenDialog(_factory.Services, TestDaten.Teamleitung, null);
        dialog.Show();
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        dialog.Inhalt.Text = "## Kopf\n- Punkt";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        Assert.Equal("## Kopf\n- Punkt", dialog.Vorschau.Text);
        var kopf = Lesetexte(dialog.Vorschau).Single(t => t.Classes.Contains("kartenkopf"));
        Assert.Equal("Kopf", kopf.Inlines!.Text);
        Assert.True(dialog.Hinweis.IsVisible);
        Assert.Contains("**fett**", dialog.Hinweis.Text);
        Assert.Contains("1. Schritt", dialog.Hinweis.Text);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task Der_Freigaben_Dialog_zeigt_Vorschlag_und_alten_Stand_mit_Titel_und_Auszeichnung()
    {
        int artikelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var kb = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
            var artikel = await kb.SpeichernAsync(null, "WLAN", "Alt **roh**", null, true, 180, [], TestDaten.Teamleitung);
            artikelId = artikel.Id;
            await kb.VorschlagEinreichenAsync(artikelId, "WLAN neu", "## Besser", null, TestDaten.Bearbeiter1);
        }

        var dialog = new FreigabenDialog(_factory.Services, TestDaten.Teamleitung);
        dialog.Show();
        await dialog.LadenAsync();

        Assert.Equal("WLAN neu", dialog.NeuTitel.Text);
        Assert.Equal("## Besser", dialog.Neu.Text);
        Assert.Equal("Besser", Lesetexte(dialog.Neu).Single(t => t.Classes.Contains("kartenkopf")).Inlines!.Text);
        Assert.Equal("WLAN", dialog.AltTitel.Text);
        Assert.Equal("Alt **roh**", dialog.Alt.Text);
        Assert.True(dialog.Alt.Gedaempft);
        dialog.Close();
    }

    public void Dispose() => _factory.Dispose();
}
