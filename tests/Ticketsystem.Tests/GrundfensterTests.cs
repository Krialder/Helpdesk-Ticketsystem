using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Prüft die Verdrahtung Hauptfenster zu Presenter: Kommen die gewählten
// Filter an, landet das Ergebnis in Liste, Leertext und Hinweis. Die
// Filterlogik selbst belegen die TicketlistenPresenterTests.
public sealed class GrundfensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(string titel)
    {
        using var scope = _factory.Services.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        return await tickets.CreatePhoneAsync(titel, "Beschreibung.", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Teamleitung.Id, createdByName: TestDaten.Teamleitung.Name,
            address: "A-1");
    }

    [AvaloniaFact]
    public async Task Die_gewaehlte_Ansicht_erreicht_den_Presenter()
    {
        await TicketAsync("Offen bleibt");
        var zu = await TicketAsync("Geschlossen faellt");
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .ChangeStatusAsync(zu.Id, TicketStatus.Closed, TestDaten.Teamleitung);
        }

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        await fenster.LadenAsync();
        var offen = ((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!).Select(z => z.Titel).ToList();

        fenster.Ansicht.SelectedItem = "Alle";
        await fenster.LadenAsync();
        var alle = ((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!).Count;

        Assert.Equal(["Offen bleibt"], offen);
        Assert.Equal(2, alle);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Suche_erreicht_den_Presenter_und_der_Filterleertext_erscheint()
    {
        await TicketAsync("Drucker klemmt");

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Suche.Text = "xyzzy";
        await fenster.LadenAsync();

        Assert.Empty((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!);
        Assert.True(fenster.Leertext.IsVisible);
        Assert.Contains("Filter", fenster.Leertext.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_leerer_Bestand_laedt_zum_ersten_Ticket_ein()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Ansicht.SelectedItem = "Alle";
        await fenster.LadenAsync();

        Assert.True(fenster.Leertext.IsVisible);
        Assert.Contains("noch keine Vorgänge", fenster.Leertext.Text);
        fenster.Close();
    }

    // Suchen ist der häufigste Handgriff nach dem Öffnen; ohne Beschleuniger
    // kostet er jedes Mal den Weg zur Maus, hundertfach am Tag.
    [AvaloniaFact]
    public void Strg_F_springt_in_die_Suche()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();

        fenster.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);

        Assert.True(fenster.Suche.IsFocused);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
