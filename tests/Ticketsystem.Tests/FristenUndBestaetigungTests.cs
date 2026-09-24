using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Benachrichtigung ohne Server: der Fristenstand in der Anwendung und die
// Eingangsbestätigung mit ihrem Auslöser. Beides sind Zusagen an den
// Betrieb, nicht an die Oberfläche, deshalb stehen die Tests am Dienst.
public sealed class FristenUndBestaetigungTests : IDisposable
{
    private sealed class FakeMailer : ITicketMailer
    {
        public List<(string An, string Betreff, string Text)> Gesendet { get; } = [];

        public bool Streikt { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            if (Streikt)
            {
                throw new InvalidOperationException("Postfach nicht erreichbar.");
            }

            Gesendet.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _tickets;
    private readonly FakeMailer _mailer = new();

    public FristenUndBestaetigungTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _tickets = new TicketService(_db);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter2);
    }

    private BestaetigungsMelder Melder() =>
        new(_mailer, NullLogger<BestaetigungsMelder>.Instance);

    // Verschiebt die Fälligkeiten, statt auf die Uhr zu warten.
    private async Task<Ticket> TicketMitFristAsync(
        string titel, TimeSpan reaktionIn, string? bearbeiterId = null)
    {
        var ticket = await _tickets.CreateAsync(titel, "Text", TicketPriority.Medium,
            "A. Beispiel", TicketSource.Web,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name);

        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        gespeichert.CreatedAt = DateTime.UtcNow.AddHours(-8);
        gespeichert.ReactionDueAt = DateTime.UtcNow.Add(reaktionIn);
        gespeichert.ResolutionDueAt = DateTime.UtcNow.AddDays(3);
        gespeichert.AgentId = bearbeiterId;
        await _db.SaveChangesAsync();
        return gespeichert;
    }

    [Fact]
    public async Task Der_Fristenstand_trennt_ueberfaellig_von_bald_faellig()
    {
        await TicketMitFristAsync("Lange abgelaufen", TimeSpan.FromHours(-3));
        await TicketMitFristAsync("Gleich fällig", TimeSpan.FromMinutes(20));
        await TicketMitFristAsync("Noch viel Zeit", TimeSpan.FromDays(2));

        var stand = await _tickets.FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);

        Assert.Equal(1, stand.Ueberfaellig);
        Assert.Equal(1, stand.BaldFaellig);
        Assert.True(stand.Handlungsbedarf);
    }

    [Fact]
    public async Task Erledigte_Vorgaenge_zaehlen_nicht_mehr_mit()
    {
        var ticket = await TicketMitFristAsync("Erledigt", TimeSpan.FromHours(-3));
        ticket.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var stand = await _tickets.FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);

        Assert.Equal(0, stand.Ueberfaellig);
        Assert.False(stand.Handlungsbedarf);
    }

    // Die Anzeige darf keine Zahl verraten, die die Liste verschweigt.
    [Fact]
    public async Task Der_Fristenstand_zeigt_nur_sichtbare_Vorgaenge()
    {
        await TicketMitFristAsync("Fremd zugewiesen", TimeSpan.FromHours(-3), TestDaten.Bearbeiter1.Id);

        var eigener = await _tickets.FristenstandAsync(TestDaten.Bearbeiter1, DateTime.UtcNow);
        var fremder = await _tickets.FristenstandAsync(TestDaten.Bearbeiter2, DateTime.UtcNow);

        Assert.Equal(1, eigener.Ueberfaellig);
        Assert.Equal(0, fremder.Ueberfaellig);
    }

    [Fact]
    public async Task Kunden_sehen_keinen_Fristenstand()
    {
        await TicketMitFristAsync("Überfällig", TimeSpan.FromHours(-3));

        var stand = await _tickets.FristenstandAsync(TestDaten.OhneRolle, DateTime.UtcNow);

        Assert.False(stand.Handlungsbedarf);
    }

    [Fact]
    public async Task Ohne_erfasste_Adresse_geht_keine_Bestaetigung_hinaus()
    {
        var ticket = await _tickets.CreateAsync("Anruf ohne Mailadresse", "Text",
            TicketPriority.Medium, "A. Beispiel", TicketSource.Phone);

        var versandt = await Melder().VersendenAsync(ticket);

        Assert.False(versandt);
        Assert.Empty(_mailer.Gesendet);
    }

    [Fact]
    public async Task Mit_erfasster_Adresse_geht_die_Ticketnummer_hinaus()
    {
        var ticket = await _tickets.CreatePhoneAsync("Drucker klemmt", "Text",
            TicketPriority.Medium, "A. Beispiel", "0221123456", DateTime.UtcNow, null,
            customerEmail: "a.beispiel@example.com");

        var versandt = await Melder().VersendenAsync(ticket);

        Assert.True(versandt);
        var mail = Assert.Single(_mailer.Gesendet);
        Assert.Equal("a.beispiel@example.com", mail.An);
        Assert.Contains($"#{ticket.Id}", mail.Betreff);
        Assert.Contains($"#{ticket.Id}", mail.Text);
    }

    // Der Anrufer steht gerade am Telefon; ein unerreichbares Postfach darf ihn
    // nicht die Ticketnummer kosten.
    [Fact]
    public async Task Ein_Mailfehler_kostet_nicht_das_Ticket()
    {
        _mailer.Streikt = true;
        var ticket = await _tickets.CreateAsync("Mit Adresse", "Text", TicketPriority.Medium,
            "A. Beispiel", TicketSource.Web, customerEmail: "a.beispiel@example.com");

        var versandt = await Melder().VersendenAsync(ticket);

        Assert.False(versandt);
        Assert.Equal(1, await _db.Tickets.CountAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
