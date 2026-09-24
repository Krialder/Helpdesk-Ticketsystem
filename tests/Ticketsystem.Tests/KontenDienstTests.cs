using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Wachen der Kontoverwaltung (letzte Administration, eigenes Konto)
// sind Sicherheitsregeln und stehen deshalb einmal im Dienst, mit eigener
// Rechteprüfung, statt in einer Oberfläche.
public sealed class KontenDienstTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public KontenDienstTests() => _factory.MitEinstellungen(new()
    {
        ["SeedAgent:Password"] = "Konten-Probe1!"
    });

    private (KontenDienst Dienst, UserManager<AppUser> Users, IServiceScope Scope) Aufbau()
    {
        var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return (new KontenDienst(users), users, scope);
    }

    private static Akteur AdminAkteur(AppUser seed) => new(seed.Id, seed.UserName!, RoleLevel.Administration);

    [Fact]
    public async Task Anlegen_mit_Rolle_und_Liste_zeigen_das_Konto()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var admin = AdminAkteur((await users.FindByEmailAsync("agent@ticketsystem.local"))!);

        var ergebnis = await dienst.AnlegenAsync("neu@example.org", "Start-Passwort1!", Rollen.Editor, admin);

        Assert.True(ergebnis.Gelungen);
        var zeile = Assert.Single(await dienst.ListeAsync(admin), z => z.Konto.UserName == "neu@example.org");
        Assert.Equal(RoleLevel.Bearbeiter.Anzeige(), zeile.RollenAnzeige);
    }

    [Fact]
    public async Task Die_letzte_Administration_laesst_sich_weder_degradieren_noch_entfernen()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        // Ein zweiter Akteur, damit „eigenes Konto" nicht greift und die Wache
        // selbst geprüft wird.
        var zweiterAdminAkteur = new Akteur("fremde-admin-id", "zweite@example.org", RoleLevel.Administration);

        var degradieren = await dienst.RolleSetzenAsync(seed.Id, Rollen.Editor, zweiterAdminAkteur);
        var entfernen = await dienst.EntfernenAsync(seed.Id, zweiterAdminAkteur);

        Assert.False(degradieren.Gelungen);
        Assert.Contains("letzte Administrationskonto", degradieren.Meldung);
        Assert.False(entfernen.Gelungen);
        Assert.NotNull(await users.FindByEmailAsync("agent@ticketsystem.local"));
    }

    // Die Wache darf nur das letzte Konto schützen, sonst wäre keine
    // Rollenkorrektur mehr möglich.
    [Fact]
    public async Task Mit_zweiter_Administration_ist_das_Degradieren_erlaubt()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        await dienst.AnlegenAsync("zweite@example.org", "Start-Passwort1!", Rollen.Admin, admin);

        var ergebnis = await dienst.RolleSetzenAsync(seed.Id, Rollen.Editor, admin);

        Assert.True(ergebnis.Gelungen);
    }

    [Fact]
    public async Task Das_eigene_Konto_laesst_sich_nicht_entfernen()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        await dienst.AnlegenAsync("zweite@example.org", "Start-Passwort1!", Rollen.Admin, admin);

        var ergebnis = await dienst.EntfernenAsync(seed.Id, admin);

        Assert.False(ergebnis.Gelungen);
        Assert.Contains("eigene Konto", ergebnis.Meldung);
    }

    [Fact]
    public async Task Der_Passwort_Reset_entfernt_eine_alte_Zwei_Faktor_Anmeldung()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        await dienst.AnlegenAsync("alt@example.org", "Start-Passwort1!", Rollen.Editor, admin);
        var alt = (await users.FindByEmailAsync("alt@example.org"))!;
        await users.SetTwoFactorEnabledAsync(alt, true);

        var ergebnis = await dienst.PasswortSetzenAsync(alt.Id, "Neu-Passwort1!", "Neu-Passwort1!", admin);

        Assert.True(ergebnis.Gelungen);
        Assert.Contains("Zwei-Faktor", ergebnis.Meldung);
        Assert.False(await users.GetTwoFactorEnabledAsync(alt));
        Assert.True(await users.CheckPasswordAsync(alt, "Neu-Passwort1!"));
    }

    [Fact]
    public async Task Pausieren_verlangt_ein_Ende_in_der_Zukunft()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        await dienst.AnlegenAsync("pause@example.org", "Start-Passwort1!", Rollen.Editor, admin);
        var konto = (await users.FindByEmailAsync("pause@example.org"))!;

        var vergangenheit = await dienst.PausierenAsync(konto.Id, DateTime.UtcNow.AddHours(-1), admin);
        var zukunft = await dienst.PausierenAsync(konto.Id, DateTime.UtcNow.AddDays(7), admin);

        Assert.False(vergangenheit.Gelungen);
        Assert.True(zukunft.Gelungen);
        Assert.True((await dienst.ListeAsync(admin)).Single(z => z.Konto.Id == konto.Id).Pausiert);
    }

    [Fact]
    public async Task Die_Kontoverwaltung_ist_der_Administration_vorbehalten()
    {
        var (dienst, _, scope) = Aufbau();
        using var _1 = scope;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.ListeAsync(TestDaten.Teamleitung));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dienst.AnlegenAsync("x@example.org", "Start-Passwort1!", Rollen.Editor, TestDaten.Teamleitung));
    }

    // In der Auslieferung gibt es genau ein Konto, und das ist das eigene: Ein
    // Tippfehler im maskierten Feld sperrt die Administration aus und fällt
    // erst beim nächsten Anmelden auf.
    [Fact]
    public async Task Eine_abweichende_Wiederholung_aendert_kein_Passwort()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;
        var admin = AdminAkteur(seed);
        await dienst.AnlegenAsync("tipp@example.org", "Start-Passwort1!", Rollen.Editor, admin);
        var konto = (await users.FindByEmailAsync("tipp@example.org"))!;

        var fremd = await dienst.PasswortSetzenAsync(konto.Id, "Neu-Passwort1!", "Neu-Passwort2!", admin);
        var eigen = await dienst.EigenesPasswortAendernAsync(seed.Id, "Konten-Probe1!", "Neu-Passwort1!", "Neu-Passwort2!");

        Assert.False(fremd.Gelungen);
        Assert.False(eigen.Gelungen);
        Assert.True(await users.CheckPasswordAsync(konto, "Start-Passwort1!"));
        Assert.True(await users.CheckPasswordAsync(seed, "Konten-Probe1!"));
    }

    // Die Identity-Regeln melden sonst englisch; der DeutscheIdentityFehler
    // übersetzt sie, und die Kette muss auch über den KontenDienst laufen.
    [Fact]
    public async Task Ein_zu_schwaches_Passwort_wird_mit_deutscher_Meldung_abgewiesen()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var admin = AdminAkteur((await users.FindByEmailAsync("agent@ticketsystem.local"))!);

        var ergebnis = await dienst.AnlegenAsync("schwach@example.org", "kurz", Rollen.Editor, admin);

        Assert.False(ergebnis.Gelungen);
        Assert.Contains("Zeichen", ergebnis.Meldung);
        Assert.Null(await users.FindByEmailAsync("schwach@example.org"));
    }

    [Fact]
    public async Task Ein_fremdes_Konto_laesst_sich_entfernen()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var admin = AdminAkteur((await users.FindByEmailAsync("agent@ticketsystem.local"))!);
        await dienst.AnlegenAsync("weg@example.org", "Start-Passwort1!", Rollen.Editor, admin);
        var konto = (await users.FindByEmailAsync("weg@example.org"))!;

        var ergebnis = await dienst.EntfernenAsync(konto.Id, admin);

        Assert.True(ergebnis.Gelungen);
        Assert.Null(await users.FindByEmailAsync("weg@example.org"));
    }

    [Fact]
    public async Task Das_eigene_Passwort_verlangt_das_alte()
    {
        var (dienst, users, scope) = Aufbau();
        using var _ = scope;
        var seed = (await users.FindByEmailAsync("agent@ticketsystem.local"))!;

        var falsch = await dienst.EigenesPasswortAendernAsync(seed.Id, "falsch", "Neu-Passwort1!", "Neu-Passwort1!");
        var richtig = await dienst.EigenesPasswortAendernAsync(seed.Id, "Konten-Probe1!", "Neu-Passwort1!", "Neu-Passwort1!");

        Assert.False(falsch.Gelungen);
        Assert.True(richtig.Gelungen);
        Assert.True(await users.CheckPasswordAsync(seed, "Neu-Passwort1!"));
    }

    public void Dispose() => _factory.Dispose();
}
