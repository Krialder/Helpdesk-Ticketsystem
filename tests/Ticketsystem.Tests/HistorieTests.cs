using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein frischer Vorgang zeigte „Historie (0 Einträge)", und der Name dessen,
// der ihn aufgenommen hatte, fehlte. Das Anlegen ist der erste Beitrag des
// Fadens: Eingangsweg, Zeit und Person.
public sealed class HistorieTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(Akteur wer)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Weber, Sabine", "0221123456",
            DateTime.UtcNow, null, wer.Id, wer.Name, "A-101");
    }

    [Fact]
    public async Task Das_Anlegen_steht_in_der_Historie()
    {
        var wer = new Akteur("t-1", "Weber, Sabine", RoleLevel.Teamleitung);
        var ticket = await TicketAsync(wer);

        using var scope = _factory.Services.CreateScope();
        var frisch = await scope.ServiceProvider.GetRequiredService<TicketService>()
            .FindForUserAsync(ticket.Id, TestDaten.Administration);

        var eintrag = Assert.Single(frisch!.History);
        Assert.Equal("Angelegt", eintrag.Field);
        Assert.Equal("Weber, Sabine", eintrag.ChangedBy);
        // Ob ein Vorgang aus einem Anruf oder einer Mail entstand, ist die erste
        // Frage bei jeder Rückfrage.
        Assert.Equal(TicketSource.Phone.Anzeige(), eintrag.NewValue);
    }

    [AvaloniaFact]
    public async Task Das_Anlegen_ist_der_erste_Beitrag_und_nennt_den_Namen()
    {
        var wer = new Akteur("t-1", "Weber, Sabine", RoleLevel.Teamleitung);
        var ticket = await TicketAsync(wer);

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.StartsWith("Angelegt über Telefon", fenster.Eingang.Text);
        Assert.Equal("Weber, Sabine", fenster.ErstellerWert.Text);
        Assert.True(fenster.ErstellerWert.IsVisible);
        // Hinter „t-1" steht kein Konto, also bleibt der Name Text; ein Klick ins
        // Leere wäre ein Versprechen ohne Ziel.
        Assert.False(fenster.ErstellerKnopf.IsVisible);
        Assert.DoesNotContain("@", fenster.ErstellerWert.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Eine_Aktion_verlaengert_den_Faden()
    {
        var wer = new Akteur("t-1", "Weber, Sabine", RoleLevel.Teamleitung);
        var ticket = await TicketAsync(wer);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();
        Assert.Empty((IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!);

        await fenster.AktionAsync(dienst =>
            dienst.ChangePriorityAsync(ticket.Id, TicketPriority.High, TestDaten.Administration));

        var eintrag = Assert.Single((IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!);
        Assert.StartsWith("Priorität:", eintrag.Text);
        Assert.False(eintrag.IstKommentar);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
