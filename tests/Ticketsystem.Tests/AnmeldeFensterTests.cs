using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Ticketsystem.App;
using Ticketsystem.App.Fenster;
using Ticketsystem.Tests;

// Die Headless-Testanwendung baut dieselbe App-Klasse (Styles, Ressourcen)
// über der Headless-Plattform. Assembly-weit registriert, damit alle
// [AvaloniaFact]-Tests auf einem Avalonia-Dispatcher laufen.
[assembly: AvaloniaTestApplication(typeof(AvaloniaTestUmgebung))]

namespace Ticketsystem.Tests;

public sealed class AvaloniaTestUmgebung
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<Ticketsystem.App.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

// Prüft die Verdrahtung Fenster zu Dienst, nicht den Dienst selbst; der hat
// seine eigenen Tests am KernWirt.
public sealed class AnmeldeFensterTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public AnmeldeFensterTests() => _factory.MitEinstellungen(new()
    {
        ["SeedAgent:Password"] = "Fenster-Probe1!"
    });

    [AvaloniaFact]
    public async Task Ein_falsches_Passwort_zeigt_die_Dienstmeldung_im_Fenster()
    {
        var fenster = new AnmeldeFenster(_factory.Services);
        fenster.Show();
        fenster.Email.Text = "agent@ticketsystem.local";
        fenster.Passwort.Text = "falsch";

        await fenster.AnmeldenAsync();

        Assert.True(fenster.Fehler.IsVisible, "Die Fehlermeldung müsste sichtbar sein.");
        Assert.Contains("Anmeldung fehlgeschlagen", fenster.Fehler.Text);
        // Sonst bleibt nach einem Tippfehler ein totes Fenster.
        Assert.True(fenster.Anmelden.IsEnabled);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
