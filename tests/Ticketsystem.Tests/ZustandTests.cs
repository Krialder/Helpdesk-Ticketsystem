using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Zustand überlebt den Feierabend. Stünde der Prioritätsfilter nach
// jedem Start wieder auf Werk, zahlte der Bearbeiter jeden Morgen denselben
// Einrichtungsaufwand. „Nur meine" ist die Ausnahme, siehe StartzustandTests.
public sealed class ZustandTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Ticket> TicketAsync(string titel, TicketPriority prio, int alterTage = 0)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            titel, "Papierstau.", prio, "Weber, Sabine", "0221123456",
            DateTime.UtcNow.AddDays(-alterTage), null,
            TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    // Sortieren erst nach dem Laden hieße, die 200 geladenen Zeilen zu
    // sortieren und nicht den Bestand: Bei einer gekürzten Liste stünde oben,
    // was zufällig geladen wurde.
    [Fact]
    public async Task Sortiert_wird_im_Dienst_und_nicht_erst_in_der_Anzeige()
    {
        await TicketAsync("Mittel", TicketPriority.Medium);
        await TicketAsync("Kritisch", TicketPriority.Critical);
        await TicketAsync("Niedrig", TicketPriority.Low);

        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<TicketService>();

        var nachPrio = await dienst.ListAsync(TestDaten.Teamleitung,
            sortierung: Sortierung.Prioritaet, absteigend: false);
        var umgekehrt = await dienst.ListAsync(TestDaten.Teamleitung,
            sortierung: Sortierung.Prioritaet, absteigend: true);

        Assert.Equal(["Kritisch", "Mittel", "Niedrig"], nachPrio.Zeilen.Select(t => t.Title));
        Assert.Equal(["Niedrig", "Mittel", "Kritisch"], umgekehrt.Zeilen.Select(t => t.Title));
    }

    [Fact]
    public async Task Die_Nummernsortierung_bleibt_die_Voreinstellung()
    {
        await TicketAsync("Erst", TicketPriority.Medium);
        await TicketAsync("Dann", TicketPriority.Medium);

        using var scope = _factory.Services.CreateScope();
        var liste = await scope.ServiceProvider.GetRequiredService<TicketService>()
            .ListAsync(TestDaten.Teamleitung);

        Assert.Equal(["Dann", "Erst"], liste.Zeilen.Select(t => t.Title));
    }

    [Fact]
    public async Task Der_Zustand_eines_Kontos_ueberlebt_den_Neustart()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
            var zustand = await dienst.LadenAsync(TestDaten.Teamleitung);
            await dienst.SpeichernAsync(TestDaten.Teamleitung, zustand with
            {
                Ansicht = "Alle",
                Prioritaet = 2,
                Sortierung = Sortierung.Frist,
                Absteigend = true,
                VerwaltungsReiter = 2
            });
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var frisch = await scope.ServiceProvider.GetRequiredService<ZustandService>()
                .LadenAsync(TestDaten.Teamleitung);

            Assert.Equal("Alle", frisch.Ansicht);
            Assert.Equal(2, frisch.Prioritaet);
            Assert.Equal(Sortierung.Frist, frisch.Sortierung);
            Assert.True(frisch.Absteigend);
            Assert.Equal(2, frisch.VerwaltungsReiter);
        }
    }

    [Fact]
    public async Task Jedes_Konto_merkt_sich_seinen_eigenen_Zustand()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
        await dienst.SpeichernAsync(TestDaten.Teamleitung,
            (await dienst.LadenAsync(TestDaten.Teamleitung)) with { Ansicht = "Alle" });

        var anderer = await dienst.LadenAsync(TestDaten.Bearbeiter1);

        Assert.Equal("Offen", anderer.Ansicht);
    }

    [AvaloniaFact]
    public async Task Das_Hauptfenster_nimmt_den_gemerkten_Filter_wieder_auf()
    {
        await TicketAsync("Offen", TicketPriority.Medium);
        using (var scope = _factory.Services.CreateScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
            await dienst.SpeichernAsync(TestDaten.Teamleitung,
                (await dienst.LadenAsync(TestDaten.Teamleitung)) with
                {
                    Ansicht = "Alle", Sortierung = Sortierung.Prioritaet, Absteigend = true
                });
        }

        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        await fenster.ZustandUebernehmenAsync();

        Assert.Equal("Alle", fenster.Ansicht.SelectedItem);
        Assert.Equal(Sortierung.Prioritaet, fenster.AktiveSortierung);
        Assert.True(fenster.SortierungAbsteigend);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_Klick_auf_den_Spaltenkopf_dreht_die_Sortierung()
    {
        await TicketAsync("Mittel", TicketPriority.Medium);
        // Als Bearbeiter, dessen Vorgabe die Nummer ist: Die Teamleitung stünde
        // schon auf Priorität, und der erste Klick drehte sie um.
        var fenster = new Grundfenster(_factory.Services, TestDaten.Bearbeiter1);
        fenster.Show();
        await fenster.LadenAsync();

        // Der Pfeil zeigt, was wirklich oben steht: Nach Priorität beginnt die
        // Liste mit Kritisch, also absteigend, nach Frist mit der dringendsten,
        // also aufsteigend.
        fenster.KopfPrioritaet.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Sortierung.Prioritaet, fenster.AktiveSortierung);
        Assert.False(fenster.SortierungAbsteigend);
        Assert.Equal("Priorität ▼", fenster.KopfPrioritaet.Content?.ToString());
        Assert.Equal("Frist", fenster.KopfFrist.Content?.ToString());

        fenster.KopfPrioritaet.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(fenster.SortierungAbsteigend);
        Assert.Equal("Priorität ▲", fenster.KopfPrioritaet.Content?.ToString());

        fenster.KopfFrist.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Sortierung.Frist, fenster.AktiveSortierung);
        Assert.False(fenster.SortierungAbsteigend);
        Assert.Equal("Frist ▲", fenster.KopfFrist.Content?.ToString());
        Assert.Equal("Priorität", fenster.KopfPrioritaet.Content?.ToString());
        fenster.Close();
    }

    // Der Aufklappzustand der Historie wird dagegen nicht gemerkt: Er gälte für
    // alle Vorgänge gemeinsam und spränge von Akte zu Akte auf.
    [AvaloniaFact]
    public async Task Der_Verwaltungsreiter_kommt_so_zurueck_wie_verlassen()
    {
        var verwaltung = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        verwaltung.Show();
        await verwaltung.LadenAsync();
        verwaltung.Reiter.SelectedIndex = 2;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        verwaltung.Close();

        var zweite = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        zweite.Show();
        await zweite.LadenAsync();

        Assert.Equal(2, zweite.Reiter.SelectedIndex);
        zweite.Close();
    }

    public void Dispose() => _factory.Dispose();
}
