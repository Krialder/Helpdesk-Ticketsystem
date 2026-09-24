using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;
using static Ticketsystem.Kern.Services.StammdatenService;

namespace Ticketsystem.Tests;

// Was die Anwendung über einen Anrufer schon weiß, zeigt sie beim Namen,
// statt es in einer Auswahlliste zu verstecken, die erst beim Tippen ins
// Feld aufgeht. Rufnummer und Raum sind genau die zwei Werte, die man am
// Telefon nicht erfragen möchte, wenn sie schon im Bestand stehen.
public sealed class KontaktvorschlagTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private static KontaktTreffer Treffer(string name, string? adresse = "A-101", string? nummer = "0221123456") =>
        new(name, adresse, nummer);

    [Fact]
    public void Genau_ein_Treffer_wird_angeboten()
    {
        var vorschlag = Erfassung.VorschlagWaehlen([Treffer("Weber, Sabine")], "Web");

        Assert.NotNull(vorschlag);
        Assert.Equal("Weber, Sabine", vorschlag.Name);
        Assert.Equal("A-101", vorschlag.Adresse);
        Assert.Equal("0221123456", vorschlag.Rufnummer);
    }

    [Fact]
    public void Bei_mehreren_Treffern_wird_nichts_angeboten()
    {
        var vorschlag = Erfassung.VorschlagWaehlen(
            [Treffer("Weber, Sabine"), Treffer("Weber, Thomas")], "Web");

        Assert.Null(vorschlag);
    }

    // „Weber" trifft drei Personen, aber eine heißt genau so; dann ist sie
    // gemeint und nicht die längeren Namen.
    [Fact]
    public void Der_exakte_Name_gewinnt_gegen_die_Menge()
    {
        var vorschlag = Erfassung.VorschlagWaehlen(
            [Treffer("Weber", "B-12"), Treffer("Weber, Sabine"), Treffer("Weberling")], "weber");

        Assert.NotNull(vorschlag);
        Assert.Equal("Weber", vorschlag.Name);
        Assert.Equal("B-12", vorschlag.Adresse);
    }

    [Fact]
    public void Ohne_Treffer_gibt_es_keinen_Vorschlag()
    {
        Assert.Null(Erfassung.VorschlagWaehlen([], "Weber"));
    }

    private async Task<ErfassungsFenster> FensterMitKontaktAsync(bool alsMail = false, string name = "Weber, Sabine")
    {
        using (var scope = _factory.Services.CreateScope())
        {
            // Zwei Anrufe derselben Person mit zwei Nummern; der Bestand führt den
            // Stand des letzten Anrufs, und der Vorschlag zeigt genau den.
            var stammdaten = scope.ServiceProvider.GetRequiredService<StammdatenService>();
            await stammdaten.ErfasseAsync("Weber, Sabine", "0221123456", "A-101", TestDaten.Teamleitung);
            await stammdaten.ErfasseAsync("Weber, Sabine", "015123423423", "A-101", TestDaten.Teamleitung);
        }

        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung, alsMail);
        fenster.Show();
        // Erst mit Vorlage verhält sich die AutoCompleteBox wie ein Feld; ohne
        // Layout schluckt sie einen gesetzten Text.
        fenster.Measure(new Size(620, 760));
        fenster.Arrange(new Rect(0, 0, 620, 760));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();

        fenster.Kunde.Text = name;
        await fenster.KontaktVorschlagAsync();
        return fenster;
    }

    private static IReadOnlyList<Button> Werte(ErfassungsFenster fenster) =>
        fenster.VorschlagsWerte.GetVisualDescendants().OfType<Button>().ToList();

    [AvaloniaFact]
    public async Task Die_bekannten_Werte_stehen_beim_Namen_ohne_Klick_ins_Feld()
    {
        var fenster = await FensterMitKontaktAsync();

        Assert.True(fenster.VorschlagsZeile.IsVisible);
        // Der Name steht schon im Feld, also nennt ihn die Beschriftung nicht ein
        // zweites Mal; die Nummer des ersten Anrufs ist ersetzt, nicht ergänzt.
        Assert.Equal("Bekannt:", fenster.VorschlagsName.Text);
        Assert.Equal(
            ["015123423423", "A-101"],
            Werte(fenster).Select(k => k.Content?.ToString()));
        fenster.Close();
    }

    // Der Name ist der Wert, an dem die Stammdaten hängen: Tippt jemand ihn
    // anders, entsteht beim nächsten Anruf ein zweiter Stammsatz.
    [AvaloniaFact]
    public async Task Der_volle_Name_ist_der_erste_Vorschlag()
    {
        var fenster = await FensterMitKontaktAsync(name: "Web");

        var erster = Werte(fenster)[0];
        Assert.Equal("Weber, Sabine", erster.Content?.ToString());

        erster.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        await fenster.KontaktVorschlagAsync();

        Assert.Equal("Weber, Sabine", fenster.Kunde.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Steht_der_Name_schon_da_wird_er_nicht_noch_einmal_angeboten()
    {
        var fenster = await FensterMitKontaktAsync();

        Assert.DoesNotContain("Weber, Sabine", Werte(fenster).Select(k => k.Content?.ToString()));
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_Klick_traegt_den_Wert_ein()
    {
        var fenster = await FensterMitKontaktAsync();

        Werte(fenster)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("015123423423", fenster.Nummer.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_eigener_Wert_verdeckt_den_Vorschlag_und_gibt_ihn_wieder_frei()
    {
        var fenster = await FensterMitKontaktAsync();

        fenster.Nummer.Text = "0301111111";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(["A-101"], Werte(fenster).Select(k => k.Content?.ToString()));

        fenster.Adresse.Text = "C-9";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(fenster.VorschlagsZeile.IsVisible);

        fenster.Nummer.Text = "";
        fenster.Adresse.Text = "";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(fenster.VorschlagsZeile.IsVisible);
        Assert.Equal(2, Werte(fenster).Count);
        fenster.Close();
    }

    // Eine still vorbelegte Adresse sieht nach Wissen aus, ist aber eine
    // Behauptung: Wer umgezogen ist, bekäme die alte Adresse ins Ticket, ohne
    // dass jemand sie bestätigt hätte.
    [AvaloniaFact]
    public async Task Die_Adresse_wird_nicht_mehr_still_vorbelegt()
    {
        var fenster = await FensterMitKontaktAsync();

        Assert.True(string.IsNullOrEmpty(fenster.Adresse.Text));
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Bei_einer_Mail_wird_keine_Rufnummer_angeboten()
    {
        var fenster = await FensterMitKontaktAsync(alsMail: true);

        Assert.Equal(["A-101"], Werte(fenster).Select(k => k.Content?.ToString()));
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
