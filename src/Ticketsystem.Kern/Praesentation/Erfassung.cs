using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

// Die Presenter in diesem Ordner bereiten Daten für die Fenster auf und
// entscheiden, was die Fenster nur anzeigen (Humble View): Die Fenster binden,
// hier fallen die Entscheidungen, und hier sind sie ohne Oberfläche testbar.
//
// Alles, was das Erfassungsfenster eingibt, als ein Datensatz; ZeitpunktUtc
// ist die Anrufzeit oder der Mail-Eingang, null heißt „jetzt“.
public sealed record ErfassungsEingabe(
    TicketSource Quelle,
    string? KundenName,
    string? Titel,
    string? Beschreibung,
    string? Adresse,
    TicketPriority Prioritaet,
    string? Rueckrufnummer,
    string? KundenEmail,
    DateTime? ZeitpunktUtc,
    string? Notiz,
    int? VerweisId,
    bool MirZuweisen);

public enum Erfassungsfeld
{
    Quelle,
    Name,
    Absender,
    Nummer,
    Titel,
    Beschreibung,
    Adresse,
    Verweis
}

public sealed record Erfassungsfehler(Erfassungsfeld Feld, string Text);

public sealed record ErfassungsErgebnis(int? TicketId, string? Meldung, IReadOnlyList<Erfassungsfehler> Fehler)
{
    public bool Gelungen => TicketId is not null;
}

public sealed record Kontaktvorschlag(string Name, string? Adresse, string? Rufnummer);

public static class Erfassung
{
    // Ein Vorschlag wird nur übernommen, wenn er eindeutig ist: exakt derselbe
    // Name, oder ein einziger Treffer. Bei „Web“ und „Weber“ füllt sonst der
    // Falsche das Formular.
    public static Kontaktvorschlag? VorschlagWaehlen(
        IReadOnlyList<StammdatenService.KontaktTreffer> treffer, string? eingabe)
    {
        var name = eingabe?.Trim() ?? "";
        var gewaehlt = treffer.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase))
                       ?? (treffer.Count == 1 ? treffer[0] : null);

        return gewaehlt is null
            ? null
            : new Kontaktvorschlag(gewaehlt.Name, gewaehlt.LetzteAdresse, gewaehlt.Rufnummer);
    }

    // Ein leerer Verweis ist kein Fehler, ein nicht numerischer schon; die
    // Raute vorn ist erlaubt, weil man Vorgänge so ausspricht.
    public static (int? Id, string? Fehler) VerweisParsen(string? text)
    {
        var bereinigt = text?.Trim().TrimStart('#');
        if (string.IsNullOrEmpty(bereinigt))
        {
            return (null, null);
        }

        return int.TryParse(bereinigt, out var id)
            ? (id, null)
            : (null, "Der Verweis muss eine Ticketnummer sein, etwa 123 oder #123.");
    }
}

// Prüfen und Anlegen in einem Ablauf, damit jeder Erfassungsweg dieselben
// Regeln durchläuft. Die Meldungen sind für den Nutzer geschrieben; das
// Fenster stellt sie nur an das genannte Feld.
public sealed class ErfassungsAblauf(
    TicketService tickets, StammdatenService stammdaten, BestaetigungsMelder bestaetigung)
{
    public async Task<IReadOnlyList<Erfassungsfehler>> PruefenAsync(ErfassungsEingabe eingabe, Akteur akteur)
    {
        var fehler = new List<Erfassungsfehler>();
        void Melden(Erfassungsfeld feld, string text) => fehler.Add(new Erfassungsfehler(feld, text));

        if (eingabe.Quelle is not (TicketSource.Phone or TicketSource.Email))
        {
            Melden(Erfassungsfeld.Quelle, "Web ist kein Eingangsweg mehr. Bitte Telefon oder E-Mail wählen.");
        }

        if (string.IsNullOrWhiteSpace(eingabe.Titel))
        {
            Melden(Erfassungsfeld.Titel, "Bitte einen Titel angeben.");
        }
        else if (eingabe.Titel.Trim().Length > 200)
        {
            Melden(Erfassungsfeld.Titel, "Der Titel darf höchstens 200 Zeichen lang sein.");
        }

        if (string.IsNullOrWhiteSpace(eingabe.Beschreibung))
        {
            Melden(Erfassungsfeld.Beschreibung, "Bitte das Problem beschreiben.");
        }

        if (string.IsNullOrWhiteSpace(eingabe.Adresse))
        {
            Melden(Erfassungsfeld.Adresse, "Bitte die Adresse angeben (etwa A-101 oder extern).");
        }
        else if (!AdressRegel.IstGueltig(eingabe.Adresse))
        {
            Melden(Erfassungsfeld.Adresse, "Adresse bitte als Gebäude-Raum (etwa A-101) oder als extern angeben.");
        }

        if (eingabe.Quelle == TicketSource.Email)
        {
            if (string.IsNullOrWhiteSpace(eingabe.KundenName))
            {
                Melden(Erfassungsfeld.Name, "Bitte den Absender der E-Mail angeben.");
            }

            if (string.IsNullOrWhiteSpace(eingabe.KundenEmail))
            {
                Melden(Erfassungsfeld.Absender, "Bitte die Absenderadresse der E-Mail angeben.");
            }
        }

        if (eingabe.Quelle == TicketSource.Phone)
        {
            if (string.IsNullOrWhiteSpace(eingabe.KundenName))
            {
                Melden(Erfassungsfeld.Name, "Bei Telefon-Tickets bitte den Namen des Anrufers angeben.");
            }

            if (string.IsNullOrWhiteSpace(eingabe.Rueckrufnummer))
            {
                Melden(Erfassungsfeld.Nummer, "Bei Telefon-Tickets bitte eine Rückrufnummer angeben.");
            }
            else if (!RufnummerRegel.IstGueltig(eingabe.Rueckrufnummer))
            {
                Melden(Erfassungsfeld.Nummer, "Rufnummer bitte als Durchwahl (2 bis 6 Ziffern), Festnetz mit Vorwahl (8 bis 12 Ziffern) oder Mobilnummer angeben.");
            }
        }

        // Ein Verweis auf ein fremdes Ticket darf nicht verraten, dass es das
        // Ticket gibt; „gibt es nicht“ und „nicht sichtbar“ sind dieselbe Meldung.
        if (eingabe.VerweisId is int bezug && !await tickets.IstSichtbarAsync(bezug, akteur))
        {
            Melden(Erfassungsfeld.Verweis, $"Ticket #{bezug} gibt es nicht oder es ist für dich nicht sichtbar.");
        }

        return fehler;
    }

    public async Task<ErfassungsErgebnis> AnlegenAsync(ErfassungsEingabe eingabe, Akteur akteur)
    {
        var fehler = await PruefenAsync(eingabe, akteur);
        if (fehler.Count > 0)
        {
            return new ErfassungsErgebnis(null, null, fehler);
        }

        var email = string.IsNullOrWhiteSpace(eingabe.KundenEmail) ? null : eingabe.KundenEmail.Trim();
        Ticket ticket;
        if (eingabe.Quelle == TicketSource.Phone)
        {
            ticket = await tickets.CreatePhoneAsync(
                eingabe.Titel!.Trim(), eingabe.Beschreibung!, eingabe.Prioritaet, eingabe.KundenName!.Trim(),
                eingabe.Rueckrufnummer!, eingabe.ZeitpunktUtc ?? DateTime.UtcNow,
                string.IsNullOrWhiteSpace(eingabe.Notiz) ? null : eingabe.Notiz,
                akteur.Id, akteur.Name, eingabe.Adresse, eingabe.VerweisId, email);

            // Bei Telefon wandern Name, Nummer und Raum in die Stammdaten, damit der
            // nächste Anruf derselben Person vorgeschlagen wird.
            await stammdaten.ErfasseAsync(eingabe.KundenName!, eingabe.Rueckrufnummer, eingabe.Adresse, akteur);
        }
        else
        {
            ticket = await tickets.CreateEmailAsync(
                eingabe.Titel!.Trim(), eingabe.Beschreibung!, eingabe.Prioritaet, eingabe.KundenName!.Trim(),
                email!, eingabe.ZeitpunktUtc ?? DateTime.UtcNow,
                akteur.Id, akteur.Name, eingabe.Adresse, eingabe.VerweisId);
        }

        if (eingabe.MirZuweisen)
        {
            await tickets.AssignAsync(ticket.Id, akteur.Id, akteur.Name, akteur);
        }

        var bestaetigt = await bestaetigung.VersendenAsync(ticket);
        var meldung = bestaetigt
            ? $"Ticket #{ticket.Id} wurde angelegt, die Eingangsbestätigung ist unterwegs."
            : $"Ticket #{ticket.Id} wurde angelegt.";

        return new ErfassungsErgebnis(ticket.Id, meldung, []);
    }
}
