using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Start;

// Beispieldaten für die Sichtprobe und den Hinweis-Wächtertest, nur in eine leere
// Datenbank: je Priorität und Fristzustand eine Zeile, eine Wiedervorlage,
// ein Kommentar und ein Artikel mit jeder Auszeichnung, damit die Bilder
// alle Farben und Formen zeigen.
public static class Demodaten
{
    public static async Task AnlegenAsync(IServiceProvider scoped, Akteur akteur)
    {
        var db = scoped.GetRequiredService<TicketsystemContext>();
        if (db.Tickets.Any())
        {
            return;
        }

        var tickets = scoped.GetRequiredService<TicketService>();
        var server = await tickets.CreatePhoneAsync("Server im Keller piept", "Dauerton aus dem Schrank.",
            TicketPriority.Critical, "Huber, Karl", "0221555111",
            DateTime.UtcNow, null, akteur.Id, akteur.Name, "A-001");
        // Direkt zurückdatiert, damit ein Vorgang überfällig ist; der Dienst
        // rechnet Fristen sonst immer ab jetzt.
        server.CreatedAt = DateTime.UtcNow.AddHours(-3);
        server.ReactionDueAt = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();
        await tickets.CreatePhoneAsync("Bildschirm bleibt dunkel", "Kein Bild, Lüfter läuft.",
            TicketPriority.High, "Schneider, Eva", "0221777888",
            DateTime.UtcNow, null, akteur.Id, akteur.Name, "D-4");
        var drucker = await tickets.CreatePhoneAsync("Drucker klemmt", "Papierstau im Fach 2.",
            TicketPriority.Medium, "Weber, Sabine", "0221333444",
            DateTime.UtcNow, null, akteur.Id, akteur.Name, "B-12");
        await tickets.WiedervorlageSetzenAsync(drucker.Id, DateTime.UtcNow.AddMinutes(-5),
            "Netzteil da? Einbau", akteur);
        await tickets.AssignAsync(drucker.Id, akteur.Id, akteur.Name, akteur);
        await tickets.AddCommentAsync(drucker.Id, akteur, "Fach 2 geöffnet, Papierrest entfernt.");

        var wissen = scoped.GetRequiredService<KnowledgeBaseService>();
        var wlan = await wissen.SpeichernAsync(null, "WLAN-Profil erneuern",
            "Altes Profil entfernen, neu verbinden, Anmeldedaten eingeben.",
            null, published: true,
            reviewIntervallTage: KbArticle.StandardPruefzyklusTage,
            [], akteur);
        await wissen.SpeichernAsync(wlan.Id, "WLAN-Profil erneuern",
            "## Vorgehen\n"
            + "1. Altes Profil entfernen: `netsh wlan delete profile name=\"Firma\"`\n"
            + "2. Neu verbinden und Anmeldedaten eingeben.\n"
            + "3. Bei Fehler **0x80004005** zuerst den Adapter neu starten.\n"
            + "\n"
            + "## Prüfen\n"
            + "```\n"
            + "netsh wlan show interfaces\n"
            + "```\n"
            + "\n"
            + "- Stand **Verbunden** und die richtige SSID: fertig.\n"
            + "- Sonst Ticket an die Netzwerkgruppe.",
            null, published: true,
            reviewIntervallTage: KbArticle.StandardPruefzyklusTage,
            [], akteur);
    }
}
