namespace Ticketsystem.Kern.Domain;

// Zwei Namen je Konto, an einer Stelle gebaut: Der volle Name (Nachname,
// Vorname) setzt nur die Administration und landet in Historie, Bearbeiter und
// Ersteller; der Anzeigename ist änderbar und trägt nur die Anzeige.
// Dieselbe Schreibweise wie bei Kunden, damit beim Sortieren nicht der
// Vorname führt.
public static class Kontenname
{
    public static string Voll(string? nachname, string? vorname, string rueckfall)
    {
        var nach = (nachname ?? "").Trim();
        var vor = (vorname ?? "").Trim();

        if (nach.Length > 0 && vor.Length > 0)
        {
            return $"{nach}, {vor}";
        }

        // Ohne Namen bleibt der Rückfall (Anmeldename, notfalls Kennung); ein
        // erfundener Name sähe aus wie Wissen.
        return nach.Length > 0 ? nach
            : vor.Length > 0 ? vor
            : rueckfall;
    }

    public static string Voll(AppUser konto) =>
        Voll(konto.Nachname, konto.Vorname, konto.UserName ?? konto.Id);

    public static string Anzeige(string? anzeigename, string? nachname, string? vorname, string rueckfall) =>
        string.IsNullOrWhiteSpace(anzeigename) ? Voll(nachname, vorname, rueckfall) : anzeigename.Trim();

    public static string Anzeige(AppUser konto) =>
        Anzeige(konto.Anzeigename, konto.Nachname, konto.Vorname, konto.UserName ?? konto.Id);
}
