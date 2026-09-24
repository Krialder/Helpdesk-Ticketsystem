namespace Ticketsystem.Kern.Domain;

public enum RufnummerArt
{
    Durchwahl,
    Festnetz,
    FestnetzLang,
    Mobil
}

// Gespeichert wird nur die Ziffernfolge, damit dieselbe Nummer nicht in
// fünf Schreibweisen im Bestand landet; das Format wird aus den Ziffern
// gelesen.
public static class RufnummerRegel
{
    public static string Normalisieren(string? eingabe)
    {
        var text = (eingabe ?? string.Empty).Trim();
        var ziffern = new string(text.Where(char.IsDigit).ToArray());

        // +49 und 0049 sind dieselbe Nummer wie die mit führender Null.
        if (text.StartsWith('+') && ziffern.StartsWith("49", StringComparison.Ordinal))
        {
            return "0" + ziffern[2..];
        }

        return ziffern.StartsWith("0049", StringComparison.Ordinal) ? "0" + ziffern[4..] : ziffern;
    }

    public static RufnummerArt? Erkenne(string? eingabe)
    {
        var ziffern = Normalisieren(eingabe);

        // Die Prüfung unten greift auf die zweite Ziffer zu; ohne die Schranke
        // wirft eine einzelne getippte Null IndexOutOfRangeException.
        if (ziffern.Length < 2)
        {
            return null;
        }

        // Mobil vor Festnetz: 015x, 016x und 017x beginnen ebenfalls mit 0, sind
        // aber keine Ortsvorwahl.
        if (ziffern.StartsWith("015") || ziffern.StartsWith("016") || ziffern.StartsWith("017"))
        {
            return ziffern.Length is >= 10 and <= 12 ? RufnummerArt.Mobil : null;
        }

        if (ziffern[0] == '0')
        {
            // Zweite Stelle 1 wäre eine Sonderrufnummer; als Rückrufnummer nicht
            // vorgesehen.
            if (ziffern[1] == '1')
            {
                return null;
            }

            return ziffern.Length switch
            {
                >= 8 and <= 11 => RufnummerArt.Festnetz,
                12 => RufnummerArt.FestnetzLang,
                _ => null
            };
        }

        // Ohne führende Null bleibt die hausinterne Durchwahl.
        return ziffern.Length is >= 2 and <= 6 ? RufnummerArt.Durchwahl : null;
    }

    public static bool IstGueltig(string? eingabe) => Erkenne(eingabe) is not null;

    public static string Anzeige(this RufnummerArt art) => art switch
    {
        RufnummerArt.Durchwahl => "Durchwahl",
        RufnummerArt.Festnetz => "Festnetz",
        RufnummerArt.FestnetzLang => "Festnetz (lange Vorwahl)",
        RufnummerArt.Mobil => "Mobil",
        _ => art.ToString()
    };
}
