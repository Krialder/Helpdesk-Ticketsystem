using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Eine Farbquelle statt drei: Das FluentTheme bringt eigene Werte mit, und
// stehen die neben der Palette, erscheint derselbe Gedanke „aktiv" in
// #0078d7 am Reiter, in Palette.Akzent am Primärknopf und in einem dritten
// Ton an der Listenauswahl. Gemessen werden die aufgelösten Ressourcen,
// nicht die Absicht.
public sealed class FarbquelleTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private static Color Farbe(string schluessel)
    {
        Assert.True(Application.Current!.TryGetResource(schluessel, ThemeVariant.Dark, out var wert),
            $"Die Ressource {schluessel} gibt es nicht; der Schlüsselname stimmt nicht mehr.");
        return wert switch
        {
            Color c => c,
            ISolidColorBrush b => b.Color,
            _ => throw new InvalidOperationException($"{schluessel} ist weder Farbe noch Flächenpinsel: {wert}.")
        };
    }

    // Diese Schlüssel malen von Haus aus #0078d7, einen Blauton, den die Palette
    // nicht kennt und den der Kontrasttest deshalb nie sähe.
    [AvaloniaFact]
    public void Jede_Akzentflaeche_traegt_den_Ton_der_Palette()
    {
        foreach (var schluessel in new[]
                 {
                     "SystemAccentColor", "AccentButtonBackground", "ToggleButtonBackgroundChecked",
                     "TextControlBorderBrushFocused", "SystemControlHighlightAccentBrush",
                     "CheckBoxCheckBackgroundFillChecked", "RadioButtonOuterEllipseCheckedFill"
                 })
        {
            Assert.Equal(Palette.Akzent, Farbe(schluessel));
        }
    }

    // Fluent malt halbdurchsichtige Schleier: Eingabefelder mit #66000000
    // dunkler als der Fenstergrund, Knöpfe mit #33ffffff heller als jede Karte.
    [AvaloniaFact]
    public void Eingabefelder_und_Knoepfe_stehen_auf_Flaechen_der_Palette()
    {
        foreach (var schluessel in new[]
                 {
                     "TextControlBackground", "ButtonBackground", "ComboBoxBackground",
                     "DatePickerButtonBackground", "TimePickerButtonBackground",
                     "CalendarDatePickerBackground", "ToggleButtonBackground"
                 })
        {
            Assert.Equal(Palette.Erhaben, Farbe(schluessel));
        }

        foreach (var schluessel in new[]
                 {
                     "TextControlBorderBrush", "ComboBoxBorderBrush",
                     "DatePickerButtonBorderBrush", "TimePickerButtonBorderBrush",
                     "CalendarDatePickerBorderBrush", "RadioButtonOuterEllipseStroke",
                     "CheckBoxCheckBackgroundStrokeUnchecked"
                 })
        {
            Assert.Equal(Palette.Linie, Farbe(schluessel));
        }
    }

    // Gesperrt heißt „gerade nicht dran", nicht „unwichtig": Die Beschriftung
    // ist dann die Erklärung, warum nichts passiert. Fluent malt sie mit 40
    // Prozent Weiß auf einen 20-Prozent-Weiß-Schleier.
    [AvaloniaFact]
    public void Ein_gesperrtes_Bedienelement_bleibt_lesbar()
    {
        var kontrast = Palette.Kontrast(Farbe("ButtonForegroundDisabled"), Palette.Karte);
        Assert.True(kontrast >= 4.5,
            $"Beschriftung eines gesperrten Knopfes: Kontrast {kontrast:F2} liegt unter 4,5:1.");
        Assert.Equal(Palette.Karte, Farbe("ButtonBackgroundDisabled"));
    }

    // Weiße Schrift war auf dem dunklen #0078d7 gerade noch die AA-Grenze; auf
    // dem hellen Akzent der Palette wäre sie unlesbar.
    [AvaloniaFact]
    public void Text_auf_dem_Akzent_bleibt_lesbar()
    {
        foreach (var schluessel in new[]
                 {
                     "AccentButtonForeground", "ToggleButtonForegroundChecked",
                     "ToggleButtonForegroundCheckedPointerOver"
                 })
        {
            var kontrast = Palette.Kontrast(Farbe(schluessel), Palette.Akzent);
            Assert.True(kontrast >= 4.5,
                $"{schluessel} auf der Akzentfläche: Kontrast {kontrast:F2} liegt unter 4,5:1.");
        }
    }

    // Fluents Standard-Padding malt die Auswahl als ungleichmäßigen Rahmen um
    // die Karte und färbt den Abstand zur nächsten Karte mit ein.
    [AvaloniaFact]
    public async Task Die_Auswahl_faerbt_die_Karte_und_nicht_den_Abstand()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Weber, Sabine", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
        }

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.LadenAsync();
        fenster.Liste.SelectedIndex = 0;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();

        var zeile = Assert.IsType<ListBoxItem>(fenster.Liste.ContainerFromIndex(0));
        Assert.Equal(new Thickness(0), zeile.Padding);
        var flaeche = zeile.GetVisualDescendants().OfType<ContentPresenter>().First().Background;
        Assert.True(flaeche is null || (flaeche as ISolidColorBrush)?.Color.A == 0,
            $"Die Auswahl malt hinter der Karte: {flaeche}.");
        fenster.Close();
    }

    // Liefe „Nichts ging verloren" im selben Rot wie „Dieser Übergang ist nicht
    // erlaubt", lernte der Leser, dass Rot nichts bedeutet.
    [AvaloniaFact]
    public async Task Ein_Hinweis_traegt_nicht_die_Farbe_einer_Abweisung()
    {
        Ticket ticket;
        using (var scope = _factory.Services.CreateScope())
        {
            ticket = await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Weber, Sabine", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        await fenster.AktionAsync(dienst =>
            dienst.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, TestDaten.Teamleitung));
        var abweisung = fenster.Meldung.Foreground;

        fenster.HinweisZeigen("Nichts ging verloren.");
        var hinweis = fenster.Meldung.Foreground;

        Assert.Equal(Palette.GefahrText, ((ISolidColorBrush)abweisung!).Color);
        Assert.NotEqual(Palette.GefahrText, ((ISolidColorBrush)hinweis!).Color);
        Assert.True(fenster.MeldungSchliessen.IsVisible,
            "Eine Meldung, die man nicht wegklicken kann, bleibt im Weg stehen.");
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
