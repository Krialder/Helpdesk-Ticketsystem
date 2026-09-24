using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Ticketsystem.App.Fenster;

namespace Ticketsystem.Tests;

// Auf einem geteilten Helpdesk-Rechner muss eine Sitzung enden können, ohne
// dass jemand die Anwendung schließt; sonst arbeitet der Nächste unter
// fremdem Namen, mit dessen Rechten und in dessen Historie.
public sealed class AbmeldenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [AvaloniaFact]
    public void Abmelden_fuehrt_zurueck_zur_Anmeldung()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();
        var geschlossen = false;
        fenster.Closed += (_, _) => geschlossen = true;

        var anmeldung = fenster.AbmeldenAusfuehren();

        Assert.NotNull(anmeldung);
        Assert.True(geschlossen, "Das Hauptfenster muss dabei schließen, sonst bleibt die Sitzung offen.");
        anmeldung.Close();
    }

    // Ein vorbelegtes Konto wäre eine Einladung, es stehen zu lassen, und
    // verriete nebenbei, wer zuletzt gearbeitet hat.
    [AvaloniaFact]
    public void Die_Anmeldung_beginnt_leer()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();

        var anmeldung = fenster.AbmeldenAusfuehren();

        Assert.True(string.IsNullOrEmpty(anmeldung.Email.Text));
        Assert.True(string.IsNullOrEmpty(anmeldung.Passwort.Text));
        anmeldung.Close();
    }

    // Wer sich abmelden will, sucht dort, wo sein Name steht; die Kopfzeile
    // trägt die Arbeit, nicht die Sitzung.
    [AvaloniaFact]
    public void Der_Knopf_steht_am_angemeldeten_Konto()
    {
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        fenster.Show();

        Assert.True(fenster.Abmelden.IsVisible);
        Assert.Contains(TestDaten.Teamleitung.Name, fenster.Angemeldet.Text);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
