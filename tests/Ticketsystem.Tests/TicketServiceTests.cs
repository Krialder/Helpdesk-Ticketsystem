using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// SQLite im Speicher statt EF-InMemory-Provider: dieselbe Engine wie im
// Betrieb, also gelten dieselben Constraints. Die Verbindung bleibt offen,
// weil SQLite die Speicherdatenbank sonst zwischen zwei Zugriffen verwirft.
public sealed class TicketServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TicketsystemContext(options);
        // EnsureCreated statt Migrate: Geprüft wird das Modell, nicht die
        // Migrationskette; die hat ihre eigenen Tests.
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
    }

    [Fact]
    public async Task CreateAsync_startet_jedes_Ticket_im_Status_Neu()
    {
        var ticket = await _service.CreateAsync(
            "Drucker druckt nicht", "Fehlermeldung Papierstau, aber kein Papier im Gerät.",
            TicketPriority.High, "A. Beispiel", TicketSource.Web);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.New, gespeichert.Status);
        Assert.Equal(TicketSource.Web, gespeichert.Source);
        Assert.NotEqual(default, gespeichert.CreatedAt);
        Assert.Equal(gespeichert.CreatedAt, gespeichert.UpdatedAt);
    }

    [Fact]
    public async Task ListAsync_liefert_das_neueste_Ticket_zuerst()
    {
        var erstes = await _service.CreateAsync(
            "Altes Ticket", "Beschreibung", TicketPriority.Low, "A. Beispiel", TicketSource.Web);
        var zweites = await _service.CreateAsync(
            "Neues Ticket", "Beschreibung", TicketPriority.Low, "A. Beispiel", TicketSource.Web);

        var liste = (await _service.ListAsync(TestDaten.Teamleitung)).Zeilen;

        Assert.Equal(2, liste.Count);
        Assert.Equal(zweites.Id, liste[0].Id);
        Assert.Equal(erstes.Id, liste[1].Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
