using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

// Stammdaten für die Erfassung: Anrufer mit letzter Rufnummer und letzter
// Adresse, dazu die schon verwendeten Adressen. Der Bestand ist
// personenbezogen: Die Suche liefert nur Treffer zu einem Begriff, nie
// den ganzen Bestand; die volle Liste sieht allein die Administration,
// als Löschweg für Auskunfts- und Löschanfragen nach DSGVO.
public class StammdatenService(TicketsystemContext db)
{
    // Kürzere Begriffe reichten faktisch den ganzen Bestand durch.
    public const int MindestZeichen = 3;

    public const int MaxTreffer = 8;

    public sealed record KontaktTreffer(string Name, string? LetzteAdresse, string? Rufnummer);

    public async Task<IReadOnlyList<KontaktTreffer>> SucheKontakteAsync(string begriff, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var such = (begriff ?? string.Empty).Trim().ToLowerInvariant();
        if (such.Length < MindestZeichen)
        {
            return [];
        }

        return await db.Kontakte
            // Verglichen wird die gespeicherte Kleinschreibung, weil SQLite Umlaute
            // nicht fallunabhängig vergleicht.
            .Where(k => k.NameNormalisiert.Contains(such))
            .OrderBy(k => k.Name)
            .Take(MaxTreffer)
            .Select(k => new KontaktTreffer(k.Name, k.LetzteAdresse, k.LetzteRufnummer))
            .ToListAsync();
    }

    // Adressen kommen aus den erfassten Tickets statt aus einer eigenen
    // Tabelle: Ein zweiter Bestand wäre doppelte Pflege ohne Gewinn.
    public async Task<IReadOnlyList<string>> SucheAdressenAsync(string begriff, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var such = (begriff ?? string.Empty).Trim().ToUpperInvariant();
        if (such.Length < 1)
        {
            return [];
        }

        return await db.Tickets
            .Where(t => t.Address != null && t.Address.StartsWith(such))
            .Select(t => t.Address!)
            .Distinct()
            .OrderBy(a => a)
            .Take(MaxTreffer)
            .ToListAsync();
    }

    public async Task ErfasseAsync(string name, string? rufnummer, string? adresse, Akteur akteur)
    {
        RequireMitarbeiter(akteur);

        var sauber = (name ?? string.Empty).Trim();
        if (sauber.Length == 0)
        {
            return;
        }

        var normalisiert = sauber.ToLowerInvariant();
        var kontakt = await db.Kontakte.SingleOrDefaultAsync(k => k.NameNormalisiert == normalisiert);

        var jetzt = DateTime.UtcNow;
        if (kontakt is null)
        {
            kontakt = new Kontakt
            {
                Name = sauber,
                NameNormalisiert = normalisiert,
                CreatedAt = jetzt,
                UpdatedAt = jetzt
            };
            db.Kontakte.Add(kontakt);
        }
        else
        {
            kontakt.UpdatedAt = jetzt;
        }

        // Nur ein gültiger Wert ersetzt den bisherigen; ein ungültiger verdrängt
        // keinen gültigen. Gleiche Regel für die Rufnummer.
        if (adresse is not null && AdressRegel.IstGueltig(adresse))
        {
            kontakt.LetzteAdresse = adresse;
        }

        if (!string.IsNullOrWhiteSpace(rufnummer) && RufnummerRegel.Erkenne(rufnummer) is not null)
        {
            kontakt.LetzteRufnummer = RufnummerRegel.Normalisieren(rufnummer);
        }

        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Kontakt>> KontakteAsync(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Administration))
        {
            throw new InvalidOperationException("Die Stammdatenliste sieht nur die Administration.");
        }

        return await db.Kontakte
            .AsNoTracking()
            .OrderBy(k => k.NameNormalisiert)
            .ToListAsync();
    }

    // Die Tickets bleiben stehen: Sie sind Vorgangsdokumentation, und ihr
    // Löschweg steht getrennt in der DSGVO-Checkliste.
    public async Task<bool> LoescheKontaktAsync(string name, Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Administration))
        {
            throw new InvalidOperationException("Stammdaten löschen darf nur die Administration.");
        }

        var normalisiert = (name ?? string.Empty).Trim().ToLowerInvariant();
        var kontakt = await db.Kontakte.SingleOrDefaultAsync(k => k.NameNormalisiert == normalisiert);

        if (kontakt is null)
        {
            return false;
        }

        db.Kontakte.Remove(kontakt);
        await db.SaveChangesAsync();
        return true;
    }

    private static void RequireMitarbeiter(Akteur akteur)
    {
        if (!akteur.IstMitarbeiter)
        {
            throw new InvalidOperationException("Stammdaten sind Mitarbeitern vorbehalten.");
        }
    }
}
