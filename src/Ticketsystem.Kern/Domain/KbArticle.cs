using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Feste Kategorie statt Freitext: Freitext ergibt „Drucker“, „drucker“ und
// „Druckerprobleme“ nebeneinander, und die Ordnung ist nichts mehr wert.
// Dasselbe gilt für die Schlagworte (KbTag).
public class KbKategorie
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<KbArticle> Artikel { get; set; } = [];
}

public class KbTag
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Vergleichsform für die Dublettenprüfung.
    [Required, MaxLength(50)]
    public string NameNormalisiert { get; set; } = string.Empty;

    public List<KbArticle> Artikel { get; set; } = [];
}

// Ein Artikel ist entweder Entwurf oder veröffentlicht; eine zweite Achse
// (intern, öffentlich) gibt es bewusst nicht, denn jeder Leser ist ein
// Mitarbeiter. Jedes Speichern legt eine Fassung an (KbArtikelFassung).
public class KbArticle
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public int? KategorieId { get; set; }

    public KbKategorie? Kategorie { get; set; }

    public List<KbTag> Tags { get; set; } = [];

    public bool Published { get; set; }

    [MaxLength(200)]
    public string? Owner { get; set; }

    // Owner ist der Namenstext, OwnerId die Kontokennung dahinter; über sie
    // zeigt die Anzeige den heutigen Anzeigenamen. Null bei alten und
    // importierten Artikeln.
    [MaxLength(450)]
    public string? OwnerId { get; set; }

    // Der eine Ort für die 180 Tage; Dienst und Import lesen die Zahl hier.
    public const int StandardPruefzyklusTage = 180;

    public int ReviewIntervallTage { get; set; } = StandardPruefzyklusTage;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Nach Ablauf zeigt die Oberfläche „Prüfung fällig“, der Artikel bleibt
    // sichtbar: Wer ihn am Telefon sucht, ist mit Hinweis besser bedient als
    // ohne Artikel.
    public bool PruefungFaellig(DateTime now) =>
        UpdatedAt.AddDays(ReviewIntervallTage) < now;

    public List<KbArtikelFassung> Fassungen { get; set; } = [];
}

// Bearbeiter schlagen vor, Teamleitung übernimmt oder lehnt ab. ArticleId
// null heißt: ein ganz neuer Artikel wird vorgeschlagen.
public class KbAenderungsvorschlag
{
    public int Id { get; set; }

    public int? ArticleId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public int? KategorieId { get; set; }

    [Required, MaxLength(200)]
    public string VorgeschlagenVon { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? VorgeschlagenVonId { get; set; }

    public DateTime VorgeschlagenAm { get; set; }
}
