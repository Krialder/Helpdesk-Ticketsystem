using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;

namespace Ticketsystem.Tests;

// Die Frist sagt, wie viel Zeit bleibt, nicht nur, wann sie endet: Der
// Bearbeiter soll nicht rechnen, während ein Kunde in der Leitung wartet.
// Die Formatierung liegt im Kern, damit Liste, Detail und Statusleiste
// dieselbe Sprache sprechen.
public sealed class FristanzeigeTests
{
    private static readonly TimeZoneInfo Berlin =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // 25.08.2026 12:00 Ortszeit Berlin ist 10:00 UTC (Sommerzeit).
    private static readonly DateTime Jetzt = new(2026, 8, 25, 10, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(42, "noch 42 min (bis 12:42)")]
    [InlineData(90, "noch 1 h 30 min (bis 13:30)")]
    [InlineData(60 * 26, "noch 1 Tag 2 h (bis 26.08.2026 14:00)")]
    public void Eine_laufende_Frist_nennt_die_Restzeit_und_den_Zeitpunkt(int minuten, string erwartet)
    {
        var text = Fristanzeige.Text(SlaState.Laeuft, Jetzt.AddMinutes(minuten), Jetzt, Berlin);

        Assert.Equal(erwartet, text);
    }

    [Fact]
    public void Eine_gerissene_Frist_nennt_die_verstrichene_Zeit()
    {
        var text = Fristanzeige.Text(SlaState.Ueberfaellig, Jetzt.AddMinutes(-130), Jetzt, Berlin);

        Assert.Equal("überfällig seit 2 h 10 min (seit 09:50)", text);
    }

    [Fact]
    public void Eine_erledigte_Frist_rechnet_nicht_weiter()
    {
        Assert.Equal("erfüllt", Fristanzeige.Text(SlaState.Erfuellt, Jetzt.AddHours(-3), Jetzt, Berlin));
        Assert.Equal("verspätet erfüllt",
            Fristanzeige.Text(SlaState.VerspaetetErfuellt, Jetzt.AddHours(-3), Jetzt, Berlin));
        Assert.Equal("entfällt", Fristanzeige.Text(SlaState.Entfaellt, Jetzt.AddHours(-3), Jetzt, Berlin));
    }

    [Fact]
    public void Unter_einer_Minute_bleibt_die_Anzeige_ehrlich()
    {
        Assert.Equal("noch unter 1 min (bis 12:00)",
            Fristanzeige.Text(SlaState.BaldFaellig, Jetzt.AddSeconds(30), Jetzt, Berlin));
    }

    [Fact]
    public void Die_Liste_bekommt_dieselbe_Aussage_in_kuerzer()
    {
        Assert.Equal("noch 42 min", Fristanzeige.Kurz(SlaState.Laeuft, Jetzt.AddMinutes(42), Jetzt));
        Assert.Equal("seit 2 h 10 min", Fristanzeige.Kurz(SlaState.Ueberfaellig, Jetzt.AddMinutes(-130), Jetzt));
        Assert.Equal("erfüllt", Fristanzeige.Kurz(SlaState.Erfuellt, Jetzt.AddHours(-3), Jetzt));
    }

    // Das Zeichen ist die dritte Ebene neben Farbe und Wort: Rund acht Prozent
    // der Männer unterscheiden Rot und Grün schlecht, und Farbe trägt hier den
    // Handlungsdruck.
    [Fact]
    public void Jeder_Zustand_traegt_ein_eigenes_Zeichen()
    {
        var zeichen = Enum.GetValues<SlaState>().ToDictionary(z => z, Fristanzeige.Zeichen);

        Assert.All(zeichen.Values, z => Assert.False(string.IsNullOrWhiteSpace(z)));
        Assert.NotEqual(zeichen[SlaState.Laeuft], zeichen[SlaState.BaldFaellig]);
        Assert.NotEqual(zeichen[SlaState.BaldFaellig], zeichen[SlaState.Ueberfaellig]);
        Assert.NotEqual(zeichen[SlaState.Erfuellt], zeichen[SlaState.VerspaetetErfuellt]);
        Assert.NotEqual(zeichen[SlaState.Erfuellt], zeichen[SlaState.Ueberfaellig]);
    }
}
