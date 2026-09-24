using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Hinweise beim Überfahren sind für den Neuen an und lassen sich je
// Konto abstellen: Hinweise, die man abstellen kann, statt einer Einführung,
// die man wegklickt. Angewendet wird die Wahl über eine Ressource, an der
// jeder Hinweis der Anwendung hängt; der Zustand ist global, deshalb stellt
// jeder Fenstertest ihn im finally zurück.
public sealed class HinweiseTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, "Meier, Anna", "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    private async Task HinweiseSpeichernAsync(Akteur akteur, bool an)
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
        await dienst.SpeichernAsync(akteur, (await dienst.LadenAsync(akteur)) with { Hinweise = an });
    }

    [Fact]
    public async Task Die_Vorgabe_zeigt_Hinweise_und_die_Wahl_ueberlebt_den_Neustart()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();

        Assert.True((await dienst.LadenAsync(TestDaten.Bearbeiter1)).Hinweise);

        await HinweiseSpeichernAsync(TestDaten.Bearbeiter1, an: false);
        Assert.False((await dienst.LadenAsync(TestDaten.Bearbeiter1)).Hinweise);
        Assert.True((await dienst.LadenAsync(TestDaten.Teamleitung)).Hinweise);
    }

    [AvaloniaFact]
    public async Task Das_Hauptfenster_schaltet_die_Hinweise_nach_der_Wahl_des_Kontos()
    {
        try
        {
            await HinweiseSpeichernAsync(TestDaten.Teamleitung, an: false);
            var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
            fenster.Show();
            await fenster.ZustandUebernehmenAsync();

            Assert.Equal("Einstellungen", fenster.Einstellungen.Content);
            // Der Hinweis steht am Knopf, wird aber nicht gezeigt: Die Wahl des
            // Kontos wirkt über die eine Ressource auf jedes Element.
            Assert.NotNull(ToolTip.GetTip(fenster.NeuesTicket));
            Assert.False(Hinweise.Aktiv);
            Assert.False(ToolTip.GetServiceEnabled(fenster.NeuesTicket));

            Hinweise.Anwenden(true);
            Assert.True(ToolTip.GetServiceEnabled(fenster.NeuesTicket));

            // Abmelden stellt die Vorgabe wieder her, damit der Nächste am selben
            // Rechner nicht die Einstellungen des Vorgängers vorfindet.
            Hinweise.Anwenden(false);
            var anmeldung = fenster.AbmeldenAusfuehren();
            Assert.True(Hinweise.Aktiv);
            anmeldung.Close();
        }
        finally
        {
            Hinweise.Anwenden(true);
        }
    }

    // Ein Hinweis erklärt, was passiert, nicht was da steht; deshalb hat jeder
    // Knopf einen, auch die mit sprechender Beschriftung.
    [AvaloniaFact]
    public async Task Die_Hauptfenster_erklaeren_ihre_Knoepfe_und_Filter()
    {
        var ticket = await TicketAsync();
        var grund = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        var detail = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        var wissen = new WissensFenster(_factory.Services, TestDaten.Teamleitung);

        Control[] erklaert =
        [
            grund.Suche, grund.Ansicht, grund.Prioritaet, grund.NurMeine, grund.NeuesTicket,
            grund.Wissen, grund.Verwaltung, grund.Einstellungen, grund.Abmelden,
            detail.Senden, detail.Zuweisen, detail.SelbstZuweisen, detail.ZuweisungAufheben,
            detail.WvMerken, detail.WvEntfernen, detail.PrioSetzen, detail.Nachtragen, detail.Entwerfen,
            wissen.NurPruefung, wissen.Neu, wissen.Freigaben, wissen.Ordnung, wissen.Bearbeiten,
            wissen.AlsGeprueft, wissen.Fassungen
        ];

        var ohne = erklaert.Where(c => string.IsNullOrWhiteSpace(ToolTip.GetTip(c) as string))
            .Select(c => c.Name).ToList();
        Assert.True(ohne.Count == 0, "Ohne Hinweis: " + string.Join(", ", ohne));
    }

    [AvaloniaFact]
    public async Task Der_Einstellungen_Dialog_speichert_den_Schalter_und_wendet_ihn_an()
    {
        try
        {
            var dialog = new EinstellungenDialog(_factory.Services, TestDaten.Teamleitung);
            dialog.Show();
            await dialog.LadenAsync();

            Assert.Equal("Einstellungen", dialog.Title);
            Assert.True(dialog.HinweiseSchalter.IsChecked);

            dialog.HinweiseSchalter.IsChecked = false;
            for (var i = 0; i < 20 && Hinweise.Aktiv; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                await Task.Delay(20);
            }

            Assert.False(Hinweise.Aktiv);
            using var scope = _factory.Services.CreateScope();
            Assert.False((await scope.ServiceProvider.GetRequiredService<ZustandService>()
                .LadenAsync(TestDaten.Teamleitung)).Hinweise);
            dialog.Close();
        }
        finally
        {
            Hinweise.Anwenden(true);
        }
    }

    public void Dispose() => _factory.Dispose();
}
