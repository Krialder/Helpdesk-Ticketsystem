using Ticketsystem.Kern.Praesentation;

namespace Ticketsystem.Tests;

// Auszeichnung im Wissensartikel: Gespeichert bleibt reiner Text, der Kern
// zerlegt ihn in Blöcke. Die Teilmenge ist klein und so gewählt, dass
// Zeichen aus Helpdesk-Texten (*.docx, datei_name, ein einzelnes Sternchen)
// nichts auslösen: Ein alter Artikel darf nicht plötzlich anders aussehen.
public sealed class WissenstextTests
{
    private static string Klartext(IReadOnlyList<Lauf> laeufe) => string.Concat(laeufe.Select(l => l.Text));

    [Fact]
    public void Ein_Text_ohne_Auszeichnung_bleibt_ein_Absatz_mit_seinen_Zeilen()
    {
        var bloecke = Wissenstext.Lesen("sf\nasfrw33");

        var absatz = Assert.IsType<Absatz>(Assert.Single(bloecke));
        Assert.Equal(2, absatz.Zeilen.Count);
        Assert.Equal("sf", Klartext(absatz.Zeilen[0]));
        Assert.Equal("asfrw33", Klartext(absatz.Zeilen[1]));
        Assert.All(absatz.Zeilen.SelectMany(z => z), l => Assert.False(l.Fett || l.Festbreite));
    }

    [Fact]
    public void Eine_Leerzeile_trennt_Absaetze()
    {
        var bloecke = Wissenstext.Lesen("Erster.\n\n\n\nZweiter.");

        Assert.Equal(2, bloecke.Count);
        Assert.Equal("Erster.", Klartext(Assert.IsType<Absatz>(bloecke[0]).Zeilen[0]));
        Assert.Equal("Zweiter.", Klartext(Assert.IsType<Absatz>(bloecke[1]).Zeilen[0]));
    }

    // Eine Stufe, nicht sechs: Der Artikel hat schon einen Titel, und eine
    // Anleitung fürs Telefon braucht Zwischenüberschriften, keine
    // Gliederungstiefe.
    [Fact]
    public void Rauten_am_Zeilenanfang_machen_eine_Ueberschrift()
    {
        var bloecke = Wissenstext.Lesen("## Vorgehen\n# Auch\n### Auch das\n#ohne Leerzeichen bleibt Text");

        Assert.Equal("Vorgehen", Assert.IsType<Ueberschrift>(bloecke[0]).Text);
        Assert.Equal("Auch", Assert.IsType<Ueberschrift>(bloecke[1]).Text);
        Assert.Equal("Auch das", Assert.IsType<Ueberschrift>(bloecke[2]).Text);
        Assert.Equal("#ohne Leerzeichen bleibt Text", Klartext(Assert.IsType<Absatz>(bloecke[3]).Zeilen[0]));
        Assert.Equal(4, bloecke.Count);
    }

    [Fact]
    public void Doppelte_Sterne_machen_fett()
    {
        var laeufe = Wissenstext.Laeufe("Vorher **Sicherung** anlegen, dann **neu starten**.");

        Assert.Collection(laeufe,
            l => Assert.Equal(("Vorher ", false), (l.Text, l.Fett)),
            l => Assert.Equal(("Sicherung", true), (l.Text, l.Fett)),
            l => Assert.Equal((" anlegen, dann ", false), (l.Text, l.Fett)),
            l => Assert.Equal(("neu starten", true), (l.Text, l.Fett)),
            l => Assert.Equal((".", false), (l.Text, l.Fett)));
    }

    // Das ist die Grenze der Teilmenge, nicht eine Lücke. Ein offenes ** ist
    // ein Tippfehler und wird gezeigt statt verschluckt.
    [Fact]
    public void Einzelne_Sterne_Unterstriche_und_offene_Doppelsterne_bleiben_Text()
    {
        foreach (var zeile in new[] { "*.docx nach datei_name_alt kopieren", "Preis ** offen", "a * b * c", "****" })
        {
            var lauf = Assert.Single(Wissenstext.Laeufe(zeile));
            Assert.Equal(zeile, lauf.Text);
            Assert.False(lauf.Fett);
        }
    }

    [Fact]
    public void Rueckwaertsapostrophe_machen_Festbreite_und_schuetzen_ihren_Inhalt()
    {
        var laeufe = Wissenstext.Laeufe("Dann `ipconfig /flushdns` und `**kein** Fett` eingeben");

        Assert.Collection(laeufe,
            l => Assert.Equal(("Dann ", false), (l.Text, l.Festbreite)),
            l => Assert.Equal(("ipconfig /flushdns", true), (l.Text, l.Festbreite)),
            l => Assert.Equal((" und ", false), (l.Text, l.Festbreite)),
            l => Assert.Equal(("**kein** Fett", true), (l.Text, l.Festbreite)),
            l => Assert.Equal((" eingeben", false), (l.Text, l.Festbreite)));
        Assert.All(laeufe, l => Assert.False(l.Fett));

        Assert.Equal("Taste ` drücken", Assert.Single(Wissenstext.Laeufe("Taste ` drücken")).Text);
    }

    // Ohne Leerzeichen nach dem Strich ist es kein Aufzählungspunkt, sondern
    // ein Wort mit Bindestrich (etwa „-v" als Schalter).
    [Fact]
    public void Striche_am_Zeilenanfang_bilden_eine_Aufzaehlung()
    {
        var bloecke = Wissenstext.Lesen("- Kabel prüfen\n- Adapter neu starten\n* Auch der Stern gilt\n-e etfgg");

        var liste = Assert.IsType<Aufzaehlung>(bloecke[0]);
        Assert.False(liste.Nummeriert);
        Assert.Equal(["Kabel prüfen", "Adapter neu starten", "Auch der Stern gilt"], liste.Eintraege.Select(Klartext));
        Assert.Equal("-e etfgg", Klartext(Assert.IsType<Absatz>(bloecke[1]).Zeilen[0]));
        Assert.Equal(2, bloecke.Count);
    }

    // Gezählt wird bei der Anzeige, nicht beim Tippen: Wer einen Schritt in der
    // Mitte einfügt, muss nicht umnummerieren. Nur die erste Zahl zählt, damit
    // eine Liste nach einem Absatz bei 3 weitergehen kann.
    [Fact]
    public void Zahlen_mit_Punkt_bilden_Schritte_und_gezaehlt_wird_ab_der_ersten()
    {
        var bloecke = Wissenstext.Lesen("1. Profil entfernen\n1. Neu verbinden\n7) Anmelden");

        var liste = Assert.IsType<Aufzaehlung>(Assert.Single(bloecke));
        Assert.True(liste.Nummeriert);
        Assert.Equal(1, liste.Beginn);
        Assert.Equal(["Profil entfernen", "Neu verbinden", "Anmelden"], liste.Eintraege.Select(Klartext));

        var spaeter = Assert.IsType<Aufzaehlung>(Assert.Single(Wissenstext.Lesen("3. Weiter\n4. Fertig")));
        Assert.Equal(3, spaeter.Beginn);
    }

    [Fact]
    public void Ein_Absatz_oder_ein_Wechsel_der_Art_beendet_die_Liste()
    {
        var bloecke = Wissenstext.Lesen("1. Eins\nDazwischen Text\n2. Zwei\n- Punkt");

        Assert.Equal(4, bloecke.Count);
        var erste = Assert.IsType<Aufzaehlung>(bloecke[0]);
        Assert.Equal(1, erste.Beginn);
        Assert.Single(erste.Eintraege);
        Assert.Equal("Dazwischen Text", Klartext(Assert.IsType<Absatz>(bloecke[1]).Zeilen[0]));
        var zweite = Assert.IsType<Aufzaehlung>(bloecke[2]);
        Assert.True(zweite.Nummeriert);
        Assert.Equal(2, zweite.Beginn);
        Assert.False(Assert.IsType<Aufzaehlung>(bloecke[3]).Nummeriert);
    }

    [Fact]
    public void Drei_Apostrophe_rahmen_einen_Befehlsblock_wortgetreu()
    {
        var bloecke = Wissenstext.Lesen("Vorher:\n```\n  net use \\\\srv\\share **x**\n- kein Punkt\n```\nDanach.");

        Assert.Equal(3, bloecke.Count);
        Assert.Equal("  net use \\\\srv\\share **x**\n- kein Punkt", Assert.IsType<Befehlsblock>(bloecke[1]).Text);
        Assert.Equal("Danach.", Klartext(Assert.IsType<Absatz>(bloecke[2]).Zeilen[0]));

        // Ein offener Block läuft bis zum Ende, statt den Rest zu verschlucken:
        // Beim Tippen ist er immer kurz offen.
        var offen = Wissenstext.Lesen("```\nnetsh wlan show interfaces");
        Assert.Equal("netsh wlan show interfaces", Assert.IsType<Befehlsblock>(Assert.Single(offen)).Text);
    }

    [Fact]
    public void Auszeichnung_gilt_auch_in_Listeneintraegen()
    {
        var liste = Assert.IsType<Aufzaehlung>(Assert.Single(Wissenstext.Lesen("- **Wichtig:** `cmd` öffnen")));

        Assert.Collection(liste.Eintraege[0],
            l => Assert.True(l is { Text: "Wichtig:", Fett: true }),
            l => Assert.Equal(" ", l.Text),
            l => Assert.True(l is { Text: "cmd", Festbreite: true }),
            l => Assert.Equal(" öffnen", l.Text));
    }

    [Fact]
    public void Windows_Zeilenenden_und_Leerraum_am_Rand_stoeren_nicht()
    {
        var bloecke = Wissenstext.Lesen("## Kopf\r\n   - eingerückt \r\n\r\n  Text  ");

        Assert.Equal(3, bloecke.Count);
        Assert.Equal("Kopf", Assert.IsType<Ueberschrift>(bloecke[0]).Text);
        Assert.Equal("eingerückt", Klartext(Assert.IsType<Aufzaehlung>(bloecke[1]).Eintraege[0]));
        Assert.Equal("Text", Klartext(Assert.IsType<Absatz>(bloecke[2]).Zeilen[0]));
    }

    [Fact]
    public void Leerer_Text_und_leere_Markierungen_ergeben_nichts_Besonderes()
    {
        Assert.Empty(Wissenstext.Lesen(null));
        Assert.Empty(Wissenstext.Lesen(""));
        Assert.Empty(Wissenstext.Lesen("  \n\r\n "));

        var bloecke = Wissenstext.Lesen("##\n1.\n-");
        var absatz = Assert.IsType<Absatz>(Assert.Single(bloecke));
        Assert.Equal(["##", "1.", "-"], absatz.Zeilen.Select(Klartext));
    }
}
