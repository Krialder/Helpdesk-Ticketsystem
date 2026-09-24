using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein vergebener Vorgang wird wieder für alle zugänglich, sonst bleibt er
// an einem ausgefallenen Bearbeiter hängen. Teamleitung aufwärts hebt jede
// Zuweisung auf, ein Bearbeiter nur seine eigene: Er gibt zurück, was er
// sich genommen hat, mehr nicht.
public sealed class ZuweisungAufhebenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Zuweisen prüft das Zielkonto (vorhanden, nicht pausiert), deshalb
    // brauchen die Akteure hier ein Konto in der Datenbank.
    public ZuweisungAufhebenTests()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter2);
        TestDaten.KontoAnlegen(db, TestDaten.Teamleitung);
    }

    private async Task<Ticket> TicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Meier, Anna", "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    private async Task<T> DienstAsync<T>(Func<TicketService, Task<T>> aufruf)
    {
        using var scope = _factory.Services.CreateScope();
        return await aufruf(scope.ServiceProvider.GetRequiredService<TicketService>());
    }

    private Task DienstAsync(Func<TicketService, Task> aufruf) =>
        DienstAsync(async d => { await aufruf(d); return 0; });

    private async Task<Ticket> GespeichertAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketsystemContext>()
            .Tickets.Include(t => t.History).SingleAsync(t => t.Id == id);
    }

    [Fact]
    public async Task Teamleitung_hebt_jede_Zuweisung_auf_und_Zugewiesen_wird_wieder_Neu()
    {
        var ticket = await TicketAsync();
        await DienstAsync(d => d.AssignAsync(ticket.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung));

        await DienstAsync(d => d.ZuweisungAufhebenAsync(ticket.Id, TestDaten.Teamleitung));

        var frisch = await GespeichertAsync(ticket.Id);
        Assert.Null(frisch.AgentId);
        Assert.Null(frisch.AgentName);
        // Neu zu Zugewiesen kam durch die Zuweisung, also geht es mit ihr zurück.
        // Die Bearbeiterzeile steht so, dass die Historie sie als „X zu leer"
        // zeigen kann.
        Assert.Equal(TicketStatus.New, frisch.Status);
        var zeile = frisch.History.Last(h => h.Field == "Agent");
        Assert.Equal(TestDaten.Bearbeiter2.Name, zeile.OldValue);
        Assert.Null(zeile.NewValue);
        Assert.Equal(TestDaten.Teamleitung.Id, zeile.ChangedById);
        Assert.Contains(frisch.History, h => h.Field == "Status" && h.NewValue == TicketStatus.New.Anzeige());
    }

    [Fact]
    public async Task Ein_Bearbeiter_gibt_nur_seinen_eigenen_Vorgang_zurueck()
    {
        var eigenes = await TicketAsync();
        await DienstAsync(d => d.AssignAsync(eigenes.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Bearbeiter1));
        await DienstAsync(d => d.ZuweisungAufhebenAsync(eigenes.Id, TestDaten.Bearbeiter1));
        Assert.Null((await GespeichertAsync(eigenes.Id)).AgentId);

        var fremdes = await TicketAsync();
        await DienstAsync(d => d.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DienstAsync(d => d.ZuweisungAufhebenAsync(fremdes.Id, TestDaten.Bearbeiter1)));
        Assert.Equal(TestDaten.Bearbeiter2.Id, (await GespeichertAsync(fremdes.Id)).AgentId);
    }

    // Nur Zugewiesen ist die Umkehrung der Zuweisung. Wer einen Vorgang in
    // Arbeit übernimmt, sieht am Status, dass schon jemand dran war.
    [Fact]
    public async Task Ein_Vorgang_in_Arbeit_behaelt_seinen_Status()
    {
        var ticket = await TicketAsync();
        await DienstAsync(d => d.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung));
        await DienstAsync(d => d.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung, null));

        await DienstAsync(d => d.ZuweisungAufhebenAsync(ticket.Id, TestDaten.Teamleitung));

        var frisch = await GespeichertAsync(ticket.Id);
        Assert.Null(frisch.AgentId);
        Assert.Equal(TicketStatus.InProgress, frisch.Status);
        Assert.DoesNotContain(frisch.History, h => h.Field == "Status" && h.NewValue == TicketStatus.New.Anzeige());
    }

    [Fact]
    public async Task Ohne_Zuweisung_und_nach_dem_Schliessen_gibt_es_nichts_aufzuheben()
    {
        var ticket = await TicketAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DienstAsync(d => d.ZuweisungAufhebenAsync(ticket.Id, TestDaten.Teamleitung)));

        await DienstAsync(d => d.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung));
        await DienstAsync(d => d.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung, null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DienstAsync(d => d.ZuweisungAufhebenAsync(ticket.Id, TestDaten.Teamleitung)));
        Assert.Equal(TestDaten.Bearbeiter1.Id, (await GespeichertAsync(ticket.Id)).AgentId);
    }

    // Teamleitung aufwärts hebt über die Auswahl auf: „Niemand“ steht an
    // erster Stelle, aber nur, solange jemand zugewiesen ist. Der Knopf bleibt
    // dem Bearbeiter, der keine Auswahl hat und seinen Vorgang zurückgibt.
    [AvaloniaFact]
    public async Task Die_Teamleitung_hebt_ueber_die_Auswahl_auf_der_Bearbeiter_ueber_den_Knopf()
    {
        var ticket = await TicketAsync();

        var ohne = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        ohne.Show();
        await ohne.LadenAsync();
        Assert.False(ohne.ZuweisungAufheben.IsVisible);
        Assert.DoesNotContain(TicketdetailPresenter.Niemand, ohne.ZielWahl.Items.Select(i => i?.ToString()));
        ohne.Close();

        await DienstAsync(d => d.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung));

        var teamleitung = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        teamleitung.Show();
        await teamleitung.LadenAsync();
        Assert.False(teamleitung.ZuweisungAufheben.IsVisible);
        Assert.Equal(TicketdetailPresenter.Niemand, teamleitung.ZielWahl.Items[0]?.ToString());
        teamleitung.ZielWahl.SelectedIndex = 0;
        Assert.Equal("Aufheben", teamleitung.Zuweisen.Content);
        teamleitung.Zuweisen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 40 && (await GespeichertAsync(ticket.Id)).AgentId is not null; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.Null((await GespeichertAsync(ticket.Id)).AgentId);
        teamleitung.Close();

        await DienstAsync(d => d.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung));
        var eigener = new DetailFenster(_factory.Services, TestDaten.Bearbeiter1, ticket.Id);
        eigener.Show();
        await eigener.LadenAsync();
        Assert.True(eigener.ZuweisungAufheben.IsVisible);
        eigener.Close();
    }

    public void Dispose() => _factory.Dispose();
}
