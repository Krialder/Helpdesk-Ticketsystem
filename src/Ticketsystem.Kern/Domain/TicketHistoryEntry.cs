using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Eine Zeile der Historie: welches Feld wann von wem von welchem Wert auf
// welchen. Schreibt ausschließlich der TicketService, sonst wäre die
// Historie nicht glaubwürdig.
public class TicketHistoryEntry
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required, MaxLength(100)]
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    // Der Name, wie er beim Schreiben galt: die Aufzeichnung und der Rückfall,
    // wenn sich das Konto (ChangedById) nicht mehr zuordnen lässt.
    [Required, MaxLength(200)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? ChangedById { get; set; }

    public DateTime ChangedAt { get; set; }
}
