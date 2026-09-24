using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Jedes Fenster passt auf den Bildschirm, auf dem es aufgeht: 760 px Höhe
// sind mehr, als ein 768-px-Laptop unter der Taskleiste hat. Je Konto gibt
// es eine Darstellungsgröße (100, 115, 130 Prozent), die Schrift und
// Abstände gemeinsam skaliert, damit die Geometrieleiter im Verhältnis
// bleibt. Die Stufe ist global, deshalb setzt jeder Test sie im finally
// zurück.
public sealed class DarstellungTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [Fact]
    public void Nur_drei_Stufen_und_alles_andere_ist_hundert()
    {
        Assert.Equal(1.0, Darstellung.Faktor(100));
        Assert.Equal(1.15, Darstellung.Faktor(115));
        Assert.Equal(1.3, Darstellung.Faktor(130));
        // Ein Wert, den es nicht gibt (Altbestand, Tippfehler in der Datenbank),
        // fällt auf die Vorgabe, nicht auf eine Ausnahme.
        Assert.Equal(1.0, Darstellung.Faktor(0));
        Assert.Equal(1.0, Darstellung.Faktor(200));
        Assert.Equal([100, 115, 130], Darstellung.Stufen);
        try
        {
            Darstellung.Prozent = 200;
            Assert.Equal(100, Darstellung.Prozent);
            Darstellung.Prozent = 115;
            Assert.Equal(115, Darstellung.Prozent);
        }
        finally
        {
            Darstellung.Prozent = 100;
        }
    }

    [Fact]
    public async Task Die_Stufe_ist_je_Konto_gemerkt_und_hundert_die_Vorgabe()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
        Assert.Equal(100, (await dienst.LadenAsync(TestDaten.Bearbeiter1)).Darstellung);

        await dienst.SpeichernAsync(TestDaten.Bearbeiter1,
            (await dienst.LadenAsync(TestDaten.Bearbeiter1)) with { Darstellung = 130 });

        Assert.Equal(130, (await dienst.LadenAsync(TestDaten.Bearbeiter1)).Darstellung);
        Assert.Equal(100, (await dienst.LadenAsync(TestDaten.Teamleitung)).Darstellung);
    }

    // Der Headless-Bildschirm ist 1920 mal 1280. Gestutzt wird samt Mindesthöhe,
    // denn eine Mindesthöhe über dem Bildschirm hielte das Fenster über dem
    // Rand fest.
    [AvaloniaFact]
    public void Ein_zu_hohes_Fenster_wird_beim_Oeffnen_auf_den_Bildschirm_gestutzt()
    {
        var fenster = new WissensFenster(_factory.Services, TestDaten.Teamleitung)
        {
            Height = 2000, MinHeight = 1500, Width = 900
        };
        fenster.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var arbeit = fenster.Screens.Primary!.WorkingArea;
        Assert.Equal(arbeit.Height, fenster.MaxHeight);
        Assert.Equal(arbeit.Width, fenster.MaxWidth);
        Assert.True(fenster.Height <= arbeit.Height, $"Höhe {fenster.Height} über dem Bildschirm ({arbeit.Height}).");
        Assert.True(fenster.MinHeight <= arbeit.Height);
        Assert.Equal(900, fenster.Width);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Darstellungsgroesse_des_Kontos_skaliert_Schrift_und_Abstaende_gemeinsam()
    {
        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
                await dienst.SpeichernAsync(TestDaten.Teamleitung,
                    (await dienst.LadenAsync(TestDaten.Teamleitung)) with { Darstellung = 130 });
            }

            var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
            fenster.Show();
            await fenster.ZustandUebernehmenAsync();
            Messen.Auslegen(fenster, 1600, 1000);

            Assert.Equal(130, Darstellung.Prozent);
            // Der Knopf ist 32 px hoch; im Fenster gemessen steht er auf 130
            // Prozent, weil das ganze Fenster skaliert und nicht nur die Schrift.
            var oben = fenster.NeuesTicket.TranslatePoint(new Point(0, 0), fenster)!.Value.Y;
            var unten = fenster.NeuesTicket.TranslatePoint(new Point(0, fenster.NeuesTicket.Bounds.Height), fenster)!.Value.Y;
            var hoehe = unten - oben;
            Assert.InRange(hoehe, 32 * 1.3 - 1, 32 * 1.3 + 1);

            var anmeldung = fenster.AbmeldenAusfuehren();
            Assert.Equal(100, Darstellung.Prozent);
            anmeldung.Close();
        }
        finally
        {
            Darstellung.Prozent = 100;
        }
    }

    [AvaloniaFact]
    public async Task Der_Einstellungen_Dialog_speichert_die_Stufe_und_wendet_sie_an()
    {
        try
        {
            var dialog = new EinstellungenDialog(_factory.Services, TestDaten.Teamleitung);
            dialog.Show();
            await dialog.LadenAsync();
            Assert.Equal("100 %", dialog.DarstellungWahl.SelectedItem);

            dialog.DarstellungWahl.SelectedItem = "130 %";
            for (var i = 0; i < 20 && Darstellung.Prozent != 130; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                await Task.Delay(20);
            }

            Assert.Equal(130, Darstellung.Prozent);
            using var scope = _factory.Services.CreateScope();
            Assert.Equal(130, (await scope.ServiceProvider.GetRequiredService<ZustandService>()
                .LadenAsync(TestDaten.Teamleitung)).Darstellung);
            dialog.Close();
        }
        finally
        {
            Darstellung.Prozent = 100;
        }
    }

    public void Dispose() => _factory.Dispose();
}
