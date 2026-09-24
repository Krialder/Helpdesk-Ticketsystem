using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

// Baut aus allen Konten ein Namensbuch, das gespeicherte Kennungen und
// Namen auf den heutigen Anzeigenamen abbildet. Aufgelöst wird bei der
// Anzeige, nicht im Bestand: Die Akte bleibt, wie sie geschrieben wurde.
public sealed class Namensverzeichnis(TicketsystemContext db)
{
    public async Task<Namensbuch> LadenAsync() =>
        new(await db.Users.AsNoTracking().ToListAsync());
}

public sealed class Namensbuch
{
    private readonly Dictionary<string, AppUser> _nachId;
    private readonly Dictionary<string, AppUser> _nachText;

    public Namensbuch(IReadOnlyCollection<AppUser> konten)
    {
        _nachId = konten.ToDictionary(k => k.Id, StringComparer.Ordinal);

        _nachText = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);
        // Ein gespeicherter Text kann Adresse oder voller Name sein, je nach Alter
        // der Zeile; fallunabhängig, weil abgetippte Adressen nicht immer passen.
        foreach (var konto in konten)
        {
            Merken(konto.UserName, konto);
            Merken(konto.Email, konto);
            Merken(Kontenname.Voll(konto), konto);
        }
    }

    public string Anzeige(string? kontoId, string? gespeichert) =>
        Finden(kontoId, gespeichert) is { } konto
            ? Kontenname.Anzeige(konto)
            : gespeichert ?? "";

    public string? KontoId(string? kontoId, string? gespeichert) =>
        Finden(kontoId, gespeichert)?.Id;

    public AppUser? Konto(string kontoId) =>
        _nachId.TryGetValue(kontoId, out var konto) ? konto : null;

    // Die Kennung gewinnt; der Text ist der Rückfall für Zeilen ohne Kennung.
    // Ein gelöschtes Konto bleibt unaufgelöst: Eine erfundene Zuordnung wäre
    // schlimmer als keine.
    private AppUser? Finden(string? kontoId, string? gespeichert)
    {
        if (!string.IsNullOrEmpty(kontoId) && _nachId.TryGetValue(kontoId, out var ueberId))
        {
            return ueberId;
        }

        return !string.IsNullOrWhiteSpace(gespeichert)
               && _nachText.TryGetValue(gespeichert.Trim(), out var ueberText)
            ? ueberText
            : null;
    }

    private void Merken(string? schluessel, AppUser konto)
    {
        if (!string.IsNullOrWhiteSpace(schluessel))
        {
            _nachText[schluessel.Trim()] = konto;
        }
    }
}
