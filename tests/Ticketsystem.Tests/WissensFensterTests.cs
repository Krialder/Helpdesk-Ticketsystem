using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Prüft am Wissensfenster die Rollenweiche der Knöpfe, Laden und Auswahl
// und das zweistufige Löschen. Die Regeln selbst (Freigabe-Workflow,
// Zweistufigkeit) belegen die KnowledgeBaseTests.
public sealed class WissensFensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task ArtikelAnlegenAsync(string titel)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>().SpeichernAsync(
            null, titel, "Inhalt.", null, published: true,
            reviewIntervallTage: KbArticle.StandardPruefzyklusTage, [], TestDaten.Teamleitung);
    }

    [AvaloniaFact]
    public async Task Die_Rollen_entscheiden_Beschriftung_und_Pflegeknoepfe()
    {
        await ArtikelAnlegenAsync("WLAN-Profil erneuern");

        var teamleitung = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        var bearbeiter = new WissensFenster(_factory.Services, TestDaten.Bearbeiter1);

        Assert.Equal("Neuer Artikel", teamleitung.Neu.Content);
        Assert.True(teamleitung.Freigaben.IsVisible);
        Assert.Equal("Artikel vorschlagen", bearbeiter.Neu.Content);
        Assert.False(bearbeiter.Freigaben.IsVisible);
        Assert.False(bearbeiter.Ordnung.IsVisible);
        Assert.True(teamleitung.Fassungen.IsVisible);
        Assert.False(bearbeiter.Fassungen.IsVisible);
        teamleitung.Close();
        bearbeiter.Close();
    }

    [AvaloniaFact]
    public async Task Laden_zeigt_den_ersten_Artikel_in_der_Lesespalte()
    {
        await ArtikelAnlegenAsync("WLAN-Profil erneuern");

        var fenster = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("WLAN-Profil erneuern", fenster.Titel.Text);
        Assert.Equal("Inhalt.", fenster.Inhalt.Text);
        Assert.False(fenster.Leertext.IsVisible);
        fenster.Close();
    }

    // Zweistufig, aber ohne modalen Dialog, weil der bei täglicher Wiederholung
    // blind bestätigt wird. Eine stehende Rückfrage, die nach einem
    // Auswahlwechsel auf den falschen Artikel zeigt, wäre gefährlicher als gar
    // keine.
    [AvaloniaFact]
    public async Task Der_erste_Klick_auf_Loeschen_loescht_nicht_und_der_Wechsel_nimmt_die_Frage_zurueck()
    {
        await ArtikelAnlegenAsync("Bleibt beim ersten Klick");
        await ArtikelAnlegenAsync("Zweiter Artikel");

        var fenster = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.LadenAsync();

        fenster.ArtikelLoeschen.AusloeserKnopf.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        Assert.True(fenster.ArtikelLoeschen.IstScharf);
        using (var scope = _factory.Services.CreateScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<TicketsystemContext>()
                .KbArticles.CountAsync());
        }

        fenster.Liste.SelectedIndex = 1;

        Assert.False(fenster.ArtikelLoeschen.IstScharf);
        fenster.Close();
    }

    [AvaloniaFact]
    public void Escape_schliesst_das_Fenster()
    {
        var fenster = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        Assert.True(fenster.IsVisible);

        fenster.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(fenster.IsVisible);
    }

    public void Dispose() => _factory.Dispose();
}
