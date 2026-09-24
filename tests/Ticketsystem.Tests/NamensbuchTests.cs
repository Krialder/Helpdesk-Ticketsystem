using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Dieselbe Person steht in der Akte als Adresse, Anzeigename oder voller
// Name, je nachdem, was beim Schreiben gesetzt war. Das Namensbuch löst jede
// Angabe auf denselben Anzeigenamen auf, ohne den gespeicherten Text
// umzuschreiben: Der bleibt als Rückfall, wenn kein Konto mehr passt.
public sealed class NamensbuchTests : IDisposable
{
    private readonly KernWirt _factory = new();

    private async Task<(Namensbuch Buch, AppUser Konto)> BuchAsync(
        string email = "sabine@example.org", string? nachname = "Weber",
        string? vorname = "Sabine", string? anzeige = "Sabine")
    {
        using var scope = _factory.Services.CreateScope();
        var konten = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var konto = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Nachname = nachname, Vorname = vorname, Anzeigename = anzeige
        };
        await konten.CreateAsync(konto, "Start-1234!");

        var buch = await scope.ServiceProvider.GetRequiredService<Namensverzeichnis>().LadenAsync();
        return (buch, konto);
    }

    [Fact]
    public async Task Die_Kontokennung_gewinnt_immer()
    {
        var (buch, konto) = await BuchAsync();

        Assert.Equal("Sabine", buch.Anzeige(konto.Id, "irgendein alter Text"));
    }

    [Fact]
    public async Task Eine_alte_Zeile_mit_Adresse_findet_ihr_Konto()
    {
        var (buch, _) = await BuchAsync();

        Assert.Equal("Sabine", buch.Anzeige(null, "sabine@example.org"));
        Assert.Equal("Sabine", buch.Anzeige(null, "SABINE@EXAMPLE.ORG"));
    }

    [Fact]
    public async Task Eine_alte_Zeile_mit_vollem_Namen_findet_ihr_Konto_auch()
    {
        var (buch, _) = await BuchAsync();

        Assert.Equal("Sabine", buch.Anzeige(null, "Weber, Sabine"));
    }

    // Gelöschtes Konto oder Import von woanders: Eine erfundene Zuordnung wäre
    // schlimmer als keine.
    [Fact]
    public async Task Ohne_Zuordnung_bleibt_der_gespeicherte_Text_stehen()
    {
        var (buch, _) = await BuchAsync();

        Assert.Equal("Huber, Karl", buch.Anzeige(null, "Huber, Karl"));
        Assert.Equal("weg@example.org", buch.Anzeige("gibt-es-nicht", "weg@example.org"));
    }

    [Fact]
    public async Task Ohne_Anzeigenamen_gilt_der_volle_Name()
    {
        var (buch, konto) = await BuchAsync(anzeige: null);

        Assert.Equal("Weber, Sabine", buch.Anzeige(konto.Id, "egal"));
    }

    [Fact]
    public async Task Das_Buch_nennt_auch_das_Konto_hinter_einem_Namen()
    {
        var (buch, konto) = await BuchAsync();

        Assert.Equal(konto.Id, buch.KontoId(null, "sabine@example.org"));
        Assert.Equal(konto.Id, buch.KontoId(konto.Id, "egal"));
        Assert.Null(buch.KontoId(null, "Huber, Karl"));
    }

    public void Dispose() => _factory.Dispose();
}
