using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Während einer Ruhezeit steht die Frist still, sie verschiebt sich also
// genau um die Überschneidung. Überlappende Fenster darf der Rechner deshalb
// nicht einzeln aufaddieren, sonst verschieben zwei Einträge über denselben
// Tag die Frist um zwei Tage.
public class RuhezeitUeberlappungTests
{
    private static readonly DateTime Start = new(2026, 12, 1, 8, 0, 0);

    // Der Alltagsfall: Jemand trägt die Betriebsruhe ein, und ein Kollege trägt
    // sie noch einmal ein. Der Liste sieht niemand an, dass die Frist doppelt
    // wandert.
    [Fact]
    public void Zwei_deckungsgleiche_Ruhezeiten_verschieben_wie_eine()
    {
        var einmal = SlaRechner.Faelligkeit(Start, 8, [Fenster("2026-12-01 09:00", "2026-12-02 09:00")]);

        var zweimal = SlaRechner.Faelligkeit(Start, 8, [
            Fenster("2026-12-01 09:00", "2026-12-02 09:00"),
            Fenster("2026-12-01 09:00", "2026-12-02 09:00")
        ]);

        Assert.Equal(einmal, zweimal);
    }

    [Fact]
    public void Teilweise_ueberlappende_Ruhezeiten_zaehlen_nur_einmal()
    {
        var zusammen = SlaRechner.Faelligkeit(Start, 8, [
            Fenster("2026-12-01 09:00", "2026-12-02 09:00"),
            Fenster("2026-12-01 21:00", "2026-12-02 21:00")
        ]);

        var einStueck = SlaRechner.Faelligkeit(Start, 8, [Fenster("2026-12-01 09:00", "2026-12-02 21:00")]);

        Assert.Equal(einStueck, zusammen);
    }

    [Fact]
    public void Ein_Fenster_das_ein_anderes_enthaelt_zaehlt_einmal()
    {
        var zusammen = SlaRechner.Faelligkeit(Start, 8, [
            Fenster("2026-12-01 09:00", "2026-12-05 09:00"),
            Fenster("2026-12-02 09:00", "2026-12-03 09:00")
        ]);

        var nurDasGrosse = SlaRechner.Faelligkeit(Start, 8, [Fenster("2026-12-01 09:00", "2026-12-05 09:00")]);

        Assert.Equal(nurDasGrosse, zusammen);
    }

    [Fact]
    public void Aneinandergrenzende_Ruhezeiten_ergeben_die_Summe()
    {
        var geteilt = SlaRechner.Faelligkeit(Start, 8, [
            Fenster("2026-12-01 09:00", "2026-12-02 09:00"),
            Fenster("2026-12-02 09:00", "2026-12-03 09:00")
        ]);

        var amStueck = SlaRechner.Faelligkeit(Start, 8, [Fenster("2026-12-01 09:00", "2026-12-03 09:00")]);

        Assert.Equal(amStueck, geteilt);
    }

    // Die Gegenprobe: Zwei Fenster ohne Berührung wirken beide, sonst wäre die
    // Behebung schlimmer als der Befund.
    [Fact]
    public void Getrennte_Ruhezeiten_verschieben_weiterhin_beide()
    {
        var beide = SlaRechner.Faelligkeit(Start, 100, [
            Fenster("2026-12-01 09:00", "2026-12-02 09:00"),
            Fenster("2026-12-04 09:00", "2026-12-05 09:00")
        ]);

        var nurDasErste = SlaRechner.Faelligkeit(Start, 100, [Fenster("2026-12-01 09:00", "2026-12-02 09:00")]);

        Assert.Equal(TimeSpan.FromHours(24), beide - nurDasErste);
    }

    [Fact]
    public void Die_Reihenfolge_der_Eingabe_spielt_keine_Rolle()
    {
        Ruhefenster[] fenster =
        [
            Fenster("2026-12-04 09:00", "2026-12-05 09:00"),
            Fenster("2026-12-01 09:00", "2026-12-02 09:00"),
            Fenster("2026-12-01 21:00", "2026-12-02 21:00")
        ];

        Assert.Equal(
            SlaRechner.Faelligkeit(Start, 8, fenster),
            SlaRechner.Faelligkeit(Start, 8, fenster.Reverse().ToArray()));
    }

    private static Ruhefenster Fenster(string von, string bis) =>
        new(DateTime.Parse(von), DateTime.Parse(bis));
}
