using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Prüft am Detailfenster, dass die Presenter-Entscheidungen die Bausteine
// erreichen und dass eine Aktion samt Meldung über den Dienst läuft. Die
// Regeln selbst belegen Presenter- und Diensttests.
public sealed class DetailFensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(Akteur ersteller)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Ticketsystem.Kern.Data.TicketsystemContext>();
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter1);
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        return await tickets.CreatePhoneAsync("Drucker klemmt", "Papierstau.", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: ersteller.Id, createdByName: ersteller.Name, address: "B-12");
    }

    [AvaloniaFact]
    public async Task Die_Rollen_entscheiden_welche_Aktionskarten_erscheinen()
    {
        var ticket = await TicketAsync(TestDaten.Bearbeiter1);

        var teamleitung = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        await teamleitung.LadenAsync();
        var bearbeiter = new DetailFenster(_factory.Services, TestDaten.Bearbeiter1, ticket.Id);
        await bearbeiter.LadenAsync();

        Assert.True(teamleitung.ZuweisenKarte.IsVisible);
        Assert.False(teamleitung.SelbstKarte.IsVisible);
        Assert.False(bearbeiter.ZuweisenKarte.IsVisible);
        Assert.True(bearbeiter.SelbstKarte.IsVisible);
        teamleitung.Close();
        bearbeiter.Close();
    }

    [AvaloniaFact]
    public async Task Eine_fachliche_Abweisung_erscheint_als_Meldungsleiste()
    {
        var ticket = await TicketAsync(TestDaten.Teamleitung);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        await fenster.LadenAsync();

        await fenster.AktionAsync(dienst =>
            dienst.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, TestDaten.Teamleitung));

        Assert.True(fenster.MeldungsLeiste.IsVisible);
        Assert.Contains("nicht erlaubt", fenster.Meldung.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_Statuswechsel_ueber_das_Fenster_wirkt_und_frischt_die_Ansicht_auf()
    {
        var ticket = await TicketAsync(TestDaten.Teamleitung);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        await fenster.LadenAsync();

        await fenster.AktionAsync(dienst =>
            dienst.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung));

        Assert.False(fenster.MeldungsLeiste.IsVisible);
        Assert.Equal(TicketStatus.Assigned.Anzeige(), fenster.StatusWert.Text);
        fenster.Close();
    }

    // Eine Klick-Bindung im Nachladeweg hängt sich bei jedem Laden erneut an;
    // ein Klick löste dann so viele Zuweisungen aus, wie das Fenster geladen
    // hatte. Der Test klickt wirklich (RaiseEvent), denn die Registrierung ist
    // der Prüfgegenstand, und lässt danach nachlaufen, damit weitere Bindungen
    // sichtbar würden.
    [AvaloniaFact]
    public async Task Ein_Klick_auf_Zuweisen_wirkt_genau_einmal_auch_nach_mehrfachem_Laden()
    {
        var ticket = await TicketAsync(TestDaten.Teamleitung);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();
        await fenster.LadenAsync();
        await fenster.LadenAsync();
        fenster.ZielWahl.SelectedIndex = 0;

        fenster.Zuweisen.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await WarteBisAsync(async () => await ZuweisungenAsync(ticket.Id) > 0);
        await NachlaufenAsync();

        Assert.Equal(1, await ZuweisungenAsync(ticket.Id));
        fenster.Close();
    }

    // Schrieb das Nachladen die Nachtragsfelder aus dem Bestand zurück,
    // verschwand die Eingabe von jemandem, der tippt, während im Hintergrund
    // eine Aktion läuft.
    [AvaloniaFact]
    public async Task Getippte_Nachtraege_ueberleben_das_Nachladen()
    {
        var ticket = await TicketAsync(TestDaten.Teamleitung);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        fenster.NachAdresse.Text = "C-42";
        await fenster.LadenAsync();

        Assert.Equal("C-42", fenster.NachAdresse.Text);
        fenster.Close();
    }

    // Standen die Felder auf der Vorgabe (morgen 9:00), obwohl ein Termin
    // gesetzt war, tippte ihn blind neu, wer ihn verschieben wollte.
    [AvaloniaFact]
    public async Task Eine_vorhandene_Wiedervorlage_steht_in_den_Feldern()
    {
        var ticket = await TicketAsync(TestDaten.Teamleitung);
        var termin = DateTime.UtcNow.Date.AddDays(3).AddHours(14);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .WiedervorlageSetzenAsync(ticket.Id, termin, "Netzteil da? Einbau", TestDaten.Teamleitung);
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var ortszeit = Zeitanzeige.AlsOrtszeit(termin);
        Assert.Equal(ortszeit.Date, fenster.WvDatum.SelectedDate?.Date);
        Assert.Equal(ortszeit.TimeOfDay, fenster.WvZeit.SelectedTime);
        Assert.Equal("Netzteil da? Einbau", fenster.WvGrund.Text);
        fenster.Close();
    }

    private async Task<int> ZuweisungenAsync(int ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Ticketsystem.Kern.Data.TicketsystemContext>()
            .TicketHistory.CountAsync(h => h.TicketId == ticketId && h.Field == "Agent");
    }

    // Wartet auf die Wirkung statt auf eine feste Zeitspanne: Der Klick-Handler
    // ist async void, und ein fester Schlaf wäre entweder zu kurz (falsch grün)
    // oder unnötig langsam.
    private static async Task WarteBisAsync(Func<Task<bool>> bedingung)
    {
        for (var i = 0; i < 100; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (await bedingung())
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static async Task NachlaufenAsync()
    {
        for (var i = 0; i < 15; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    public void Dispose() => _factory.Dispose();
}
