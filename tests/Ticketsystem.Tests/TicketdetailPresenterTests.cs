using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Presenter entscheidet, welche Knöpfe und Übergänge das Detailfenster
// anbietet. Der Dienst weist jede unerlaubte Aktion zwar ab (zwei
// Schlösser), aber mit einem Fehler hier hätte die Oberfläche gelogen.
public sealed class TicketdetailPresenterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private (TicketdetailPresenter Presenter, TicketService Tickets, IServiceScope Scope) Aufbau()
    {
        var scope = _factory.Services.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<Ticketsystem.Kern.Data.TicketsystemContext>();
        // Zuweisen prüft, ob das Zielkonto existiert; der Wirt kennt nur das
        // Seed-Konto.
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(db, TestDaten.Bearbeiter2);
        return (new TicketdetailPresenter(tickets, users,
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>()), tickets, scope);
    }

    [Fact]
    public async Task Ein_unsichtbarer_Vorgang_liefert_null()
    {
        var (presenter, tickets, scope) = Aufbau();
        using var _ = scope;
        var fremdes = await tickets.CreatePhoneAsync("Fremd", "x", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter2.Id, createdByName: TestDaten.Bearbeiter2.Name);
        await tickets.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        Assert.Null(await presenter.LadenAsync(fremdes.Id, TestDaten.Bearbeiter1, DateTime.UtcNow));
    }

    [Fact]
    public async Task Eine_Mail_heisst_Eingangsdaten_ohne_Rueckrufnummer()
    {
        var (presenter, tickets, scope) = Aufbau();
        using var _ = scope;
        var mail = await tickets.CreateEmailAsync("Mail: Drucker", "Text.", TicketPriority.Medium,
            "Weber, Sabine", "s.weber@example.com", DateTime.UtcNow,
            TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name);

        var ansicht = await presenter.LadenAsync(mail.Id, TestDaten.Teamleitung, DateTime.UtcNow);

        Assert.NotNull(ansicht);
        Assert.Equal("Eingangsdaten", ansicht!.EingangsTitel);
        Assert.Equal("Eingegangen", ansicht.ZeitBeschriftung);
        Assert.False(ansicht.ZeigeRueckruf);
    }

    [Fact]
    public async Task Geschlossen_ist_endgueltig_ohne_Uebergaenge()
    {
        var (presenter, tickets, scope) = Aufbau();
        using var _ = scope;
        var ticket = await tickets.CreatePhoneAsync("Zu", "x", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Teamleitung.Id, createdByName: TestDaten.Teamleitung.Name);
        await tickets.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        var ansicht = await presenter.LadenAsync(ticket.Id, TestDaten.Teamleitung, DateTime.UtcNow);

        Assert.True(ansicht!.Geschlossen);
        Assert.Empty(ansicht.NaechsteStatus);
    }

    [Fact]
    public async Task Zuweisen_ist_Teamleitungssache_Selbstzuweisung_Bearbeitersache()
    {
        var (presenter, tickets, scope) = Aufbau();
        using var _ = scope;
        var unzugewiesen = await tickets.CreatePhoneAsync("Frei", "x", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name);

        var teamleitung = await presenter.LadenAsync(unzugewiesen.Id, TestDaten.Teamleitung, DateTime.UtcNow);
        var bearbeiter = await presenter.LadenAsync(unzugewiesen.Id, TestDaten.Bearbeiter1, DateTime.UtcNow);

        Assert.True(teamleitung!.DarfZuweisen);
        Assert.False(teamleitung.DarfSelbstZuweisen);
        Assert.False(bearbeiter!.DarfZuweisen);
        Assert.True(bearbeiter.DarfSelbstZuweisen);
    }

    [Fact]
    public async Task Ein_zugewiesener_Vorgang_bietet_keine_Selbstzuweisung_mehr()
    {
        var (presenter, tickets, scope) = Aufbau();
        using var _ = scope;
        var ticket = await tickets.CreatePhoneAsync("Vergeben", "x", TicketPriority.Medium,
            "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name);
        await tickets.AssignAsync(ticket.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung);

        var ansicht = await presenter.LadenAsync(ticket.Id, TestDaten.Bearbeiter1, DateTime.UtcNow);

        Assert.False(ansicht!.DarfSelbstZuweisen);
    }

    public void Dispose() => _factory.Dispose();
}
