using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

// Eine Zeile der Vorgangsliste, fertig formatiert: Die Klassen (PrioKlasse,
// SlaKlasse) übersetzt die Oberfläche über die Palette in Farben.
public sealed record TicketZeile(
    int Id,
    string Titel,
    string Prioritaet,
    string PrioKlasse,
    string Status,
    string Sla,
    string SlaZeichen,
    string SlaKlasse,
    double SlaAnteil,
    string Adresse,
    string Kunde,
    string Bearbeiter,
    string? BearbeiterKontoId,
    string Quelle,
    string Erstellt,
    string Geaendert)
{
    public bool HatBearbeiter => BearbeiterKontoId is not null;
}

public sealed record TicketlistenErgebnis(
    IReadOnlyList<TicketZeile> Zeilen,
    string? Kuerzungshinweis,
    string Leertext,
    string Trefferzaehler);

// Übersetzt die Ansichtsnamen der Oberfläche in Dienstparameter und die
// Tickets in Zeilen. Die Ansichtsnamen sind zugleich die Texte der Auswahl;
// die Statusansichten heißen wie der Status.
public sealed class TicketlistenPresenter(TicketService tickets, Namensverzeichnis namen)
{
    public static IReadOnlyList<string> AnsichtenFuer(Akteur akteur)
    {
        var ansichten = new List<string> { "Offen", "Alle", "Überfällig", "Unzugewiesen", "Wiedervorlage fällig" };
        if (akteur.IstMindestens(RoleLevel.Teamleitung))
        {
            ansichten.Add("Pausierte Zuweisungen");
        }

        ansichten.AddRange(Enum.GetValues<TicketStatus>().Select(s => s.Anzeige()));
        return ansichten;
    }

    public async Task<TicketlistenErgebnis> LadenAsync(
        Akteur akteur, string ansicht, TicketPriority? prioritaet, bool nurMeine, string? suche, DateTime jetzt,
        Sortierung sortierung = Sortierung.Nummer, bool absteigend = false,
        string? kunde = null, string? adresse = null, string? bearbeiterId = null)
    {
        var status = Enum.GetValues<TicketStatus>().Cast<TicketStatus?>()
            .FirstOrDefault(s => s!.Value.Anzeige() == ansicht);

        var liste = await tickets.ListAsync(
            akteur,
            status,
            prioritaet,
            nurOffene: ansicht == "Offen",
            nurUeberfaellige: ansicht == "Überfällig",
            nurMeine: nurMeine,
            nurUnzugewiesene: ansicht == "Unzugewiesen",
            nurPausierteZuweisungen: ansicht == "Pausierte Zuweisungen",
            suche: suche,
            nurWiedervorlagen: ansicht == "Wiedervorlage fällig",
            sortierung: sortierung,
            absteigend: absteigend,
            kunde: kunde,
            adresse: adresse,
            bearbeiterId: bearbeiterId);

        var buch = await namen.LadenAsync();

        var zeilen = liste.Zeilen.Select(t =>
        {
            // Die Frist der Zeile ist die dringlichere der beiden; Countdown und Meter
            // folgen derselben Frist, sonst passten sie nicht zusammen.
            var sla = SlaEvaluation.Schlechtester(t, jetzt);
            var reaktionMassgeblich = sla == SlaEvaluation.ReactionState(t, jetzt);
            var faellig = reaktionMassgeblich ? t.ReactionDueAt : t.ResolutionDueAt;
            var anteil = reaktionMassgeblich
                ? SlaEvaluation.ReaktionsAnteil(t, jetzt)
                : SlaEvaluation.LoesungsAnteil(t, jetzt);
            return new TicketZeile(
                t.Id, t.Title, t.Priority.Anzeige(), t.Priority.PrioKlasse(),
                t.Status.Anzeige(), Fristanzeige.Kurz(sla, faellig, jetzt),
                Fristanzeige.Zeichen(sla), sla.BadgeKlasse(), anteil,
                t.Address ?? "-", t.CustomerName,
                t.AgentName is null ? "-" : buch.Anzeige(t.AgentId, t.AgentName),
                t.AgentName is null ? null : buch.KontoId(t.AgentId, t.AgentName),
                t.Source.Anzeige(), t.CreatedAt.Anzeige(), t.UpdatedAt.Anzeige());
        }).ToList();

        var hinweis = liste.Gekuerzt
            ? $"Es gibt {liste.Gesamt} Vorgänge in dieser Ansicht; gezeigt werden die {zeilen.Count} neuesten. " +
              "Ansicht über Suche, Status oder Priorität weiter eingrenzen."
            : null;

        // Der Leertext unterscheidet „nichts da“ von „nichts passt zum Filter“:
        // Wer einen Filter vergessen hat, hält die Liste sonst für vollständig.
        var gefiltert = prioritaet is not null || nurMeine || !string.IsNullOrWhiteSpace(suche);
        var leertext = gefiltert
            ? "Kein Vorgang passt zu diesen Filtern. Filter oder Suche zurücksetzen, um den Bestand zu sehen."
            : ansicht switch
            {
                "Überfällig" => "Keine Frist ist überfällig.",
                "Offen" => "Keine offenen Vorgänge.",
                "Wiedervorlage fällig" => "Keine Wiedervorlage ist fällig.",
                "Alle" => "Es gibt noch keine Vorgänge. Der erste entsteht über „Neues Ticket“.",
                _ => "Kein Vorgang passt zu dieser Ansicht."
            };

        return new TicketlistenErgebnis(
            zeilen, hinweis, leertext,
            Trefferzaehler(zeilen.Count, await tickets.SichtbarerBestandAsync(akteur)));
    }

    // „12 von 40 Vorgängen“ verrät, dass ein Filter greift; „40 Vorgänge“, dass
    // man alles sieht.
    internal static string Trefferzaehler(int gezeigt, int bestand) => gezeigt == bestand
        ? bestand switch
        {
            0 => "Keine Vorgänge",
            1 => "1 Vorgang",
            _ => $"{bestand} Vorgänge"
        }
        : $"{gezeigt} von {bestand} Vorgängen";
}
