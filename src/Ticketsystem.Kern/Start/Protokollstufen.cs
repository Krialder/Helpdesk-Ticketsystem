namespace Ticketsystem.Kern.Start;

// EF Core protokolliert jede SQL-Anweisung auf Information; das sind
// Hunderte Zeilen je Fenster, in denen die eine Warnung untergeht.
public static class Protokollstufen
{
    public static ILoggingBuilder Anwenden(ILoggingBuilder logging) =>
        logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
}
