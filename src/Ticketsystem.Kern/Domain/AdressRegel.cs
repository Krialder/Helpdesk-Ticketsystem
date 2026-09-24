using System.Text.RegularExpressions;

namespace Ticketsystem.Kern.Domain;

// Die Adresse eines Vorgangs: ein Großbuchstabe fürs Gebäude, Bindestrich,
// ein bis vier Ziffern Raum (etwa „A-101“), oder genau das Wort „extern“.
// Im Kern statt im Formular, damit jeder Erfassungsweg dieselbe Prüfung
// nutzt.
public static partial class AdressRegel
{
    public const string Extern = "extern";

    [GeneratedRegex(@"^[A-Z]-[0-9]{1,4}$")]
    private static partial Regex GebaeudeRaum();

    // Randleerzeichen und Kleinschreibung sind Tippfehler, keine anderen Orte;
    // sonst führte die Raum-Historie „A-101“ und „a-101 “ als zwei Räume.
    public static string Normalisieren(string? adresse)
    {
        var text = (adresse ?? string.Empty).Trim();
        return text.Equals(Extern, StringComparison.OrdinalIgnoreCase)
            ? Extern
            : text.ToUpperInvariant();
    }

    // Null ist ungültig, kein Fehler: Ein leeres Feld soll „ungültig“ ergeben
    // und keinen Abbruch.
    public static bool IstGueltig(string? adresse)
    {
        var text = Normalisieren(adresse);
        return text == Extern || GebaeudeRaum().IsMatch(text);
    }
}
