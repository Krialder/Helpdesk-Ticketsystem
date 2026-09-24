using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Praesentation;

namespace Ticketsystem.Tests;

// Prüft die Verdrahtung Erfassungsfenster zu ErfassungsAblauf: Werte kommen
// an, Fehler kommen zurück, der Verweis-Tippfehler steht am Feld. Die
// Prüfregeln selbst belegen die ErfassungsAblaufTests.
public sealed class ErfassungsFensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [AvaloniaFact]
    public async Task Ein_vertippter_Verweis_steht_am_Feld_und_es_entsteht_kein_Ticket()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Kunde.Text = "Weber, Sabine";
        fenster.Titel.Text = "Drucker klemmt";
        fenster.Beschreibung.Text = "Papierstau.";
        fenster.Nummer.Text = "0221123456";
        fenster.Adresse.Text = "A-1";
        fenster.Verweis.Text = "12a";

        await fenster.AnlegenAsync();

        Assert.True(fenster.VerweisFehler.IsVisible);
        Assert.Contains("Ticketnummer", fenster.VerweisFehler.Text);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<TicketsystemContext>()
            .Tickets.ToListAsync());
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Eine_vollstaendige_Telefonerfassung_legt_das_Ticket_an()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Kunde.Text = "Weber, Sabine";
        fenster.Titel.Text = "Drucker klemmt";
        fenster.Beschreibung.Text = "Papierstau.";
        fenster.Nummer.Text = "0221123456";
        fenster.Adresse.Text = "A-1";

        await fenster.AnlegenAsync();

        using var scope = _factory.Services.CreateScope();
        var ticket = Assert.Single(await scope.ServiceProvider
            .GetRequiredService<TicketsystemContext>().Tickets.ToListAsync());
        Assert.Equal("Drucker klemmt", ticket.Title);
        Assert.Equal("0221123456", ticket.CallbackNumber);
    }

    [AvaloniaFact]
    public async Task Fehler_des_Ablaufs_erscheinen_in_der_Fehlerliste()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Kunde.Text = "Weber, Sabine";
        fenster.Titel.Text = "Ohne Nummer";
        fenster.Beschreibung.Text = "x";
        fenster.Adresse.Text = "A-1";

        await fenster.AnlegenAsync();

        var fehler = (IReadOnlyList<Erfassungsfehler>)fenster.Fehlerliste.ItemsSource!;
        Assert.Contains(fehler, f => f.Text.Contains("Rückrufnummer"));
        fenster.Close();
    }

    // Eine TextBox mit AcceptsReturn schluckt die Tastenkombination und schreibt
    // einen Zeilenumbruch, also genau dort, wo der Bearbeiter beim Telefonat am
    // längsten steht. Deshalb drückt der Test die Tasten wirklich (KeyPress):
    // Der Weg über die Tastatur ist der Prüfgegenstand.
    [AvaloniaFact]
    public void Strg_Enter_wirkt_auch_aus_dem_mehrzeiligen_Beschreibungsfeld()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        fenster.Kunde.Text = "Weber, Sabine";
        fenster.Titel.Text = "Drucker klemmt";
        fenster.Nummer.Text = "0221123456";
        fenster.Adresse.Text = "A-1";
        fenster.Beschreibung.Text = "Papierstau.";
        fenster.Beschreibung.Focus();

        fenster.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);

        Assert.Equal("Papierstau.", fenster.Beschreibung.Text);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
