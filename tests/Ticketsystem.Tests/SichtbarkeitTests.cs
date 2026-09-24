using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Was sichtbar sein muss, ist sichtbar. Gemessen wird am gerenderten Bild
// statt an der Absicht, denn genau das war der Befund: Aktion und
// Begründung lagen unterhalb der Fensterkante, der Mensch klickte, und das
// Fenster schien nichts zu tun.
public sealed class SichtbarkeitTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Headless rendert nur auf Anforderung; ohne diesen Durchlauf haben alle
    // Bausteine die Größe null.
    private static void Auslegen(Window fenster, double breite, double hoehe)
    {
        fenster.Show();
        fenster.Measure(new Size(breite, hoehe));
        fenster.Arrange(new Rect(0, 0, breite, hoehe));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        fenster.UpdateLayout();
    }

    private static double Unterkante(Window fenster, Control baustein)
    {
        var oben = baustein.TranslatePoint(new Point(0, 0), fenster)
            ?? throw new InvalidOperationException($"{baustein.Name} liegt nicht im Fenster.");
        return oben.Y + baustein.Bounds.Height;
    }

    private async Task<Ticket> TicketAsync(string titel, string beschreibung = "Papierstau.")
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            titel, beschreibung, TicketPriority.Medium, "Weber, Sabine", "0221123456",
            DateTime.UtcNow, null, TestDaten.Teamleitung.Id, TestDaten.Teamleitung.Name, "A-101");
    }

    [AvaloniaFact]
    public async Task Aktion_und_Fehlerliste_der_Erfassung_bleiben_im_Fenster()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster, 620, 760);
        await fenster.AnlegenAsync();
        Auslegen(fenster, 620, 760);

        Assert.True(Unterkante(fenster, fenster.Anlegen) <= 760,
            $"Der Anlegen-Knopf endet bei y={Unterkante(fenster, fenster.Anlegen):F0} und damit unter der Kante.");
        // Gemessen wird der sichtbare Bereich, nicht der Inhalt: Bei vielen
        // Abweisungen ist die Liste absichtlich länger als ihr Fenster und scrollt
        // darin.
        Assert.True(Unterkante(fenster, fenster.Fehlerbereich) <= 760,
            $"Die Fehlerliste endet bei y={Unterkante(fenster, fenster.Fehlerbereich):F0} und damit unter der Kante.");
        Assert.NotEmpty((System.Collections.IEnumerable)fenster.Fehlerliste.ItemsSource!);
        fenster.Close();
    }

    // Die Sammelliste am Fußende sagt, was fehlt, aber nicht wo; der Fokus
    // sagt beides und spart den Weg zur Maus.
    [AvaloniaFact]
    public async Task Nach_einem_Fehlversuch_steht_der_Fokus_im_ersten_bemaengelten_Feld()
    {
        var fenster = new ErfassungsFenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster, 620, 760);
        fenster.Kunde.Text = "Weber, Sabine";
        fenster.Nummer.Text = "0221123456";
        fenster.Beschreibung.Text = "Papierstau.";
        fenster.Adresse.Text = "A-101";

        await fenster.AnlegenAsync();

        Assert.True(fenster.Titel.IsFocused, "Der Fokus müsste im fehlenden Titelfeld stehen.");
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Das_Kommentarfeld_bleibt_auch_bei_langem_Verlauf_im_Fenster()
    {
        var ticket = await TicketAsync("Drucker klemmt");
        using (var scope = _factory.Services.CreateScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<TicketService>();
            for (var i = 0; i < 15; i++)
            {
                await dienst.AddCommentAsync(ticket.Id, TestDaten.Teamleitung, $"Zwischenstand {i}.");
            }
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        Auslegen(fenster, 1180, 760);
        await fenster.LadenAsync();
        Auslegen(fenster, 1180, 760);

        Assert.True(Unterkante(fenster, fenster.Senden) <= 760,
            $"Der Senden-Knopf endet bei y={Unterkante(fenster, fenster.Senden):F0} und damit unter der Kante.");
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Historie_steht_in_der_Akte_und_nicht_zwischen_den_Aktionen()
    {
        var ticket = await TicketAsync("Drucker klemmt");
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        Auslegen(fenster, 1180, 760);
        await fenster.LadenAsync();

        Assert.DoesNotContain(fenster.Verlauf,
            fenster.AktionsSpalte.GetVisualDescendants().OfType<Control>());
        fenster.Close();
    }

    // 680 px sind bei 14 px Schrift rund 90 Zeichen. Ohne Grenze wurde das
    // Fenster durch Maximieren schlechter lesbar statt besser: gemessen 1450 px.
    // Der Text muss lang sein, sonst begrenzt schon die Textmenge die Breite.
    [AvaloniaFact]
    public async Task Fliesstext_bleibt_bei_rund_neunzig_Zeichen_lesbar()
    {
        var lang = string.Join(" ", Enumerable.Repeat(
            "Der Anwender meldet, dass der Etikettendrucker im Lager kein Papier einzieht.", 8));
        var ticket = await TicketAsync("Drucker klemmt", lang);
        var fenster = new DetailFenster(_factory.Services, TestDaten.Teamleitung, ticket.Id);
        Auslegen(fenster, 1920, 900);
        await fenster.LadenAsync();
        Auslegen(fenster, 1920, 900);

        Assert.True(fenster.Beschreibung.Bounds.Width <= 680,
            $"Die Beschreibung ist {fenster.Beschreibung.Bounds.Width:F0} px breit.");
        fenster.Close();
    }

    // Wer „Nur meine" gesetzt hat und es vergisst, sieht eine kurze Liste ohne
    // erkennbaren Grund; der Zähler nennt beide Zahlen.
    [AvaloniaFact]
    public async Task Der_Trefferzaehler_erklaert_die_kurze_Liste()
    {
        await TicketAsync("Drucker klemmt");
        await TicketAsync("Bildschirm bleibt schwarz");
        var fenster = new Grundfenster(_factory.Services, TestDaten.Teamleitung);
        Auslegen(fenster, 1240, 760);
        await fenster.LadenAsync();
        var ohneFilter = fenster.Zaehler.Text;

        fenster.Suche.Text = "Drucker";
        await fenster.LadenAsync();

        Assert.Equal("2 Vorgänge", ohneFilter);
        Assert.Equal("1 von 2 Vorgängen", fenster.Zaehler.Text);
        fenster.Close();
    }

    public void Dispose() => _factory.Dispose();
}
