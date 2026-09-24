using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public sealed record Ticketliste(IReadOnlyList<Ticket> Zeilen, int Gesamt, bool Gekuerzt);

// Die Vorgangsverwaltung: Anlegen, Status, Priorität, Zuweisung, Angaben,
// Wiedervorlage, Kommentare, Liste und Fristen. Jede Lesemethode filtert
// über Sichtbar nach der Rechtematrix, jede Änderung schreibt Historie.
// Bearbeiter ändern nur eigene Vorgänge (selbst erstellt oder zugewiesen),
// ein geschlossener Vorgang ist endgültig; Verstöße werfen eine Ausnahme.
public class TicketService(TicketsystemContext db, IOptions<SlaOptions>? slaOptions = null)
{
    private readonly SlaOptions _sla = slaOptions?.Value ?? new SlaOptions();

    // Gelöst ist wiedereröffenbar, Geschlossen endgültig, Direktschluss aus
    // jedem Status.
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Transitions = new()
    {
        [TicketStatus.New] = [TicketStatus.Assigned, TicketStatus.Closed],
        [TicketStatus.Assigned] = [TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.Closed] = []
    };

    public static IReadOnlyList<TicketStatus> AllowedTransitions(TicketStatus from) => Transitions[from];

    // Die Sichtbarkeitsregel der Rechtematrix an genau einer Stelle: Bearbeiter
    // sehen selbst erstellte, sich zugewiesene und unzugewiesene Vorgänge,
    // Teamleitung aufwärts alles. Ohne Rolle gibt es die leere Menge statt einer
    // Ausnahme, weil für die Liste eine leere Liste die ehrliche Antwort ist.
    private IQueryable<Ticket> Sichtbar(Akteur akteur) => akteur.Level switch
    {
        >= RoleLevel.Teamleitung => db.Tickets,
        RoleLevel.Bearbeiter => db.Tickets.Where(t =>
            t.CreatedById == akteur.Id || t.AgentId == akteur.Id || t.AgentId == null),
        _ => db.Tickets.Where(t => false)
    };

    // Sortiert wird in der Abfrage, nicht nach dem Laden: Sonst stünde bei
    // einer gekürzten Liste oben, was zufällig geladen wurde. Die Nummer als
    // letzter Schlüssel hält gleiche Werte beim Auffrischen an ihrem Platz.
    private static IQueryable<Ticket> Sortieren(IQueryable<Ticket> query, Sortierung sortierung, bool absteigend)
    {
        var sortiert = (sortierung, absteigend) switch
        {
            (Sortierung.Frist, false) => query.OrderBy(t => t.ResolutionDueAt),
            (Sortierung.Frist, true) => query.OrderByDescending(t => t.ResolutionDueAt),
            // Kritisch zuerst: Der Enum-Wert steigt von Niedrig nach Kritisch,
            // aufsteigend sortiert stünde also das Unwichtigste oben.
            (Sortierung.Prioritaet, false) => query.OrderByDescending(t => t.Priority),
            (Sortierung.Prioritaet, true) => query.OrderBy(t => t.Priority),
            (Sortierung.Aenderung, false) => query.OrderByDescending(t => t.UpdatedAt),
            (Sortierung.Aenderung, true) => query.OrderBy(t => t.UpdatedAt),
            (_, true) => query.OrderBy(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        return absteigend ? sortiert.ThenBy(t => t.Id) : sortiert.ThenByDescending(t => t.Id);
    }

    // Bezugspunkt des Trefferzählers: Erst der Vergleich verrät, dass eine
    // kurze Liste an einem vergessenen Filter liegt, nicht am leeren Bestand.
    public Task<int> SichtbarerBestandAsync(Akteur akteur) => Sichtbar(akteur).CountAsync();

    public async Task<Ticket> CreateAsync(
        string title,
        string description,
        TicketPriority priority,
        string customerName,
        TicketSource source,
        string? customerEmail = null,
        string? createdById = null,
        string? createdByName = null,
        string? address = null,
        int? relatedTicketId = null)
    {
        var ticket = BuildTicket(title, description, priority, customerName, source,
            customerEmail, createdById, createdByName, address, relatedTicketId,
            await RuhefensterAsync());
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> CreateEmailAsync(
        string title,
        string description,
        TicketPriority priority,
        string customerName,
        string senderEmail,
        DateTime receivedAtUtc,
        string? createdById = null,
        string? createdByName = null,
        string? address = null,
        int? relatedTicketId = null)
    {
        var ticket = BuildTicket(title, description, priority, customerName, TicketSource.Email,
            senderEmail, createdById, createdByName, address, relatedTicketId,
            await RuhefensterAsync());
        // Der Eingang der Mail landet im Feld der Anrufzeit: Es ist derselbe
        // Sachverhalt, nämlich wann sich der Kunde gemeldet hat.
        ticket.CallTime = receivedAtUtc;
        Anlegen(ticket, TicketSource.Email);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> CreatePhoneAsync(
        string title,
        string description,
        TicketPriority priority,
        string customerName,
        string callbackNumber,
        DateTime callTime,
        string? callNote,
        string? createdById = null,
        string? createdByName = null,
        string? address = null,
        int? relatedTicketId = null,
        string? customerEmail = null)
    {
        var ticket = BuildTicket(title, description, priority, customerName, TicketSource.Phone,
            customerEmail, createdById, createdByName, address, relatedTicketId,
            await RuhefensterAsync());
        ticket.CallbackNumber = callbackNumber;
        ticket.CallTime = callTime;
        ticket.CallNote = callNote;
        Anlegen(ticket, TicketSource.Phone);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    private Ticket BuildTicket(
        string title,
        string description,
        TicketPriority priority,
        string customerName,
        TicketSource source,
        string? customerEmail,
        string? createdById,
        string? createdByName,
        string? address,
        int? relatedTicketId,
        IReadOnlyList<Ruhefenster> ruhezeiten)
    {
        // Adressregel und Status Neu sitzen hier, damit kein Eingangsweg sie
        // umgeht. Die Adresse wird normalisiert gespeichert, sonst führt die
        // Raum-Historie „A-101" und „a-101 " als zwei Räume.
        if (address is not null && !AdressRegel.IstGueltig(address))
        {
            throw new InvalidOperationException("Adresse bitte als Gebäude-Raum (etwa A-101) oder als extern angeben.");
        }

        var now = DateTime.UtcNow;
        var window = _sla.For(priority);
        return new Ticket
        {
            Title = title,
            Description = description,
            Priority = priority,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CreatedById = createdById,
            CreatedByName = createdByName ?? customerName,
            Address = address is null ? null : AdressRegel.Normalisieren(address),
            RelatedTicketId = relatedTicketId,
            Source = source,
            Status = TicketStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
            ReactionDueAt = SlaRechner.Faelligkeit(now, window.ReactionHours, ruhezeiten),
            ResolutionDueAt = SlaRechner.Faelligkeit(now, window.ResolutionHours, ruhezeiten)
        };
    }

    public async Task ChangePriorityAsync(int ticketId, TicketPriority newPriority, Akteur akteur)
    {
        var ticket = await LadeFuerBearbeitungAsync(ticketId, akteur);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Ein geschlossenes Ticket ist endgültig; die Priorität wird nicht mehr geändert.");
        }

        if (ticket.Priority == newPriority)
        {
            return;
        }

        AddHistory(ticket, "Priorität", ticket.Priority.Anzeige(), newPriority.Anzeige(), akteur.Name, akteur.Id);
        ticket.Priority = newPriority;

        // Fälligkeiten ab Erstellung neu, nicht ab Umstufung: Die Uhr des Kunden
        // läuft seit dem Eingang.
        var window = _sla.For(newPriority);
        var fenster = await RuhefensterAsync();
        ticket.ReactionDueAt = SlaRechner.Faelligkeit(ticket.CreatedAt, window.ReactionHours, fenster);
        ticket.ResolutionDueAt = SlaRechner.Faelligkeit(ticket.CreatedAt, window.ResolutionHours, fenster);
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // null heißt „nicht anfassen", ein leerer Text heißt „leeren": Ohne die
    // Unterscheidung könnte ein Formular, das ein Feld nicht anbietet, es
    // löschen. Der Kundenname darf nicht geleert werden.
    public async Task<int> AngabenNachtragenAsync(
        int ticketId, string? kundenname, string? adresse, string? rueckrufnummer, Akteur akteur)
    {
        var ticket = await LadeFuerBearbeitungAsync(ticketId, akteur);

        // Auch Angaben bleiben nach dem Schließen stehen. Hingenommen ist, dass
        // ein falscher Raum dann für immer in der Raum-Historie steht.
        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException(
                "Ein geschlossenes Ticket ist endgültig; Angaben werden nicht mehr nachgetragen.");
        }

        var aenderungen = 0;

        if (kundenname is not null)
        {
            var neu = kundenname.Trim();
            if (neu.Length == 0)
            {
                throw new InvalidOperationException("Ohne Namen geht es nicht; das Feld darf nicht geleert werden.");
            }

            if (!string.Equals(ticket.CustomerName, neu, StringComparison.Ordinal) && ticket.CustomerId is not null)
            {
                throw new InvalidOperationException(
                    "Dieses Ticket hängt an einem Kundenkonto; der Name steht dort und wird hier nicht geändert.");
            }

            aenderungen += Uebernehmen(ticket, "Kunde", ticket.CustomerName, neu, akteur.Name,
                wert => ticket.CustomerName = wert!);
        }

        if (adresse is not null)
        {
            var neu = AdressRegel.Normalisieren(adresse);
            if (neu.Length > 0 && !AdressRegel.IstGueltig(neu))
            {
                throw new InvalidOperationException(
                    "Adresse bitte als Gebäude-Raum (etwa A-101) oder als extern angeben.");
            }

            aenderungen += Uebernehmen(ticket, "Adresse", ticket.Address, neu.Length == 0 ? null : neu,
                akteur.Name, wert => ticket.Address = wert, akteur.Id);
        }

        if (rueckrufnummer is not null)
        {
            var roh = rueckrufnummer.Trim();
            if (roh.Length > 0 && !RufnummerRegel.IstGueltig(roh))
            {
                throw new InvalidOperationException(
                    "Rufnummer bitte als Durchwahl (2 bis 6 Ziffern), Festnetz mit Vorwahl (8 bis 12 Ziffern) oder Mobilnummer angeben.");
            }

            var neu = roh.Length == 0 ? null : RufnummerRegel.Normalisieren(roh);
            aenderungen += Uebernehmen(ticket, "Rückrufnummer", ticket.CallbackNumber, neu, akteur.Name,
                wert => ticket.CallbackNumber = wert, akteur.Id);
        }

        if (aenderungen == 0)
        {
            return 0;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return aenderungen;
    }

    private static int Uebernehmen(
        Ticket ticket, string feld, string? alt, string? neu, string akteur, Action<string?> setzen,
        string? akteurId = null)
    {
        if (string.Equals(alt, neu, StringComparison.Ordinal))
        {
            return 0;
        }

        AddHistory(ticket, feld, alt, neu, akteur, akteurId);
        setzen(neu);
        return 1;
    }

    public async Task ChangeStatusAsync(int ticketId, TicketStatus newStatus, Akteur akteur, string? loesung = null)
    {
        var ticket = await LadeFuerBearbeitungAsync(ticketId, akteur);

        if (!Transitions[ticket.Status].Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Statusübergang {ticket.Status.Anzeige()} -> {newStatus.Anzeige()} ist nicht erlaubt.");
        }

        AddHistory(ticket, "Status", ticket.Status.Anzeige(), newStatus.Anzeige(), akteur.Name, akteur.Id);
        ApplyStatusEffects(ticket, ticket.Status, newStatus, DateTime.UtcNow);
        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Erst der Statuswechsel, dann der Lösungstext: Scheitert der Übergang,
        // bleibt kein verwaister Kommentar. Als Kommentar statt eigenem Feld, weil
        // der Kommentarstrom schon in Export, Sicherung und Historie hängt.
        if (newStatus == TicketStatus.Resolved && !string.IsNullOrWhiteSpace(loesung))
        {
            await AddCommentAsync(ticketId, akteur, "Lösung: " + loesung.Trim());
        }
    }

    public async Task AssignAsync(int ticketId, string zielId, string zielName, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Ein geschlossenes Ticket ist endgültig und wird nicht mehr zugewiesen.");
        }

        if (akteur.Level == RoleLevel.Bearbeiter)
        {
            if (zielId != akteur.Id)
            {
                throw new InvalidOperationException("Bearbeiter können Tickets nur sich selbst zuweisen.");
            }

            if (ticket.AgentId is not null && ticket.AgentId != akteur.Id)
            {
                throw new InvalidOperationException("Das Ticket ist bereits zugewiesen; Umverteilen kann nur die Teamleitung.");
            }
        }

        var ziel = await db.Users.SingleOrDefaultAsync(u => u.Id == zielId)
            ?? throw new InvalidOperationException("Zielkonto nicht gefunden.");
        if (ziel.IstPausiert(DateTime.UtcNow))
        {
            throw new InvalidOperationException($"Das Konto {zielName} ist pausiert und kann nicht zugewiesen werden.");
        }

        AddHistory(ticket, "Agent", ticket.AgentName, zielName, akteur.Name, akteur.Id);
        ticket.AgentId = zielId;
        ticket.AgentName = zielName;

        // Die Zuweisung ist der fachliche Anlass des Übergangs Neu nach
        // Zugewiesen; in anderen Status wechselt nur die Person.
        if (ticket.Status == TicketStatus.New)
        {
            AddHistory(ticket, "Status", TicketStatus.New.Anzeige(), TicketStatus.Assigned.Anzeige(), akteur.Name, akteur.Id);
            ApplyStatusEffects(ticket, TicketStatus.New, TicketStatus.Assigned, DateTime.UtcNow);
            ticket.Status = TicketStatus.Assigned;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ZuweisungAufhebenAsync(int ticketId, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Ein geschlossenes Ticket ist endgültig; seine Zuweisung bleibt, wie sie war.");
        }

        if (ticket.AgentId is null)
        {
            throw new InvalidOperationException("Das Ticket ist niemandem zugewiesen; es gibt nichts aufzuheben.");
        }

        if (akteur.Level == RoleLevel.Bearbeiter && ticket.AgentId != akteur.Id)
        {
            throw new InvalidOperationException("Bearbeiter geben nur ihre eigenen Tickets zurück; Umverteilen kann nur die Teamleitung.");
        }

        AddHistory(ticket, "Agent", ticket.AgentName, null, akteur.Name, akteur.Id);
        ticket.AgentId = null;
        ticket.AgentName = null;

        // Nur Zugewiesen geht auf Neu zurück, denn nur dieser Übergang kam durch
        // die Zuweisung; ein Vorgang in Arbeit bleibt in Arbeit, nur ohne Person.
        // FirstReactionAt bleibt stehen: Die Reaktion hat stattgefunden.
        if (ticket.Status == TicketStatus.Assigned)
        {
            AddHistory(ticket, "Status", TicketStatus.Assigned.Anzeige(), TicketStatus.New.Anzeige(), akteur.Name, akteur.Id);
            ticket.Status = TicketStatus.New;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Kommentieren darf, wer den Vorgang sehen darf; deshalb Sichtbar statt
    // LadeFuerBearbeitung.
    public async Task AddCommentAsync(int ticketId, Akteur akteur, string text)
    {
        var ticket = await Sichtbar(akteur).SingleOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new InvalidOperationException("Dieses Ticket ist für dieses Konto nicht sichtbar.");

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Ein geschlossenes Ticket ist endgültig; bitte ein neues Ticket mit Verweis anlegen.");
        }

        db.TicketComments.Add(new TicketComment
        {
            TicketId = ticketId,
            AuthorId = akteur.Id,
            AuthorName = akteur.Name,
            Text = text,
            CreatedAt = DateTime.UtcNow
        });
        AddHistory(ticket, "Kommentar", null, Auszug(text), akteur.Name, akteur.Id);
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // null für „gibt es nicht" wie für „nicht sichtbar", damit sich aus der
    // Antwort nicht ableiten lässt, welche Nummern existieren.
    public Task<Ticket?> FindForUserAsync(int ticketId, Akteur akteur) =>
        Sichtbar(akteur)
            .Include(t => t.History)
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == ticketId);

    // Bei rund zwanzig sichtbaren Zeilen sind das zehn Bildschirmlängen; wer
    // weiter unten sucht, sucht falsch, dafür sind die Filter da.
    public const int Obergrenze = 200;

    public async Task<Ticketliste> ListAsync(
        Akteur akteur,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        bool nurOffene = false,
        bool nurUeberfaellige = false,
        bool nurMeine = false,
        bool nurUnzugewiesene = false,
        bool nurPausierteZuweisungen = false,
        string? suche = null,
        bool nurWiedervorlagen = false,
        Sortierung sortierung = Sortierung.Nummer,
        bool absteigend = false,
        string? kunde = null,
        string? adresse = null,
        string? bearbeiterId = null)
    {
        var query = Sichtbar(akteur);

        if (nurMeine)
        {
            query = query.Where(t => t.AgentId == akteur.Id);
        }

        if (nurUnzugewiesene)
        {
            query = query.Where(t => t.AgentId == null);
        }

        // Tickets an pausierten Konten, damit nichts still liegen bleibt; nur ab
        // Teamleitung, weil die Ansicht den Blick auf alle voraussetzt.
        if (nurPausierteZuweisungen)
        {
            if (!akteur.IstMindestens(RoleLevel.Teamleitung))
            {
                throw new InvalidOperationException("Die Ansicht pausierter Zuweisungen ist der Teamleitung vorbehalten.");
            }

            var now = DateTime.UtcNow;
            var pausiert = db.Users.Where(u => u.PausedUntil != null && u.PausedUntil > now).Select(u => u.Id);
            query = query.Where(t => t.AgentId != null && pausiert.Contains(t.AgentId));
        }

        if (status is not null)
        {
            query = query.Where(t => t.Status == status);
        }

        // Nur fällige Wiedervorlagen: Mit den künftigen wäre die Ansicht eine
        // Liste aller Wartezustände statt eines Arbeitsvorrats.
        if (nurWiedervorlagen)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => t.Status != TicketStatus.Closed
                && t.FollowUpAt != null && t.FollowUpAt <= now);
        }

        // Offen heißt, es gibt noch etwas zu tun. Gelöst wartet nur auf das
        // Schließen und zählt nicht als offen.
        if (nurOffene)
        {
            query = query.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        }

        if (nurUeberfaellige)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => t.Status != TicketStatus.Closed &&
                ((t.FirstReactionAt == null && t.ReactionDueAt < now)
                 || (t.ResolvedAt == null && t.ResolutionDueAt < now)));
        }

        if (priority is not null)
        {
            query = query.Where(t => t.Priority == priority);
        }

        // Ein LIKE über Nummer, Titel, Beschreibung und Kundenname; ein
        // Volltextindex lohnt bei ein paar tausend Zeilen nicht. ToLower auf
        // beiden Seiten, weil SQLite bei LIKE nur ASCII fallunabhängig vergleicht,
        // Umlaute nicht. „#12" steht so auf jeder Seite, also zählt es als Nummer.
        if (!string.IsNullOrWhiteSpace(suche))
        {
            var begriff = suche.Trim();
            int? nummer = int.TryParse(begriff.TrimStart('#'), out var id) ? id : null;
            var klein = begriff.ToLowerInvariant();
            query = query.Where(t =>
                (nummer != null && t.Id == nummer)
                || t.Title.ToLower().Contains(klein)
                || t.Description.ToLower().Contains(klein)
                || t.CustomerName.ToLower().Contains(klein));
        }

        // Kunde und Adresse sind exakte Filter, keine Suche: Wer die Vorgänge zu
        // „Weber, Sabine" will, will nicht die mit „Weber" in der Beschreibung.
        if (!string.IsNullOrWhiteSpace(kunde))
        {
            var name = kunde.Trim().ToLowerInvariant();
            query = query.Where(t => t.CustomerName.ToLower() == name);
        }

        if (!string.IsNullOrWhiteSpace(adresse))
        {
            var ort = adresse.Trim().ToLowerInvariant();
            query = query.Where(t => t.Address != null && t.Address.ToLower() == ort);
        }

        // Über die Kennung, nicht den Namen: Der Name kann sich ändern.
        if (!string.IsNullOrWhiteSpace(bearbeiterId))
        {
            query = query.Where(t => t.AgentId == bearbeiterId);
        }

        // Eine Zeile mehr holen als angezeigt: Daran ist zu erkennen, ob gekürzt
        // wurde. Die Gesamtzahl kostet dann eine eigene Abfrage, aber „wie viel
        // mehr" ist genau die Frage, die als Nächstes kommt.
        var zeilen = await Sortieren(query, sortierung, absteigend)
            .Take(Obergrenze + 1)
            .ToListAsync();

        if (zeilen.Count <= Obergrenze)
        {
            return new Ticketliste(zeilen, zeilen.Count, false);
        }

        zeilen.RemoveAt(zeilen.Count - 1);
        return new Ticketliste(zeilen, await query.CountAsync(), true);
    }

    // Personen- und Raum-Historie folgen derselben Sichtbarkeitsregel wie die
    // Liste: Eine Sammelansicht darf nichts zeigen, was einzeln verborgen wäre.
    public async Task<List<Ticket>> ListByPersonAsync(string kundenName, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var name = (kundenName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return [];
        }

        return await Sichtbar(akteur)
            .Where(t => t.CustomerName == name)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<List<Ticket>> ListByRoomAsync(string adresse, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var raum = (adresse ?? string.Empty).Trim();
        if (raum.Length == 0)
        {
            return [];
        }

        return await Sichtbar(akteur)
            .Where(t => t.Address == raum)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    // Zwischenspeicher je Anfrage: Der Dienst ist scoped, das Feld lebt so
    // lange wie er. Kehrseite: Wer in derselben Anfrage einen Vorgang anlegt,
    // sieht ihn im zweiten Fristenstand nicht. Der Schlüssel ist der Akteur,
    // sonst bekäme ein zweiter Akteur in derselben Anfrage die Zahl des ersten.
    private (string Akteur, Fristenstand Stand)? _fristenstand;

    public async Task<Fristenstand> FristenstandAsync(Akteur akteur, DateTime jetzt)
    {
        if (!akteur.IstMitarbeiter)
        {
            return new Fristenstand(0, 0);
        }

        if (_fristenstand is { } gemerkt && gemerkt.Akteur == akteur.Id)
        {
            return gemerkt.Stand;
        }

        var offene = await Sichtbar(akteur)
            .Where(t => t.Status != TicketStatus.Closed && t.ResolvedAt == null)
            .ToListAsync();

        // Im Speicher statt in SQL: „Bald fällig" heißt weniger als ein Viertel
        // der Frist übrig, und das in SQL auszudrücken hieße, die Regel ein
        // zweites Mal zu schreiben. Die Wiedervorlagen zählen über derselben
        // offenen Menge: Die eines gelösten Vorgangs hat ihren Zweck erfüllt.
        var zustaende = offene.Select(t => SlaEvaluation.Schlechtester(t, jetzt)).ToList();
        var stand = new Fristenstand(
            zustaende.Count(z => z == SlaState.Ueberfaellig),
            zustaende.Count(z => z == SlaState.BaldFaellig),
            offene.Count(t => t.FollowUpAt != null && t.FollowUpAt <= jetzt));

        _fristenstand = (akteur.Id, stand);
        return stand;
    }

    public async Task<Fristenuebersicht> FristenuebersichtAsync(Akteur akteur, DateTime jetzt, TimeZoneInfo? zone = null)
    {
        if (!akteur.IstMitarbeiter)
        {
            return new Fristenuebersicht(0, 0, 0, 0, 0, 0, 0, 0, [], [], []);
        }

        var unerledigt = await Sichtbar(akteur)
            .Where(t => t.Status != TicketStatus.Closed)
            .ToListAsync();
        var alle = await Sichtbar(akteur).CountAsync();
        var offene = unerledigt.Where(t => t.ResolvedAt == null).ToList();
        var bewertet = offene.Select(t => (Ticket: t, Zustand: SlaEvaluation.Schlechtester(t, jetzt))).ToList();
        var ueberfaellig = bewertet.Count(b => b.Zustand == SlaState.Ueberfaellig);
        var bald = bewertet.Count(b => b.Zustand == SlaState.BaldFaellig);

        // „Heute fällig" nach der Wanduhr des Helpdesks, nicht nach UTC: Um 23:30
        // Ortszeit ist eine Frist um 00:30 UTC des nächsten Tags noch heute.
        // Überfällige zählen nicht mit, ihre Frist ist vorbei.
        var heute = Zeitanzeige.AlsOrtszeit(jetzt, zone).Date;
        var heuteFaellig = offene.Count(t =>
        {
            var frist = t.FirstReactionAt is null ? t.ReactionDueAt : t.ResolutionDueAt;
            return frist > jetzt && Zeitanzeige.AlsOrtszeit(frist, zone).Date == heute;
        });

        var jePrioritaet = Enum.GetValues<TicketPriority>().OrderByDescending(p => p)
            .Select(p => new Prioritaetsstand(p,
                bewertet.Count(b => b.Ticket.Priority == p && b.Zustand == SlaState.Ueberfaellig),
                bewertet.Count(b => b.Ticket.Priority == p && b.Zustand == SlaState.BaldFaellig),
                bewertet.Count(b => b.Ticket.Priority == p && b.Zustand is not (SlaState.Ueberfaellig or SlaState.BaldFaellig))))
            .ToList();

        var alter = offene.Select(t => (jetzt - t.CreatedAt).TotalDays).ToList();
        IReadOnlyList<Altersstufe> stufen =
        [
            new("bis 1 Tag", alter.Count(a => a <= 1)),
            new("2 bis 7 Tage", alter.Count(a => a > 1 && a <= 7)),
            new("8 bis 30 Tage", alter.Count(a => a > 7 && a <= 30)),
            new("über 30 Tage", alter.Count(a => a > 30))
        ];

        var jeStatus = Enum.GetValues<TicketStatus>()
            .Where(s => s != TicketStatus.Closed)
            .Select(s => new Statusstand(s, unerledigt.Count(t => t.Status == s)))
            .ToList();

        return new Fristenuebersicht(
            offene.Count, ueberfaellig, bald, offene.Count - ueberfaellig - bald,
            offene.Count(t => t.FollowUpAt != null && t.FollowUpAt <= jetzt),
            heuteFaellig, offene.Count(t => t.AgentId == null), alle,
            jePrioritaet, stufen, jeStatus);
    }

    // Prüfung für Verweisziele: Ein Verweis auf ein fremdes Ticket wäre sonst
    // eine stille Bestätigung, dass es dieses Ticket gibt.
    public Task<bool> IstSichtbarAsync(int ticketId, Akteur akteur) =>
        Sichtbar(akteur).AnyAsync(t => t.Id == ticketId);

    private async Task<Ticket> LadeFuerBearbeitungAsync(int ticketId, Akteur akteur)
    {
        RequireMitarbeiter(akteur);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);

        if (akteur.Level == RoleLevel.Bearbeiter
            && ticket.CreatedById != akteur.Id
            && ticket.AgentId != akteur.Id)
        {
            throw new InvalidOperationException("Bearbeiter ändern nur eigene Tickets (selbst erstellt oder zugewiesen).");
        }

        return ticket;
    }

    public async Task WiedervorlageSetzenAsync(int ticketId, DateTime? datum, string? grund, Akteur akteur)
    {
        var ticket = await LadeFuerBearbeitungAsync(ticketId, akteur);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException(
                "Ein geschlossenes Ticket ist endgültig; eine Wiedervorlage wäre ein Termin für einen Vorgang, den niemand mehr öffnet.");
        }

        var vorher = ticket.FollowUpAt is DateTime alt
            ? $"{alt.Anzeige()}: {ticket.FollowUpNote}"
            : null;

        // Entfernen muss möglich sein: Eine Wiedervorlage, die nach dem Nachfassen
        // stehen bliebe, würde zum Daueralarm, den niemand mehr liest.
        if (datum is null)
        {
            ticket.FollowUpAt = null;
            ticket.FollowUpNote = null;
            AddHistory(ticket, "Wiedervorlage", vorher, "entfernt", akteur.Name, akteur.Id);
        }
        else
        {
            ticket.FollowUpAt = datum;
            ticket.FollowUpNote = string.IsNullOrWhiteSpace(grund) ? null : grund.Trim();
            AddHistory(ticket, "Wiedervorlage", vorher,
                $"{datum.Value.Anzeige()}: {ticket.FollowUpNote ?? "ohne Grund"}", akteur.Name, akteur.Id);
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Fenster, die vor über einem Jahr endeten, bleiben draußen: Sie
    // verschieben keine Frist mehr, die ab einem jüngeren Eingang gerechnet wird.
    private async Task<IReadOnlyList<Ruhefenster>> RuhefensterAsync()
    {
        var relevant = await db.Ruhezeiten
            .Where(r => r.Bis > DateTime.UtcNow.AddYears(-1))
            .Select(r => new { r.Von, r.Bis })
            .ToListAsync();

        return relevant.Select(r => new Ruhefenster(r.Von, r.Bis)).ToList();
    }

    private static string Auszug(string text)
    {
        var eine_zeile = text.ReplaceLineEndings(" ").Trim();
        return eine_zeile.Length <= 80 ? eine_zeile : eine_zeile[..77] + "...";
    }

    private static void RequireMitarbeiter(Akteur akteur)
    {
        if (!akteur.IstMitarbeiter)
        {
            throw new InvalidOperationException("Diese Aktion ist Mitarbeitern vorbehalten.");
        }
    }

    // SLA-Wirkung der Übergänge: Die Reaktion ist, dass das Ticket Neu
    // verlässt, einmalig. Die Lösung ist Gelöst erreicht; eine Wiedereröffnung
    // löscht den Zeitpunkt wieder, die Frist bleibt stehen.
    private static void ApplyStatusEffects(Ticket ticket, TicketStatus from, TicketStatus to, DateTime now)
    {
        if (from == TicketStatus.New && ticket.FirstReactionAt is null)
        {
            ticket.FirstReactionAt = now;
        }

        if (to == TicketStatus.Resolved)
        {
            ticket.ResolvedAt = now;
        }

        if (from == TicketStatus.Resolved && to == TicketStatus.InProgress)
        {
            ticket.ResolvedAt = null;
        }
    }

    // Das Anlegen gehört in die Akte: Der erste Schritt wäre die schlechteste
    // Stelle für eine Lücke. Der Eingangsweg steht dabei, weil er die erste
    // Frage bei jeder Rückfrage ist.
    private static void Anlegen(Ticket ticket, TicketSource quelle) =>
        AddHistory(ticket, "Angelegt", null, quelle.Anzeige(),
            ticket.CreatedByName ?? ticket.CustomerName, ticket.CreatedById);

    // Name und Kennung: Der Name ist die Aufzeichnung, die Kennung erlaubt der
    // Anzeige den heutigen Anzeigenamen (Namensbuch).
    private static void AddHistory(
        Ticket ticket, string field, string? oldValue, string? newValue, string actor, string? actorId = null)
    {
        ticket.History.Add(new TicketHistoryEntry
        {
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = actor,
            ChangedById = actorId,
            ChangedAt = DateTime.UtcNow
        });
    }
}
