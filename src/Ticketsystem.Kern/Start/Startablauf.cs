using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Start;

// Der Start in fester Reihenfolge: Migrationen, Rollen und Startkonto,
// dann die Sicherung beim Start. Migrate statt EnsureCreated, sonst
// bekäme eine bestehende Datenbank keine neuen Spalten.
public static class Startablauf
{
    public static async Task DatenbankVorbereitenAsync(
        IServiceProvider services, IConfiguration configuration, ILogger log)
    {
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<TicketsystemContext>().Database.Migrate();
        await IdentitySeed.EnsureRolesAndAdminAsync(scope.ServiceProvider, configuration);

        var sicherung = scope.ServiceProvider.GetRequiredService<SicherungService>();

        var herkunft = Datenablage.Herkunft(
            configuration.GetConnectionString("Default"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        log.LogInformation(
            "Datenbank: {Datei} (aus {Herkunft}) | Sicherungen: {Ordner}",
            sicherung.DatenbankDatei, herkunft, sicherung.SicherungsOrdner);

        if (herkunft == "Programmordner")
        {
            log.LogWarning(
                "Weder Benutzerprofil noch Benutzerordner waren zu ermitteln. Die Daten liegen " +
                "neben der Programmdatei und gehen bei einem Ordner-Austausch verloren. " +
                "Abhilfe: ConnectionStrings:Default auf einen festen Pfad setzen.");
        }

        await sicherung.BeimStartSichernAsync();
    }
}
