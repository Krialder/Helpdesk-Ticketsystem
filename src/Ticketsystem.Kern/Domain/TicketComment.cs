using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Kommentar zwischen Mitarbeitern; schreibt nur der TicketService, und
// kommentieren darf, wer das Ticket sehen darf.
public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required, MaxLength(450)]
    public string AuthorId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string AuthorName { get; set; } = string.Empty;

    [Required]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
