using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Stammdaten eines wiederkehrenden Anrufers, nur was die Erfassung
// beschleunigt. Personenbezogen: nur für angemeldete Mitarbeiter sichtbar,
// Löschweg in der DSGVO-Checkliste.
public class Kontakt
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Klein und ohne Randleerzeichen, damit „Meier“ und „meier “ nicht zwei
    // Stammsätze werden.
    [Required, MaxLength(200)]
    public string NameNormalisiert { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? LetzteAdresse { get; set; }

    // Nur Ziffern, und nur die zuletzt genannte Nummer: sparsamer (DSGVO), und
    // der Bearbeiter braucht nur die aktuelle. Die Art (Durchwahl, Festnetz,
    // Mobil) liest RufnummerRegel jederzeit aus den Ziffern.
    [MaxLength(20)]
    public string? LetzteRufnummer { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
