using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Auch der Name im Satz führt zur Person. In der Zeile „Bearbeiter: leer zu
// Sabine W." war der Handelnde am Ende ein Knopf zur Kontokarte, der
// zugewiesene Name im Satz nicht, denn der Satz war eine Zeichenkette. Der
// Presenter liefert die Zeile deshalb als Teile: Text oder Name mit
// Kontokennung.
public sealed class VerlaufsteileTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Akteur> KontoAsync(string email, string anzeige, string nachname, string vorname)
    {
        using var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Nachname = nachname, Vorname = vorname, Anzeigename = anzeige
        };
        await konten.CreateAsync(konto, "Start-1234!");
        await konten.AddToRoleAsync(konto, Rollen.Editor);
        return new Akteur(konto.Id, Kontenname.Voll(konto), RoleLevel.Bearbeiter, anzeige);
    }

    private async Task<Ticket> TicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Meier, Anna", "0221123456",
            DateTime.UtcNow, null, TestDaten.Administration.Id, TestDaten.Administration.Name, "A-101");
    }

    private async Task<IReadOnlyList<VerlaufsEintrag>> VerlaufAsync(int ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        // Gebaut wie im Detailfenster: Der Presenter ist kein registrierter Dienst,
        // sondern entsteht je Ladevorgang aus dreien.
        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var ansicht = await presenter.LadenAsync(ticketId, TestDaten.Administration, DateTime.UtcNow);
        return ansicht!.Verlauf;
    }

    [Fact]
    public async Task Die_Zuweisungszeile_traegt_den_Bearbeiter_als_eigenen_Teil_mit_Kennung()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        var ticket = await TicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, sabine.Id, sabine.Name, TestDaten.Administration);
        }

        var zeile = Assert.Single(await VerlaufAsync(ticket.Id),
            e => e.Text.StartsWith("Bearbeiter:", StringComparison.Ordinal));

        Assert.Equal("Bearbeiter: leer zu Sabine W.", zeile.Text);
        Assert.Collection(zeile.Teile,
            t => Assert.Equal(("Bearbeiter: ", (string?)null), (t.Text, t.KontoId)),
            t => Assert.Equal(("leer", (string?)null), (t.Text, t.KontoId)),
            t => Assert.Equal((" zu ", (string?)null), (t.Text, t.KontoId)),
            t => Assert.Equal(("Sabine W.", (string?)sabine.Id), (t.Text, t.KontoId)));
        Assert.True(zeile.Teile[3].Anklickbar);
        Assert.False(zeile.Teile[1].Anklickbar);
        // Das Testkonto der Administration hat kein Konto in der Datenbank, also
        // bleibt der Handelnde Text.
        Assert.Equal(TestDaten.Administration.Name, zeile.Wer);
    }

    [Fact]
    public async Task Eine_Umverteilung_traegt_beide_Namen_mit_Kennung()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        var karl = await KontoAsync("karl@example.org", "Karl H.", "Huber", "Karl");
        var ticket = await TicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.AssignAsync(ticket.Id, sabine.Id, sabine.Name, TestDaten.Administration);
            await tickets.AssignAsync(ticket.Id, karl.Id, karl.Name, TestDaten.Administration);
        }

        var umverteilung = (await VerlaufAsync(ticket.Id))
            .Last(e => e.Text.StartsWith("Bearbeiter:", StringComparison.Ordinal));

        Assert.Equal("Bearbeiter: Sabine W. zu Karl H.", umverteilung.Text);
        Assert.Equal(sabine.Id, umverteilung.Teile[1].KontoId);
        Assert.Equal(karl.Id, umverteilung.Teile[3].KontoId);
    }

    // Gelöschtes Konto oder Import von woanders: Ein Klick darauf wäre ein
    // Versprechen ohne Ziel. Zuweisen prüft das Zielkonto, deshalb steht die
    // Zeile hier direkt in der Historie.
    [Fact]
    public async Task Ein_Name_ohne_Konto_im_Satz_bleibt_Text_und_eine_Statuszeile_ist_ein_Teil()
    {
        var ticket = await TicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
            var akte = await db.Tickets.SingleAsync(t => t.Id == ticket.Id);
            akte.History.Add(new TicketHistoryEntry
            {
                Field = "Agent", OldValue = null, NewValue = "Huber, Karl",
                ChangedBy = TestDaten.Administration.Name, ChangedById = TestDaten.Administration.Id,
                ChangedAt = DateTime.UtcNow
            });
            akte.History.Add(new TicketHistoryEntry
            {
                Field = "Status", OldValue = "Neu", NewValue = "Zugewiesen",
                ChangedBy = TestDaten.Administration.Name, ChangedById = TestDaten.Administration.Id,
                ChangedAt = DateTime.UtcNow.AddSeconds(1)
            });
            await db.SaveChangesAsync();
        }

        var verlauf = await VerlaufAsync(ticket.Id);
        var zuweisung = Assert.Single(verlauf, e => e.Text.StartsWith("Bearbeiter:", StringComparison.Ordinal));
        Assert.Equal("Bearbeiter: leer zu Huber, Karl", zuweisung.Text);
        Assert.Null(zuweisung.Teile[3].KontoId);
        Assert.All(zuweisung.Teile, t => Assert.False(t.Anklickbar));

        var status = Assert.Single(verlauf, e => e.Text.StartsWith("Status:", StringComparison.Ordinal));
        var teil = Assert.Single(status.Teile);
        Assert.Equal("Status: Neu zu Zugewiesen", teil.Text);
        Assert.Null(teil.KontoId);
    }

    [AvaloniaFact]
    public async Task Im_Detailfenster_ist_der_zugewiesene_Name_ein_Knopf_zur_Kontokarte()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        var ticket = await TicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, sabine.Id, sabine.Name, TestDaten.Administration);
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();
        Messen.Auslegen(fenster, 1180, 760);

        var knoepfe = fenster.Verlauf.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsVisible && b.Classes.Contains("zelle"))
            .ToList();
        var zugewiesen = Assert.Single(knoepfe, b => Equals(b.Content, "Sabine W."));
        Assert.Equal(sabine.Id, zugewiesen.CommandParameter);
        Assert.NotNull(zugewiesen.Command);
        Assert.DoesNotContain(knoepfe, b => Equals(b.Content, "leer"));
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
