namespace Ticketsystem.Kern.Domain;

// Die technischen Rollennamen, wie Identity sie speichert; den deutschen
// Namen liefert TicketDisplay.
public static class Rollen
{
    public const string Editor = "Editor";
    public const string TeamLead = "TeamLead";
    public const string Admin = "Admin";

    public static readonly string[] Alle = [Editor, TeamLead, Admin];
}

// Numerisch geordnet, damit „kumulativ“ ein Vergleich ist: Level größer
// gleich Teamleitung heißt Teamleitung oder Administration. OhneRolle kommt
// nicht durch die Anmeldung.
public enum RoleLevel
{
    OhneRolle = 0,
    Bearbeiter = 1,
    Teamleitung = 2,
    Administration = 3
}

// Wer gerade handelt. Name ist der volle, unveränderliche Name für die Akte,
// Anzeigename der änderbare für den Schirm: Stünde der Anzeigename in der
// Historie, schriebe die nächste Umbenennung die Vergangenheit um.
public readonly record struct Akteur(string Id, string Name, RoleLevel Level, string? Anzeigename = null)
{
    public string Anzeige => string.IsNullOrWhiteSpace(Anzeigename) ? Name : Anzeigename;

    public bool IstMitarbeiter => Level >= RoleLevel.Bearbeiter;

    public bool IstMindestens(RoleLevel level) => Level >= level;
}
