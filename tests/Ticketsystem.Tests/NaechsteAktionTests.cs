using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Genau eine nächste Aktion, und keine, die abgewiesen wird: Eine Spalte,
// die vier Wege anbietet, die der Dienst sicher abweist, lehrt niemanden
// die Regel, sondern nur, dass die Oberfläche ihn hereinlegt.
public sealed class NaechsteAktionTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(TicketStatus status = TicketStatus.New)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Ticketsystem.Kern.Data.TicketsystemContext>();
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter1);
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        var ticket = await tickets.CreatePhoneAsync("Drucker klemmt", "Papierstau.", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");

        foreach (var schritt in Weg(status))
        {
            await tickets.ChangeStatusAsync(ticket.Id, schritt, TestDaten.Teamleitung, "Getauscht.");
        }

        return ticket;
    }

    private static IEnumerable<TicketStatus> Weg(TicketStatus ziel) => ziel switch
    {
        TicketStatus.New => [],
        TicketStatus.Assigned => [TicketStatus.Assigned],
        TicketStatus.InProgress => [TicketStatus.Assigned, TicketStatus.InProgress],
        TicketStatus.Resolved => [TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved],
        _ => [TicketStatus.Closed]
    };

    private async Task<DetailAnsicht> AnsichtAsync(int ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        return (await presenter.LadenAsync(ticketId, TestDaten.Teamleitung, DateTime.UtcNow))!;
    }

    // „Geschlossen" steht in jeder Zeile der Übergangstabelle und ist nie der
    // nächste Schritt, sondern der Ausstieg.
    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    public async Task Der_hervorgehobene_Weg_ist_der_vorwaerts_fuehrende(TicketStatus stand, TicketStatus erwartet)
    {
        var ticket = await TicketAsync(stand);

        var ansicht = await AnsichtAsync(ticket.Id);

        Assert.Equal(erwartet, ansicht.HauptUebergang);
        Assert.True(ansicht.ZeigeAbschluss);
    }

    [AvaloniaFact]
    public async Task Ein_geschlossener_Vorgang_hat_keinen_naechsten_Schritt()
    {
        var ticket = await TicketAsync(TicketStatus.Closed);

        var ansicht = await AnsichtAsync(ticket.Id);

        Assert.Null(ansicht.HauptUebergang);
        Assert.False(ansicht.ZeigeAbschluss);
    }

    // Die Primärfarbe gehört dem Statuswechsel, nicht der Nebenhandlung „Mir
    // zuweisen"; sonst sähe die Teamleitung in der ganzen Spalte keinen
    // hervorgehobenen Knopf.
    [AvaloniaFact]
    public async Task In_der_Aktionsspalte_ist_genau_ein_Knopf_hervorgehoben()
    {
        var ticket = await TicketAsync(TicketStatus.Assigned);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var hervorgehoben = fenster.AktionsSpalte.GetVisualDescendants().OfType<Button>()
            .Where(k => k.IsVisible && k.Classes.Contains("primaer")).ToList();

        var einer = Assert.Single(hervorgehoben);
        Assert.Equal(TicketStatus.InProgress.Anzeige(), einer.Content);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Der_Bearbeiter_sieht_die_Selbstzuweisung_als_die_eine_Aktion()
    {
        var ticket = await TicketAsync();
        var fenster = new DetailFenster(_factory.Services, TestDaten.Bearbeiter1, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var hervorgehoben = fenster.AktionsSpalte.GetVisualDescendants().OfType<Button>()
            .Where(k => k.IsVisible && k.Classes.Contains("primaer")).ToList();

        var einer = Assert.Single(hervorgehoben);
        Assert.Equal(fenster.SelbstZuweisen, einer);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_geschlossener_Vorgang_zeigt_seine_Angaben_aber_keine_Aenderung_mehr_an()
    {
        var ticket = await TicketAsync(TicketStatus.Closed);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        foreach (var aktion in new[]
                 {
                     fenster.StatusAktion, fenster.BearbeiterAktion, fenster.WvAktion,
                     fenster.PrioAktion, fenster.NachtragAktion
                 })
        {
            Assert.False(aktion.IsVisible, $"{aktion.Name} bietet an einem geschlossenen Vorgang noch etwas an.");
        }

        Assert.True(fenster.GeschlossenKarte.IsVisible);
        Assert.Equal(TicketStatus.Closed.Anzeige(), fenster.StatusWert.Text);
        Assert.DoesNotContain(fenster.AktionsSpalte.GetVisualDescendants().OfType<Button>(),
            k => k.IsEffectivelyVisible && k.Classes.Contains("primaer"));
        // Der Artikelentwurf bleibt: Ein gelöster, geschlossener Vorgang ist genau
        // der Moment, in dem die Lösung ins Nachschlagewerk gehört.
        Assert.True(fenster.Entwerfen.IsEffectivelyVisible);
        fenster.Close();
    }

    // Der einzige Statuswechsel mit Rückfrage, weil er als einziger keinen
    // Nachfolger hat; alle übrigen sind umkehrbar.
    [AvaloniaFact]
    public async Task Endgueltig_schliessen_geschieht_erst_auf_die_zweite_Bestaetigung()
    {
        var ticket = await TicketAsync(TicketStatus.Assigned);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        fenster.Abschliessen.AusloeserKnopf.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(TicketStatus.Assigned, (await AnsichtAsync(ticket.Id)).Ticket.Status);

        fenster.Abschliessen.ZusageKnopf.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await NachlaufenAsync();

        Assert.Equal(TicketStatus.Closed, (await AnsichtAsync(ticket.Id)).Ticket.Status);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ohne_Auswahl_ist_kein_Knopf_der_Verwaltung_bedienbar()
    {
        var fenster = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.False(fenster.KontoKarte.IsEnabled, "Die Kontokarte ist ohne gewähltes Konto bedienbar.");
        Assert.Contains("Gewähltes Konto", fenster.KontoKarteName.Text);
        Assert.False(fenster.StammdatenLoeschen.IsEnabled);
        Assert.False(fenster.Zurueckspielen.IsEnabled);
        Assert.False(fenster.RuheLoeschen.IsEnabled);

        fenster.Konten.SelectedIndex = 0;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(fenster.KontoKarte.IsEnabled);
        // Die Liste steht rund 200 px weiter oben; ohne den Namen in der
        // Überschrift landet das Passwort irgendwann am falschen Konto.
        var gewaehlt = ((IReadOnlyList<string>)fenster.Konten.ItemsSource!)[0];
        Assert.Contains(gewaehlt[..gewaehlt.IndexOf(" (", StringComparison.Ordinal)],
            fenster.KontoKarteName.Text);
        fenster.Close();
    }

    private static async Task NachlaufenAsync()
    {
        for (var i = 0; i < 40; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    public void Dispose() => _factory.Dispose();
}
