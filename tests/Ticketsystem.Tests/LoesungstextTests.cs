using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Lösungstext beim Übergang auf Gelöst. Der Fall: WLAN in B-204 gelöst,
// drei Wochen später findet der Kollege über die Raumhistorie das Problem,
// aber keine Zeile zur Lösung, und löst neu. Bewusst kein eigenes Feld am
// Ticket: Der Kommentarstrom ist in Export, Sicherung, Historie und DSGVO-Wege
// schon eingebunden; ein neues Feld wäre überall ein neuer Sonderfall.
public sealed class LoesungstextTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [Fact]
    public async Task Geloest_mit_Loesungstext_speichert_den_Text_als_Kommentar()
    {
        using var scope = _factory.Services.CreateScope();
        var (tickets, db) = Dienste(scope);
        var id = await TicketAnlegenAsync(tickets);

        await tickets.ChangeStatusAsync(id, TicketStatus.Resolved, TestDaten.Teamleitung,
            loesung: "  Funkprofil neu angelegt, danach sofort Verbindung.  ");

        Assert.Equal(TicketStatus.Resolved, (await db.Tickets.SingleAsync(t => t.Id == id)).Status);
        var kommentar = Assert.Single(await db.TicketComments.Where(k => k.TicketId == id).ToListAsync());
        Assert.StartsWith("Lösung:", kommentar.Text);
        Assert.Contains("Funkprofil neu angelegt", kommentar.Text);
        Assert.DoesNotContain("  Funkprofil", kommentar.Text);
    }

    [Fact]
    public async Task Geloest_ohne_Text_erzeugt_keinen_leeren_Kommentar()
    {
        using var scope = _factory.Services.CreateScope();
        var (tickets, db) = Dienste(scope);
        var id = await TicketAnlegenAsync(tickets);

        await tickets.ChangeStatusAsync(id, TicketStatus.Resolved, TestDaten.Teamleitung, loesung: "   ");

        Assert.Equal(TicketStatus.Resolved, (await db.Tickets.SingleAsync(t => t.Id == id)).Status);
        Assert.Empty(await db.TicketComments.Where(k => k.TicketId == id).ToListAsync());
    }

    // Sonst hinge ein „Lösung:"-Kommentar an einem Vorgang, der gar nicht gelöst
    // ist, und der Artikelentwurf trüge ihn als Lösung weiter.
    [Fact]
    public async Task Ausserhalb_von_Geloest_wird_kein_Loesungstext_angenommen()
    {
        using var scope = _factory.Services.CreateScope();
        var (tickets, db) = Dienste(scope);
        var id = await TicketAnlegenAsync(tickets);

        await tickets.ChangeStatusAsync(id, TicketStatus.Closed, TestDaten.Teamleitung,
            loesung: "Verirrter Text.");

        Assert.Empty(await db.TicketComments.Where(k => k.TicketId == id).ToListAsync());
    }

    [Fact]
    public async Task Der_Artikelentwurf_nimmt_die_Loesung_mit()
    {
        using var scope = _factory.Services.CreateScope();
        var (tickets, _) = Dienste(scope);
        var id = await TicketAnlegenAsync(tickets);
        await tickets.ChangeStatusAsync(id, TicketStatus.Resolved, TestDaten.Teamleitung,
            loesung: "Funkprofil neu angelegt.");

        var ansicht = await LadenAsync(scope, id);

        Assert.Contains("Kein Netz im ganzen Raum.", ansicht.EntwurfsInhalt);
        Assert.Contains("Lösung: Funkprofil neu angelegt.", ansicht.EntwurfsInhalt);
    }

    [Fact]
    public async Task Ohne_Loesung_traegt_der_Entwurf_nur_das_Problem()
    {
        using var scope = _factory.Services.CreateScope();
        var (tickets, _) = Dienste(scope);
        var id = await TicketAnlegenAsync(tickets);

        var ansicht = await LadenAsync(scope, id);

        Assert.Equal("Kein Netz im ganzen Raum.", ansicht.EntwurfsInhalt);
    }

    private static (TicketService Tickets, TicketsystemContext Db) Dienste(IServiceScope scope) => (
        scope.ServiceProvider.GetRequiredService<TicketService>(),
        scope.ServiceProvider.GetRequiredService<TicketsystemContext>());

    private static async Task<DetailAnsicht> LadenAsync(IServiceScope scope, int id)
    {
        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var ansicht = await presenter.LadenAsync(id, TestDaten.Teamleitung, DateTime.UtcNow);
        Assert.NotNull(ansicht);
        return ansicht!;
    }

    private static async Task<int> TicketAnlegenAsync(TicketService tickets)
    {
        var ticket = await tickets.CreatePhoneAsync("WLAN tot in B-204", "Kein Netz im ganzen Raum.",
            TicketPriority.Medium, "Weber, Sabine", "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Teamleitung.Id, createdByName: TestDaten.Teamleitung.Name,
            address: "B-204");
        // Gelöst ist nur aus „In Arbeit" erreichbar.
        await tickets.ChangeStatusAsync(ticket.Id, TicketStatus.Assigned, TestDaten.Teamleitung);
        await tickets.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, TestDaten.Teamleitung);
        return ticket.Id;
    }

    public void Dispose() => _factory.Dispose();
}
