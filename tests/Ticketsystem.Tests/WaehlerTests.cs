using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Ticketsystem.App.Fenster;

namespace Ticketsystem.Tests;

// Der Monat im Datumswähler steht mittig wie Tag und Jahr. Fluents Vorlage
// streckt PART_MonthTextBlock über seine Spalte und zentriert nur die beiden
// anderen Felder; ohne Gegenmaßnahme klebt der Monatstext links.
public sealed class WaehlerTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [AvaloniaFact]
    public void Der_Monat_steht_mittig_wie_Tag_und_Jahr()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        // Der Wähler ist beim Anruf eingeklappt; erst aufklappen.
        fenster.ZeitAendern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fenster.Measure(new Size(620, 760));
        fenster.Arrange(new Rect(0, 0, 620, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();

        // Gemessen wird die Schrift, nicht der Kasten: Ein gestreckter Textblock
        // füllt seine Spalte, seine Mitte stimmt also, während die Schrift darin
        // links klebt.
        var raster = fenster.ZeitDatum.GetVisualDescendants().OfType<Grid>()
            .First(g => g.Name == "PART_ButtonContentGrid");
        var monat = fenster.ZeitDatum.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Name == "PART_MonthTextBlock");

        var spalte = raster.ColumnDefinitions[0].ActualWidth;
        Assert.True(monat.Bounds.Width < spalte - 4,
            $"Der Monat fuellt seine Spalte ({monat.Bounds.Width:F0} von {spalte:F0} px) und klebt damit links.");

        var mitteDesTextes = (monat.TranslatePoint(new Point(0, 0), raster)?.X ?? 0) + monat.Bounds.Width / 2;
        Assert.True(Math.Abs(mitteDesTextes - spalte / 2) <= 2,
            $"Der Monat steht bei Mitte {mitteDesTextes:F0}, die Spaltenmitte liegt bei {spalte / 2:F0}.");

        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
