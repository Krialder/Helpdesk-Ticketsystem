using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Das Erfassungsformular ist auf das Telefonat zugeschnitten. Eine Kontur
// um zwölf Felder sagte „das alles ist eine Sache"; nicht die Zahl der
// Felder war das Problem, sondern dass sie ununterscheidbar waren.
public sealed class ErfassungsschnittTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private static void Auslegen(Window fenster)
    {
        fenster.Show();
        fenster.Measure(new Size(620, 760));
        fenster.Arrange(new Rect(0, 0, 620, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();
    }

    // Vor dem Umbau begann das Titelfeld bei y=311, weil die beiden Zeitwähler
    // rund 100 px davor verbrauchten, obwohl sie beim Anruf praktisch nie
    // geändert werden. Die zweite Zahl ist die eigentliche Forderung: Titel und
    // Beschreibung stehen ohne Scrollen da.
    [AvaloniaFact]
    public void Der_Titel_steht_ohne_Blaettern_im_Blick()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster);

        var y = fenster.Titel.TranslatePoint(new Point(0, 0), fenster)?.Y ?? 0;
        var beschreibungUnten = (fenster.Beschreibung.TranslatePoint(new Point(0, 0), fenster)?.Y ?? 0)
                                + fenster.Beschreibung.Bounds.Height;

        Assert.True(y <= 250, $"Das Titelfeld beginnt bei y={y:F0}.");
        Assert.True(beschreibungUnten <= 700,
            $"Die Beschreibung endet bei y={beschreibungUnten:F0} und damit hinter der Fußleiste.");
        fenster.Close();
    }

    // Beim Anruf ist „jetzt" fast immer richtig; bei der Mail kommt der
    // Zeitpunkt aus der Nachricht und ist fast immer zu ändern.
    [AvaloniaFact]
    public void Die_Zeitzeile_ist_beim_Anruf_gefaltet_und_bei_der_Mail_offen()
    {
        var anruf = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(anruf);
        Assert.False(anruf.ZeitWaehler.IsVisible);
        Assert.True(anruf.ZeitZeile.IsVisible);
        Assert.Contains(":", anruf.ZeitText.Text);

        anruf.ZeitAendern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(anruf.ZeitWaehler.IsVisible);
        anruf.Close();

        var mail = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung, alsMail: true);
        Auslegen(mail);
        Assert.True(mail.ZeitWaehler.IsVisible);
        mail.Close();
    }

    [AvaloniaFact]
    public async Task Ein_vertippter_Verweis_meldet_sich_beim_Verlassen_des_Feldes()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster);
        fenster.Verweis.Text = "12a";
        fenster.Verweis.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));

        Assert.True(fenster.VerweisFehler.IsVisible);
        Assert.Contains("Ticketnummer", fenster.VerweisFehler.Text);

        // Wer korrigiert, soll nicht weiter angeschrien werden.
        fenster.Verweis.Text = "12";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(fenster.VerweisFehler.IsVisible);

        await Task.CompletedTask;
        fenster.Close();
    }

    // Sonst steht die Fehlerliste vom vorigen Versuch neben einem
    // Verweis-Tippfehler und behauptet Mängel, die längst behoben sind.
    [AvaloniaFact]
    public async Task Ein_frueher_Abbruch_laesst_keine_alten_Meldungen_stehen()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster);
        await fenster.AnlegenAsync();
        Assert.NotEmpty((IReadOnlyList<Erfassungsfehler>)fenster.Fehlerliste.ItemsSource!);

        fenster.Verweis.Text = "12a";
        await fenster.AnlegenAsync();

        Assert.Empty((IReadOnlyList<Erfassungsfehler>)fenster.Fehlerliste.ItemsSource!);
        Assert.True(fenster.VerweisFehler.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Der_Suchtext_wandert_in_die_Erfassung()
    {
        var haupt = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        haupt.Show();
        haupt.Suche.Text = "Weber, Sabine";
        await haupt.LadenAsync();

        var erfassung = haupt.ErfassungBauen();

        Assert.Equal("Weber, Sabine", erfassung.Kunde.Text);
        erfassung.Close();
        haupt.Close();
    }

    [AvaloniaFact]
    public async Task Nach_dem_Anlegen_ist_der_neue_Vorgang_in_der_Liste_markiert()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Alt", "Alt.", TicketPriority.Medium, "Huber, Karl", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-1");
        }

        var haupt = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        haupt.Show();
        await haupt.LadenAsync();

        int neu;
        using (var scope = _factory.Services.CreateScope())
        {
            var ticket = await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Neu", "Neu.", TicketPriority.Medium, "Weber, Sabine", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-1");
            neu = ticket.Id;
        }

        await haupt.ErfolgZeigenAsync(neu, $"Ticket #{neu} wurde angelegt.");

        Assert.Equal($"Ticket #{neu} wurde angelegt.", haupt.Meldung.Text);
        var zeile = Assert.IsType<TicketZeile>(haupt.Liste.SelectedItem);
        Assert.Equal(neu, zeile.Id);
        haupt.Close();
    }

    public void Dispose() => _factory.Dispose();
}
