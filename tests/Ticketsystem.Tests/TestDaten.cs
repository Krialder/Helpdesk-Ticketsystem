using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Akteure für die Dienst-Tests, je Rolle der Rechtematrix einer, damit
// Testnamen und Matrix dieselbe Sprache sprechen.
public static class TestDaten
{
    // Kommt nicht durch die Anmeldung; die Dienste weisen das Konto trotzdem ab,
    // und genau das prüfen die Tests damit.
    public static readonly Akteur OhneRolle = new("ohne-rolle-1", "ohne-rolle-1@example.net", RoleLevel.OhneRolle);
    public static readonly Akteur Bearbeiter1 = new("bearbeiter-1", "bearbeiter-1@example.org", RoleLevel.Bearbeiter);
    public static readonly Akteur Bearbeiter2 = new("bearbeiter-2", "bearbeiter-2@example.org", RoleLevel.Bearbeiter);
    public static readonly Akteur Teamleitung = new("teamleitung-1", "teamleitung@example.org", RoleLevel.Teamleitung);
    public static readonly Akteur Administration = new("admin-1", "admin@example.org", RoleLevel.Administration);

    // AssignAsync prüft das Zielkonto in der Datenbank (vorhanden, nicht
    // pausiert), deshalb reicht der Akteur allein für eine Zuweisung nicht.
    public static AppUser KontoAnlegen(TicketsystemContext db, Akteur akteur, DateTime? pausiertBis = null)
    {
        var konto = new AppUser
        {
            Id = akteur.Id,
            UserName = akteur.Name,
            Email = akteur.Name,
            NormalizedEmail = akteur.Name.ToUpperInvariant(),
            NormalizedUserName = akteur.Name.ToUpperInvariant(),
            PausedUntil = pausiertBis
        };
        db.Users.Add(konto);
        db.SaveChanges();
        return konto;
    }
}
