using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Vorgang im Vordergrund: links der Fall als Faden (Beschreibung als
// erster Beitrag, Kommentare als Blasen, Änderungen als leise Zeilen
// dazwischen, in Zeitfolge), rechts eine Seitenspalte, in der jede Angabe
// genau einmal steht, direkt bei dem Knopf, der sie ändert.
public sealed class VordergrundTests : IDisposable
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

    // Angelegt, zugewiesen, kommentiert, umgestuft: vier Ereignisse in dieser
    // Reihenfolge.
    private async Task<(Ticket Ticket, Akteur Sabine)> VorgangAsync()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        using var scope = _factory.Services.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        var ticket = await tickets.CreatePhoneAsync("Drucker klemmt", "Papierstau im Fach 2.", TicketPriority.Medium,
            "Meier, Anna", "0221123456", DateTime.UtcNow, "Klemmt seit heute früh.",
            TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
        await tickets.AssignAsync(ticket.Id, sabine.Id, sabine.Name, TestDaten.Teamleitung);
        await tickets.AddCommentAsync(ticket.Id, sabine, "Netzteil bestellt.");
        await tickets.ChangePriorityAsync(ticket.Id, TicketPriority.High, TestDaten.Teamleitung);
        return (ticket, sabine);
    }

    // Nichts steht doppelt: Der Kommentar ist die Blase, nicht zusätzlich die
    // Zeile „Kommentar", und das Anlegen ist der erste Beitrag, nicht
    // zusätzlich eine Zeile „Vorgang angelegt".
    [Fact]
    public async Task Der_Verlauf_ist_ein_Faden_aus_Kommentaren_und_Aenderungen_in_Zeitfolge()
    {
        var (ticket, sabine) = await VorgangAsync();

        using var scope = _factory.Services.CreateScope();
        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var ansicht = await presenter.LadenAsync(ticket.Id, TestDaten.Teamleitung, DateTime.UtcNow);

        var verlauf = ansicht!.Verlauf;
        Assert.Equal(verlauf.OrderBy(e => e.Zeitpunkt).Select(e => e.Text), verlauf.Select(e => e.Text));
        Assert.Contains(verlauf, e => e.IstKommentar && e.Text == "Netzteil bestellt." && e.Wer == "Sabine W.");
        Assert.Contains(verlauf, e => !e.IstKommentar && e.Text.StartsWith("Bearbeiter:", StringComparison.Ordinal));
        Assert.Contains(verlauf, e => !e.IstKommentar && e.Text.StartsWith("Priorität:", StringComparison.Ordinal));
        Assert.DoesNotContain(verlauf, e => e.Text == "Kommentar");
        Assert.DoesNotContain(verlauf, e => e.Text.StartsWith("Vorgang angelegt", StringComparison.Ordinal));
        Assert.Equal("Angelegt über Telefon", ansicht.Eingang);
        Assert.Equal(sabine.Id, verlauf.Single(e => e.IstKommentar).WerKontoId);
    }

    // Stünden Status, Bearbeiter und Priorität je zweimal auf dem Schirm, wären
    // das zwei Orte, die auseinanderlaufen können.
    [AvaloniaFact]
    public async Task Jede_Angabe_steht_genau_einmal()
    {
        var (ticket, _) = await VorgangAsync();
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();
        Auslegen(fenster);

        GenauEinmal(fenster, TicketStatus.Assigned.Anzeige());
        GenauEinmal(fenster, "Sabine W.");
        GenauEinmal(fenster, TicketPriority.High.Anzeige());
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Fristen_stehen_im_Kopf_und_die_Beschreibung_ueber_dem_Faden()
    {
        var (ticket, _) = await VorgangAsync();
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();
        Auslegen(fenster);

        Assert.True(Oberkante(fenster, fenster.ReaktionPill) < 120,
            $"Die Reaktionsfrist steht bei y={Oberkante(fenster, fenster.ReaktionPill):F0}.");
        Assert.True(Oberkante(fenster, fenster.Beschreibung) < 260,
            $"Die Beschreibung steht bei y={Oberkante(fenster, fenster.Beschreibung):F0}.");
        Assert.True(Oberkante(fenster, fenster.Beschreibung) < Oberkante(fenster, fenster.Verlauf),
            "Der Faden beginnt über der Beschreibung.");
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ohne_Kommentare_und_Aenderungen_sagt_der_Faden_das()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
                "Frisch", "Nur angelegt.", TicketPriority.Low, "Meier, Anna", "0221123456",
                DateTime.UtcNow, null, sabine.Id, sabine.Name, "A-101")).Id;
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.True(fenster.KeinVerlauf.IsVisible);
        Assert.Equal("Sabine W.", fenster.ErstellerKnopf.Content);
        fenster.Close();
    }

    private static void Auslegen(Window fenster)
    {
        fenster.Measure(new Size(1180, 760));
        fenster.Arrange(new Rect(0, 0, 1180, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();
    }

    private static double Oberkante(Window fenster, Control element) =>
        element.TranslatePoint(new Point(0, 0), fenster)!.Value.Y;

    // Gezählt wird außerhalb des Fadens: Der Faden ist Aufzeichnung, und dass
    // ein Kommentar seinen Verfasser nennt, ist keine zweite Angabe zum Vorgang.
    private static void GenauEinmal(DetailFenster fenster, string text)
    {
        var imFaden = fenster.Verlauf.GetVisualDescendants().ToHashSet();
        // Ein Knopf zeichnet seine Beschriftung über einen inneren Textblock; der
        // zählt nicht noch einmal, sonst stünde jeder Knopf doppelt.
        var treffer = fenster.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && !imFaden.Contains(c))
            .Where(c => c is not TextBlock || c.GetVisualAncestors().OfType<Button>().Any() == false)
            .Where(c => c is TextBlock t && t.Text == text || c is Button k && k.Content is string s && s == text)
            .Select(c => $"{c.GetType().Name}#{c.Name ?? "(ohne Namen)"} unter {c.Parent?.GetType().Name}#{(c.Parent as Control)?.Name}")
            .ToList();
        Assert.True(treffer.Count == 1,
            $"„{text}\" steht {treffer.Count}-mal: {string.Join("; ", treffer)}");
    }

    public void Dispose() => _factory.Dispose();
}
