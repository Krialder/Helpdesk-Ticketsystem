using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Ticketsystem.App.Fenster;

namespace Ticketsystem.Tests;

// Ein Knopf verspricht mit seinem Aussehen, was er tut: Gleiche Wirkung
// sieht gleich aus, verschiedene Wirkung nicht. Geprüft werden Stilklassen
// und Ausrichtung, nicht Pixel: Die Klasse ist die Stelle, an der die
// Absicht steht, und die Übersetzung in Farbe hütet PaletteTests.
public sealed class KnopfgewichtTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Trüge nur einer der beiden Speichern-Knöpfe die Primärfarbe und die volle
    // Breite, läse sich das als Rangfolge, die es nicht gibt.
    [AvaloniaFact]
    public void Beide_Knoepfe_des_Kontodialogs_wiegen_gleich()
    {
        var dialog = new EinstellungenDialog(_factory.Services, TestDaten.Bearbeiter1);
        dialog.Show();

        Assert.Contains("primaer", dialog.NamenSpeichern.Classes);
        Assert.Equal(dialog.Aendern.Classes.Contains("primaer"),
            dialog.NamenSpeichern.Classes.Contains("primaer"));
        Assert.Equal(dialog.Aendern.HorizontalAlignment, dialog.NamenSpeichern.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, dialog.NamenSpeichern.HorizontalAlignment);
        dialog.Close();
    }

    // Die Zeiträume schalten den Bildschirm um, die Ausgabe schreibt eine Datei
    // auf die Festplatte.
    [AvaloniaFact]
    public void Die_Zeitraeume_sind_ein_Umschalter_die_Ausgabe_nicht()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();

        Assert.NotNull(Umschalter(fenster.VierWochen));
        Assert.Same(Umschalter(fenster.VierWochen), Umschalter(fenster.ZwoelfWochen));
        Assert.Null(Umschalter(fenster.CsvSpeichern));
        fenster.Close();
    }

    [AvaloniaFact]
    public void Auswahl_und_Ausgabe_stehen_nicht_in_einer_Reihe()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Measure(new Size(1240, 780));
        fenster.Arrange(new Rect(0, 0, 1240, 780));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var auswahl = Messen.Kante(fenster, Umschalter(fenster.VierWochen)!);
        var ausgabe = Messen.Kante(fenster, fenster.CsvSpeichern);

        Assert.True(ausgabe.Top > auswahl.Bottom,
            $"Die Ausgabe beginnt bei y={ausgabe.Top:0}, die Auswahl endet bei y={auswahl.Bottom:0}: dieselbe Reihe.");
        fenster.Close();
    }

    // Nicht in einer Fußzeile am Fensterrand: Dort stünde sie bei wenig Inhalt
    // weit weg von dem, was sie ausgibt.
    [AvaloniaFact]
    public void Die_Ausgabe_steht_links_direkt_unter_den_Kacheln()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Measure(new Size(1240, 780));
        fenster.Arrange(new Rect(0, 0, 1240, 780));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var knopf = Messen.Kante(fenster, fenster.CsvSpeichern);
        var letzteKarte = Messen.Kante(fenster, fenster.TagDiagramm.FindAncestorOfType<Border>()!);
        var ersteKarte = Messen.Kante(fenster, fenster.JeWoche.FindAncestorOfType<Border>()!);

        Assert.True(knopf.Top > letzteKarte.Bottom,
            $"Die Ausgabe beginnt bei y={knopf.Top:0}, die Kacheln enden bei y={letzteKarte.Bottom:0}.");
        Assert.True(knopf.Top - letzteKarte.Bottom < 40,
            $"Zwischen Kacheln und Ausgabe liegen {knopf.Top - letzteKarte.Bottom:0} px; das ist nicht „direkt darunter\".");
        Assert.True(Math.Abs(knopf.Left - ersteKarte.Left) < 4,
            $"Die Ausgabe beginnt bei x={knopf.Left:0}, die Kacheln bei x={ersteKarte.Left:0}: nicht bündig links.");
        fenster.Close();
    }

    // Zwei lose ToggleButtons schalteten den schon geltenden Zeitraum beim
    // zweiten Klick aus, und der Umschalter zeigte keine Wahl mehr, während die
    // Auswertung den alten Zeitraum rechnete.
    [AvaloniaFact]
    public async Task Ein_Klick_auf_den_geltenden_Zeitraum_laesst_ihn_gelten()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Measure(new Size(1240, 780));
        fenster.Arrange(new Rect(0, 0, 1240, 780));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        await fenster.AuswertungLadenAsync();

        Klicken(fenster, fenster.ZwoelfWochen);
        Assert.True(fenster.ZwoelfWochen.IsChecked);
        Assert.False(fenster.VierWochen.IsChecked);

        Klicken(fenster, fenster.ZwoelfWochen);
        Assert.True(fenster.ZwoelfWochen.IsChecked,
            "Nach dem zweiten Klick steht kein Zeitraum mehr da, obwohl die Auswertung einen rechnet.");
        fenster.Close();
    }

    // Ein echter Mausklick statt IsChecked setzen: Das ginge an OnClick und
    // damit an Toggle vorbei, und genau dort sitzt der Unterschied zwischen
    // einem Umschalter und zwei losen Schaltern.
    private static void Klicken(Window fenster, Control ziel)
    {
        var mitte = ziel.TranslatePoint(
            new Point(ziel.Bounds.Width / 2, ziel.Bounds.Height / 2), fenster);
        Assert.NotNull(mitte);
        Assert.True(ziel.Bounds.Width > 0, "Das Ziel hat keine Ausdehnung; ein Klick träfe nichts.");

        fenster.MouseMove(mitte!.Value);
        fenster.MouseDown(mitte.Value, MouseButton.Left);
        fenster.MouseUp(mitte.Value, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static Border? Umschalter(Control element)
    {
        for (var eltern = element.Parent; eltern is not null; eltern = eltern.Parent)
        {
            if (eltern is Border rahmen && rahmen.Classes.Contains("umschalter"))
            {
                return rahmen;
            }
        }

        return null;
    }

    private static double Abstand(Control links, Control rechts)
    {
        var a = links.TranslatePoint(new Point(links.Bounds.Width, 0), (Visual)links.GetVisualRoot()!)!.Value.X;
        var b = rechts.TranslatePoint(new Point(0, 0), (Visual)rechts.GetVisualRoot()!)!.Value.X;
        return b - a;
    }

    public void Dispose() => _factory.Dispose();
}
