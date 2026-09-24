using Avalonia.Headless.XUnit;
using Ticketsystem.App.Fenster;

namespace Ticketsystem.Tests;

// Im Erfassungsfenster fluchten die Kanten. Auf einem Bildschirmfoto des
// Mail-Modus endete das Namensfeld 12 px vor der Kante der Felder darunter;
// eine Kante von zehn, die nicht fluchtet, liest das Auge als Unordnung,
// ohne zu wissen, warum.
public sealed class ErfassungsKantenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private const double Toleranz = 1;

    // Name und Rufnummer teilen ein Raster mit Spaltenabstand; im Mail-Modus
    // verschwindet die Rufnummer, der Abstand blieb stehen.
    [AvaloniaFact]
    public void Im_Mail_Modus_endet_das_Namensfeld_an_der_Kante_der_Felder_darunter()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Bearbeiter1, alsMail: true);
        Messen.Auslegen(fenster, 620, 760);

        var name = Messen.Kante(fenster, fenster.Kunde);
        var absender = Messen.Kante(fenster, fenster.Absender);
        var titel = Messen.Kante(fenster, fenster.Titel);

        Assert.True(Math.Abs(name.Right - titel.Right) <= Toleranz,
            $"Das Namensfeld endet bei x={name.Right:0}, der Titel bei x={titel.Right:0}.");
        Assert.True(Math.Abs(absender.Right - titel.Right) <= Toleranz,
            $"Der Absender endet bei x={absender.Right:0}, der Titel bei x={titel.Right:0}.");
        Assert.True(Math.Abs(name.Left - titel.Left) <= Toleranz,
            $"Das Namensfeld beginnt bei x={name.Left:0}, der Titel bei x={titel.Left:0}.");
        fenster.Close();
    }

    [AvaloniaFact]
    public void Im_Telefon_Modus_endet_die_Rufnummer_an_der_Kante_des_Titels()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Bearbeiter1);
        Messen.Auslegen(fenster, 620, 760);

        var nummer = Messen.Kante(fenster, fenster.Nummer);
        var titel = Messen.Kante(fenster, fenster.Titel);
        var name = Messen.Kante(fenster, fenster.Kunde);

        Assert.True(Math.Abs(nummer.Right - titel.Right) <= Toleranz,
            $"Die Rufnummer endet bei x={nummer.Right:0}, der Titel bei x={titel.Right:0}.");
        Assert.True(nummer.Left - name.Right >= 8,
            $"Zwischen Name (bis x={name.Right:0}) und Rufnummer (ab x={nummer.Left:0}) fehlt die Luft.");
        fenster.Close();
    }

    // Adresse und Priorität waren 220 px breit, der Verweis 140: drei
    // linksbündige Felder mit zwei rechten Kanten, eine zu viel.
    [AvaloniaFact]
    public void Die_kurzen_Felder_haben_eine_Breite()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Bearbeiter1);
        Messen.Auslegen(fenster, 620, 760);

        var adresse = Messen.Kante(fenster, fenster.Adresse);
        var prioritaet = Messen.Kante(fenster, fenster.Prioritaet);
        var verweis = Messen.Kante(fenster, fenster.Verweis);

        Assert.True(Math.Abs(adresse.Right - prioritaet.Right) <= Toleranz,
            $"Adresse endet bei x={adresse.Right:0}, Priorität bei x={prioritaet.Right:0}.");
        Assert.True(Math.Abs(verweis.Right - adresse.Right) <= Toleranz,
            $"Der Verweis endet bei x={verweis.Right:0}, die Adresse bei x={adresse.Right:0}.");
        fenster.Close();
    }

    [AvaloniaFact]
    public void Im_Mail_Modus_fuellen_die_Zeitwaehler_die_Karte_wie_die_Felder()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Bearbeiter1, alsMail: true);
        Messen.Auslegen(fenster, 620, 760);

        var titel = Messen.Kante(fenster, fenster.Titel);
        foreach (var (name, waehler) in new (string, Avalonia.Controls.Control)[]
                 {
                     ("Datum", fenster.ZeitDatum), ("Uhrzeit", fenster.ZeitUhr)
                 })
        {
            var kante = Messen.Kante(fenster, waehler);
            Assert.True(Math.Abs(kante.Left - titel.Left) <= Toleranz && Math.Abs(kante.Right - titel.Right) <= Toleranz,
                $"{name}: x={kante.Left:0} bis {kante.Right:0}, die Felder: x={titel.Left:0} bis {titel.Right:0}.");
        }

        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
