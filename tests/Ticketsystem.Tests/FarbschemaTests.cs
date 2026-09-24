using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein helles Schema neben dem dunklen, je Konto gemerkt. Vor der Anmeldung
// gilt dunkel, weil noch kein Konto gewählt hat; beim Abmelden fällt die
// Anwendung darauf zurück, damit der Nächste am selben Rechner nicht das
// Schema des Vorgängers vorfindet. Das Schema ist globaler Zustand, deshalb
// stellt jeder Test es im finally zurück; xUnit läuft Klassen parallel.
public sealed class FarbschemaTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [Fact]
    public void Der_Name_findet_das_Schema_und_Unbekanntes_faellt_auf_dunkel()
    {
        Assert.Same(Farbschema.Hell, Farbschema.Aus("hell"));
        Assert.Same(Farbschema.Dunkel, Farbschema.Aus("dunkel"));
        Assert.Same(Farbschema.Dunkel, Farbschema.Aus(null));
        Assert.Same(Farbschema.Dunkel, Farbschema.Aus("lila"));
        Assert.Equal(2, Farbschema.Alle.Count);
        // Ein helles Schema, das nur ein dunkles mit anderem Namen wäre, bestünde
        // jeden Kontrasttest und hülfe niemandem.
        Assert.NotEqual(Farbschema.Dunkel.Grund, Farbschema.Hell.Grund);
        Assert.True(Palette.Kontrast(Farbschema.Hell.Grund, Farbschema.Dunkel.Grund) > 10);
    }

    [Fact]
    public async Task Die_Wahl_ist_je_Konto_gemerkt_und_dunkel_die_Vorgabe()
    {
        using var scope = _factory.Services.CreateScope();
        var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
        Assert.Equal(Farbschema.Dunkel.Name, (await dienst.LadenAsync(TestDaten.Bearbeiter1)).Farbschema);

        await dienst.SpeichernAsync(TestDaten.Bearbeiter1,
            (await dienst.LadenAsync(TestDaten.Bearbeiter1)) with { Farbschema = Farbschema.Hell.Name });

        Assert.Equal(Farbschema.Hell.Name, (await dienst.LadenAsync(TestDaten.Bearbeiter1)).Farbschema);
        Assert.Equal(Farbschema.Dunkel.Name, (await dienst.LadenAsync(TestDaten.Teamleitung)).Farbschema);
    }

    [AvaloniaFact]
    public void Das_Anwenden_wechselt_Palette_Ressourcen_und_Fluent_Variante_zusammen()
    {
        try
        {
            Ticketsystem.App.App.SchemaAnwenden(Farbschema.Hell);

            Assert.Same(Farbschema.Hell, Palette.Schema);
            Assert.Equal(Farbschema.Hell.Grund, Palette.Grund);
            Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
            // An den Ressourcen hängt jedes Fenster; sonst wechselte nur der Code,
            // nicht der Schirm.
            Assert.True(Application.Current.TryGetResource("GrundBrush", ThemeVariant.Light, out var grund));
            Assert.Equal(Farbschema.Hell.Grund, ((ISolidColorBrush)grund!).Color);
            Assert.True(Application.Current.TryGetResource("SystemAccentColor", ThemeVariant.Light, out var akzent));
            Assert.Equal(Farbschema.Hell.Akzent, (Color)akzent!);
        }
        finally
        {
            Ticketsystem.App.App.SchemaAnwenden(Farbschema.Dunkel);
        }

        Assert.Same(Farbschema.Dunkel, Palette.Schema);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public async Task Das_Hauptfenster_wendet_das_Schema_des_Kontos_an_und_Abmelden_stellt_dunkel_zurueck()
    {
        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dienst = scope.ServiceProvider.GetRequiredService<ZustandService>();
                await dienst.SpeichernAsync(TestDaten.Teamleitung,
                    (await dienst.LadenAsync(TestDaten.Teamleitung)) with { Farbschema = Farbschema.Hell.Name });
            }

            var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
            fenster.Show();
            await fenster.ZustandUebernehmenAsync();
            Assert.Same(Farbschema.Hell, Palette.Schema);

            var anmeldung = fenster.AbmeldenAusfuehren();
            Assert.Same(Farbschema.Dunkel, Palette.Schema);
            anmeldung.Close();
        }
        finally
        {
            Ticketsystem.App.App.SchemaAnwenden(Farbschema.Dunkel);
        }
    }

    [AvaloniaFact]
    public async Task Der_Einstellungen_Dialog_speichert_die_Wahl_und_wendet_sie_an()
    {
        try
        {
            var dialog = new EinstellungenDialog(_factory.Services, TestDaten.Teamleitung);
            dialog.Show();
            await dialog.LadenAsync();
            Assert.Equal("Dunkel", dialog.FarbschemaWahl.SelectedItem);

            dialog.FarbschemaWahl.SelectedItem = "Hell";
            for (var i = 0; i < 20 && Palette.Schema != Farbschema.Hell; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                await Task.Delay(20);
            }

            Assert.Same(Farbschema.Hell, Palette.Schema);
            using var scope = _factory.Services.CreateScope();
            Assert.Equal(Farbschema.Hell.Name, (await scope.ServiceProvider.GetRequiredService<ZustandService>()
                .LadenAsync(TestDaten.Teamleitung)).Farbschema);
            dialog.Close();
        }
        finally
        {
            Ticketsystem.App.App.SchemaAnwenden(Farbschema.Dunkel);
        }
    }

    public void Dispose() => _factory.Dispose();
}
