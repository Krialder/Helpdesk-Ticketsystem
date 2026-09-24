using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Domain;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Wer darf welchen Namen setzen, und was passiert bei einem doppelten
// Anzeigenamen. Auf dem Schirm steht, wie jemand heißen will; in der Akte
// steht, wer es war.
public sealed class KontennamenDienstTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<(KontenDienst Dienst, UserManager<AppUser> Konten, AppUser Wer)> AufbauAsync(
        string email = "sabine@example.org")
    {
        var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await konten.CreateAsync(konto, "Start-1234!");
        return (scope.ServiceProvider.GetRequiredService<KontenDienst>(), konten, konto);
    }

    [Fact]
    public async Task Die_Administration_setzt_den_unveraenderlichen_Namen()
    {
        var (dienst, konten, konto) = await AufbauAsync();

        var ergebnis = await dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Administration);

        Assert.True(ergebnis.Gelungen);
        var frisch = await konten.FindByIdAsync(konto.Id);
        Assert.Equal("Weber", frisch!.Nachname);
        Assert.Equal("Sabine", frisch.Vorname);
        Assert.Equal("Weber, Sabine", Kontenname.Voll(frisch));
    }

    // An diesem Namen hängt die Nachvollziehbarkeit; ein Name, den der
    // Betroffene selbst ändern kann, trägt sie nicht.
    [Fact]
    public async Task Ein_Bearbeiter_setzt_keinen_Namen()
    {
        var (dienst, _, konto) = await AufbauAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Bearbeiter1));
    }

    [Fact]
    public async Task Den_Anzeigenamen_setzt_jeder_fuer_sich()
    {
        var (dienst, konten, konto) = await AufbauAsync();
        await dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Administration);

        var ergebnis = await dienst.EigenenAnzeigenamenSetzenAsync(konto.Id, " Sabine ");

        Assert.True(ergebnis.Gelungen);
        var frisch = await konten.FindByIdAsync(konto.Id);
        Assert.Equal("Sabine", frisch!.Anzeigename);
        Assert.Equal("Sabine", Kontenname.Anzeige(frisch));
        Assert.Equal("Weber, Sabine", Kontenname.Voll(frisch));
    }

    // Sonst stünden in der Zuweisen-Auswahl zwei Zeilen „Sabine", und niemand
    // wüsste mehr, wen er zuweist.
    [Fact]
    public async Task Ein_vergebener_Anzeigename_wird_abgewiesen()
    {
        var (dienst, _, erste) = await AufbauAsync("sabine@example.org");
        var (_, _, zweite) = await AufbauAsync("sabine.zwei@example.org");
        await dienst.EigenenAnzeigenamenSetzenAsync(erste.Id, "Sabine");

        var ergebnis = await dienst.EigenenAnzeigenamenSetzenAsync(zweite.Id, "sabine");

        Assert.False(ergebnis.Gelungen);
        Assert.Contains("vergeben", ergebnis.Meldung);
    }

    [Fact]
    public async Task Ein_geleerter_Anzeigename_gibt_den_Namen_zurueck()
    {
        var (dienst, konten, konto) = await AufbauAsync();
        await dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Administration);
        await dienst.EigenenAnzeigenamenSetzenAsync(konto.Id, "Sabine");

        await dienst.EigenenAnzeigenamenSetzenAsync(konto.Id, "   ");

        var frisch = await konten.FindByIdAsync(konto.Id);
        Assert.Null(frisch!.Anzeigename);
        Assert.Equal("Weber, Sabine", Kontenname.Anzeige(frisch));
    }

    [Fact]
    public async Task Die_Anmeldung_liefert_beide_Namen()
    {
        var (dienst, _, konto) = await AufbauAsync("angemeldet@example.org");
        await dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Administration);
        await dienst.EigenenAnzeigenamenSetzenAsync(konto.Id, "Sabine");
        using (var scope = _factory.Services.CreateScope())
        {
            var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await konten.AddToRoleAsync((await konten.FindByIdAsync(konto.Id))!, Rollen.Editor);
        }

        using var pruef = _factory.Services.CreateScope();
        var ergebnis = await pruef.ServiceProvider.GetRequiredService<AnmeldeDienst>()
            .AnmeldenAsync("angemeldet@example.org", "Start-1234!");

        Assert.NotNull(ergebnis.Akteur);
        Assert.Equal("Weber, Sabine", ergebnis.Akteur!.Value.Name);
        Assert.Equal("Sabine", ergebnis.Akteur.Value.Anzeige);
    }

    [AvaloniaFact]
    public void Die_Statusleiste_nennt_den_Anzeigenamen_statt_der_Adresse()
    {
        var akteur = new Akteur("x", "Weber, Sabine", RoleLevel.Teamleitung, "Sabine");
        var fenster = new Grundfenster(_factory.Services, akteur);
        fenster.Show();

        Assert.Contains("Sabine", fenster.Angemeldet.Text);
        Assert.DoesNotContain("Weber", fenster.Angemeldet.Text);
        Assert.DoesNotContain("@", fenster.Angemeldet.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Zuweisung_zeigt_den_Anzeigenamen_und_schreibt_den_echten_Namen()
    {
        var (dienst, konten, konto) = await AufbauAsync("bearbeiter@example.org");
        await dienst.NamenSetzenAsync(konto.Id, "Weber", "Sabine", TestDaten.Administration);
        await dienst.EigenenAnzeigenamenSetzenAsync(konto.Id, "Sabine");
        using (var scope = _factory.Services.CreateScope())
        {
            var m = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await m.AddToRoleAsync((await m.FindByIdAsync(konto.Id))!, Rollen.Editor);
        }

        Ticket ticket;
        using (var scope = _factory.Services.CreateScope())
        {
            ticket = await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Drucker", "Papierstau.", TicketPriority.Medium, "Huber, Karl", "0221123456",
                DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Contains("Sabine", fenster.ZielWahl.Items.Select(i => i?.ToString()));
        Assert.DoesNotContain("bearbeiter@example.org", fenster.ZielWahl.Items.Select(i => i?.ToString()));

        fenster.ZielWahl.SelectedIndex = fenster.ZielWahl.Items
            .Select((i, n) => (i?.ToString(), n)).First(p => p.Item1 == "Sabine").n;
        fenster.Zuweisen.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 40; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }

        using var pruef = _factory.Services.CreateScope();
        var frisch = await pruef.ServiceProvider.GetRequiredService<TicketService>()
            .FindForUserAsync(ticket.Id, TestDaten.Teamleitung);
        Assert.Equal("Weber, Sabine", frisch!.AgentName);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
