namespace Ticketsystem.Kern.Domain;

public enum TicketStatus
{
    New,
    Assigned,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TicketSource
{
    Web,
    Email,
    Phone
}

// Deutsche Anzeigenamen und Stilklassen an einer Stelle, damit derselbe
// Status überall gleich heißt; in Farben übersetzt die Oberfläche die
// Klassen über die Palette.
public static class TicketDisplay
{
    public static string Anzeige(this TicketStatus status) => status switch
    {
        TicketStatus.New => "Neu",
        TicketStatus.Assigned => "Zugewiesen",
        TicketStatus.InProgress => "In Arbeit",
        TicketStatus.Resolved => "Gelöst",
        TicketStatus.Closed => "Geschlossen",
        _ => status.ToString()
    };

    public static string Anzeige(this TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "Niedrig",
        TicketPriority.Medium => "Mittel",
        TicketPriority.High => "Hoch",
        TicketPriority.Critical => "Kritisch",
        _ => priority.ToString()
    };

    // Nur Hoch und Kritisch tragen eine Klasse: Wer alles markiert, markiert
    // nichts.
    public static string PrioKlasse(this TicketPriority priority) => priority switch
    {
        TicketPriority.High => "prio-hoch",
        TicketPriority.Critical => "prio-kritisch",
        _ => ""
    };

    public static string Anzeige(this TicketSource source) => source switch
    {
        TicketSource.Web => "Web",
        TicketSource.Email => "E-Mail",
        TicketSource.Phone => "Telefon",
        _ => source.ToString()
    };

    public static string Anzeige(this RoleLevel level) => level switch
    {
        RoleLevel.OhneRolle => "Ohne Rolle",
        RoleLevel.Bearbeiter => "Bearbeiter",
        RoleLevel.Teamleitung => "Teamleitung",
        RoleLevel.Administration => "Administration",
        _ => level.ToString()
    };

    public static string Anzeige(this SlaState state) => state switch
    {
        SlaState.Laeuft => "Läuft",
        SlaState.BaldFaellig => "Bald fällig",
        SlaState.Ueberfaellig => "Überfällig",
        SlaState.Erfuellt => "Erfüllt",
        SlaState.VerspaetetErfuellt => "Verspätet erfüllt",
        SlaState.Entfaellt => "Entfällt",
        _ => state.ToString()
    };

    // Läuft und Entfällt sind leise, damit die Warnung ihren Vorsprung behält.
    public static string BadgeKlasse(this SlaState state) => state switch
    {
        SlaState.Ueberfaellig => "badge-frist-ueberfaellig",
        SlaState.VerspaetetErfuellt => "badge-frist-nachtraeglich",
        SlaState.BaldFaellig => "badge-frist-bald",
        SlaState.Laeuft => "badge-frist-neutral",
        SlaState.Erfuellt => "badge-frist-erfuellt",
        SlaState.Entfaellt => "badge-frist-neutral",
        _ => "badge-frist-neutral"
    };
}
