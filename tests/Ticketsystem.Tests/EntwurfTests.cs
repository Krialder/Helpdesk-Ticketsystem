using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App;
using Ticketsystem.App.Fenster;

namespace Ticketsystem.Tests;

// Die Entwurfssicherung ist der Grund, warum das Erfassungsfenster ohne
// Rückfrage schließt: Was gesichert ist, geht nicht verloren, und eine
// Rückfrage vor jedem Schließen wäre binnen einer Woche ein Reflex ohne
// Schutzwirkung.
public sealed class EntwurfTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private string Pfad()
    {
        using var scope = _factory.Services.CreateScope();
        return Entwurfsablage.Pfad(scope.ServiceProvider.GetRequiredService<IConfiguration>());
    }

    [Fact]
    public void Ein_leerer_Entwurf_wird_nicht_angeboten()
    {
        var pfad = Pfad();
        Entwurfsablage.Speichern(pfad, new ErfassungsEntwurf(
            false, "", "", "", "", "", "", 1, "", "", false, DateTime.UtcNow));

        Assert.Null(Entwurfsablage.Laden(pfad));
    }

    // Der Entwurf ist Zusatznutzen, der Vorgang die Hauptsache; der Fehler wird
    // wegdefiniert statt nach oben gereicht.
    [Fact]
    public void Eine_kaputte_Entwurfsdatei_verweigert_die_Erfassung_nicht()
    {
        var pfad = Pfad();
        File.WriteAllText(pfad, "{kein gueltiges json");

        Assert.Null(Entwurfsablage.Laden(pfad));
    }

    [AvaloniaFact]
    public void Getipptes_ueberlebt_das_Schliessen_und_wird_beim_naechsten_Mal_angeboten()
    {
        var erste = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        erste.Show();
        erste.Kunde.Text = "Weber, Sabine";
        erste.Titel.Text = "Drucker klemmt";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        erste.Close();
        // Das Tippen allein muss reichen; ein eigener Speichern-Schritt wäre genau
        // der Handgriff, den man im Telefonat vergisst.
        Assert.NotNull(Entwurfsablage.Laden(Pfad()));

        var zweite = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        zweite.Show();

        Assert.True(zweite.EntwurfsLeiste.IsVisible);
        Assert.Contains("Weber, Sabine", zweite.EntwurfsText.Text);
        zweite.Close();
    }

    [AvaloniaFact]
    public void Das_Angebot_wird_erst_auf_Wunsch_uebernommen()
    {
        var erste = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        erste.Show();
        erste.Kunde.Text = "Huber, Karl";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        erste.Close();

        var zweite = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        zweite.Show();
        Assert.True(string.IsNullOrEmpty(zweite.Kunde.Text));

        zweite.EntwurfWeiter.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        Assert.Equal("Huber, Karl", zweite.Kunde.Text);
        Assert.False(zweite.EntwurfsLeiste.IsVisible);
        zweite.Close();
    }

    [AvaloniaFact]
    public void Neu_beginnen_raeumt_den_Entwurf_weg()
    {
        var erste = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        erste.Show();
        erste.Kunde.Text = "Schneider, Eva";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        erste.Close();

        var zweite = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        zweite.Show();
        zweite.EntwurfVerwerfen.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        zweite.Close();

        Assert.Null(Entwurfsablage.Laden(Pfad()));
    }

    public void Dispose()
    {
        var pfad = Pfad();
        _factory.Dispose();
        Entwurfsablage.Loeschen(pfad);
    }
}
