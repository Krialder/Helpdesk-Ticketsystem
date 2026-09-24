using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// „Nur meine" ist bei jeder Anmeldung aus: Ein Filter, der über Nacht stehen
// bleibt, versteckt am Morgen Vorgänge, und die Liste sieht vollständig aus.
// Teamleitung und Administration starten nach Priorität sortiert, Bearbeiter
// nach Nummer: Wer verteilt, will das Kritische oben, wer abarbeitet, das
// Neueste.
public sealed class StartzustandTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [Fact]
    public async Task Die_Vorgabe_richtet_sich_nach_der_Rolle()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();

        var teamleitung = await dienst.LadenAsync(TestDaten.Teamleitung);
        var administration = await dienst.LadenAsync(TestDaten.Administration);
        var bearbeiter = await dienst.LadenAsync(TestDaten.Bearbeiter1);

        // Absteigend false heißt bei der Priorität: Kritisch zuerst.
        Assert.Equal(Sortierung.Prioritaet, teamleitung.Sortierung);
        Assert.False(teamleitung.Absteigend);
        Assert.Equal(Sortierung.Prioritaet, administration.Sortierung);
        Assert.Equal(Sortierung.Nummer, bearbeiter.Sortierung);
        Assert.Equal("Offen", teamleitung.Ansicht);
        Assert.Equal(0, teamleitung.Prioritaet);
    }

    [Fact]
    public async Task Eine_gemerkte_Sortierung_geht_der_Vorgabe_vor()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
        await dienst.SpeichernAsync(TestDaten.Teamleitung,
            (await dienst.LadenAsync(TestDaten.Teamleitung)) with { Sortierung = Sortierung.Frist });

        Assert.Equal(Sortierung.Frist, (await dienst.LadenAsync(TestDaten.Teamleitung)).Sortierung);
    }

    [AvaloniaFact]
    public async Task Das_Hauptfenster_startet_je_Rolle_sortiert()
    {
        var teamleitung = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        teamleitung.Show();
        await teamleitung.ZustandUebernehmenAsync();
        Assert.Equal(Sortierung.Prioritaet, teamleitung.AktiveSortierung);
        Assert.False(teamleitung.SortierungAbsteigend);
        Assert.Equal("Priorität ▼", teamleitung.KopfPrioritaet.Content?.ToString());
        teamleitung.Close();

        var bearbeiter = new Grundfenster(_factory.Services, TestDaten.Bearbeiter1);
        bearbeiter.Show();
        await bearbeiter.ZustandUebernehmenAsync();
        Assert.Equal(Sortierung.Nummer, bearbeiter.AktiveSortierung);
        bearbeiter.Close();
    }

    [AvaloniaFact]
    public async Task Nur_meine_ist_bei_jeder_Anmeldung_aus()
    {
        var erstes = new Grundfenster(_factory.Services, TestDaten.Bearbeiter1);
        erstes.Show();
        await erstes.ZustandUebernehmenAsync();
        erstes.NurMeine.IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // Ein zweiter, gemerkter Wert, damit der Test nicht an einem Speichern
        // hängt, das gar nicht stattfand.
        erstes.Ansicht.SelectedItem = "Alle";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        await Task.Delay(100);
        erstes.Close();

        var zweites = new Grundfenster(_factory.Services, TestDaten.Bearbeiter1);
        zweites.Show();
        await zweites.ZustandUebernehmenAsync();

        Assert.Equal("Alle", zweites.Ansicht.SelectedItem);
        Assert.NotEqual(true, zweites.NurMeine.IsChecked);
        zweites.Close();
    }

    public void Dispose() => _factory.Dispose();
}
