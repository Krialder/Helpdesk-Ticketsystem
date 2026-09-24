using Avalonia.Headless.XUnit;
using Ticketsystem.App.Fenster;
using Ticketsystem.App.Stil;

namespace Ticketsystem.Tests;

// Ein Knopf verspricht mit seinem Aussehen, was er tut: Was löscht oder
// überschreibt, ist rot (Auslöser umrandet, Zusage gefüllt), alles andere
// neutral. Abbrechen fragt zwar nach, löscht aber nichts, und darf nicht
// aussehen wie Löschen. Die Lesbarkeit der roten Töne prüft PaletteTests.
public sealed class GefahrknopfTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [AvaloniaFact]
    public void Eine_Bestaetigung_ist_gefaehrlich_bis_jemand_das_Gegenteil_sagt()
    {
        var baustein = new Bestaetigung();
        Assert.True(baustein.Gefaehrlich);
        Assert.Contains("gefahr", baustein.AusloeserKnopf.Classes);
        Assert.Contains("gefahr", baustein.ZusageKnopf.Classes);
        Assert.Contains("voll", baustein.ZusageKnopf.Classes);
        Assert.DoesNotContain("voll", baustein.AusloeserKnopf.Classes);

        baustein.Gefaehrlich = false;
        Assert.DoesNotContain("gefahr", baustein.AusloeserKnopf.Classes);
        Assert.DoesNotContain("gefahr", baustein.ZusageKnopf.Classes);
        Assert.DoesNotContain("voll", baustein.ZusageKnopf.Classes);
    }

    [AvaloniaFact]
    public void Loeschen_und_Ueberschreiben_sind_rot_Abbrechen_und_Zurueck_nicht()
    {
        var wissen = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        var bearbeiten = new WissensBearbeitenDialog(_factory.Services, TestDaten.Teamleitung, null);
        var versionen = new FassungenDialog(_factory.Services, TestDaten.Teamleitung, 1);
        var detail = new DetailFenster(_factory.Services, TestDaten.Teamleitung, 1);
        var verwaltung = new VerwaltungsFenster(_factory.Services, TestDaten.Administration);
        var ordnung = new OrdnungDialog(_factory.Services, TestDaten.Teamleitung);
        var freigaben = new FreigabenDialog(_factory.Services, TestDaten.Teamleitung);

        Assert.True(wissen.ArtikelLoeschen.Gefaehrlich);
        Assert.True(detail.Abschliessen.Gefaehrlich);
        Assert.True(verwaltung.KontoEntfernen.Gefaehrlich);
        Assert.True(verwaltung.StammdatenLoeschen.Gefaehrlich);
        Assert.True(verwaltung.Zurueckspielen.Gefaehrlich);
        Assert.True(verwaltung.ImportJson.Gefaehrlich);
        Assert.Contains("gefahr", verwaltung.RuheLoeschen.Classes);
        Assert.Contains("gefahr", ordnung.KategorieWeg.Classes);
        Assert.Contains("gefahr", ordnung.TagWeg.Classes);
        Assert.Contains("gefahr", freigaben.Verwerfen.Classes);

        Assert.False(bearbeiten.Abbrechen.Gefaehrlich);
        // Der Rückweg auf eine Fassung legt eine neue an und nimmt keine weg.
        Assert.False(versionen.Zurueck.Gefaehrlich);
        // Eine Wiedervorlage wegzunehmen ist ein Handgriff, kein Verlust.
        Assert.DoesNotContain("gefahr", detail.WvEntfernen.Classes);
    }

    public void Dispose() => _factory.Dispose();
}
