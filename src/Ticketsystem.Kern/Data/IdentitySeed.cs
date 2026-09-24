using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Data;

// Beim Start: Rollen anlegen, alte „Agent“-Rolle zu Administration
// migrieren und, wenn es keine Administration gibt, ein Startkonto anlegen.
// Ohne konfiguriertes Passwort wird eines gewürfelt und ins Protokoll
// geschrieben, damit nie ein bekanntes Passwort im Repository steht.
public static class IdentitySeed
{
    private const string LegacyAgentRole = "Agent";

    // Je ein Zeichen aus jeder Klasse zuerst, damit die Passwortregeln sicher
    // erfüllt sind, dann gemischt; ohne 0, O, l und 1, die man am Telefon
    // verwechselt.
    public static string Zufallspasswort()
    {
        const string klein = "abcdefghijkmnpqrstuvwxyz";
        const string gross = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string ziffern = "23456789";
        const string sonder = "!#$%*+-=?";
        const string alle = klein + gross + ziffern + sonder;

        var zeichen = new char[16];
        zeichen[0] = Wahl(klein);
        zeichen[1] = Wahl(gross);
        zeichen[2] = Wahl(ziffern);
        zeichen[3] = Wahl(sonder);
        for (var i = 4; i < zeichen.Length; i++)
        {
            zeichen[i] = Wahl(alle);
        }

        for (var i = zeichen.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (zeichen[i], zeichen[j]) = (zeichen[j], zeichen[i]);
        }

        return new string(zeichen);
    }

    private static char Wahl(string menge) => menge[RandomNumberGenerator.GetInt32(menge.Length)];

    public static async Task EnsureRolesAndAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rolle in Rollen.Alle)
        {
            if (!await roleManager.RoleExistsAsync(rolle))
            {
                await roleManager.CreateAsync(new IdentityRole(rolle));
            }
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        // Altbestand: Wer die frühere Rolle „Agent“ hatte, wird Administration,
        // sonst käme nach dem Umstieg niemand mehr in die Verwaltung.
        if (await roleManager.RoleExistsAsync(LegacyAgentRole))
        {
            foreach (var altAgent in await userManager.GetUsersInRoleAsync(LegacyAgentRole))
            {
                if (!await userManager.IsInRoleAsync(altAgent, Rollen.Admin))
                {
                    await userManager.AddToRoleAsync(altAgent, Rollen.Admin);
                }

                await userManager.RemoveFromRoleAsync(altAgent, LegacyAgentRole);
            }

            var agentRolle = await roleManager.FindByNameAsync(LegacyAgentRole);
            if (agentRolle is not null)
            {
                await roleManager.DeleteAsync(agentRolle);
            }
        }

        // Gibt es schon eine Administration, wird kein Startkonto angelegt; sonst
        // entstünde bei jedem Start ein neues.
        if ((await userManager.GetUsersInRoleAsync(Rollen.Admin)).Count > 0)
        {
            return;
        }

        var email = configuration["SeedAgent:Email"] ?? "agent@ticketsystem.local";
        var konfiguriertesPasswort = configuration["SeedAgent:Password"];

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            var passwort = konfiguriertesPasswort ?? Zufallspasswort();
            admin = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Nachname = "Startkonto",
            };
            var result = await userManager.CreateAsync(admin, passwort);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Seed-Administrationskonto konnte nicht angelegt werden: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            if (konfiguriertesPasswort is null)
            {
                services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Ticketsystem.Start")
                    .LogWarning(
                        "Startkonto {Email} angelegt. Passwort: {Passwort} " +
                        "Bitte nach der ersten Anmeldung über den Knopf Einstellungen im Hauptfenster ändern, " +
                        "und in der Verwaltung den echten Namen setzen.",
                        email, passwort);
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Rollen.Admin))
        {
            await userManager.AddToRoleAsync(admin, Rollen.Admin);
        }
    }
}
