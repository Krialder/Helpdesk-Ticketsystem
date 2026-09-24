using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ohne Web-Host gibt es keinen SignInManager; der Dienst prüft über den
// UserManager. Fehlversuche zählen und sperren muss er deshalb selbst, sonst
// wäre die Desktop-Tür frei durchprobierbar, während die Web-Anmeldung
// sperrt. Das Fenster zeigt nur an, was der Dienst entscheidet.
public sealed class AnmeldeDienstTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public AnmeldeDienstTests() => _factory.MitEinstellungen(new()
    {
        ["SeedAgent:Password"] = "Anmelde-Probe1!"
    });

    private AnmeldeDienst Dienst(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());

    [Fact]
    public async Task Richtige_Zugangsdaten_liefern_den_Akteur_mit_hoechster_Rolle()
    {
        using var scope = _factory.Services.CreateScope();

        var ergebnis = await Dienst(scope).AnmeldenAsync("agent@ticketsystem.local", "Anmelde-Probe1!");

        Assert.NotNull(ergebnis.Akteur);
        Assert.Equal(RoleLevel.Administration, ergebnis.Akteur!.Value.Level);
        // Das Startkonto heißt so, bis die Administration den echten Namen setzt.
        Assert.Equal("Startkonto", ergebnis.Akteur.Value.Name);
        Assert.Equal("Startkonto", ergebnis.Akteur.Value.Anzeige);
    }

    [Fact]
    public async Task Falsches_Passwort_wird_mit_deutscher_Meldung_abgewiesen()
    {
        using var scope = _factory.Services.CreateScope();

        var ergebnis = await Dienst(scope).AnmeldenAsync("agent@ticketsystem.local", "falsch");

        Assert.Null(ergebnis.Akteur);
        Assert.Contains("Anmeldung fehlgeschlagen", ergebnis.Fehler);
    }

    [Fact]
    public async Task Ein_unbekanntes_Konto_bekommt_dieselbe_Meldung_wie_ein_falsches_Passwort()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = Dienst(scope);

        var unbekannt = await dienst.AnmeldenAsync("gibtsnicht@example.org", "egal");
        var falsch = await dienst.AnmeldenAsync("agent@ticketsystem.local", "falsch");

        Assert.Null(unbekannt.Akteur);
        Assert.Equal(falsch.Fehler, unbekannt.Fehler);
    }

    [Fact]
    public async Task Fuenf_Fehlversuche_sperren_das_Konto_voruebergehend()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = Dienst(scope);
        for (var i = 0; i < 5; i++)
        {
            await dienst.AnmeldenAsync("agent@ticketsystem.local", "falsch");
        }

        var gesperrt = await dienst.AnmeldenAsync("agent@ticketsystem.local", "Anmelde-Probe1!");

        Assert.Null(gesperrt.Akteur);
        Assert.Contains("gesperrt", gesperrt.Fehler);
    }

    // Der Desktop hat keine Codeeingabe. Einschalten kann Zwei-Faktor niemand
    // mehr, aber Altbestände tragen es noch; der Passwort-Reset der
    // Administration räumt es ab, und die Meldung nennt diesen Ausweg.
    [Fact]
    public async Task Ein_Altkonto_mit_Zwei_Faktor_kommt_ohne_zweiten_Faktor_nicht_herein()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser { UserName = "alt2fa@example.org", Email = "alt2fa@example.org", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(konto, "Alt-Passwort1!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(konto, Rollen.Editor)).Succeeded);
        Assert.True((await users.ResetAuthenticatorKeyAsync(konto)).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(konto, true)).Succeeded);

        var ergebnis = await Dienst(scope).AnmeldenAsync("alt2fa@example.org", "Alt-Passwort1!");

        Assert.Null(ergebnis.Akteur);
        Assert.Contains("Zwei-Faktor", ergebnis.Fehler);
        Assert.Contains("Administration", ergebnis.Fehler);
    }

    [Fact]
    public async Task Ein_Konto_ohne_Mitarbeiterrolle_wird_an_der_Tuer_abgewiesen()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser { UserName = "ohnerolle@example.org", Email = "ohnerolle@example.org", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(konto, "Alt-Passwort1!")).Succeeded);

        var ergebnis = await Dienst(scope).AnmeldenAsync("ohnerolle@example.org", "Alt-Passwort1!");

        Assert.Null(ergebnis.Akteur);
        // Der Grund kommt erst nach richtigem Passwort, damit die Meldung keine
        // Kontoexistenz an Unbefugte verrät.
        Assert.Contains("keine Rolle", ergebnis.Fehler);
        Assert.Contains("Administration", ergebnis.Fehler);
    }

    // Sonst sammelt ein Konto über Wochen Versuche an und sperrt beim fünften
    // Tippfehler des Jahres.
    [Fact]
    public async Task Eine_erfolgreiche_Anmeldung_setzt_den_Fehlversuchszaehler_zurueck()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = Dienst(scope);
        for (var i = 0; i < 4; i++)
        {
            await dienst.AnmeldenAsync("agent@ticketsystem.local", "falsch");
        }
        Assert.NotNull((await dienst.AnmeldenAsync("agent@ticketsystem.local", "Anmelde-Probe1!")).Akteur);

        for (var i = 0; i < 4; i++)
        {
            await dienst.AnmeldenAsync("agent@ticketsystem.local", "falsch");
        }
        var ergebnis = await dienst.AnmeldenAsync("agent@ticketsystem.local", "Anmelde-Probe1!");

        Assert.NotNull(ergebnis.Akteur);
    }

    public void Dispose() => _factory.Dispose();
}
