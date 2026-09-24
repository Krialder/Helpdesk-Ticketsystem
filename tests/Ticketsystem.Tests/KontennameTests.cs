using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Ein Konto hat zwei Namen mit verschiedenen Aufgaben: Der volle Name ist
// unveränderlich und steht in der Akte, der Anzeigename ist änderbar und
// steht nur auf dem Schirm. Eine E-Mail-Adresse ist ein Zustellweg und
// taugt nicht als Antwort auf „wer war das?".
public sealed class KontennameTests
{
    [Theory]
    [InlineData("Weber", "Sabine", "Weber, Sabine")]
    [InlineData("Weber", null, "Weber")]
    [InlineData("Weber", "  ", "Weber")]
    [InlineData(null, "Sabine", "Sabine")]
    public void Der_volle_Name_wird_wie_bei_Kunden_geschrieben(string? nachname, string? vorname, string erwartet)
    {
        // Nachname zuerst, damit beim Sortieren nicht der Vorname führt.
        Assert.Equal(erwartet, Kontenname.Voll(nachname, vorname, rueckfall: "w@example.org"));
    }

    [Fact]
    public void Ohne_Namen_bleibt_die_Adresse_die_ehrliche_Antwort()
    {
        // Bestandskonten haben keinen Namen, bis die Administration ihn setzt; ein
        // erfundener Name sähe aus wie Wissen.
        Assert.Equal("w@example.org", Kontenname.Voll(null, null, rueckfall: "w@example.org"));
        Assert.Equal("w@example.org", Kontenname.Voll("  ", "", rueckfall: "w@example.org"));
    }

    [Fact]
    public void Der_Anzeigename_gewinnt_nur_fuer_die_Anzeige()
    {
        Assert.Equal("Sabine", Kontenname.Anzeige("Sabine", "Weber", "Sabine", "w@example.org"));
        Assert.Equal("Weber, Sabine", Kontenname.Anzeige(null, "Weber", "Sabine", "w@example.org"));
        Assert.Equal("Weber, Sabine", Kontenname.Anzeige("   ", "Weber", "Sabine", "w@example.org"));
        Assert.Equal("w@example.org", Kontenname.Anzeige(null, null, null, "w@example.org"));
    }

    [Fact]
    public void Der_Akteur_trennt_beides_ebenfalls()
    {
        var mitAnzeige = new Akteur("1", "Weber, Sabine", RoleLevel.Bearbeiter, "Sabine");
        var ohne = new Akteur("2", "Huber, Karl", RoleLevel.Bearbeiter);

        Assert.Equal("Weber, Sabine", mitAnzeige.Name);
        Assert.Equal("Sabine", mitAnzeige.Anzeige);
        Assert.Equal("Huber, Karl", ohne.Anzeige);
    }
}
