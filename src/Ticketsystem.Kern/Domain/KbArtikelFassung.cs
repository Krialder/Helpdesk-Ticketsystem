using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Der Stand eines Artikels nach einer Änderung. Fassungen werden nie
// umgeschrieben; ein Rückweg auf einen alten Stand ist selbst wieder eine
// Fassung. Kategorie und Tags stehen als Namen darin, nicht als Verweise:
// Beides wird umbenannt und gelöscht, und eine Fassung, die ins Leere
// zeigt, sagt nichts mehr.
public class KbArtikelFassung
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    // Fortlaufend je Artikel ab 1; unter dieser Nummer geht man zurück.
    public int Nummer { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Kategorie { get; set; }

    // Alphabetisch, mit Komma und Leerzeichen getrennt; leer ohne Tags.
    [Required, MaxLength(1000)]
    public string Tags { get; set; } = string.Empty;

    public bool Published { get; set; }

    [Required, MaxLength(300)]
    public string Anlass { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string GespeichertVon { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? GespeichertVonId { get; set; }

    public DateTime GespeichertAm { get; set; }
}

// Die Anlässe an einer Stelle, damit die Liste der Versionen lesbar bleibt.
// Die Texte werden gespeichert; ändert sich der Wortlaut hier, behalten
// alte Fassungen ihren von damals. Auf dem Schirm heißt die Fassung
// „Version“.
public static class Fassungsanlass
{
    public const string Angelegt = "Angelegt";

    public const string Bearbeitet = "Bearbeitet";

    // Die Fassung 1, die die Migration jedem Artikel von vor der Versionierung
    // gegeben hat; AusSicherung ist dasselbe für alte Sicherungsdateien.
    public const string Altbestand = "Stand vor F3";

    public const string AusSicherung = "Aus Sicherung ohne Versionen";

    public static string VorschlagUebernommen(string von) => $"Vorschlag von {von} übernommen";

    public static string Zurueck(int nummer) => $"Zurück auf Version {nummer}";
}
