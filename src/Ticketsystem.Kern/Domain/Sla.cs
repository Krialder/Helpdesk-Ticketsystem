namespace Ticketsystem.Kern.Domain;

// Fristen je Priorität in Stunden Kalenderzeit, gebunden an die Sla-Sektion
// der Einstellungsdatei; die Werte hier sind die Vorgaben.
public class SlaOptions
{
    public SlaWindow Critical { get; set; } = new(1, 4);

    public SlaWindow High { get; set; } = new(4, 8);

    public SlaWindow Medium { get; set; } = new(8, 24);

    public SlaWindow Low { get; set; } = new(24, 72);

    // Takt, in dem der SlaMelder gerissene Fristen meldet.
    public int PruefIntervallMinuten { get; set; } = 5;

    public SlaWindow For(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => Critical,
        TicketPriority.High => High,
        TicketPriority.Medium => Medium,
        TicketPriority.Low => Low,
        _ => Medium
    };
}

public class SlaWindow
{
    // Für die Konfigurationsbindung, die die Stunden danach setzt.
    public SlaWindow()
    {
    }

    public SlaWindow(double reactionHours, double resolutionHours)
    {
        ReactionHours = reactionHours;
        ResolutionHours = resolutionHours;
    }

    public double ReactionHours { get; set; }

    public double ResolutionHours { get; set; }
}

// Sechs Stufen, damit „verspätet erfüllt“ und „offen und überfällig“ nicht
// in denselben Topf fallen; die Auswertung braucht den Unterschied.
public enum SlaState
{
    Laeuft,
    BaldFaellig,
    Ueberfaellig,
    Erfuellt,
    VerspaetetErfuellt,
    // Geschlossen ohne Erfüllung (Spam, Duplikat): Die Frist gilt nicht mehr.
    Entfaellt
}

// Die drei Zahlen der Statusleiste. Fristenuebersicht darunter ist der
// ganze Stand für das Lagebild, auch mit dem, was im Rahmen ist.
public readonly record struct Fristenstand(int Ueberfaellig, int BaldFaellig, int WiedervorlagenFaellig = 0)
{
    public bool Handlungsbedarf => Ueberfaellig > 0 || BaldFaellig > 0 || WiedervorlagenFaellig > 0;
}

public readonly record struct Fristenuebersicht(
    int Offen, int Ueberfaellig, int BaldFaellig, int ImRahmen, int WiedervorlagenFaellig,
    int HeuteFaellig, int NichtZugewiesen, int Alle,
    IReadOnlyList<Prioritaetsstand> JePrioritaet, IReadOnlyList<Altersstufe> Alter,
    IReadOnlyList<Statusstand> JeStatus);

public readonly record struct Statusstand(TicketStatus Status, int Zahl);

public readonly record struct Prioritaetsstand(TicketPriority Prioritaet, int Ueberfaellig, int BaldFaellig, int ImRahmen)
{
    public int Gesamt => Ueberfaellig + BaldFaellig + ImRahmen;
}

public readonly record struct Altersstufe(string Bezeichnung, int Zahl);

public readonly record struct Ruhefenster(DateTime Von, DateTime Bis);

// Reine Funktionen über dem Ticket, ohne Uhr: Die Jetzt-Zeit kommt herein,
// damit jeder Randfall testbar ist.
public static class SlaEvaluation
{
    public static SlaState ReactionState(Ticket ticket, DateTime now) =>
        State(ticket.FirstReactionAt, ticket.ReactionDueAt, ticket.CreatedAt, ticket.Status, now);

    public static SlaState ResolutionState(Ticket ticket, DateTime now) =>
        State(ticket.ResolvedAt, ticket.ResolutionDueAt, ticket.CreatedAt, ticket.Status, now);

    // Der verstrichene Anteil einer Frist, gedeckelt auf 1: Die Überschreitung
    // zeigt der Countdown, nicht der Balken. Eine Frist ohne Länge gilt als voll,
    // statt durch null zu teilen.
    public static double Anteil(DateTime? erfuelltAm, DateTime due, DateTime createdAt, DateTime now)
    {
        var gesamt = (due - createdAt).TotalSeconds;
        if (gesamt <= 0)
        {
            return 1.0;
        }

        var verstrichen = ((erfuelltAm ?? now) - createdAt).TotalSeconds;
        return Math.Clamp(verstrichen / gesamt, 0.0, 1.0);
    }

    public static double ReaktionsAnteil(Ticket ticket, DateTime now) =>
        Anteil(ticket.FirstReactionAt, ticket.ReactionDueAt, ticket.CreatedAt, now);

    public static double LoesungsAnteil(Ticket ticket, DateTime now) =>
        Anteil(ticket.ResolvedAt, ticket.ResolutionDueAt, ticket.CreatedAt, now);

    // Der dringlichere der beiden Zustände (Reaktion, Lösung), damit in der
    // Liste eine Spalte reicht.
    public static SlaState Schlechtester(Ticket ticket, DateTime now)
    {
        // Von dringend nach harmlos.
        var reihenfolge = new[]
        {
            SlaState.Ueberfaellig, SlaState.VerspaetetErfuellt, SlaState.BaldFaellig,
            SlaState.Laeuft, SlaState.Erfuellt, SlaState.Entfaellt
        };
        var a = ReactionState(ticket, now);
        var b = ResolutionState(ticket, now);
        return Array.IndexOf(reihenfolge, a) <= Array.IndexOf(reihenfolge, b) ? a : b;
    }

    private static SlaState State(DateTime? erfuelltAm, DateTime due, DateTime createdAt, TicketStatus status, DateTime now)
    {
        if (erfuelltAm is not null)
        {
            return erfuelltAm <= due ? SlaState.Erfuellt : SlaState.VerspaetetErfuellt;
        }

        // Geschlossen ohne Erfüllung: Die Frist entfällt, statt für immer als
        // gerissen zu stehen.
        if (status == TicketStatus.Closed)
        {
            return SlaState.Entfaellt;
        }

        if (now > due)
        {
            return SlaState.Ueberfaellig;
        }

        // Bald fällig heißt weniger als ein Viertel der Frist übrig: relativ, damit
        // die Warnung bei kurzen wie langen Fristen zur passenden Zeit kommt.
        return (due - now) < (due - createdAt) / 4 ? SlaState.BaldFaellig : SlaState.Laeuft;
    }
}

// Während einer Ruhezeit steht die Frist still und verschiebt sich genau um
// die Überschneidung. Rein und ohne Uhr, damit jeder Randfall testbar ist.
public static class SlaRechner
{
    public static DateTime Faelligkeit(DateTime start, double stunden, IEnumerable<Ruhefenster> ruhezeiten)
    {
        var faellig = start.AddHours(stunden);

        // Jedes Fenster, das hineinragt, schiebt die Fälligkeit nach hinten und kann
        // damit weitere Fenster in den Zeitraum ziehen; darum sortiert und Schritt
        // für Schritt.
        foreach (var fenster in Verschmolzen(ruhezeiten))
        {
            if (fenster.Bis <= start || fenster.Von >= faellig)
            {
                continue;
            }

            var beginn = fenster.Von > start ? fenster.Von : start;
            faellig = faellig.Add(fenster.Bis - beginn);
        }

        return faellig;
    }

    // Überlappende und angrenzende Fenster werden eins: Zwei deckungsgleiche
    // Einträge (zwei Personen tragen dieselbe Betriebsruhe ein) verschöben die
    // Frist sonst um das Doppelte.
    private static List<Ruhefenster> Verschmolzen(IEnumerable<Ruhefenster> ruhezeiten)
    {
        var zusammen = new List<Ruhefenster>();

        foreach (var fenster in ruhezeiten.Where(r => r.Bis > r.Von).OrderBy(r => r.Von))
        {
            if (zusammen.Count > 0 && fenster.Von <= zusammen[^1].Bis)
            {
                var offen = zusammen[^1];
                zusammen[^1] = offen with { Bis = fenster.Bis > offen.Bis ? fenster.Bis : offen.Bis };
                continue;
            }

            zusammen.Add(fenster);
        }

        return zusammen;
    }
}
