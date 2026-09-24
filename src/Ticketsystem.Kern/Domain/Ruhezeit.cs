using System.ComponentModel.DataAnnotations;

namespace Ticketsystem.Kern.Domain;

// Ferien oder Betriebsruhe: In diesem Zeitraum stehen die SLA-Fristen still
// (SlaRechner). Zeiten in UTC.
public class Ruhezeit
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Bezeichnung { get; set; } = string.Empty;

    public DateTime Von { get; set; }

    public DateTime Bis { get; set; }

    [Required, MaxLength(200)]
    public string AngelegtVon { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
