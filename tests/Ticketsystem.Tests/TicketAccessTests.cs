using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Sichtbarkeit und Kommentarrecht liegen im Dienst, nicht in der Oberfläche,
// deshalb werden sie auch dort geprüft. Ein rollenloses Konto ist ein
// Altbestand ohne Rechte, kein Nutzertyp: Es sieht und kommentiert nichts,
// auch nicht die Tickets, deren CustomerId auf es zeigt.
public sealed class TicketAccessTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public TicketAccessTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter2);
    }

    // Ein Altticket aus der Portalzeit: CreateAsync kennt keine customerId mehr,
    // der Bestand schon, deshalb wird das Feld nachträglich gesetzt.
    private async Task<Ticket> TicketVonAsync(string kundenId)
    {
        var ticket = await _service.CreateAsync("Titel", "Beschreibung", TicketPriority.Medium,
            $"kunde-{kundenId}@example.net", TicketSource.Web);
        ticket.CustomerId = kundenId;
        await _db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task Ein_rollenloses_Konto_sieht_auch_eigene_Alttickets_nicht_mehr()
    {
        await TicketVonAsync("kunde-1");
        await TicketVonAsync("kunde-2");

        Assert.Empty((await _service.ListAsync(TestDaten.OhneRolle)).Zeilen);
    }

    [Fact]
    public async Task Ohne_Rolle_findet_fremdes_Ticket_nicht()
    {
        var fremdes = await TicketVonAsync("kunde-2");

        var gefunden = await _service.FindForUserAsync(fremdes.Id, TestDaten.OhneRolle);

        Assert.Null(gefunden);
    }

    [Fact]
    public async Task Agent_findet_jedes_Ticket()
    {
        var ticket = await TicketVonAsync("kunde-2");

        var gefunden = await _service.FindForUserAsync(ticket.Id, TestDaten.Teamleitung);

        Assert.NotNull(gefunden);
    }

    [Fact]
    public async Task Ein_rollenloses_Konto_kommentiert_auch_eigene_Alttickets_nicht_mehr()
    {
        var ticket = await TicketVonAsync("kunde-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddCommentAsync(ticket.Id, TestDaten.OhneRolle, "Klappt immer noch nicht."));

        Assert.Empty(await _db.TicketComments.ToListAsync());
    }

    [Fact]
    public async Task Ohne_Rolle_kommentiert_fremdes_Ticket_nicht()
    {
        var fremdes = await TicketVonAsync("kunde-2");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddCommentAsync(fremdes.Id, TestDaten.OhneRolle, "Hallo?"));

        Assert.Empty(await _db.TicketComments.ToListAsync());
    }

    [Fact]
    public async Task Geschlossenes_Ticket_nimmt_keine_Kommentare_mehr_an()
    {
        var ticket = await TicketVonAsync("kunde-1");
        await _service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddCommentAsync(ticket.Id, TestDaten.OhneRolle, "Noch eine Frage."));
    }

    [Fact]
    public async Task Meine_Filter_liefert_nur_dem_Agenten_zugewiesene_Tickets()
    {
        var meins = await TicketVonAsync("kunde-1");
        await _service.AssignAsync(meins.Id, TestDaten.Bearbeiter1.Id, TestDaten.Bearbeiter1.Name, TestDaten.Teamleitung);
        var fremdes = await TicketVonAsync("kunde-2");
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Bearbeiter1, nurMeine: true)).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(meins.Id, einziges.Id);
    }

    // Die Historie zeigt den ganzen Vorgang, nicht nur Feldänderungen; der
    // Volltext bleibt im Kommentar, die Historie führt einen Auszug.
    [Fact]
    public async Task Kommentar_erscheint_auch_in_der_Historie()
    {
        var ticket = await TicketVonAsync("kunde-1");

        await _service.AddCommentAsync(ticket.Id, TestDaten.Bearbeiter1, "Der Drucker klemmt weiterhin.");

        var eintrag = Assert.Single(await _db.TicketHistory.Where(h => h.TicketId == ticket.Id).ToListAsync());
        Assert.Equal("Kommentar", eintrag.Field);
        Assert.Equal("Der Drucker klemmt weiterhin.", eintrag.NewValue);
        Assert.Equal(TestDaten.Bearbeiter1.Name, eintrag.ChangedBy);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
