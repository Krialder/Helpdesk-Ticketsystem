using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

// Das Lagebild als eigenes Fenster: sechs Kacheln mit je einer Zahl,
// darunter ein Ring nach Priorität, Balken nach Status und die
// Altersstufen. Es öffnet sich für Teamleitung und Administration beim
// Anmelden und über den Knopf „Lagebild"; jede Kachel führt in die
// passende Liste.
public sealed class LagebildfensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public LagebildfensterTests()
    {
        using var scope = _factory.Services.CreateScope();
        TestDaten.KontoAnlegen(scope.ServiceProvider.GetRequiredService<TicketsystemContext>(), TestDaten.Teamleitung);
    }

    private async Task<Ticket> TicketAsync(string titel, TicketPriority prio = TicketPriority.Medium)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            titel, "Text.", prio, "Meier, Anna", "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    private async Task FristSetzenAsync(int id, DateTime erstellt, DateTime reaktion)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == id);
        ticket.CreatedAt = erstellt;
        ticket.ReactionDueAt = reaktion;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Der_Kern_zaehlt_heute_faellige_nicht_zugewiesene_alle_und_je_Status()
    {
        // Mittag UTC, damit „heute" in jeder Zone bis zehn Stunden Abstand
        // derselbe Tag bleibt; die Zone kommt ausdrücklich herein.
        var jetzt = DateTime.UtcNow.Date.AddHours(12);
        var heute = await TicketAsync("Heute");
        await FristSetzenAsync(heute.Id, jetzt.AddHours(-1), jetzt.AddHours(3));
        var morgen = await TicketAsync("Morgen", TicketPriority.Low);
        await FristSetzenAsync(morgen.Id, jetzt.AddHours(-1), jetzt.AddHours(30));
        var ueberfaellig = await TicketAsync("Überfällig");
        await FristSetzenAsync(ueberfaellig.Id, jetzt.AddHours(-3), jetzt.AddHours(-1));
        var zugewiesen = await TicketAsync("Zugewiesen");
        var geschlossen = await TicketAsync("Geschlossen");
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.AssignAsync(zugewiesen.Id, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, TestDaten.Teamleitung);
            await tickets.ChangeStatusAsync(geschlossen.Id, TicketStatus.Closed, TestDaten.Teamleitung, null);
        }

        Fristenuebersicht u;
        using (var scope = _factory.Services.CreateScope())
        {
            u = await scope.ServiceProvider.GetRequiredService<TicketService>()
                .FristenuebersichtAsync(TestDaten.Teamleitung, jetzt, TimeZoneInfo.Utc);
        }

        Assert.Equal(4, u.Offen);
        // Überfällig zählt nicht als „heute fällig": Die Frist ist vorbei.
        Assert.Equal(1, u.HeuteFaellig);
        Assert.Equal(3, u.NichtZugewiesen);
        Assert.Equal(5, u.Alle);
        Assert.Equal(3, u.JeStatus.Single(s => s.Status == TicketStatus.New).Zahl);
        Assert.Equal(1, u.JeStatus.Single(s => s.Status == TicketStatus.Assigned).Zahl);
        Assert.Equal(0, u.JeStatus.Single(s => s.Status == TicketStatus.InProgress).Zahl);
        Assert.DoesNotContain(u.JeStatus, s => s.Status == TicketStatus.Closed);
    }

    [Fact]
    public void Der_Presenter_liefert_sechs_Kacheln_mit_Ziel_und_einen_Ring_der_sich_schliesst()
    {
        var u = new Fristenuebersicht(
            Offen: 10, Ueberfaellig: 2, BaldFaellig: 1, ImRahmen: 7, WiedervorlagenFaellig: 3,
            HeuteFaellig: 4, NichtZugewiesen: 5, Alle: 34,
            JePrioritaet:
            [
                new(TicketPriority.Critical, 1, 0, 1),
                new(TicketPriority.High, 1, 1, 2),
                new(TicketPriority.Medium, 0, 0, 4),
                new(TicketPriority.Low, 0, 0, 0)
            ],
            Alter: [new("bis 1 Tag", 10)],
            JeStatus: [new(TicketStatus.New, 6), new(TicketStatus.Assigned, 3), new(TicketStatus.InProgress, 1), new(TicketStatus.Resolved, 0)]);

        var kacheln = Fristenblick.Kacheln(u);
        Assert.Collection(kacheln,
            k => { Assert.Equal("Überfällig", k.Bezeichnung); Assert.Equal(2, k.Zahl); Assert.Equal("Überfällig", k.Ansicht); },
            k => { Assert.Equal("Heute fällig", k.Bezeichnung); Assert.Equal(4, k.Zahl); Assert.Equal("Offen", k.Ansicht); Assert.Equal(Sortierung.Frist, k.Sortierung); },
            k => { Assert.Equal("Offen", k.Bezeichnung); Assert.Equal(10, k.Zahl); Assert.Equal("Offen", k.Ansicht); },
            k => { Assert.Equal("Wiedervorlagen fällig", k.Bezeichnung); Assert.Equal(3, k.Zahl); Assert.Equal("Wiedervorlage fällig", k.Ansicht); },
            k => { Assert.Equal("Nicht zugewiesen", k.Bezeichnung); Assert.Equal(5, k.Zahl); Assert.Equal("Unzugewiesen", k.Ansicht); },
            k => { Assert.Equal("Alle", k.Bezeichnung); Assert.Equal(34, k.Zahl); Assert.Equal("Alle", k.Ansicht); });
        Assert.Equal("badge-frist-ueberfaellig", kacheln[0].Klasse);
        Assert.Equal("badge-frist-neutral", kacheln[2].Klasse);

        // Der Ring: nur Prioritäten mit Vorgängen, die Winkel schließen sich zu
        // 360 Grad, die Prozente sind ganze Zahlen mit Leerzeichen.
        var ring = Fristenblick.Ring(u);
        Assert.Equal(3, ring.Count);
        Assert.Equal(-90, ring[0].StartWinkel);
        Assert.Equal(360, ring.Sum(s => s.Winkel), 3);
        Assert.Equal(ring[0].StartWinkel + ring[0].Winkel, ring[1].StartWinkel, 3);
        Assert.Equal("20 %", ring[0].Prozent);
        Assert.Equal("Kritisch", ring[0].Bezeichnung);
        Assert.Equal("prio-kritisch", ring[0].Klasse);
        Assert.Empty(Fristenblick.Ring(u with { JePrioritaet = [] }));

        var status = Fristenblick.Status(u);
        Assert.Equal(4, status.Count);
        Assert.Equal("Neu", status[0].Bezeichnung);
        Assert.Equal(1.0, status[0].Anteil);
        Assert.Equal(0.5, status[1].Anteil);
        Assert.Equal("Neu", status[0].Ansicht);
    }

    [AvaloniaFact]
    public async Task Die_Teamleitung_bekommt_das_Lagebild_beim_Anmelden_und_eine_Kachel_fuehrt_in_die_Liste()
    {
        var jetzt = DateTime.UtcNow;
        var ueberfaellig = await TicketAsync("Überfällig", TicketPriority.Critical);
        await FristSetzenAsync(ueberfaellig.Id, jetzt.AddHours(-2), jetzt.AddHours(-1));
        await TicketAsync("Im Rahmen", TicketPriority.Low);

        var grund = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        grund.Show();
        await grund.ZustandUebernehmenAsync();
        await grund.AktualisierenAsync();
        var lagebild = await grund.LagebildOeffnenAsync();
        Assert.NotNull(lagebild);
        Messen.Auslegen(lagebild, 1100, 700);

        Assert.True(lagebild.IsVisible);
        Assert.Equal(6, lagebild.Kacheln.Children.Count);
        Assert.Contains("2 offene", lagebild.Summe.Text);
        var boegen = lagebild.Ring.GetVisualDescendants().OfType<Arc>().ToList();
        Assert.Equal(2, boegen.Count);
        // Ein Grad Luft je Stück, damit die Stücke zählbar bleiben.
        Assert.Equal(360 - boegen.Count, boegen.Sum(b => b.SweepAngle), 3);
        Assert.Equal("2", lagebild.RingMitte.Text);
        Assert.Equal(4, lagebild.StatusBalken.Children.Count);
        Assert.Equal(4, lagebild.AlterBalken.Children.Count);

        var kachel = lagebild.Kacheln.GetVisualDescendants().OfType<Button>()
            .Single(k => k.Name == "Kachel_Ueberfaellig");
        kachel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Überfällig", grund.Ansicht.SelectedItem);
        Assert.False(lagebild.IsVisible);

        Assert.True(grund.Lagebild.IsVisible);
        grund.Close();
    }

    [AvaloniaFact]
    public async Task Ein_Bearbeiter_bekommt_weder_Lagebild_noch_Knopf()
    {
        await TicketAsync("Im Rahmen");
        var grund = new Grundfenster(_factory.Services, TestDaten.Bearbeiter1);
        grund.Show();
        await grund.ZustandUebernehmenAsync();
        await grund.AktualisierenAsync();

        Assert.False(grund.Lagebild.IsVisible);
        Assert.Null(await grund.LagebildOeffnenAsync());
        grund.Close();
    }

    public void Dispose() => _factory.Dispose();
}
