using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

public sealed record Artikelkopf(string Stand, string? Eigentuemer, string? EigentuemerKontoId);

public sealed record VorschlagsZeile(int Id, string Text, string Herkunft, string? HerkunftKontoId);

// Texte für Kopf und Vorschlagsliste der Wissensdatenbank; die Namen
// kommen aus dem Namensbuch, damit der heutige Anzeigename steht.
public sealed class WissensPresenter(Namensverzeichnis namen)
{
    public async Task<Artikelkopf> KopfAsync(KbArticle artikel) => Kopf(artikel, await namen.LadenAsync());

    public async Task<IReadOnlyList<VorschlagsZeile>> VorschlaegeAsync(IReadOnlyList<KbAenderungsvorschlag> vorschlaege)
    {
        var buch = await namen.LadenAsync();
        return vorschlaege.Select(v => Zeile(v, buch)).ToList();
    }

    public static Artikelkopf Kopf(KbArticle artikel, Namensbuch buch) => new(
        $"{artikel.Kategorie?.Name ?? "Ohne Kategorie"}, Stand {artikel.UpdatedAt.TagesAnzeige()}, "
        + (artikel.Published ? "veröffentlicht" : "Entwurf")
        + $", Tags: {(artikel.Tags.Count == 0 ? "-" : string.Join(", ", artikel.Tags.OrderBy(t => t.Name).Select(t => t.Name)))}",
        artikel.Owner is null ? null : buch.Anzeige(artikel.OwnerId, artikel.Owner),
        artikel.Owner is null ? null : buch.KontoId(artikel.OwnerId, artikel.Owner));

    public static VorschlagsZeile Zeile(KbAenderungsvorschlag vorschlag, Namensbuch buch)
    {
        var wer = buch.Anzeige(vorschlag.VorgeschlagenVonId, vorschlag.VorgeschlagenVon);
        return new VorschlagsZeile(
            vorschlag.Id,
            $"{vorschlag.Title} ({(vorschlag.ArticleId is int id ? $"Änderung an #{id}" : "neuer Artikel")}, von {wer})",
            wer,
            buch.KontoId(vorschlag.VorgeschlagenVonId, vorschlag.VorgeschlagenVon));
    }
}
