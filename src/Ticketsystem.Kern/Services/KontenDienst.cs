using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public sealed record KontoZeile(AppUser Konto, string RollenAnzeige, bool Pausiert);

public sealed record KontenErgebnis(bool Gelungen, string Meldung);

// Die Kontoverwaltung: Liste, Anlegen, Passwort, Rolle, Pause, Entfernen
// und Namen. Die Verwaltungsmethoden verlangen Administration und werfen
// sonst; die Methoden fürs eigene Konto (Anzeigename, Passwort) prüfen
// nichts, weil jeder nur sein eigenes Konto erreicht. Fachliche
// Ablehnungen kommen als gescheitertes KontenErgebnis zurück.
public sealed class KontenDienst(UserManager<AppUser> users)
{
    public async Task<IReadOnlyList<KontoZeile>> ListeAsync(Akteur akteur)
    {
        RequireAdministration(akteur);

        var jetzt = DateTime.UtcNow;
        var zeilen = new List<KontoZeile>();
        foreach (var konto in users.Users.OrderBy(u => u.UserName).ToList())
        {
            var rollen = await users.GetRolesAsync(konto);
            var anzeige = rollen.Contains(Rollen.Admin) ? RoleLevel.Administration.Anzeige()
                : rollen.Contains(Rollen.TeamLead) ? RoleLevel.Teamleitung.Anzeige()
                : rollen.Contains(Rollen.Editor) ? RoleLevel.Bearbeiter.Anzeige()
                : "Ohne Rolle";
            zeilen.Add(new KontoZeile(konto, anzeige, konto.PausedUntil is DateTime bis && bis > jetzt));
        }

        return zeilen;
    }

    public async Task<KontenErgebnis> AnlegenAsync(string email, string passwort, string rolle, Akteur akteur)
    {
        RequireAdministration(akteur);

        if (!Rollen.Alle.Contains(rolle))
        {
            return new KontenErgebnis(false, "Unbekannte Rolle.");
        }

        var konto = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        var ergebnis = await users.CreateAsync(konto, passwort);
        if (!ergebnis.Succeeded)
        {
            return new KontenErgebnis(false, string.Join(" ", ergebnis.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(konto, rolle);
        return new KontenErgebnis(true, $"Konto {email} angelegt.");
    }

    public async Task<KontenErgebnis> PasswortSetzenAsync(string id, string neuesPasswort, string wiederholung, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(neuesPasswort))
        {
            return new KontenErgebnis(false, "Bitte ein neues Passwort angeben.");
        }

        if (!string.Equals(neuesPasswort, wiederholung, StringComparison.Ordinal))
        {
            return new KontenErgebnis(false, "Die beiden Passwörter stimmen nicht überein. Es wurde nichts geändert.");
        }

        // Über Token und ResetPasswordAsync statt über einen direkten Hash, damit
        // dieselben Passwortregeln greifen wie beim Anlegen.
        var token = await users.GeneratePasswordResetTokenAsync(konto);
        var ergebnis = await users.ResetPasswordAsync(konto, token, neuesPasswort);
        if (!ergebnis.Succeeded)
        {
            return new KontenErgebnis(false, "Passwort nicht gesetzt: "
                + string.Join(" ", ergebnis.Errors.Select(f => f.Description)));
        }

        // Eine Zwei-Faktor-Anmeldung fällt mit weg: Ein Altkonto wäre sonst
        // unbenutzbar, weil kein Fenster einen Code entgegennimmt.
        var hatteZweiFaktor = await users.GetTwoFactorEnabledAsync(konto);
        if (hatteZweiFaktor)
        {
            await users.SetTwoFactorEnabledAsync(konto, false);
        }

        return new KontenErgebnis(true,
            $"Passwort für {konto.UserName} gesetzt. Bitte der Person persönlich mitteilen, nicht per Mail."
            + (hatteZweiFaktor ? " Die Zwei-Faktor-Anmeldung dieses Kontos wurde dabei entfernt." : string.Empty));
    }

    public async Task<KontenErgebnis> RolleSetzenAsync(string id, string rolle, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        if (!Rollen.Alle.Contains(rolle))
        {
            return new KontenErgebnis(false, "Unbekannte Rolle.");
        }

        if (await LetzteAdministrationAsync(konto, rolle) is { } wache)
        {
            return wache;
        }

        var bisher = await users.GetRolesAsync(konto);
        if (bisher.Count > 0)
        {
            await users.RemoveFromRolesAsync(konto, bisher);
        }

        await users.AddToRoleAsync(konto, rolle);
        return new KontenErgebnis(true, $"{konto.UserName} ist jetzt {AnzeigeVon(rolle)}.");
    }

    public async Task<KontenErgebnis> EntfernenAsync(string id, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        if (konto.Id == akteur.Id)
        {
            return new KontenErgebnis(false, "Das eigene Konto lässt sich hier nicht entfernen.");
        }

        if (await LetzteAdministrationAsync(konto, null) is { } wache)
        {
            return wache;
        }

        var ergebnis = await users.DeleteAsync(konto);
        return ergebnis.Succeeded
            ? new KontenErgebnis(true, $"Konto {konto.UserName} entfernt. Die Vorgänge dieser Person bleiben bestehen.")
            : new KontenErgebnis(false, "Konto nicht entfernt: " + string.Join(" ", ergebnis.Errors.Select(f => f.Description)));
    }

    public async Task<KontenErgebnis> PausierenAsync(string id, DateTime bisUtc, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        if (bisUtc <= DateTime.UtcNow)
        {
            return new KontenErgebnis(false, "Das Pausenende muss in der Zukunft liegen.");
        }

        konto.PausedUntil = DateTime.SpecifyKind(bisUtc, DateTimeKind.Utc);
        await users.UpdateAsync(konto);
        return new KontenErgebnis(true, $"Konto {konto.UserName} pausiert bis {konto.PausedUntil.Anzeige()}.");
    }

    public async Task<KontenErgebnis> AktivierenAsync(string id, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        konto.PausedUntil = null;
        await users.UpdateAsync(konto);
        return new KontenErgebnis(true, $"Konto {konto.UserName} ist wieder aktiv.");
    }

    // Nach- und Vornamen setzt die Administration, nicht der Inhaber: An
    // ihnen hängt die Nachvollziehbarkeit in Historie und Bearbeiterfeld, und
    // ein Name, den der Betroffene selbst ändern kann, trägt sie nicht. Der
    // Anzeigename dagegen gehört dem Inhaber. Leer heißt „nicht gesetzt",
    // dann bleibt die Adresse der Rückfall der Anzeige.
    public async Task<KontenErgebnis> NamenSetzenAsync(
        string id, string? nachname, string? vorname, Akteur akteur)
    {
        RequireAdministration(akteur);

        var konto = await users.FindByIdAsync(id);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        konto.Nachname = Leer(nachname);
        konto.Vorname = Leer(vorname);
        var ergebnis = await users.UpdateAsync(konto);
        return ergebnis.Succeeded
            ? new KontenErgebnis(true, $"Der Name lautet jetzt {Kontenname.Voll(konto)}.")
            : new KontenErgebnis(false, string.Join(" ", ergebnis.Errors.Select(f => f.Description)));
    }

    public async Task<KontenErgebnis> EigenenAnzeigenamenSetzenAsync(string userId, string? anzeigename)
    {
        var konto = await users.FindByIdAsync(userId);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        var wert = Leer(anzeigename);
        if (wert is not null)
        {
            var klein = wert.ToLowerInvariant();
            // Eindeutig ohne Rücksicht auf Groß- und Kleinschreibung, weil in der
            // Zuweisen-Auswahl sonst zweimal dasselbe Wort stünde.
            var vergeben = await users.Users
                .AnyAsync(u => u.Id != userId && u.Anzeigename != null && u.Anzeigename.ToLower() == klein);
            if (vergeben)
            {
                return new KontenErgebnis(false,
                    $"„{wert}“ ist schon vergeben. Bitte einen anderen Anzeigenamen wählen.");
            }
        }

        konto.Anzeigename = wert;
        var ergebnis = await users.UpdateAsync(konto);
        return ergebnis.Succeeded
            ? new KontenErgebnis(true, wert is null
                ? $"Der Anzeigename ist entfernt; angezeigt wird jetzt {Kontenname.Voll(konto)}."
                : $"Angezeigt wirst du jetzt als {wert}.")
            : new KontenErgebnis(false, string.Join(" ", ergebnis.Errors.Select(f => f.Description)));
    }

    private static string? Leer(string? wert) =>
        string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

    public async Task<KontenErgebnis> EigenesPasswortAendernAsync(
        string userId, string altesPasswort, string neuesPasswort, string wiederholung)
    {
        var konto = await users.FindByIdAsync(userId);
        if (konto is null)
        {
            return new KontenErgebnis(false, "Konto nicht gefunden.");
        }

        if (!string.Equals(neuesPasswort, wiederholung, StringComparison.Ordinal))
        {
            return new KontenErgebnis(false, "Die beiden neuen Passwörter stimmen nicht überein.");
        }

        var ergebnis = await users.ChangePasswordAsync(konto, altesPasswort, neuesPasswort);
        return ergebnis.Succeeded
            ? new KontenErgebnis(true, "Das Passwort ist geändert.")
            : new KontenErgebnis(false, string.Join(" ", ergebnis.Errors.Select(f => f.Description)));
    }

    // Das letzte Administrationskonto bleibt Administration und bleibt
    // bestehen: Ohne eines ist das System von innen nicht mehr zu reparieren,
    // denn es gibt keine Registrierung und die Kontoverwaltung wäre
    // unerreichbar. zielRolle null heißt entfernen.
    private async Task<KontenErgebnis?> LetzteAdministrationAsync(AppUser konto, string? zielRolle)
    {
        var istAdmin = await users.IsInRoleAsync(konto, Rollen.Admin);
        if (!istAdmin || zielRolle == Rollen.Admin)
        {
            return null;
        }

        var administratoren = await users.GetUsersInRoleAsync(Rollen.Admin);
        if (administratoren.Count > 1)
        {
            return null;
        }

        return new KontenErgebnis(false,
            "Das ist das letzte Administrationskonto. Ohne ein solches Konto kommt niemand mehr in "
            + "die Kontoverwaltung. Erst ein zweites anlegen, dann dieses ändern.");
    }

    private static string AnzeigeVon(string rolle) => rolle switch
    {
        Rollen.Admin => RoleLevel.Administration.Anzeige(),
        Rollen.TeamLead => RoleLevel.Teamleitung.Anzeige(),
        _ => RoleLevel.Bearbeiter.Anzeige()
    };

    private static void RequireAdministration(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Administration))
        {
            throw new InvalidOperationException("Die Kontoverwaltung ist der Administration vorbehalten.");
        }
    }
}
