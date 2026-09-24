using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Alles, was sich anklicken oder befüllen lässt, erklärt sich beim
// Überfahren. Der Wächter geht durch jedes Fenster mit Demodaten und meldet
// jedes Element ohne Hinweis, damit das nächste Fenster ihn nicht vergisst.
// Teile aus Vorlagen der Steuerelemente (der Pfeil einer ComboBox, der
// Kalender eines DatePickers) zählen nicht: Sie gehören dem Element, das den
// Hinweis trägt.
public sealed class HinweiseUeberallTests : IDisposable
{
    private readonly KernWirt _factory = new();

    public HinweiseUeberallTests()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
        TestDaten.KontoAnlegen(db, TestDaten.Teamleitung);
        TestDaten.KontoAnlegen(db, TestDaten.Administration);
        Ticketsystem.Kern.Start.Demodaten.AnlegenAsync(scope.ServiceProvider, TestDaten.Teamleitung).GetAwaiter().GetResult();
    }

    [AvaloniaFact]
    public async Task Jedes_klickbare_Element_jedes_Fensters_traegt_einen_Hinweis()
    {
        int ticketId, artikelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsystemContext>();
            ticketId = await db.Tickets.OrderBy(t => t.Id).Select(t => t.Id).FirstAsync();
            artikelId = await db.KbArticles.OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();
        }

        var tl = TestDaten.Teamleitung;
        var fehlend = new List<string>();

        await Pruefen(fehlend, new AnmeldeFenster(_factory.Services), _ => Task.CompletedTask);
        await Pruefen(fehlend, new Grundfenster(_factory.Services, tl), async f =>
        {
            await f.ZustandUebernehmenAsync();
            await f.AktualisierenAsync();
        });
        await Pruefen(fehlend, new LagebildFenster(_factory.Services, tl, (_, _) => Task.CompletedTask), f => f.LadenAsync());
        await Pruefen(fehlend, new DetailFenster(_factory.Services, tl, ticketId), f => f.LadenAsync());
        await Pruefen(fehlend, new ErfassungsFenster(_factory.Services, tl), _ => Task.CompletedTask);
        await Pruefen(fehlend, new ErfassungsFenster(_factory.Services, tl, alsMail: true), _ => Task.CompletedTask);
        await Pruefen(fehlend, new WissensFenster(_factory.Services, tl), async f =>
        {
            await f.LadenAsync();
            f.Liste.SelectedIndex = 0;
            await f.ArtikelZeigenAsync();
        });
        await Pruefen(fehlend, new WissensBearbeitenDialog(_factory.Services, tl, artikelId), _ => Task.CompletedTask);
        await Pruefen(fehlend, new FassungenDialog(_factory.Services, tl, artikelId), f => f.LadenAsync());
        await Pruefen(fehlend, new FreigabenDialog(_factory.Services, tl), f => f.LadenAsync());
        await Pruefen(fehlend, new OrdnungDialog(_factory.Services, tl), _ => Task.CompletedTask);
        // Die Verwaltung als Administration, denn nur sie sieht alle Reiter.
        await Pruefen(fehlend, new VerwaltungsFenster(_factory.Services, TestDaten.Administration), async f =>
        {
            await f.LadenAsync();
            await f.KontenLadenAsync();
            await f.DatenLadenAsync();
        });
        await Pruefen(fehlend, new KontoFenster(_factory.Services, tl, tl.Id), f => f.LadenAsync());
        await Pruefen(fehlend, new VorgangslisteFenster(_factory.Services, tl, Vorgangsfilter.FuerKunde("Huber, Karl")), f => f.LadenAsync());
        await Pruefen(fehlend, new EinstellungenDialog(_factory.Services, tl), f => f.LadenAsync());

        // Die ganze Liste in eine Datei, denn die Fehlermeldung zeigt nur den
        // Anfang.
        if (fehlend.Count > 0)
        {
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "hinweise-fehlend.txt"), fehlend);
        }

        Assert.True(fehlend.Count == 0,
            $"{fehlend.Count} Elemente ohne Hinweis: {string.Join("; ", fehlend.Take(40))}");
    }

    private static async Task Pruefen<T>(List<string> fehlend, T fenster, Func<T, Task> laden) where T : Window
    {
        fenster.Show();
        await laden(fenster);
        Messen.Auslegen(fenster, 1240, 760);
        // Reiter nacheinander, denn nur der offene Reiter hat einen Baum.
        var reiter = fenster.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        var seiten = reiter is null ? 1 : reiter.ItemCount;
        for (var i = 0; i < seiten; i++)
        {
            if (reiter is not null)
            {
                reiter.SelectedIndex = i;
                fenster.UpdateLayout();
            }

            foreach (var element in fenster.GetVisualDescendants().OfType<Control>())
            {
                if (!Bedienbar(element) || element.TemplatedParent is not null || !element.IsVisible)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ToolTip.GetTip(element)?.ToString()))
                {
                    fehlend.Add($"{fenster.GetType().Name}: {element.GetType().Name} {element.Name ?? Inhalt(element)}");
                }
            }
        }

        fenster.Close();
    }

    // Zeilen einer Liste sind der Rahmen ihrer Vorlage, der in den Fenstern die
    // Klasse „zeile" trägt.
    private static bool Bedienbar(Control c) =>
        c is Button or ToggleButton or CheckBox or ComboBox or TextBox or DatePicker or TimePicker
            or AutoCompleteBox or TabItem or Expander or ListBox
        || c is Border b && b.Classes.Contains("zeile");

    private static string Inhalt(Control c) => c switch
    {
        ContentControl { Content: string s } => $"„{s}“",
        HeaderedContentControl { Header: string h } => $"„{h}“",
        TextBox t => $"(Wasserzeichen „{t.Watermark}“)",
        _ => "(ohne Namen)"
    };

    public void Dispose() => _factory.Dispose();
}
