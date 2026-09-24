using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Anrufer sagt „das Ticket wegen des Druckers von letzter Woche", und der
// Bearbeiter kennt weder Nummer noch Status. Der Suchparameter sitzt in
// ListAsync neben den bestehenden Filtern, damit die Sichtbarkeitsregel
// ungefragt weiter gilt; genau das hält der Verbots-Test fest.
public sealed class SucheTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public SucheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter2);
    }

    private Task<Ticket> TicketAsync(string titel, string beschreibung, string kunde) =>
        _service.CreatePhoneAsync(titel, beschreibung, TicketPriority.Medium, kunde,
            "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name);

    [Theory]
    [InlineData("Drucker")]
    [InlineData("drucker")]
    [InlineData("Papierstau")]
    [InlineData("Weber")]
    public async Task Die_Suche_findet_ueber_Titel_Beschreibung_und_Kundenname(string suche)
    {
        var treffer = await TicketAsync("Drucker klemmt", "Papierstau im Fach 2.", "Weber, Sabine");
        await TicketAsync("Zugang fehlt", "Kein Zugriff auf das Laufwerk.", "Huber, Karl");

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, suche: suche)).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(treffer.Id, einziges.Id);
    }

    [Theory]
    [InlineData("{0}")]
    [InlineData("#{0}")]
    public async Task Die_Suche_findet_die_Ticketnummer_mit_und_ohne_Raute(string muster)
    {
        var gesucht = await TicketAsync("Drucker klemmt", "Papierstau.", "Weber, Sabine");
        await TicketAsync("Zugang fehlt", "Kein Zugriff.", "Huber, Karl");

        var liste = (await _service.ListAsync(TestDaten.Teamleitung,
            suche: string.Format(muster, gesucht.Id))).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(gesucht.Id, einziges.Id);
    }

    // Das fremde Ticket ist von einem anderen erstellt und einem anderen
    // zugewiesen, denn Bearbeiter sehen Eigenes, Zugewiesenes und Unzugewiesenes.
    // Ließe Bearbeiter1 es selbst erstellen, bewiese der Test nur, dass
    // Ersteller sehen dürfen.
    [Fact]
    public async Task Die_Suche_umgeht_die_Sichtbarkeitsregel_nicht()
    {
        var fremdes = await _service.CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Weber, Sabine",
            "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter2.Id, createdByName: TestDaten.Bearbeiter2.Name);
        await _service.AssignAsync(fremdes.Id, TestDaten.Bearbeiter2.Id, TestDaten.Bearbeiter2.Name, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Bearbeiter1, suche: "Drucker")).Zeilen;

        Assert.Empty(liste);
    }

    [Fact]
    public async Task Die_Suche_arbeitet_mit_den_Filtern_zusammen_statt_gegen_sie()
    {
        var offen = await TicketAsync("Drucker klemmt", "Papierstau.", "Weber, Sabine");
        var geschlossen = await TicketAsync("Drucker piept", "Tonerfehler.", "Weber, Sabine");
        await _service.ChangeStatusAsync(geschlossen.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        var liste = (await _service.ListAsync(TestDaten.Teamleitung, nurOffene: true, suche: "Drucker")).Zeilen;

        var einziges = Assert.Single(liste);
        Assert.Equal(offen.Id, einziges.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Eine_leere_Suche_filtert_nichts(string? suche)
    {
        await TicketAsync("Drucker klemmt", "Papierstau.", "Weber, Sabine");
        await TicketAsync("Zugang fehlt", "Kein Zugriff.", "Huber, Karl");

        Assert.Equal(2, (await _service.ListAsync(TestDaten.Teamleitung, suche: suche)).Zeilen.Count);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
