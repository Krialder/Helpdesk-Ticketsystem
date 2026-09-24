using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Ein Vorgang des Helpdesks. Status, Fristen und Historie setzt der
// TicketService, hier stehen nur die Felder. Alle Zeitpunkte sind UTC; die
// Anzeige rechnet in Ortszeit um (Zeitanzeige).
public class Ticket
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public TicketSource Source { get; set; }

    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? CustomerEmail { get; set; }

    // „Eigene Tickets“ in der Rechtematrix heißt: selbst erstellt (CreatedById)
    // oder mir zugewiesen (AgentId).
    [MaxLength(450)]
    public string? CreatedById { get; set; }

    [MaxLength(200)]
    public string? CreatedByName { get; set; }

    // Nur noch bei Altdaten gefüllt; neue Vorgänge haben kein Kundenkonto.
    [MaxLength(450)]
    public string? CustomerId { get; set; }

    [MaxLength(450)]
    public string? AgentId { get; set; }

    [MaxLength(200)]
    public string? AgentName { get; set; }

    // Die drei Anruffelder sind nur bei Quelle Telefon gefüllt.
    [MaxLength(50)]
    public string? CallbackNumber { get; set; }

    public DateTime? CallTime { get; set; }

    public string? CallNote { get; set; }

    // Wiedervorlage. Bewusst kein eigener Status: Die Fälligkeit rechnet die
    // Anzeige beim Hinschauen aus, einen Hintergrunddienst gibt es dafür nicht.
    public DateTime? FollowUpAt { get; set; }

    [MaxLength(200)]
    public string? FollowUpNote { get; set; }

    // Gebäude-Raum wie „A-101“ oder „extern“ (AdressRegel); null nur bei
    // Vorgängen aus E-Mail, weil die Mail keinen Raum mitbringt.
    [MaxLength(10)]
    public string? Address { get; set; }

    // Ohne Fremdschlüssel: Ein später gelöschter alter Vorgang darf den neuen
    // nicht mitnehmen.
    public int? RelatedTicketId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Konkurrenzmerkmal, bei jedem Speichern neu vergeben (TicketsystemContext).
    // Nicht UpdatedAt: Zwei Schreibvorgänge im selben Augenblick hätten dieselbe
    // Zeit, und genau der Doppelklick ist der Fall, um den es geht.
    public Guid RowVersion { get; set; }

    // Aus der Priorität berechnet; Ruhezeiten schieben beide Fristen nach hinten
    // (SlaRechner).
    public DateTime ReactionDueAt { get; set; }

    public DateTime ResolutionDueAt { get; set; }

    // null heißt: noch nicht erfüllt.
    public DateTime? FirstReactionAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    // Merker, damit derselbe Fristriss nicht bei jedem Durchlauf des SlaMelders
    // erneut gemailt wird.
    public DateTime? SlaBreachNotifiedAt { get; set; }

    public List<TicketHistoryEntry> History { get; set; } = [];

    public List<TicketComment> Comments { get; set; } = [];
}
