using Avalonia.Headless.XUnit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein Name, auch in der Wissensdatenbank: Die Artikelansicht zeigte
// „Eigentümer C@EXAMPLE.org". Eigentümer und Vorschlagender zeigen den heutigen
// Anzeigenamen und tragen eine Kontokennung; der gespeicherte Text ist nur
// der Rückfall.
public sealed class WissensNamenTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private static (Namensbuch Buch, AppUser Konto) BuchMit(string email, string? anzeige, string? nachname = null, string? vorname = null)
    {
        var konto = new AppUser
        {
            Id = "k-" + email, UserName = email, Email = email,
            Nachname = nachname, Vorname = vorname, Anzeigename = anzeige
        };
        return (new Namensbuch([konto]), konto);
    }

    [Fact]
    public void Der_Eigentuemer_zeigt_den_heutigen_Anzeigenamen()
    {
        var (buch, konto) = BuchMit("c@example.org", anzeige: "Chris");
        var artikel = new KbArticle { Title = "G", Content = "sf", Owner = "c@example.org", Published = true };

        var kopf = WissensPresenter.Kopf(artikel, buch);

        Assert.Equal("Chris", kopf.Eigentuemer);
        Assert.Equal(konto.Id, kopf.EigentuemerKontoId);
        Assert.DoesNotContain("c@example.org", kopf.Stand);
        Assert.Contains("veröffentlicht", kopf.Stand);
    }

    [Fact]
    public void Ohne_Konto_bleibt_der_gespeicherte_Text_und_der_Klick_fuehrt_nirgendwohin()
    {
        var (buch, _) = BuchMit("c@example.org", anzeige: "Chris");
        var artikel = new KbArticle { Title = "G", Content = "sf", Owner = "Huber, Karl" };

        var kopf = WissensPresenter.Kopf(artikel, buch);

        Assert.Equal("Huber, Karl", kopf.Eigentuemer);
        Assert.Null(kopf.EigentuemerKontoId);
    }

    [Fact]
    public void Ohne_Eigentuemer_gibt_es_keine_Eigentuemerzeile()
    {
        var (buch, _) = BuchMit("c@example.org", anzeige: "Chris");
        var artikel = new KbArticle { Title = "G", Content = "sf", Owner = null };

        var kopf = WissensPresenter.Kopf(artikel, buch);

        Assert.Null(kopf.Eigentuemer);
        Assert.Null(kopf.EigentuemerKontoId);
    }

    [Fact]
    public void Der_Vorschlagende_wird_genauso_aufgeloest()
    {
        var (buch, konto) = BuchMit("c@example.org", anzeige: "Chris");
        var vorschlag = new KbAenderungsvorschlag
        {
            Id = 7, Title = "Besser", Content = "Text", ArticleId = 3, VorgeschlagenVon = "c@example.org"
        };

        var zeile = WissensPresenter.Zeile(vorschlag, buch);

        Assert.Equal(7, zeile.Id);
        Assert.Contains("Änderung an #3", zeile.Text);
        Assert.Contains("von Chris", zeile.Text);
        Assert.DoesNotContain("c@example.org", zeile.Text);
        Assert.Equal("Chris", zeile.Herkunft);
        Assert.Equal(konto.Id, zeile.HerkunftKontoId);
    }

    [Fact]
    public async Task Speichern_und_Vorschlagen_tragen_die_Kontokennung_mit()
    {
        using var verbindung = new SqliteConnection("DataSource=:memory:");
        verbindung.Open();
        using var db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(verbindung).Options);
        db.Database.EnsureCreated();
        var kb = new KnowledgeBaseService(db);

        var artikel = await kb.SpeichernAsync(null, "G", "sf", null, published: true, 180, [], TestDaten.Teamleitung);
        Assert.Equal(TestDaten.Teamleitung.Id, artikel.OwnerId);

        // Ein zweiter Bearbeiter ändert den Text: Der Eigentümer bleibt, samt
        // Kennung; ein ??= auf die Kennung allein hätte den Falschen eingetragen.
        await kb.SpeichernAsync(artikel.Id, "G", "sf 2", null, published: true, 180, [], TestDaten.Administration);
        Assert.Equal(TestDaten.Teamleitung.Id, (await kb.FindAsync(artikel.Id))!.OwnerId);

        await kb.AlsGeprueftMarkierenAsync(artikel.Id, TestDaten.Administration);
        Assert.Equal(TestDaten.Administration.Id, (await kb.FindAsync(artikel.Id))!.OwnerId);

        var vorschlag = await kb.VorschlagEinreichenAsync(artikel.Id, "G", "sf 3", null, TestDaten.Bearbeiter1);
        Assert.Equal(TestDaten.Bearbeiter1.Id, vorschlag.VorgeschlagenVonId);

        var neu = await kb.VorschlagEinreichenAsync(null, "Neu", "Text", null, TestDaten.Bearbeiter1);
        var uebernommen = await kb.VorschlagUebernehmenAsync(neu.Id, TestDaten.Teamleitung);
        Assert.Equal(TestDaten.Teamleitung.Id, uebernommen.OwnerId);

        // Ein Altartikel mit Eigentümer als Text ohne Kennung: Wer ihn heute
        // ändert, wird nicht sein Eigentümer, auch nicht per Kennung.
        var alt = new KbArticle { Title = "Alt", Content = "Text", Owner = "Huber, Karl", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.KbArticles.Add(alt);
        await db.SaveChangesAsync();
        await kb.SpeichernAsync(alt.Id, "Alt", "Text 2", null, published: false, 180, [], TestDaten.Administration);
        var frisch = await kb.FindAsync(alt.Id);
        Assert.Equal("Huber, Karl", frisch!.Owner);
        Assert.Null(frisch.OwnerId);
    }

    private async Task<Akteur> KontoAsync(RoleLevel stufe, string rolle)
    {
        using var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        // Kein Nach- und Vorname, aber ein Anzeigename: genau das Konto aus dem
        // Befund, dessen gespeicherter Name die Adresse ist.
        var konto = new AppUser { UserName = "c@example.org", Email = "c@example.org", EmailConfirmed = true, Anzeigename = "Chris" };
        await konten.CreateAsync(konto, "Start-1234!");
        await konten.AddToRoleAsync(konto, rolle);
        return new Akteur(konto.Id, Kontenname.Voll(konto), stufe, "Chris");
    }

    [AvaloniaFact]
    public async Task Das_Wissensfenster_nennt_den_Eigentuemer_mit_Anzeigenamen_und_fuehrt_zum_Konto()
    {
        var chris = await KontoAsync(RoleLevel.Teamleitung, Rollen.TeamLead);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>()
                .SpeichernAsync(null, "G", "sf", null, published: true, 180, [], chris);
        }

        var fenster = new WissensFenster(_factory.Services, TestDaten.Administration);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.True(fenster.Eigentuemer.IsVisible);
        Assert.Equal("Chris", fenster.Eigentuemer.Content);
        Assert.Equal(chris.Id, fenster.EigentuemerKontoId);
        Assert.DoesNotContain("c@example.org", fenster.Stand.Text);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Der_Freigaben_Dialog_nennt_den_Vorschlagenden_mit_Anzeigenamen()
    {
        var chris = await KontoAsync(RoleLevel.Bearbeiter, Rollen.Editor);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>()
                .VorschlagEinreichenAsync(null, "Neu", "Text", null, chris);
        }

        var dialog = new FreigabenDialog(_factory.Services, TestDaten.Teamleitung);
        dialog.Show();
        await dialog.LadenAsync();

        var zeile = Assert.Single((IReadOnlyList<string>)dialog.Liste.ItemsSource!);
        Assert.Contains("von Chris", zeile);
        Assert.DoesNotContain("c@example.org", zeile);
        Assert.Equal("Chris", dialog.HerkunftName.Content);
        Assert.Equal(chris.Id, dialog.HerkunftKontoId);
        dialog.Close();
    }

    public void Dispose() => _factory.Dispose();
}
