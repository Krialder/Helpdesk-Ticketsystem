using Microsoft.Data.Sqlite;

namespace Ticketsystem.Kern.Data;

// Wo die Datenbank liegt: konfiguriert über ConnectionStrings:Default, sonst
// im Profil des Nutzers (LocalApplicationData), sonst im Benutzerordner,
// sonst neben dem Programm. Die Kette ist nötig, weil LocalApplicationData
// auf manchen Rechnern leer ist; daneben liegt der Sicherungsordner.
public static class Datenablage
{
    public const string Anwendungsordner = "Ticketsystem";

    public const string Dateiname = "app.db";

    public const string Sicherungsordner = "Sicherungen";

    public static string Verbindung(IConfiguration configuration) =>
        Verbindung(
            configuration.GetConnectionString("Default"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppContext.BaseDirectory);

    public static string Verbindung(
        string? konfiguriert, string profilOrdner, string benutzerOrdner, string programmOrdner)
    {
        if (!string.IsNullOrWhiteSpace(konfiguriert))
        {
            return konfiguriert;
        }

        var ordner = Vorgabeordner(profilOrdner, benutzerOrdner, programmOrdner);
        Directory.CreateDirectory(ordner);
        return $"Data Source={Path.Combine(ordner, Dateiname)}";
    }

    public static string Vorgabeordner(string profilOrdner, string benutzerOrdner, string programmOrdner) =>
        Path.GetFullPath(Path.Combine(Erste(profilOrdner, benutzerOrdner, programmOrdner), Anwendungsordner));

    // Für die Startmeldung: Der Nutzer soll sehen, warum die Datei dort liegt.
    public static string Herkunft(string? konfiguriert, string profilOrdner, string benutzerOrdner) =>
        !string.IsNullOrWhiteSpace(konfiguriert) ? "Konfiguration"
        : !string.IsNullOrWhiteSpace(profilOrdner) ? "Benutzerprofil"
        : !string.IsNullOrWhiteSpace(benutzerOrdner) ? "Benutzerordner"
        : "Programmordner";

    private static string Erste(params string[] kandidaten) =>
        kandidaten.First(k => !string.IsNullOrWhiteSpace(k));

    public static string DateiAus(string verbindung) =>
        Path.GetFullPath(new SqliteConnectionStringBuilder(verbindung).DataSource);

    public static string SicherungsOrdnerFuer(string datenbankDatei) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(datenbankDatei)) ?? ".",
            Sicherungsordner);
}
