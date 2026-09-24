using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Anzeigeseite der Fassungen: Der Presenter macht aus den gespeicherten
// Ständen Zeilen mit dem heutigen Anzeigenamen und markiert den aktuellen
// Stand. Wer Fassungen sieht und wer zurückgeht, belegen KnowledgeBaseTests.
public sealed class FassungenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<Akteur> KontoAsync(string email, string anzeige, string nachname, string vorname)
    {
        using var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Nachname = nachname, Vorname = vorname, Anzeigename = anzeige
        };
        await konten.CreateAsync(konto, "Start-1234!");
        await konten.AddToRoleAsync(konto, Rollen.TeamLead);
        return new Akteur(konto.Id, Kontenname.Voll(konto), RoleLevel.Teamleitung, anzeige);
    }

    [Fact]
    public async Task Die_Zeilen_nennen_den_heutigen_Anzeigenamen_und_markieren_den_aktuellen_Stand()
    {
        var sabine = await KontoAsync("sabine@example.org", "Sabine W.", "Weber", "Sabine");
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var kb = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
            var artikel = await kb.SpeichernAsync(null, "WLAN", "Erster Stand.", null, true, 180, [], sabine);
            await kb.SpeichernAsync(artikel.Id, "WLAN", "Zweiter Stand.", null, true, 180, [], sabine);
            id = artikel.Id;
        }

        using var pruefung = _factory.Services.CreateScope();
        var zeilen = await pruefung.ServiceProvider.GetRequiredService<FassungenPresenter>()
            .LadenAsync(id, TestDaten.Administration);

        // Neueste zuerst, wie die Historie eines Vorgangs: Die Frage ist fast immer
        // „was war zuletzt?", selten „wie fing es an?".
        Assert.Equal([2, 1], zeilen.Select(z => z.Nummer));
        Assert.True(zeilen[0].IstAktuell);
        Assert.False(zeilen[1].IstAktuell);
        Assert.All(zeilen, z => Assert.Equal("Sabine W.", z.Wer));
        Assert.Equal(sabine.Id, zeilen[0].WerKontoId);
        Assert.Contains("Version 2", zeilen[0].Kopf);
        Assert.Contains(Fassungsanlass.Bearbeitet, zeilen[0].Kopf);
    }

    [AvaloniaFact]
    public async Task Der_Dialog_listet_die_Staende_und_der_Rueckweg_ist_zweistufig()
    {
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var kb = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
            var artikel = await kb.SpeichernAsync(null, "WLAN", "Erster Stand.", null, true, 180, [], TestDaten.Teamleitung);
            await kb.SpeichernAsync(artikel.Id, "WLAN", "Zweiter Stand.", null, true, 180, [], TestDaten.Teamleitung);
            id = artikel.Id;
        }

        var dialog = new FassungenDialog(_factory.Services, TestDaten.Teamleitung, id);
        dialog.Show();
        await dialog.LadenAsync();

        Assert.Equal(2, dialog.Liste.ItemCount);
        // Dorthin führt kein Weg zurück, also gibt es den Knopf nicht erst als
        // Möglichkeit.
        Assert.False(dialog.Zurueck.IsEnabled, "Beim aktuellen Stand darf es keinen Rückweg geben.");

        dialog.Liste.SelectedIndex = 1;
        Assert.True(dialog.Zurueck.IsEnabled);
        Assert.Equal("Erster Stand.", dialog.Inhalt.Text);

        dialog.Zurueck.AusloeserKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(dialog.Zurueck.IstScharf);
        Assert.Equal("Zweiter Stand.", await InhaltAsync(id));

        dialog.Zurueck.ZusageKnopf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 40 && await InhaltAsync(id) != "Erster Stand."; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.Equal("Erster Stand.", await InhaltAsync(id));
        using var pruefung = _factory.Services.CreateScope();
        Assert.Equal(3, await pruefung.ServiceProvider.GetRequiredService<TicketsystemContext>()
            .KbFassungen.CountAsync(f => f.ArticleId == id));
    }

    private async Task<string> InhaltAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<TicketsystemContext>()
            .KbArticles.AsNoTracking().SingleAsync(a => a.Id == id)).Content;
    }

    public void Dispose() => _factory.Dispose();
}
