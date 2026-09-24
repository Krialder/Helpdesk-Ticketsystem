using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Prüft am Verwaltungsfenster die Reiter je Rolle, die Kacheln der
// Auswertung und den Meldungsweg des KontenDienstes. Die Regeln selbst
// belegen KontenDienstTests und AuswertungTests.
public sealed class VerwaltungsFensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public VerwaltungsFensterTests() => _factory.MitEinstellungen(new()
    {
        ["SeedAgent:Password"] = "Verwaltung-Probe1!"
    });

    [AvaloniaFact]
    public void Die_Reiter_folgen_der_Rolle()
    {
        var teamleitung = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        var administration = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);

        Assert.False(teamleitung.KontenReiter.IsVisible);
        Assert.False(teamleitung.DatenReiter.IsVisible);
        Assert.True(administration.KontenReiter.IsVisible);
        Assert.True(administration.StammdatenReiter.IsVisible);
        Assert.True(administration.DatenReiter.IsVisible);
        teamleitung.Close();
        administration.Close();
    }

    [AvaloniaFact]
    public async Task Die_Auswertung_fuellt_die_Kacheln_aus_dem_Dienst()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.CreatePhoneAsync("Zaehlt", "x", TicketPriority.Medium, "Weber, Sabine",
                "0221123456", DateTime.UtcNow, null,
                createdById: TestDaten.Teamleitung.Id, createdByName: TestDaten.Teamleitung.Name, address: "A-1");
        }

        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        await fenster.AuswertungLadenAsync();

        Assert.Equal("1", fenster.KachelGesamt.Text);
        Assert.Equal("1", fenster.KachelOhneRiss.Text);
        Assert.Equal("100 %", fenster.KachelQuote.Text);
        fenster.Close();
    }

    // Die letzte Administration lässt sich nicht herabstufen; die Wache des
    // Dienstes muss als Meldung im Fenster ankommen, nicht als Ausnahme.
    [AvaloniaFact]
    public async Task Eine_Wache_des_KontenDienstes_erscheint_als_Meldung()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        await fenster.KontenLadenAsync();

        fenster.Konten.SelectedIndex = 0;
        await fenster.KontenAktionAsync(dienst =>
        {
            using var scope = _factory.Services.CreateScope();
            return dienst.RolleSetzenAsync(
                ((IReadOnlyList<string>)fenster.Konten.ItemsSource!).Count > 0
                    ? SeedId(scope.ServiceProvider)
                    : "?",
                Rollen.Editor, TestDaten.Administration);
        });

        Assert.True(fenster.KontenMeldung.IsVisible);
        Assert.Contains("letzte Administrationskonto", fenster.KontenMeldung.Text);
        fenster.Close();
    }

    private static string SeedId(IServiceProvider scoped)
    {
        var users = scoped.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        return users.FindByEmailAsync("agent@ticketsystem.local").GetAwaiter().GetResult()!.Id;
    }

    [AvaloniaFact]
    public async Task Das_Entfernen_eines_Kontos_ist_zweistufig_und_der_Wechsel_nimmt_die_Frage_zurueck()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        await fenster.KontenLadenAsync();
        fenster.Konten.SelectedIndex = 0;

        fenster.KontoEntfernen.AusloeserKnopf.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.True(fenster.KontoEntfernen.IstScharf);
        Assert.Equal("Gewähltes Konto entfernen", fenster.KontoEntfernen.AusloeserKnopf.Content);

        // Ein Auswahlwechsel auf dasselbe Element zählt nicht, deshalb ein echter
        // Wechsel über das Neuladen der Liste.
        await fenster.KontenLadenAsync();
        fenster.Konten.SelectedIndex = 0;

        Assert.False(fenster.KontoEntfernen.IstScharf);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
