using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.App.Fenster;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Ein Name, überall derselbe, und er führt zur Person dahinter. Historie,
// Kommentarkopf, Zuweisen-Auswahl und Kontenliste zeigten vier
// Schreibweisen für eine Person, und keine führte irgendwohin. Das
// Namensbuch selbst belegen die NamensbuchTests; hier geht es um die
// Strecke von dort bis in die Fenster.
public sealed class EinNameTests : IDisposable
{
    private readonly KernWirt _factory = new();

    // Ein Konto mit allen drei Namensformen, damit jeder Test zeigen kann,
    // welche ankommt: die Adresse (falsch), der volle Name (halb) oder der
    // Anzeigename (richtig).
    private async Task<Akteur> KontoAsync(
        string email = "sabine@example.org", string anzeige = "Sabine W.",
        string nachname = "Weber", string vorname = "Sabine")
    {
        using var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Nachname = nachname, Vorname = vorname, Anzeigename = anzeige
        };
        await konten.CreateAsync(konto, "Start-1234!");
        await konten.AddToRoleAsync(konto, Rollen.Editor);
        return new Akteur(konto.Id, Kontenname.Voll(konto), RoleLevel.Bearbeiter, anzeige);
    }

    private async Task<Ticket> TicketAsync(Akteur wer, string kunde = "Meier, Anna")
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketService>().CreatePhoneAsync(
            "Drucker klemmt", "Papierstau.", TicketPriority.Medium, kunde, "0221123456",
            DateTime.UtcNow, null, wer.Id, wer.Name, "A-101");
    }

    // Altbestände sind unter der Adresse geschrieben, gelesen wird der
    // Anzeigename. Die Akte behält ihren Wortlaut: Der Rückfall muss bleiben,
    // sonst hinge die Historie an einem Konto, das jemand löschen kann.
    [AvaloniaFact]
    public async Task Historie_und_Kommentar_zeigen_den_heutigen_Anzeigenamen()
    {
        var wer = new Akteur("alt-1", "alt@ticketsystem.local", RoleLevel.Bearbeiter);
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AddCommentAsync(ticket.Id, wer, "Netzteil bestellt.");
            var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await konten.CreateAsync(new AppUser
            {
                Id = "alt-1", UserName = "alt@ticketsystem.local", Email = "alt@ticketsystem.local",
                EmailConfirmed = true, Nachname = "Alt", Vorname = "Anton", Anzeigename = "Toni"
            }, "Start-1234!");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var verlauf = (IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!;
        Assert.All(verlauf, e => Assert.Equal("Toni", e.Wer));
        Assert.Equal("Toni", Assert.Single(verlauf, e => e.IstKommentar).Wer);
        Assert.Equal("Toni", fenster.ErstellerKnopf.Content);
        using var pruefung = _factory.Services.CreateScope();
        var frisch = await pruefung.ServiceProvider.GetRequiredService<TicketService>()
            .FindForUserAsync(ticket.Id, TestDaten.Administration);
        Assert.All(frisch!.History, h => Assert.Equal("alt@ticketsystem.local", h.ChangedBy));
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Auch_der_Name_IM_Satz_wird_aufgeloest()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var verlauf = (IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!;
        var zuweisung = Assert.Single(verlauf, e => e.Text.StartsWith("Bearbeiter:", StringComparison.Ordinal));
        Assert.Equal("Bearbeiter: leer zu Sabine W.", zuweisung.Text);
        Assert.DoesNotContain(verlauf, e => e.Text.Contains("Agent", StringComparison.Ordinal));
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Jeder_Name_traegt_die_Kennung_seines_Kontos()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AddCommentAsync(ticket.Id, wer, "Netzteil bestellt.");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        var verlauf = (IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!;
        Assert.All(verlauf, e =>
        {
            Assert.Equal(wer.Id, e.WerKontoId);
            Assert.True(e.Anklickbar);
        });
        Assert.True(Assert.Single(verlauf, e => e.IstKommentar).Anklickbar);
        Assert.True(fenster.ErstellerKnopf.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_Name_ohne_Konto_bleibt_stehen_und_fuehrt_nirgendwohin()
    {
        var wer = new Akteur("gibt-es-nicht", "Huber, Karl", RoleLevel.Bearbeiter);
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AddCommentAsync(ticket.Id, wer, "Von einem Konto, das es nicht mehr gibt.");
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("Huber, Karl", fenster.ErstellerWert.Text);
        Assert.False(fenster.ErstellerKnopf.IsVisible);
        var eintrag = Assert.Single((IReadOnlyList<VerlaufsEintrag>)fenster.Verlauf.ItemsSource!);
        Assert.Equal("Huber, Karl", eintrag.Wer);
        Assert.Null(eintrag.WerKontoId);
        Assert.False(eintrag.Anklickbar);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Seitenspalte_fuehrt_vom_Bearbeiter_zum_Konto_und_vom_Kunden_zur_Vorgeschichte()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.True(fenster.BearbeiterKnopf.IsVisible);
        Assert.Equal("Sabine W.", fenster.BearbeiterKnopf.Content);
        Assert.False(fenster.BearbeiterWert.IsVisible);
        Assert.Equal("Meier, Anna", fenster.NachName.Text);
        Assert.True(fenster.KundeVorgaenge.IsVisible);
        Assert.True(fenster.OrtVorgaenge.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ohne_Bearbeiter_bietet_die_Bearbeiterkarte_keinen_Klick_an()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);

        var fenster = new DetailFenster(_factory.Services, TestDaten.Administration, ticket.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("nicht zugewiesen", fenster.BearbeiterWert.Text);
        Assert.True(fenster.BearbeiterWert.IsVisible);
        Assert.False(fenster.BearbeiterKnopf.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Vorgangsliste_zeigt_den_Anzeigenamen_und_haengt_die_Kennung_an()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        var fenster = new Grundfenster(_factory.Services, TestDaten.Administration);
        fenster.Show();
        await fenster.LadenAsync();

        var zeile = Assert.Single((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!);
        Assert.Equal("Sabine W.", zeile.Bearbeiter);
        Assert.Equal(wer.Id, zeile.BearbeiterKontoId);
        Assert.True(zeile.HatBearbeiter);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Ein_unzugewiesener_Vorgang_haengt_keine_Kennung_an()
    {
        var wer = await KontoAsync();
        await TicketAsync(wer);

        var fenster = new Grundfenster(_factory.Services, TestDaten.Administration);
        fenster.Show();
        await fenster.LadenAsync();

        var zeile = Assert.Single((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!);
        Assert.False(zeile.HatBearbeiter);
        Assert.Null(zeile.BearbeiterKontoId);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Kontokarte_nennt_Person_Rolle_und_offene_Vorgaenge()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        var fenster = new KontoFenster(_factory.Services, TestDaten.Administration, wer.Id);
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("Sabine W.", fenster.Anzeigename.Text);
        Assert.Equal("Weber, Sabine", fenster.VollerName.Text);
        Assert.Equal("sabine@example.org", fenster.Email.Text);
        Assert.Equal(RoleLevel.Bearbeiter.Anzeige(), fenster.Rolle.Text);
        Assert.Equal("1 offener Vorgang", fenster.OffeneVorgaenge.Text);
        Assert.False(fenster.Pausiert.IsVisible);
        fenster.Close();
    }

    // Ohne Anzeigenamen fällt die Anzeige auf den vollen Namen zurück, und
    // beide Zeilen trügen denselben Text. Ohne Nach- und Vornamen fällt der
    // volle Name auf die Adresse zurück, und die steht schon eine Zeile tiefer.
    [AvaloniaFact]
    public async Task Die_Karte_sagt_nichts_zweimal()
    {
        var ohneAnzeigenamen = await KontoAsync("karl@example.org", anzeige: "", nachname: "Huber", vorname: "Karl");
        var ohneNamen = await KontoAsync("otto@example.org", anzeige: "Otto", nachname: "", vorname: "");

        var eins = new KontoFenster(_factory.Services, TestDaten.Administration, ohneAnzeigenamen.Id);
        eins.Show();
        await eins.LadenAsync();
        var zwei = new KontoFenster(_factory.Services, TestDaten.Administration, ohneNamen.Id);
        zwei.Show();
        await zwei.LadenAsync();

        Assert.Equal("Huber, Karl", eins.Anzeigename.Text);
        Assert.False(eins.VollerName.IsVisible);

        Assert.Equal("Otto", zwei.Anzeigename.Text);
        Assert.True(zwei.VollerName.IsVisible);
        Assert.Equal("Kein Name gesetzt (setzt die Administration)", zwei.VollerName.Text);
        eins.Close();
        zwei.Close();
    }

    // Ein Bearbeiter sieht von einem Kollegen nur, was ihm selbst gehört oder
    // unzugewiesen ist. Die glatte Null läse sich ohne Zusatz als „hat nichts
    // zu tun".
    [AvaloniaFact]
    public async Task Eine_unvollstaendige_Zahl_sagt_dass_sie_es_ist()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        var fremder = new KontoFenster(_factory.Services, TestDaten.Bearbeiter1, wer.Id);
        fremder.Show();
        await fremder.LadenAsync();

        Assert.Equal("0 offene Vorgänge für dich sichtbar", fremder.OffeneVorgaenge.Text);
        fremder.Close();
    }

    [AvaloniaFact]
    public async Task Den_Weg_in_die_Verwaltung_sieht_nur_die_Administration()
    {
        var wer = await KontoAsync();

        var bearbeiter = new KontoFenster(_factory.Services, TestDaten.Bearbeiter1, wer.Id);
        bearbeiter.Show();
        await bearbeiter.LadenAsync();
        var admin = new KontoFenster(_factory.Services, TestDaten.Administration, wer.Id);
        admin.Show();
        await admin.LadenAsync();

        Assert.False(bearbeiter.VerwaltungsKarte.IsVisible);
        Assert.True(admin.VerwaltungsKarte.IsVisible);
        bearbeiter.Close();
        admin.Close();
    }

    [AvaloniaFact]
    public async Task Ein_geloeschtes_Konto_sagt_das_statt_eine_leere_Karte_zu_zeigen()
    {
        var fenster = new KontoFenster(_factory.Services, TestDaten.Administration, "gibt-es-nicht");
        fenster.Show();
        await fenster.LadenAsync();

        Assert.True(fenster.Meldung.IsVisible);
        Assert.Contains("gibt es nicht mehr", fenster.Meldung.Text);
        Assert.False(fenster.VorgaengeZeigen.IsVisible);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Kontokarte_zaehlt_nur_was_der_Fragende_sehen_darf()
    {
        var wer = await KontoAsync();
        var ticket = await TicketAsync(wer);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TicketService>()
                .AssignAsync(ticket.Id, wer.Id, wer.Name, TestDaten.Administration);
        }

        using var pruefung = _factory.Services.CreateScope();
        var presenter = pruefung.ServiceProvider.GetRequiredService<KontokartePresenter>();
        var fuerAdmin = await presenter.LadenAsync(wer.Id, TestDaten.Administration, DateTime.UtcNow);
        var fuerFremden = await presenter.LadenAsync(wer.Id, TestDaten.OhneRolle, DateTime.UtcNow);

        Assert.Equal(1, fuerAdmin!.OffeneVorgaenge);
        Assert.Equal(0, fuerFremden!.OffeneVorgaenge);
    }

    // Über die Kennung und nicht über den Namen: Ändert die Person ihren
    // Anzeigenamen, bleibt die Liste dieselbe.
    [AvaloniaFact]
    public async Task Die_Vorgangsliste_einer_Person_filtert_ueber_die_Kennung()
    {
        var eine = await KontoAsync();
        var andere = await KontoAsync("karl@example.org", "Karl H.", "Huber", "Karl");
        var meiner = await TicketAsync(eine);
        var fremder = await TicketAsync(andere, "Schulz, Otto");
        using (var scope = _factory.Services.CreateScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<TicketService>();
            await dienst.AssignAsync(meiner.Id, eine.Id, eine.Name, TestDaten.Administration);
            await dienst.AssignAsync(fremder.Id, andere.Id, andere.Name, TestDaten.Administration);
        }

        var fenster = new VorgangslisteFenster(
            _factory.Services, TestDaten.Administration, Vorgangsfilter.FuerBearbeiter(eine.Id, "Sabine W."));
        fenster.Show();
        await fenster.LadenAsync();

        Assert.Equal("Vorgänge von Sabine W.", fenster.Ueberschrift.Text);
        var zeile = Assert.Single((IReadOnlyList<TicketZeile>)fenster.Liste.ItemsSource!);
        Assert.Equal(meiner.Id, zeile.Id);
        fenster.Close();
    }

    [AvaloniaFact]
    public async Task Die_Zuweisen_Auswahl_zeigt_denselben_Namen_wie_die_Liste()
    {
        var wer = await KontoAsync();

        using var scope = _factory.Services.CreateScope();
        var presenter = new TicketdetailPresenter(
            scope.ServiceProvider.GetRequiredService<TicketService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<Namensverzeichnis>());
        var zeile = Assert.Single(
            await presenter.MitarbeiterAsync(), m => m.Id == wer.Id);

        Assert.Equal("Sabine W.", zeile.Anzeige);
        Assert.Equal("Weber, Sabine", zeile.Name);
    }

    public void Dispose() => _factory.Dispose();
}
