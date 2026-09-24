using Microsoft.AspNetCore.Identity;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

// Alles, was das Detailfenster zeigt und darf, in einem Datensatz. Die
// Darf-Flags sind die Rechtematrix, ausgewertet für diesen Akteur und
// diesen Vorgang; das Fenster blendet danach nur ein und aus.
public sealed record DetailAnsicht(
    Ticket Ticket,
    string EingangsTitel,
    string ZeitBeschriftung,
    bool ZeigeRueckruf,
    bool ZeigeEingangsdaten,
    string Eingang,
    IReadOnlyList<VerlaufsEintrag> Verlauf,
    string BearbeiterAnzeige,
    string? BearbeiterKontoId,
    string ErstellerAnzeige,
    string? ErstellerKontoId,
    IReadOnlyList<TicketStatus> NaechsteStatus,
    TicketStatus? HauptUebergang,
    bool ZeigeAbschluss,
    bool DarfZuweisen,
    bool DarfSelbstZuweisen,
    bool DarfZuweisungAufheben,
    bool Geschlossen,
    string? Wiedervorlage,
    string SlaReaktion,
    string SlaReaktionZeichen,
    string SlaReaktionKlasse,
    double SlaReaktionAnteil,
    string SlaLoesung,
    string SlaLoesungZeichen,
    string SlaLoesungKlasse,
    double SlaLoesungAnteil,
    string EntwurfsInhalt);

public sealed record MitarbeiterZeile(string Id, string Name, string Anzeige);

// Ein Eintrag der Zuweisungsauswahl: eine Person, oder „Niemand“ ohne Konto,
// der die Zuweisung aufhebt.
public sealed record Zuweisungsziel(string? KontoId, string Name, string Anzeige)
{
    public bool IstNiemand => KontoId is null;
}

// Ein Stück einer Verlaufszeile: Text, oder ein Name mit Konto dahinter,
// der als Knopf zur Person führt. „leer“ und Namen ohne Konto bleiben Text,
// denn ein Klick darauf wäre ein Versprechen ohne Ziel.
public sealed record VerlaufsTeil(string Text, string? KontoId)
{
    public bool Anklickbar => KontoId is not null;
}

public sealed record VerlaufsEintrag(
    DateTime Zeitpunkt, string Zeit, string Wer, string? WerKontoId,
    IReadOnlyList<VerlaufsTeil> Teile, bool IstKommentar)
{
    public string Text => string.Concat(Teile.Select(t => t.Text));

    public bool Anklickbar => WerKontoId is not null;
}

// Die Fensterregeln des Detailfensters, ohne Oberfläche testbar: Wer darf
// zuweisen, welche Übergänge stehen an, welcher davon ist der Hauptweg.
public sealed class TicketdetailPresenter(
    TicketService tickets, UserManager<AppUser> users, Namensverzeichnis namen)
{
    public async Task<DetailAnsicht?> LadenAsync(int ticketId, Akteur akteur, DateTime jetzt)
    {
        var ticket = await tickets.FindForUserAsync(ticketId, akteur);
        if (ticket is null)
        {
            return null;
        }

        var buch = await namen.LadenAsync();

        var istEmail = ticket.Source == TicketSource.Email;
        var uebergaenge = TicketService.AllowedTransitions(ticket.Status);
        var reaktion = SlaEvaluation.ReactionState(ticket, jetzt);
        var loesung = SlaEvaluation.ResolutionState(ticket, jetzt);

        return new DetailAnsicht(
            ticket,
            istEmail ? "Eingangsdaten" : "Anrufdaten",
            istEmail ? "Eingegangen" : "Anrufzeit",
            ZeigeRueckruf: !istEmail,
            ZeigeEingangsdaten: ticket.Source is TicketSource.Phone or TicketSource.Email
                                || ticket.CallbackNumber is not null,
            $"Angelegt über {ticket.Source.Anzeige()}",
            VerlaufLesen(ticket, buch),
            ticket.AgentName is null ? "nicht zugewiesen" : buch.Anzeige(ticket.AgentId, ticket.AgentName),
            ticket.AgentName is null ? null : buch.KontoId(ticket.AgentId, ticket.AgentName),
            buch.Anzeige(ticket.CreatedById, ticket.CreatedByName),
            buch.KontoId(ticket.CreatedById, ticket.CreatedByName),
            uebergaenge,
            uebergaenge.Where(u => u != TicketStatus.Closed).Cast<TicketStatus?>().FirstOrDefault(),
            ZeigeAbschluss: uebergaenge.Contains(TicketStatus.Closed),
            DarfZuweisen: akteur.IstMindestens(RoleLevel.Teamleitung),
            // Bearbeiter nehmen sich nur unzugewiesene Vorgänge; Teamleitung aufwärts
            // weist über die Auswahl zu, deshalb schließen sich die beiden Flags aus.
            DarfSelbstZuweisen: ticket.AgentId is null && akteur.IstMitarbeiter
                                && !akteur.IstMindestens(RoleLevel.Teamleitung),
            // Ein Bearbeiter gibt zurück, was er sich genommen hat; Teamleitung hebt
            // jede Zuweisung auf.
            DarfZuweisungAufheben: ticket.AgentId is not null && ticket.Status != TicketStatus.Closed
                                   && (akteur.IstMindestens(RoleLevel.Teamleitung) || ticket.AgentId == akteur.Id),
            Geschlossen: ticket.Status == TicketStatus.Closed,
            ticket.FollowUpAt is DateTime wv
                ? $"{wv.Anzeige()}{(ticket.FollowUpNote is string g ? ": " + g : "")}"
                : null,
            Fristanzeige.Text(reaktion, ticket.ReactionDueAt, jetzt),
            Fristanzeige.Zeichen(reaktion), reaktion.BadgeKlasse(),
            SlaEvaluation.ReaktionsAnteil(ticket, jetzt),
            Fristanzeige.Text(loesung, ticket.ResolutionDueAt, jetzt),
            Fristanzeige.Zeichen(loesung), loesung.BadgeKlasse(),
            SlaEvaluation.LoesungsAnteil(ticket, jetzt),
            EntwurfsInhalt(ticket));
    }

    // Historie und Kommentare werden zu einem Faden nach Zeit; „Angelegt“ und
    // „Kommentar“ aus der Historie fallen weg, weil sie als Eintrag selbst
    // schon da sind.
    private static IReadOnlyList<VerlaufsEintrag> VerlaufLesen(Ticket ticket, Namensbuch buch)
    {
        var aenderungen = ticket.History
            .Where(h => h.Field is not ("Kommentar" or "Angelegt"))
            .Select(h => new VerlaufsEintrag(
                h.ChangedAt, h.ChangedAt.Anzeige(),
                buch.Anzeige(h.ChangedById, h.ChangedBy), buch.KontoId(h.ChangedById, h.ChangedBy),
                Teile(h, buch), IstKommentar: false));
        var kommentare = ticket.Comments
            .Select(k => new VerlaufsEintrag(
                k.CreatedAt, k.CreatedAt.Anzeige(),
                buch.Anzeige(k.AuthorId, k.AuthorName), buch.KontoId(k.AuthorId, k.AuthorName),
                [new VerlaufsTeil(k.Text, null)], IstKommentar: true));

        return aenderungen.Concat(kommentare).OrderBy(e => e.Zeitpunkt).ToList();
    }

    // Nur die Bearbeiterzeile trägt Namen, die zur Person führen; alle anderen
    // Felder bleiben ein Text.
    private static IReadOnlyList<VerlaufsTeil> Teile(TicketHistoryEntry h, Namensbuch buch) => h.Field switch
    {
        "Agent" =>
        [
            new VerlaufsTeil("Bearbeiter: ", null),
            Beteiligter(h.OldValue, buch),
            new VerlaufsTeil(" zu ", null),
            Beteiligter(h.NewValue, buch)
        ],
        _ => [new VerlaufsTeil($"{h.Field}: {h.OldValue ?? "leer"} zu {h.NewValue}", null)]
    };

    // In der Historie steht der Name, nicht die Kennung; das Namensbuch findet
    // das Konto über den Namen, wenn es ihn noch gibt.
    private static VerlaufsTeil Beteiligter(string? gespeichert, Namensbuch buch) =>
        string.IsNullOrEmpty(gespeichert)
            ? new VerlaufsTeil("leer", null)
            : new VerlaufsTeil(buch.Anzeige(null, gespeichert), buch.KontoId(null, gespeichert));

    // Der Text, den ein Wissensartikel aus diesem Vorgang bekäme: Beschreibung
    // plus die letzte Lösung, wenn es eine gibt.
    private static string EntwurfsInhalt(Ticket ticket)
    {
        var loesung = ticket.Comments
            .Where(k => k.Text.StartsWith("Lösung:", StringComparison.Ordinal))
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        return loesung is null ? ticket.Description : $"{ticket.Description}\n\n{loesung.Text}";
    }

    // Alle Konten mit einer Mitarbeiterrolle, je Konto einmal, auch wenn es
    // mehrere Rollen hat.
    public const string Niemand = "Niemand (Zuweisung aufheben)";

    // „Niemand“ steht an erster Stelle, aber nur, solange es etwas aufzuheben
    // gibt: Ein Eintrag, der nichts tut, wäre eine Falle in der Auswahl.
    public static IReadOnlyList<Zuweisungsziel> Zuweisungsziele(
        DetailAnsicht ansicht, IReadOnlyList<MitarbeiterZeile> mitarbeiter)
    {
        var ziele = new List<Zuweisungsziel>();
        if (ansicht.DarfZuweisungAufheben)
        {
            ziele.Add(new Zuweisungsziel(null, Niemand, Niemand));
        }

        ziele.AddRange(mitarbeiter.Select(m => new Zuweisungsziel(m.Id, m.Name, m.Anzeige)));
        return ziele;
    }

    public async Task<IReadOnlyList<MitarbeiterZeile>> MitarbeiterAsync()
    {
        var konten = new Dictionary<string, AppUser>();
        foreach (var rolle in Rollen.Alle)
        {
            foreach (var konto in await users.GetUsersInRoleAsync(rolle))
            {
                konten[konto.Id] = konto;
            }
        }

        return konten.Values
            .Select(k => new MitarbeiterZeile(k.Id, Kontenname.Voll(k), Kontenname.Anzeige(k)))
            .OrderBy(z => z.Anzeige, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
