using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Tests;

// Die Regel entscheidet, ob die Erfassung eine Nummer annimmt. Die Tabellen
// halten die Grenzen der vier Formate fest, damit eine Änderung an der Regel
// nicht unbemerkt eine Art verschluckt.
public class RufnummerRegelTests
{
    [Theory]
    [InlineData("4711", RufnummerArt.Durchwahl)]
    [InlineData("47", RufnummerArt.Durchwahl)]
    [InlineData("471147", RufnummerArt.Durchwahl)]
    [InlineData("0521 12345", RufnummerArt.Festnetz)]
    [InlineData("05211234567", RufnummerArt.Festnetz)]
    [InlineData("052191234567", RufnummerArt.FestnetzLang)]
    [InlineData("0151 2345678", RufnummerArt.Mobil)]
    [InlineData("017612345678", RufnummerArt.Mobil)]
    public void Gueltige_Nummern_werden_der_richtigen_Art_zugeordnet(string eingabe, RufnummerArt erwartet) =>
        Assert.Equal(erwartet, RufnummerRegel.Erkenne(eingabe));

    [Theory]
    [InlineData("4")]                 // zu kurz für eine Durchwahl
    [InlineData("4711471")]           // zu lang für eine Durchwahl
    [InlineData("0521123")]           // zu kurz für Festnetz
    [InlineData("0521912345678")]     // länger als die lange Vorwahl erlaubt
    [InlineData("0151 234")]          // Mobil zu kurz
    [InlineData("0110 12345")]        // Sonderrufnummer, nicht vorgesehen
    [InlineData("keine Nummer")]
    [InlineData("")]
    public void Ungueltige_Nummern_fallen_durch(string eingabe) =>
        Assert.Null(RufnummerRegel.Erkenne(eingabe));

    // Am Telefon tippt jemand die Null und wird unterbrochen. Die Prüfung griff
    // nach der führenden Null ungeprüft auf die zweite Ziffer zu, und mit der
    // Ausnahme war das getippte Formular weg.
    [Theory]
    [InlineData("0")]
    [InlineData(" 0 ")]
    [InlineData("0-")]
    public void Eine_einzelne_Null_faellt_durch_statt_abzustuerzen(string eingabe)
    {
        Assert.Null(RufnummerRegel.Erkenne(eingabe));
    }

    [Fact]
    public void Normalisieren_behaelt_nur_Ziffern()
    {
        // Sonst landet dieselbe Nummer in mehreren Schreibweisen im Bestand.
        Assert.Equal("052112345", RufnummerRegel.Normalisieren("0521 / 12-345"));
    }
}
