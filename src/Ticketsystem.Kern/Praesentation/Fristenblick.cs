using System.Globalization;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Praesentation;

public sealed record Kachel(string Bezeichnung, int Zahl, string Klasse, string Ansicht, Sortierung? Sortierung = null);

public sealed record Ringsegment(
    string Bezeichnung, TicketPriority Prioritaet, int Zahl, double Anteil, string Prozent,
    double StartWinkel, double Winkel, string Klasse);

public sealed record Statusbalken(string Bezeichnung, TicketStatus Status, int Zahl, double Anteil, string Ansicht);

public sealed record Altersbalken(string Bezeichnung, int Zahl, double Anteil);

public static class Fristenblick
{
    public static bool ZeigenFuer(Akteur akteur) => akteur.IstMindestens(RoleLevel.Teamleitung);

    public static IReadOnlyList<Kachel> Kacheln(Fristenuebersicht u) =>
    [
        new("Überfällig", u.Ueberfaellig, u.Ueberfaellig > 0 ? SlaState.Ueberfaellig.BadgeKlasse() : SlaState.Laeuft.BadgeKlasse(), "Überfällig"),
        new("Heute fällig", u.HeuteFaellig, u.HeuteFaellig > 0 ? SlaState.BaldFaellig.BadgeKlasse() : SlaState.Laeuft.BadgeKlasse(), "Offen", Sortierung.Frist),
        new("Offen", u.Offen, SlaState.Laeuft.BadgeKlasse(), "Offen"),
        new("Wiedervorlagen fällig", u.WiedervorlagenFaellig, u.WiedervorlagenFaellig > 0 ? SlaState.BaldFaellig.BadgeKlasse() : SlaState.Laeuft.BadgeKlasse(), "Wiedervorlage fällig"),
        new("Nicht zugewiesen", u.NichtZugewiesen, SlaState.Laeuft.BadgeKlasse(), "Unzugewiesen"),
        new("Alle", u.Alle, SlaState.Laeuft.BadgeKlasse(), "Alle")
    ];

    public static string Zusammenfassung(Fristenuebersicht u) =>
        u.Offen == 1 ? "1 offener Vorgang" : $"{u.Offen} offene Vorgänge";

    public static IReadOnlyList<Ringsegment> Ring(Fristenuebersicht u)
    {
        var gesamt = u.JePrioritaet.Sum(p => p.Gesamt);
        var start = -90.0;
        var stuecke = new List<Ringsegment>();
        foreach (var p in u.JePrioritaet.Where(p => p.Gesamt > 0))
        {
            var anteil = Anteil(p.Gesamt, gesamt);
            var winkel = anteil * 360;
            stuecke.Add(new Ringsegment(
                p.Prioritaet.Anzeige(), p.Prioritaet, p.Gesamt, anteil, Prozent(anteil),
                start, winkel, p.Prioritaet.PrioKlasse()));
            start += winkel;
        }

        return stuecke;
    }

    public static IReadOnlyList<Statusbalken> Status(Fristenuebersicht u)
    {
        var massstab = u.JeStatus.Count == 0 ? 0 : u.JeStatus.Max(s => s.Zahl);
        return u.JeStatus.Select(s => new Statusbalken(
            s.Status.Anzeige(), s.Status, s.Zahl, Anteil(s.Zahl, massstab), s.Status.Anzeige())).ToList();
    }

    public static IReadOnlyList<Altersbalken> Altersstufen(Fristenuebersicht u)
    {
        var massstab = u.Alter.Count == 0 ? 0 : u.Alter.Max(a => a.Zahl);
        return u.Alter.Select(a => new Altersbalken(a.Bezeichnung, a.Zahl, Anteil(a.Zahl, massstab))).ToList();
    }

    public static string Prozent(double anteil) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(anteil * 100)} %");

    private static double Anteil(int zahl, int bezug) => bezug == 0 ? 0 : (double)zahl / bezug;
}
