using Microsoft.AspNetCore.Identity;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public readonly record struct Anmeldeergebnis(Akteur? Akteur, string? Fehler);

// Meldet ein Konto an und liefert den Akteur mit seiner höchsten Rolle.
// Den Sperrzähler von Identity führt der Dienst selbst: Ohne SignInManager
// zählt sonst niemand mit, und die Anmeldung wäre frei durchprobierbar.
public sealed class AnmeldeDienst(UserManager<AppUser> users)
{
    // Dieselbe Meldung für unbekanntes Konto und falsches Passwort: Sie darf
    // nicht verraten, ob es das Konto gibt.
    private const string Abgewiesen = "Anmeldung fehlgeschlagen. E-Mail-Adresse und Passwort prüfen.";

    public async Task<Anmeldeergebnis> AnmeldenAsync(string email, string passwort)
    {
        var konto = await users.FindByEmailAsync(email.Trim());
        if (konto is null)
        {
            return new Anmeldeergebnis(null, Abgewiesen);
        }

        if (await users.IsLockedOutAsync(konto))
        {
            return new Anmeldeergebnis(null,
                "Das Konto ist nach zu vielen Fehlversuchen vorübergehend gesperrt. Bitte in einigen Minuten erneut versuchen.");
        }

        if (!await users.CheckPasswordAsync(konto, passwort))
        {
            await users.AccessFailedAsync(konto);
            if (await users.IsLockedOutAsync(konto))
            {
                return new Anmeldeergebnis(null,
                    "Das Konto ist nach zu vielen Fehlversuchen vorübergehend gesperrt. Bitte in einigen Minuten erneut versuchen.");
            }

            return new Anmeldeergebnis(null, Abgewiesen);
        }

        // Ab hier ist das Passwort richtig, die Meldungen dürfen konkret werden.
        // Zwei-Faktor lässt sich nicht mehr einschalten, Altkonten tragen sie noch,
        // und der Desktop hat keine Codeeingabe; der Passwort-Reset räumt sie ab.
        if (await users.GetTwoFactorEnabledAsync(konto))
        {
            return new Anmeldeergebnis(null,
                "Für dieses Konto ist eine Zwei-Faktor-Anmeldung aus dem Altbestand aktiv, die diese Anwendung nicht mehr anbietet. Die Administration kann das Passwort neu setzen; damit wird sie entfernt.");
        }

        var rollen = await users.GetRolesAsync(konto);
        var level = rollen.Contains(Rollen.Admin) ? RoleLevel.Administration
            : rollen.Contains(Rollen.TeamLead) ? RoleLevel.Teamleitung
            : rollen.Contains(Rollen.Editor) ? RoleLevel.Bearbeiter
            : RoleLevel.OhneRolle;

        if (level == RoleLevel.OhneRolle)
        {
            return new Anmeldeergebnis(null,
                "Dieses Konto hat keine Rolle im Ticketsystem und damit keinen Zugang. Rollen vergibt die Administration in der Kontenverwaltung.");
        }

        await users.ResetAccessFailedCountAsync(konto);

        return new Anmeldeergebnis(
            new Akteur(konto.Id, Kontenname.Voll(konto), level, konto.Anzeigename), null);
    }
}
