using Ticketsystem.App.Stil;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Farbe ist Bedeutung, nicht Dekoration: Ampelfarben melden nur Zeit- und
// Handlungsdruck, Eigenschaften bleiben grau. Geprüft werden die zentrale
// Klassenvergabe (BadgeKlasse, PrioKlasse) und die eine Übersetzung von
// Klasse zu Farbe (Palette), keine Pixel.
public sealed class GestaltTests
{
    [Theory]
    [InlineData(SlaState.Ueberfaellig, "badge-frist-ueberfaellig")]
    [InlineData(SlaState.BaldFaellig, "badge-frist-bald")]
    [InlineData(SlaState.VerspaetetErfuellt, "badge-frist-nachtraeglich")]
    [InlineData(SlaState.Erfuellt, "badge-frist-erfuellt")]
    [InlineData(SlaState.Laeuft, "badge-frist-neutral")]
    [InlineData(SlaState.Entfaellt, "badge-frist-neutral")]
    public void Die_Ampel_vergibt_semantische_Klassen(SlaState zustand, string klasse)
    {
        Assert.Equal(klasse, zustand.BadgeKlasse());
    }

    // Wer alles markiert, markiert nichts.
    [Theory]
    [InlineData(TicketPriority.Low, "")]
    [InlineData(TicketPriority.Medium, "")]
    [InlineData(TicketPriority.High, "prio-hoch")]
    [InlineData(TicketPriority.Critical, "prio-kritisch")]
    public void Nur_hohe_Prioritaeten_werden_markiert(TicketPriority prioritaet, string klasse)
    {
        Assert.Equal(klasse, prioritaet.PrioKlasse());
    }

    [Theory]
    [InlineData(SlaState.Ueberfaellig)]
    [InlineData(SlaState.BaldFaellig)]
    [InlineData(SlaState.VerspaetetErfuellt)]
    [InlineData(SlaState.Erfuellt)]
    public void Jeder_meldende_Zustand_bekommt_eine_eigene_Farbe(SlaState zustand)
    {
        // Eine Klasse, die die Palette nicht kennt, fällt still auf neutral; die
        // Ampel wäre dann unsichtbar, ohne dass etwas fehlschlägt.
        var neutral = Palette.Badge("unbekannte-klasse");

        Assert.NotEqual(neutral, Palette.Badge(zustand.BadgeKlasse()));
    }

    [Fact]
    public void Ueberfaellig_traegt_die_Gefahrenfarbe_und_Eigenschaften_bleiben_leise()
    {
        Assert.Equal(Palette.GefahrFlaeche, Palette.Badge("badge-frist-ueberfaellig").Flaeche);
        Assert.Equal(Palette.Badge("unbekannte-klasse"), Palette.Badge("badge-eigenschaft"));
    }

    [Fact]
    public void Die_Prioritaetsfarben_folgen_der_Markierungsregel()
    {
        Assert.Equal(Palette.GefahrText, Palette.Prioritaet("prio-kritisch"));
        Assert.NotEqual(Palette.Prioritaet("prio-kritisch"), Palette.Prioritaet("prio-hoch"));
        Assert.Equal(Palette.Prioritaet(""), Palette.Prioritaet("irgendwas"));
    }
}
