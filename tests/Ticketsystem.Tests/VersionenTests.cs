using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Auf dem Schirm heißt es Version, nicht Fassung: Das Wort kennt jeder aus
// Dateien und Programmen, „Fassung" kennt man aus Gesetzen. Im Code und in
// der Datenbank bleibt Fassung (KbArtikelFassung), weil ein Umbenennen der
// Tabelle eine Migration ohne Nutzen für den Leser wäre.
public sealed class VersionenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    [Fact]
    public void Zeilenkopf_und_Anlass_sagen_Version()
    {
        var zeile = new FassungsZeile(2, "17.09.2026 08:00", Fassungsanlass.Bearbeitet, "Toni", "k-1",
            "WLAN", "Text", "Ohne Kategorie", "", true, IstAktuell: true);

        Assert.StartsWith("Version 2", zeile.Kopf);
        Assert.DoesNotContain("Fassung", zeile.Kopf);
        Assert.Equal("Zurück auf Version 3", Fassungsanlass.Zurueck(3));
        Assert.DoesNotContain("Fassung", Fassungsanlass.AusSicherung);
    }

    [Fact]
    public async Task Die_Abweisungen_des_Dienstes_sagen_Version()
    {
        using var scope = _factory.Services.CreateScope();
        var kb = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
        var artikel = await kb.SpeichernAsync(null, "WLAN", "Text", null, true, 180, [], TestDaten.Teamleitung);

        var aktuell = await Assert.ThrowsAsync<InvalidOperationException>(
            () => kb.ZurueckAufFassungAsync(artikel.Id, 1, TestDaten.Teamleitung));
        var fehlt = await Assert.ThrowsAsync<InvalidOperationException>(
            () => kb.ZurueckAufFassungAsync(artikel.Id, 9, TestDaten.Teamleitung));
        var recht = await Assert.ThrowsAsync<InvalidOperationException>(
            () => kb.FassungenAsync(artikel.Id, TestDaten.Bearbeiter1));

        foreach (var meldung in new[] { aktuell.Message, fehlt.Message, recht.Message })
        {
            Assert.Contains("Version", meldung);
            Assert.DoesNotContain("Fassung", meldung);
        }
    }

    [AvaloniaFact]
    public async Task Knopf_Dialog_und_Rueckweg_sagen_Version()
    {
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var kb = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
            var artikel = await kb.SpeichernAsync(null, "WLAN", "Erster Stand.", null, true, 180, [], TestDaten.Teamleitung);
            await kb.SpeichernAsync(artikel.Id, "WLAN", "Zweiter Stand.", null, true, 180, [], TestDaten.Teamleitung);
            id = artikel.Id;
        }

        var fenster = new WissensFenster(_factory.Services, TestDaten.Teamleitung);
        Assert.Equal("Versionen", fenster.Fassungen.Content);

        var dialog = new FassungenDialog(_factory.Services, TestDaten.Teamleitung, id);
        dialog.Show();
        await dialog.LadenAsync();
        Assert.Equal("Versionen", dialog.Title);
        Assert.Equal("Version 2 (aktueller Stand)", dialog.Kopf.Text);
        Assert.Equal("Zurück auf diese Version", dialog.Zurueck.Aufforderung);
        Assert.DoesNotContain("Fassung", dialog.Zurueck.Frage);
        Assert.DoesNotContain("Fassung", dialog.Leertext.Text);
        dialog.Close();
    }

    // Der Wächter über die axaml-Dateien: Nur beschriftete Texte zählen;
    // Kommentare und Klassennamen (FassungenDialog) sind Code und bleiben.
    [Fact]
    public void Keine_Oberflaechenbeschreibung_sagt_dem_Menschen_Fassung()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !Directory.Exists(Path.Combine(ordner.FullName, "src", "Ticketsystem.App")))
        {
            ordner = ordner.Parent;
        }

        var beschriftung = new Regex("(Content|Text|Title|Aufforderung|Frage|Zusage|Watermark)=\"[^\"]*Fassung");
        var verstoesse = Directory
            .GetFiles(Path.Combine(ordner!.FullName, "src", "Ticketsystem.App"), "*.axaml", SearchOption.AllDirectories)
            .Where(d => beschriftung.IsMatch(File.ReadAllText(d)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(verstoesse.Count == 0, "Diese Fenster sagen dem Menschen noch Fassung: " + string.Join(", ", verstoesse));
    }

    public void Dispose() => _factory.Dispose();
}
