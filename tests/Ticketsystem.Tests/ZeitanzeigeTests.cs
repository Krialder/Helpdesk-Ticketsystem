using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Gespeichert wird UTC, angezeigt wird Ortszeit. Vorher war beides UTC, an
// fünfzehn von siebzehn Stellen ohne Kennzeichnung, und eine Frist „bis
// 08:00" war in Wirklichkeit 10:00. Alle Tests reichen die Zeitzone
// ausdrücklich herein: Ein Test, der sich auf die Zone des Rechners
// verlässt, prüft je nach Rechner etwas anderes.
public sealed class ZeitanzeigeTests
{
    private static readonly TimeZoneInfo Berlin =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Theory]
    // Winterzeit: eine Stunde vor. Sommerzeit: zwei.
    [InlineData("2026-01-15 08:00", "15.01.2026 09:00")]
    [InlineData("2026-08-15 08:00", "15.08.2026 10:00")]
    public void Ein_gespeicherter_Zeitpunkt_erscheint_in_Ortszeit(string utc, string erwartet) =>
        Assert.Equal(erwartet, DateTime.Parse(utc).Anzeige(Berlin));

    [Fact]
    public void Auch_ueber_die_Tagesgrenze_stimmt_der_Tag()
    {
        Assert.Equal("16.08.2026 01:30", DateTime.Parse("2026-08-15 23:30").Anzeige(Berlin));
        Assert.Equal("16.08.2026", DateTime.Parse("2026-08-15 23:30").TagesAnzeige(Berlin));
    }

    [Fact]
    public void Ohne_Zeitpunkt_gibt_es_nichts_anzuzeigen() =>
        Assert.Null(((DateTime?)null).Anzeige(Berlin));

    [Theory]
    [InlineData("2026-01-15 09:00", "2026-01-15 08:00")]
    [InlineData("2026-08-15 10:00", "2026-08-15 08:00")]
    public void Was_aus_einem_Formular_kommt_wird_zurueckgerechnet(string ortszeit, string erwartetUtc) =>
        Assert.Equal(DateTime.Parse(erwartetUtc), Zeitanzeige.NachUtc(DateTime.Parse(ortszeit), Berlin));

    [Theory]
    [InlineData("2026-02-03 14:25")]
    [InlineData("2026-07-03 14:25")]
    public void Hin_und_zurueck_ergibt_wieder_denselben_Zeitpunkt(string utc)
    {
        var gespeichert = DateTime.Parse(utc);

        var zurueck = Zeitanzeige.NachUtc(Zeitanzeige.AlsOrtszeit(gespeichert, Berlin), Berlin);

        Assert.Equal(gespeichert, zurueck);
    }

    // Die Oberfläche abzulesen ginge nicht: Der Prüfrechner läuft auf UTC, dort
    // ist die Ortszeit dieselbe, und der Fehler wäre unsichtbar. Gesucht wird
    // jedes Datums- oder Uhrzeitmuster in einem ToString-Aufruf, nicht nur
    // „dd.MM.yyyy": Die Historie formatiert mit „dd.MM. HH:mm".
    [Fact]
    public void Kein_Fenster_formatiert_einen_Zeitpunkt_an_der_Umrechnung_vorbei()
    {
        var fenster = Directory.GetFiles(AppOrdner(), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(AppOrdner(), "*.axaml", SearchOption.AllDirectories))
            .Where(datei => !datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(fenster);

        var muster = new System.Text.RegularExpressions.Regex(
            @"ToString\(""[^""]*(?:dd|MM|yyyy|HH|mm)[^""]*""");

        var verstoesse = fenster
            .Select(datei => (Datei: Path.GetFileName(datei), Inhalt: File.ReadAllText(datei)))
            .Where(a => muster.IsMatch(a.Inhalt))
            .Select(a => a.Datei)
            .ToList();

        Assert.True(verstoesse.Count == 0,
            "Diese Dateien formatieren einen Zeitpunkt selbst statt über Anzeige(): "
            + string.Join(", ", verstoesse));
    }

    // Nur String-Literale zählen: Ein Methodenname wie NachUtc ist keine
    // Beschriftung, die ein Nutzer je sieht.
    [Fact]
    public void Kein_Fenster_beschriftet_eine_Ortszeit_als_Weltzeit()
    {
        var literal = new System.Text.RegularExpressions.Regex("\"[^\"]*UTC[^\"]*\"");
        var fenster = Directory.GetFiles(
                Path.Combine(AppOrdner(), "Fenster"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(
                Path.Combine(AppOrdner(), "Fenster"), "*.axaml", SearchOption.AllDirectories))
            .ToArray();
        Assert.NotEmpty(fenster);

        var verstoesse = fenster
            .Select(datei => (Datei: Path.GetFileName(datei), Inhalt: File.ReadAllText(datei)))
            .Where(a => literal.IsMatch(a.Inhalt))
            .Select(a => a.Datei)
            .ToList();

        Assert.True(verstoesse.Count == 0,
            "Diese Fenster nennen UTC, zeigen aber Ortszeit: " + string.Join(", ", verstoesse));
    }

    // Der Export schreibt weiterhin UTC, denn die Datei ist zum Wiedereinlesen
    // da, und eine Zeit ohne Bezug ist bei einem Umzug nicht mehr zu retten.
    [Fact]
    public void Die_Tabellenausgabe_benennt_ihre_Zeiten_als_Weltzeit()
    {
        var quelle = File.ReadAllText(Path.Combine(
            Quellordner(), "src", "Ticketsystem.Kern", "Services", "AustauschService.cs"));

        Assert.Contains("\"Erstellt (UTC)\"", quelle);
        Assert.Contains("Weltzeit", quelle);
    }

    private static string Quellordner()
    {
        var ordner = AppContext.BaseDirectory;
        while (ordner is not null && !File.Exists(Path.Combine(ordner, "Ticketsystem.sln")))
        {
            ordner = Directory.GetParent(ordner)?.FullName;
        }

        return ordner ?? throw new InvalidOperationException("Die Projektwurzel wurde nicht gefunden.");
    }

    private static string AppOrdner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var kandidat = Path.Combine(ordner.FullName, "src", "Ticketsystem.App");
            if (Directory.Exists(kandidat))
            {
                return kandidat;
            }

            ordner = ordner.Parent;
        }

        throw new DirectoryNotFoundException("Der App-Ordner wurde nicht gefunden.");
    }
}
