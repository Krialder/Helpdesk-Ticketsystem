using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Ticketsystem.Kern.Domain;

// Der Identity-Benutzer plus Pause und die beiden Namen (siehe Kontenname).
// Ein pausiertes Konto bekommt keine Zuweisungen und keine Fristmails.
public class AppUser : IdentityUser
{
    public DateTime? PausedUntil { get; set; }

    [MaxLength(100)]
    public string? Nachname { get; set; }

    [MaxLength(100)]
    public string? Vorname { get; set; }

    // Eindeutig über alle Konten, damit in der Zuweisen-Auswahl nicht zweimal
    // dasselbe Wort steht.
    [MaxLength(100)]
    public string? Anzeigename { get; set; }

    public bool IstPausiert(DateTime now) => PausedUntil is not null && PausedUntil > now;
}
