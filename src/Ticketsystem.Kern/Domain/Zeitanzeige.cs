using System.Globalization;

namespace Ticketsystem.Kern.Domain;

// Gespeichert wird UTC, angezeigt Ortszeit: Eine unumgerechnete Zeit wäre
// im Sommer zwei Stunden falsch, und Fristen sind der Kern des Systems. Die
// Zone kommt als Parameter herein, damit Tests Sommer- und Winterzeit
// prüfen können; ohne Angabe gilt die Zone des Rechners.
public static class Zeitanzeige
{
    public const string MitUhrzeit = "dd.MM.yyyy HH:mm";

    public const string NurTag = "dd.MM.yyyy";

    public static string Anzeige(this DateTime gespeichert, TimeZoneInfo? zone = null) =>
        AlsOrtszeit(gespeichert, zone).ToString(MitUhrzeit, CultureInfo.InvariantCulture);

    public static string? Anzeige(this DateTime? gespeichert, TimeZoneInfo? zone = null) =>
        gespeichert is null ? null : Anzeige(gespeichert.Value, zone);

    public static string TagesAnzeige(this DateTime gespeichert, TimeZoneInfo? zone = null) =>
        AlsOrtszeit(gespeichert, zone).ToString(NurTag, CultureInfo.InvariantCulture);

    public static DateTime AlsOrtszeit(DateTime gespeichert, TimeZoneInfo? zone = null)
    {
        // Aus der Datenbank kommen die Werte als Unspecified; das ist für
        // ConvertTimeFromUtc in Ordnung, ein Local-Wert wäre es nicht.
        var utc = DateTime.SpecifyKind(gespeichert, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone ?? TimeZoneInfo.Local);
    }

    public static DateTime NachUtc(DateTime ortszeit, TimeZoneInfo? zone = null)
    {
        var ziel = zone ?? TimeZoneInfo.Local;
        var wert = DateTime.SpecifyKind(ortszeit, DateTimeKind.Unspecified);

        // In der Nacht der Umstellung auf Sommerzeit fehlt eine Stunde, und
        // ConvertTimeToUtc wirft dafür. Eine Stunde nach vorn ist besser als eine
        // abgebrochene Erfassung.
        if (ziel.IsInvalidTime(wert))
        {
            wert = wert.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(wert, ziel);
    }
}
