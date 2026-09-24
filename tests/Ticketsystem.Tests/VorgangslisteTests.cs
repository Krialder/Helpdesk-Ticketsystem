using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Alle Vorgänge zu einem Kunden oder zu einem Raum. Beim zweiten Anruf
// derselben Person ist „was war beim ersten Mal?" die erste Frage, und sie
// soll nicht über die Suche beantwortet werden müssen.
public sealed class VorgangslisteTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(string titel, string kunde, string ort)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            titel, "Papierstau.", TicketPriority.Medium, kunde, "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, ort);
    }

    private async Task BestandAsync()
    {
        await TicketAsync("Drucker klemmt", "Weber, Sabine", "A-101");
        await TicketAsync("Maus kaputt", "Weber, Sabine", "B-12");
        await TicketAsync("Bildschirm dunkel", "Huber, Karl", "A-101");
        // Zwei Namen, die den gesuchten enthalten, aber nicht er sind. Ohne sie
        // liefe eine Teiltreffer-Suche zufällig richtig durch den Test.
        await TicketAsync("Netzwerk weg", "Weberling, Tom", "C-3");
        await TicketAsync("Vertretung meldet", "Weber, Sabine (Vertretung)", "D-4");
    }

    [Fact]
    public async Task Der_Kundenfilter_ist_exakt_und_keine_Suche()
    {
        await BestandAsync();
        using var scope = _factory.Services.CreateScope();
        var presenter = new TicketlistenPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());

        var ergebnis = await presenter.LadenAsync(
            TestDaten.Teamleitung, "Alle", null, false, null, DateTime.UtcNow, kunde: "weber, sabine");

        Assert.Equal(["Maus kaputt", "Drucker klemmt"], ergebnis.Zeilen.Select(z => z.Titel));
    }

    [Fact]
    public async Task Der_Raumfilter_findet_die_Vorgeschichte_eines_Ortes()
    {
        await BestandAsync();
        using var scope = _factory.Services.CreateScope();
        var presenter = new TicketlistenPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());

        var ergebnis = await presenter.LadenAsync(
            TestDaten.Teamleitung, "Alle", null, false, null, DateTime.UtcNow, adresse: "A-1");

        // „A-1" ist kein Präfix-Treffer auf A-101, sonst zöge „A-1" die ganze Etage
        // herein.
        Assert.Empty(ergebnis.Zeilen);

        var genau = await presenter.LadenAsync(
            TestDaten.Teamleitung, "Alle", null, false, null, DateTime.UtcNow, adresse: "a-101");
        Assert.Equal(["Bildschirm dunkel", "Drucker klemmt"], genau.Zeilen.Select(z => z.Titel));
    }

    // Ein eigener Weg in den Bestand darf keine zweite Wahrheit über die
    // Sichtbarkeit sein.
    [Fact]
    public async Task Die_Sichtbarkeitsregel_gilt_auch_hier()
    {
        await BestandAsync();
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<TicketService>();
        var db = scope.ServiceProvider.GetRequiredService<Ticketsystem.Kern.Data.TicketsystemContext>();
        var anderer = new Akteur("anderer-1", "Anderer, Anna", RoleLevel.Bearbeiter);
        TestDaten.KontoAnlegen(db, anderer);
        var fremd = (await dienst.ListAsync(TestDaten.Teamleitung)).Zeilen.First(t => t.Title == "Drucker klemmt");
        await dienst.AssignAsync(fremd.Id, anderer.Id, anderer.Name, TestDaten.Teamleitung);

        var presenter = new TicketlistenPresenter(dienst, scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var ergebnis = await presenter.LadenAsync(
            TestDaten.Bearbeiter1, "Alle", null, false, null, DateTime.UtcNow, kunde: "Weber, Sabine");

        Assert.Equal(["Maus kaputt"], ergebnis.Zeilen.Select(z => z.Titel));
    }

    [AvaloniaFact]
    public async Task Das_Fenster_nennt_den_Kunden_und_zaehlt_die_Vorgaenge()
    {
        await BestandAsync();

        var fenster = new VorgangslisteFenster(_factory.Services, TestDaten.Teamleitung, Vorgangsfilter.FuerKunde("Weber, Sabine"));
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("Vorgänge zu Weber, Sabine", fenster.Ueberschrift.Text);
        Assert.Equal("2 Vorgänge", fenster.Zaehler.Text);
        Assert.False(fenster.Leertext.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ohne_Vorgeschichte_sagt_das_Fenster_das_auch()
    {
        var fenster = new VorgangslisteFenster(_factory.Services, TestDaten.Teamleitung, Vorgangsfilter.FuerOrt("Z-99"));
        fenster.Show();
        await fenster.LadenAsync();

        Assert.True(fenster.Leertext.IsVisible);
        Assert.Contains("Z-99", fenster.Leertext.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Auch_Geschlossenes_gehoert_zur_Vorgeschichte()
    {
        await BestandAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<TicketService>();
            var zu = (await dienst.ListAsync(TestDaten.Teamleitung)).Zeilen.First(t => t.Title == "Maus kaputt");
            await dienst.ChangeStatusAsync(zu.Id, TicketStatus.Closed, TestDaten.Teamleitung);
        }

        var fenster = new VorgangslisteFenster(_factory.Services, TestDaten.Teamleitung, Vorgangsfilter.FuerKunde("Weber, Sabine"));
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("2 Vorgänge", fenster.Zaehler.Text);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
