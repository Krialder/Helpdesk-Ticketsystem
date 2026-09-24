using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Teamleitung und Administration sehen beim Anmelden, wie es um die Fristen
// steht: überfällig, bald fällig, im Rahmen, dazu fällige Wiedervorlagen.
// Die Zahlen kommen aus dem Kern, die Balken aus einem Presenter. Bearbeiter
// sehen den Block nicht: Ihre Frage ist „was ist meins", nicht „wie steht
// das Haus".
public sealed class FristenblickTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Zuweisen prüft das Zielkonto; der gelöste Vorgang unten braucht eines.
    public FristenblickTests()
    {
        using var scope = _factory.Services.CreateScope();
        TestDaten.KontoAnlegen(scope.ServiceProvider.GetRequiredService<TicketsystemContext>(), TestDaten.Teamleitung);
    }

    private async Task<Ticket> TicketAsync(string titel, TicketPriority prio = TicketPriority.Critical)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            titel, "Text.", prio, "Meier, Anna", "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    // Der Dienst rechnet die Fristen aus der Priorität; hier zählt nur der
    // Zustand, nicht der Weg dorthin.
    private async Task FristSetzenAsync(int id, DateTime erstellt, DateTime reaktion)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == id);
        ticket.CreatedAt = erstellt;
        ticket.ReactionDueAt = reaktion;
        await db.SaveChangesAsync();
    }

    private async Task<Fristenuebersicht> UebersichtAsync(Akteur akteur)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>()
            .FristenuebersichtAsync(akteur, DateTime.UtcNow);
    }

    [Fact]
    public async Task Der_Kern_zaehlt_offene_Vorgaenge_nach_dem_schlechtesten_Fristzustand()
    {
        var jetzt = DateTime.UtcNow;
        var ueberfaellig = await TicketAsync("Überfällig");
        await FristSetzenAsync(ueberfaellig.Id, jetzt.AddHours(-2), jetzt.AddHours(-1));
        // Bald fällig heißt: weniger als ein Viertel der Frist übrig.
        var bald = await TicketAsync("Bald");
        await FristSetzenAsync(bald.Id, jetzt.AddMinutes(-50), jetzt.AddMinutes(5));
        await TicketAsync("Im Rahmen", TicketPriority.Low);
        var geloest = await TicketAsync("Gelöst");
        var geschlossen = await TicketAsync("Geschlossen");
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.AssignAsync(geloest.Id, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, TestDaten.Teamleitung);
            await tickets.ChangeStatusAsync(geloest.Id, TicketStatus.InProgress, TestDaten.Teamleitung, null);
            await tickets.ChangeStatusAsync(geloest.Id, TicketStatus.Resolved, TestDaten.Teamleitung, "Erledigt.");
            await tickets.ChangeStatusAsync(geschlossen.Id, TicketStatus.Closed, TestDaten.Teamleitung, null);
            await tickets.WiedervorlageSetzenAsync(bald.Id, jetzt.AddMinutes(-1), "Nachfassen", TestDaten.Teamleitung);
        }

        var uebersicht = await UebersichtAsync(TestDaten.Teamleitung);

        // Gelöst wartet nur noch auf Bestätigung und zählt nicht mehr; Geschlossen
        // ist endgültig.
        Assert.Equal(3, uebersicht.Offen);
        Assert.Equal(1, uebersicht.Ueberfaellig);
        Assert.Equal(1, uebersicht.BaldFaellig);
        Assert.Equal(1, uebersicht.ImRahmen);
        Assert.Equal(1, uebersicht.WiedervorlagenFaellig);
    }

    [Fact]
    public void Der_Presenter_zeigt_das_Lagebild_nur_ab_Teamleitung_und_teilt_ohne_Division_durch_null()
    {
        var leer = new Fristenuebersicht(0, 0, 0, 0, 0, 0, 0, 0, [], [], []);
        Assert.Empty(Fristenblick.Ring(leer));
        Assert.All(Fristenblick.Kacheln(leer), k => Assert.Equal(0, k.Zahl));
        Assert.True(Fristenblick.ZeigenFuer(TestDaten.Teamleitung));
        Assert.True(Fristenblick.ZeigenFuer(TestDaten.Administration));
        Assert.False(Fristenblick.ZeigenFuer(TestDaten.Bearbeiter1));
    }

    [AvaloniaFact]
    public async Task Die_Statusleiste_behaelt_ihre_Breite_und_der_Fristenstand_stimmt()
    {
        var jetzt = DateTime.UtcNow;
        var ueberfaellig = await TicketAsync("Überfällig");
        await FristSetzenAsync(ueberfaellig.Id, jetzt.AddHours(-2), jetzt.AddHours(-1));
        await TicketAsync("Im Rahmen", TicketPriority.Low);

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.ZustandUebernehmenAsync();
        await fenster.AktualisierenAsync();

        Assert.False(string.IsNullOrWhiteSpace(fenster.Zaehler.Text));
        Assert.Contains("überfällig", fenster.Fristen.Text);
        fenster.UpdateLayout();
        Assert.True(fenster.Zaehler.Bounds.Width > 0, "Der Zähler hat keine Breite.");
        Assert.True(fenster.Fristen.Bounds.Width > 0, "Der Fristenstand hat keine Breite.");
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
