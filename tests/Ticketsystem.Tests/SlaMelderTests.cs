using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Mail bei gerissener Frist geht an Teamleitung und Administration,
// pausierte Konten bleiben außen vor, und jedes Ticket wird höchstens einmal
// gemeldet.
public sealed class SlaMelderTests : IDisposable
{
    private sealed class FakeMailer : ITicketMailer
    {
        public List<string> Empfaenger { get; } = [];

        public List<string> Texte { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            Empfaenger.Add(to);
            Texte.Add(body);
            return Task.CompletedTask;
        }
    }

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly TicketsystemContext _db;
    private readonly FakeMailer _mailer = new();

    public SlaMelderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Echter Identity-Stack, damit Rollenzuordnung und Pausierung wie im
        // Betrieb greifen; nur der Mailversand ist ersetzt.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TicketsystemContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<AppUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TicketsystemContext>();
        _provider = services.BuildServiceProvider();

        _db = _provider.GetRequiredService<TicketsystemContext>();
        _db.Database.EnsureCreated();
    }

    private async Task<AppUser> KontoAsync(string email, string rolle, DateTime? pausiertBis = null)
    {
        var roleManager = _provider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(rolle))
        {
            await roleManager.CreateAsync(new IdentityRole(rolle));
        }

        var users = _provider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser { UserName = email, Email = email, PausedUntil = pausiertBis };
        await users.CreateAsync(konto);
        await users.AddToRoleAsync(konto, rolle);
        return konto;
    }

    private async Task<Ticket> GerissenesTicketAsync()
    {
        var tickets = new TicketService(_db);
        var ticket = await tickets.CreateAsync("Server down", "Text", TicketPriority.Critical,
            "A. Beispiel", TicketSource.Web);
        var gespeichert = await _db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        gespeichert.ReactionDueAt = DateTime.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync();
        return gespeichert;
    }

    private SlaMelder Melder() =>
        new(_db, _provider.GetRequiredService<UserManager<AppUser>>(), _mailer);

    [Fact]
    public async Task Teamleitung_wird_bei_gerissener_Frist_benachrichtigt()
    {
        await KontoAsync("teamleitung@example.org", Rollen.TeamLead);
        await GerissenesTicketAsync();

        var anzahl = await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);

        Assert.Equal(1, anzahl);
        Assert.Equal(["teamleitung@example.org"], _mailer.Empfaenger);
    }

    // Ohne Server prüft die Anwendung nur, während sie läuft. Eine Meldung vom
    // Montag kann eine Frist vom Freitag betreffen.
    [Fact]
    public async Task Die_Meldung_nennt_den_Ablauf_und_nicht_den_Versand()
    {
        await KontoAsync("teamleitung@example.org", Rollen.TeamLead);
        var ticket = await GerissenesTicketAsync();

        await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);

        var text = Assert.Single(_mailer.Texte);
        Assert.Contains($"abgelaufen am {ticket.ReactionDueAt:dd.MM.yyyy HH:mm}", text);
        Assert.Contains("nicht der Zeitpunkt dieser Mail", text);
    }

    [Fact]
    public async Task Pausiertes_Konto_bekommt_keine_Meldung()
    {
        await KontoAsync("abwesend@example.org", Rollen.TeamLead, DateTime.UtcNow.AddDays(7));
        await GerissenesTicketAsync();

        await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);

        Assert.Empty(_mailer.Empfaenger);
    }

    [Fact]
    public async Task Bearbeiter_bekommt_keine_Meldung()
    {
        await KontoAsync("bearbeiter@example.org", Rollen.Editor);
        await GerissenesTicketAsync();

        await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);

        Assert.Empty(_mailer.Empfaenger);
    }

    [Fact]
    public async Task Dasselbe_Ticket_wird_nur_einmal_gemeldet()
    {
        await KontoAsync("teamleitung@example.org", Rollen.TeamLead);
        await GerissenesTicketAsync();

        await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);
        var zweiterLauf = await Melder().MeldeVerletzungenAsync(DateTime.UtcNow);

        Assert.Equal(0, zweiterLauf);
        Assert.Single(_mailer.Empfaenger);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
