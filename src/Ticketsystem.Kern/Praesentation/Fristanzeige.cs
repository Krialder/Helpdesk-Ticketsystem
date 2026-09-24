using System.Globalization;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Praesentation;

// Die Frist als Restzeit, nicht als Endzeitpunkt: „noch 42 min“ liest man
// ohne zu rechnen, und gerechnet wird ausgerechnet, während ein Kunde
// wartet. Das Zeichen trägt den Zustand auch ohne Farbe.
public static class Fristanzeige
{
    public static string Text(SlaState zustand, DateTime faelligUtc, DateTime jetztUtc, TimeZoneInfo? zone = null)
    {
        switch (zustand)
        {
            case SlaState.Erfuellt:
                return "erfüllt";
            case SlaState.VerspaetetErfuellt:
                return "verspätet erfüllt";
            case SlaState.Entfaellt:
                return "entfällt";
        }

        var rest = faelligUtc - jetztUtc;
        var zeitpunkt = Zeitpunkt(faelligUtc, jetztUtc, zone);

        return zustand == SlaState.Ueberfaellig || rest < TimeSpan.Zero
            ? $"überfällig seit {Dauer(-rest)} (seit {zeitpunkt})"
            : $"noch {Dauer(rest)} (bis {zeitpunkt})";
    }

    // Für die Liste ohne den Zeitpunkt; der steht im Detailfenster.
    public static string Kurz(SlaState zustand, DateTime faelligUtc, DateTime jetztUtc)
    {
        switch (zustand)
        {
            case SlaState.Erfuellt:
                return "erfüllt";
            case SlaState.VerspaetetErfuellt:
                return "verspätet erfüllt";
            case SlaState.Entfaellt:
                return "entfällt";
        }

        var rest = faelligUtc - jetztUtc;
        return zustand == SlaState.Ueberfaellig || rest < TimeSpan.Zero
            ? $"seit {Dauer(-rest)}"
            : $"noch {Dauer(rest)}";
    }

    // Ein Zeichen je Zustand, damit die Frist auch ankommt, wenn die Farbe
    // nicht gesehen wird (Farbsehschwäche, Schwarz-Weiß-Druck).
    public static string Zeichen(SlaState zustand) => zustand switch
    {
        SlaState.Laeuft => "○",
        SlaState.BaldFaellig => "●",
        SlaState.Ueberfaellig => "!",
        SlaState.Erfuellt => "✓",
        SlaState.VerspaetetErfuellt => "✓!",
        SlaState.Entfaellt => "–",
        _ => "–"
    };

    // Heute nur die Uhrzeit, sonst mit Datum: „bis 14:30“ reicht, wenn es
    // derselbe Tag ist.
    private static string Zeitpunkt(DateTime faelligUtc, DateTime jetztUtc, TimeZoneInfo? zone)
    {
        var faellig = Zeitanzeige.AlsOrtszeit(faelligUtc, zone);
        var jetzt = Zeitanzeige.AlsOrtszeit(jetztUtc, zone);
        return faellig.Date == jetzt.Date
            ? faellig.ToString("HH:mm", CultureInfo.InvariantCulture)
            : faellig.ToString(Zeitanzeige.MitUhrzeit, CultureInfo.InvariantCulture);
    }

    private static string Dauer(TimeSpan spanne)
    {
        if (spanne < TimeSpan.FromMinutes(1))
        {
            return "unter 1 min";
        }

        if (spanne < TimeSpan.FromHours(1))
        {
            return $"{(int)spanne.TotalMinutes} min";
        }

        if (spanne < TimeSpan.FromDays(1))
        {
            return $"{(int)spanne.TotalHours} h {spanne.Minutes} min";
        }

        var tage = (int)spanne.TotalDays;
        return $"{tage} {(tage == 1 ? "Tag" : "Tage")} {spanne.Hours} h";
    }
}
