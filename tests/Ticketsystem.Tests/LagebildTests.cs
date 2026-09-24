using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Fristen und Vorgänge grafisch: Die Frist je Vorgang steht als Countdown
// und als Anteil der verstrichenen Zeit; Leitungen sehen offene Vorgänge
// nach Priorität und Alter, weil ein alter Vorgang ohne Frist genauso
// liegen bleibt wie einer mit gerissener. Balken statt Kuchen, Farbe für
// den Zustand, nie für die Zierde.
public sealed class LagebildTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public LagebildTests()
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

    private async Task ZeitenSetzenAsync(int id, DateTime erstellt, DateTime? reaktion = null, DateTime? loesung = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == id);
        ticket.CreatedAt = erstellt;
        if (reaktion is DateTime r) ticket.ReactionDueAt = r;
        if (loesung is DateTime l) ticket.ResolutionDueAt = l;
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Der_Anteil_der_Frist_laeuft_von_null_bis_eins_und_haelt_dort_an()
    {
        var erstellt = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc);
        var faellig = erstellt.AddHours(4);

        Assert.Equal(0.0, SlaEvaluation.Anteil(null, faellig, erstellt, erstellt));
        Assert.Equal(0.5, SlaEvaluation.Anteil(null, faellig, erstellt, erstellt.AddHours(2)));
        // Überfällig bleibt bei eins, erfüllt friert beim Erfüllen ein, und eine
        // Frist ohne Länge (Ruhezeit, Altbestand) ist voll statt durch null
        // geteilt.
        Assert.Equal(1.0, SlaEvaluation.Anteil(null, faellig, erstellt, erstellt.AddHours(9)));
        Assert.Equal(0.25, SlaEvaluation.Anteil(erstellt.AddHours(1), faellig, erstellt, erstellt.AddHours(9)));
        Assert.Equal(1.0, SlaEvaluation.Anteil(null, erstellt, erstellt, erstellt));
    }

    [Fact]
    public async Task Die_Listenzeile_und_das_Detail_tragen_den_Anteil_der_massgeblichen_Frist()
    {
        var jetzt = DateTime.UtcNow;
        var ticket = await TicketAsync("Halbzeit");
        await ZeitenSetzenAsync(ticket.Id, jetzt.AddMinutes(-30), reaktion: jetzt.AddMinutes(30));

        using var scope = _factory.Services.CreateScope();
        var liste = new TicketlistenPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var zeile = (await liste.LadenAsync(TestDaten.Teamleitung, "Offen", null, false, null, jetzt)).Zeilen.Single();
        Assert.InRange(zeile.SlaAnteil, 0.45, 0.55);

        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var ansicht = await presenter.LadenAsync(ticket.Id, TestDaten.Teamleitung, jetzt);
        Assert.InRange(ansicht!.SlaReaktionAnteil, 0.45, 0.55);
        Assert.InRange(ansicht.SlaLoesungAnteil, 0.0, 0.2);
    }

    [Fact]
    public async Task Der_Kern_zaehlt_offene_Vorgaenge_je_Prioritaet_und_je_Alter()
    {
        var jetzt = DateTime.UtcNow;
        var kritisch = await TicketAsync("Kritisch, überfällig, 3 Tage alt", TicketPriority.Critical);
        await ZeitenSetzenAsync(kritisch.Id, jetzt.AddDays(-3), reaktion: jetzt.AddDays(-2), loesung: jetzt.AddDays(-1));
        var mittel = await TicketAsync("Mittel, im Rahmen, heute");
        var hoch = await TicketAsync("Hoch, bald fällig, 15 Tage alt", TicketPriority.High);
        await ZeitenSetzenAsync(hoch.Id, jetzt.AddDays(-15), reaktion: jetzt.AddHours(1), loesung: jetzt.AddHours(2));
        var alt = await TicketAsync("Mittel, 40 Tage alt", TicketPriority.Medium);
        // Noch ein Drittel der Frist übrig: alt, aber im Rahmen. Mit nur einem Tag
        // Rest wäre er bald fällig (weniger als ein Viertel).
        await ZeitenSetzenAsync(alt.Id, jetzt.AddDays(-40), reaktion: jetzt.AddDays(20), loesung: jetzt.AddDays(30));

        using var scope = _factory.Services.CreateScope();
        var u = await scope.ServiceProvider.GetRequiredService<TicketService>()
            .FristenuebersichtAsync(TestDaten.Teamleitung, jetzt);

        var kritischZeile = Assert.Single(u.JePrioritaet, p => p.Prioritaet == TicketPriority.Critical);
        Assert.Equal((1, 0, 0), (kritischZeile.Ueberfaellig, kritischZeile.BaldFaellig, kritischZeile.ImRahmen));
        var mittelZeile = Assert.Single(u.JePrioritaet, p => p.Prioritaet == TicketPriority.Medium);
        Assert.Equal((0, 0, 2), (mittelZeile.Ueberfaellig, mittelZeile.BaldFaellig, mittelZeile.ImRahmen));
        var hochZeile = Assert.Single(u.JePrioritaet, p => p.Prioritaet == TicketPriority.High);
        Assert.Equal((0, 1, 0), (hochZeile.Ueberfaellig, hochZeile.BaldFaellig, hochZeile.ImRahmen));
        // Jede Priorität ist eine Zeile, auch mit null Vorgängen: Eine fehlende
        // Zeile liest sich wie ein Fehler, eine Null wie eine Antwort.
        Assert.Equal(4, u.JePrioritaet.Count);

        Assert.Equal(["bis 1 Tag", "2 bis 7 Tage", "8 bis 30 Tage", "über 30 Tage"], u.Alter.Select(a => a.Bezeichnung));
        Assert.Equal([1, 1, 1, 1], u.Alter.Select(a => a.Zahl));
        _ = mittel;
    }

    [Fact]
    public void Der_Presenter_macht_aus_der_Verteilung_Ringstuecke_und_Altersbalken_mit_gemeinsamem_Massstab()
    {
        var u = new Fristenuebersicht(6, 2, 1, 3, 0, 0, 0, 6,
            JePrioritaet:
            [
                new Prioritaetsstand(TicketPriority.Critical, 2, 0, 0),
                new Prioritaetsstand(TicketPriority.High, 0, 1, 3),
                new Prioritaetsstand(TicketPriority.Medium, 0, 0, 0),
                new Prioritaetsstand(TicketPriority.Low, 0, 0, 0)
            ],
            Alter: [new Altersstufe("bis 1 Tag", 4), new Altersstufe("2 bis 7 Tage", 2), new Altersstufe("8 bis 30 Tage", 0), new Altersstufe("über 30 Tage", 0)],
            JeStatus: []);

        var ring = Fristenblick.Ring(u);
        Assert.Equal(["Kritisch", "Hoch"], ring.Select(s => s.Bezeichnung));
        Assert.Equal(120, ring[0].Winkel, 3);
        Assert.Equal(240, ring[1].Winkel, 3);
        Assert.Equal(30, ring[1].StartWinkel, 3);
        Assert.Equal("33 %", ring[0].Prozent);

        var alter = Fristenblick.Altersstufen(u);
        Assert.Equal([1.0, 0.5, 0.0, 0.0], alter.Select(a => a.Anteil));
        Assert.Equal("bis 1 Tag", alter[0].Bezeichnung);
    }

    [Fact]
    public async Task Die_Auswertung_zaehlt_Eingang_und_Erledigung_je_Tag_ueber_den_ganzen_Zeitraum()
    {
        var jetzt = DateTime.UtcNow;
        await TicketAsync("Heute eins");
        await TicketAsync("Heute zwei");
        var gestern = await TicketAsync("Gestern, heute erledigt");
        await ZeitenSetzenAsync(gestern.Id, jetzt.AddDays(-1));
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.AssignAsync(gestern.Id, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, TestDaten.Teamleitung);
            await tickets.ChangeStatusAsync(gestern.Id, TicketStatus.InProgress, TestDaten.Teamleitung, null);
            await tickets.ChangeStatusAsync(gestern.Id, TicketStatus.Resolved, TestDaten.Teamleitung, "Erledigt.");
        }

        using var pruefung = _factory.Services.CreateScope();
        var auswertung = await pruefung.ServiceProvider.GetRequiredService<AuswertungService>()
            .ErstellenAsync(TestDaten.Teamleitung, wochen: 4, jetzt);

        // Jeder Tag des Zeitraums ist eine Zeile, auch ohne Vorgang; der letzte Tag
        // ist heute.
        Assert.Equal(28, auswertung.JeTag.Count);
        Assert.Equal(jetzt.Date, auswertung.JeTag[^1].Tag);
        Assert.Equal(jetzt.ToString("dd.MM.", System.Globalization.CultureInfo.InvariantCulture), auswertung.JeTag[^1].Beschriftung);
        Assert.Equal(2, auswertung.JeTag[^1].Eingegangen);
        Assert.Equal(1, auswertung.JeTag[^1].Erledigt);
        Assert.Equal(1, auswertung.JeTag[^2].Eingegangen);
        Assert.Equal(0, auswertung.JeTag[^2].Erledigt);
    }

    [AvaloniaFact]
    public void Der_Fristmesser_fuellt_seine_Spur_nach_Anteil_und_faerbt_nach_Zustand()
    {
        var messer = new Fristmesser { Anteil = 0.5, Klasse = "badge-frist-ueberfaellig" };
        var fenster = new Window { Content = messer };
        Messen.Auslegen(fenster, 400, 100);

        var fuellung = messer.GetVisualDescendants().OfType<Grid>()
            .Single(g => g.ColumnDefinitions.Count == 2);
        Assert.Equal(0.5, fuellung.ColumnDefinitions[0].Width.Value);
        var balken = Assert.IsType<Border>(fuellung.Children[0]);
        Assert.Equal(Palette.GefahrText, ((ISolidColorBrush)balken.Background!).Color);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Liste_Detail_und_Block_zeigen_die_Bilder()
    {
        var jetzt = DateTime.UtcNow;
        var ticket = await TicketAsync("Halbzeit", TicketPriority.High);
        await ZeitenSetzenAsync(ticket.Id, jetzt.AddMinutes(-30), reaktion: jetzt.AddMinutes(30));

        var grund = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        grund.Show();
        await grund.ZustandUebernehmenAsync();
        await grund.AktualisierenAsync();
        Messen.Auslegen(grund, 1240, 760);

        var messer = Assert.Single(grund.Liste.GetVisualDescendants().OfType<Fristmesser>());
        Assert.InRange(messer.Anteil, 0.45, 0.55);
        var lagebild = await grund.LagebildOeffnenAsync();
        Assert.NotNull(lagebild);
        Messen.Auslegen(lagebild, 1100, 700);
        Assert.Equal(4, lagebild.StatusBalken.Children.Count);
        Assert.Equal(4, lagebild.AlterBalken.Children.Count);
        var neu = lagebild.StatusBalken.GetVisualDescendants().OfType<Button>()
            .Single(k => Equals(k.Content, "Neu"));
        neu.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Neu", grund.Ansicht.SelectedItem);
        grund.Close();

        var detail = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        detail.Show();
        await detail.LadenAsync();
        Assert.InRange(detail.ReaktionMesser.Anteil, 0.45, 0.55);
        Assert.Equal("badge-frist-neutral", detail.ReaktionMesser.Klasse);
        detail.Close();
    }

    [AvaloniaFact]
    public async Task Die_Auswertung_zeichnet_je_Tag_zwei_Saeulen_und_nennt_die_Reihen()
    {
        await TicketAsync("Heute");
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.AuswertungLadenAsync();

        Assert.Equal(28, fenster.TagDiagramm.ColumnDefinitions.Count);
        Assert.Equal(28, fenster.TagDiagramm.Children.Count);
        // Zwei Reihen brauchen eine Legende.
        Assert.Contains("Eingang", fenster.TagLegende.Text);
        Assert.Contains("Erledigt", fenster.TagLegende.Text);
        Assert.Contains("1 eingegangen", fenster.TagSumme.Text);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
