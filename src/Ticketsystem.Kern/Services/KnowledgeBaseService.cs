using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

// Die Wissensdatenbank: Artikel, Kategorien, Tags, Änderungsvorschläge und
// Fassungen. Lesen darf jeder Angemeldete, pflegen ab Teamleitung,
// Vorschläge einreichen jeder Mitarbeiter. Eine Sichtbarkeitsregel gibt
// es nicht, weil es außer Mitarbeitern keinen Leser gibt.
public class KnowledgeBaseService(TicketsystemContext db)
{
    private IQueryable<KbArticle> Artikel =>
        db.KbArticles.Include(a => a.Kategorie).Include(a => a.Tags);

    public Task<List<KbArticle>> ListAsync(
        string? suche = null,
        int? kategorieId = null,
        int? tagId = null,
        bool nurPruefungFaellig = false)
    {
        var query = Artikel;

        if (!string.IsNullOrWhiteSpace(suche))
        {
            var begriff = suche.Trim();
            // LIKE vergleicht in SQLite nur ASCII fallunabhängig: „drucker" findet
            // „Drucker", „ärger" aber nicht „Ärger".
            query = query.Where(a =>
                EF.Functions.Like(a.Title, $"%{begriff}%")
                || EF.Functions.Like(a.Content, $"%{begriff}%")
                || (a.Kategorie != null && EF.Functions.Like(a.Kategorie.Name, $"%{begriff}%"))
                || a.Tags.Any(tg => EF.Functions.Like(tg.Name, $"%{begriff}%")));
        }

        if (kategorieId is not null)
        {
            query = query.Where(a => a.KategorieId == kategorieId);
        }

        if (tagId is not null)
        {
            query = query.Where(a => a.Tags.Any(tg => tg.Id == tagId));
        }

        if (nurPruefungFaellig)
        {
            var jetzt = DateTime.UtcNow;
            // Dieselbe Regel wie KbArticle.PruefungFaellig, hier als Ausdruck, weil
            // SQL die Methode nicht kennt; ein Test hält beide gleich.
            query = query.Where(a => a.UpdatedAt.AddDays(a.ReviewIntervallTage) < jetzt);
        }

        return query
            .OrderBy(a => a.Kategorie!.Name)
            .ThenBy(a => a.Title)
            .ToListAsync();
    }

    public Task<KbArticle?> FindAsync(int id) =>
        Artikel.SingleOrDefaultAsync(a => a.Id == id);

    public Task<List<KbKategorie>> KategorienAsync() =>
        db.KbKategorien.OrderBy(k => k.Name).ToListAsync();

    public Task<List<KbTag>> TagsAsync() =>
        db.KbTags.OrderBy(tg => tg.Name).ToListAsync();

    public async Task<KbKategorie> KategorieAnlegenAsync(string name, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Kategorien");
        var sauber = Pflicht(name, "Kategoriename");
        var vorhanden = await db.KbKategorien.SingleOrDefaultAsync(k => k.Name == sauber);
        if (vorhanden is not null)
        {
            return vorhanden;
        }

        var kategorie = new KbKategorie { Name = sauber };
        db.KbKategorien.Add(kategorie);
        await db.SaveChangesAsync();
        return kategorie;
    }

    public async Task<KbTag> TagAnlegenAsync(string name, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Tags");
        var sauber = Pflicht(name, "Tagname");
        var normalisiert = sauber.ToLowerInvariant();
        var vorhanden = await db.KbTags.SingleOrDefaultAsync(tg => tg.NameNormalisiert == normalisiert);
        if (vorhanden is not null)
        {
            return vorhanden;
        }

        var tag = new KbTag { Name = sauber, NameNormalisiert = normalisiert };
        db.KbTags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    public async Task KategorieUmbenennenAsync(int id, string name, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Kategorien");
        var sauber = Pflicht(name, "Kategoriename");
        var kategorie = await FindeAsync(db.KbKategorien, k => k.Id == id, "Diese Kategorie");

        var doppelt = await db.KbKategorien.AnyAsync(k => k.Id != id && k.Name == sauber);
        if (doppelt)
        {
            throw new InvalidOperationException($"Eine Kategorie „{sauber}\" gibt es schon.");
        }

        kategorie.Name = sauber;
        await db.SaveChangesAsync();
    }

    public async Task KategorieLoeschenAsync(int id, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Kategorien");
        var kategorie = await FindeAsync(db.KbKategorien, k => k.Id == id, "Diese Kategorie");

        // Verweigern statt still leeren: Ein Artikel, der plötzlich ohne Kategorie
        // dasteht, ist ein Verlust, den niemand bemerkt.
        var benutzt = await db.KbArticles.CountAsync(a => a.KategorieId == id);
        if (benutzt > 0)
        {
            throw new InvalidOperationException(
                $"„{kategorie.Name}\" hängt an {benutzt} Artikel(n). Erst umhängen, dann löschen.");
        }

        db.KbKategorien.Remove(kategorie);
        await db.SaveChangesAsync();
    }

    public async Task TagUmbenennenAsync(int id, string name, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Tags");
        var sauber = Pflicht(name, "Tagname");
        var normalisiert = sauber.ToLowerInvariant();
        var tag = await FindeAsync(db.KbTags, t => t.Id == id, "Dieses Tag");

        var doppelt = await db.KbTags.AnyAsync(t => t.Id != id && t.NameNormalisiert == normalisiert);
        if (doppelt)
        {
            throw new InvalidOperationException($"Ein Tag „{sauber}\" gibt es schon.");
        }

        tag.Name = sauber;
        tag.NameNormalisiert = normalisiert;
        await db.SaveChangesAsync();
    }

    public async Task TagLoeschenAsync(int id, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Tags");
        var tag = await FindeAsync(db.KbTags.Include(t => t.Artikel), t => t.Id == id, "Dieses Tag");

        if (tag.Artikel.Count > 0)
        {
            throw new InvalidOperationException(
                $"„{tag.Name}\" hängt an {tag.Artikel.Count} Artikel(n). Erst abhängen, dann löschen.");
        }

        db.KbTags.Remove(tag);
        await db.SaveChangesAsync();
    }

    // Die offenen Vorschläge zum Artikel gehen mit; seine Fassungen räumt die
    // Kaskade in der Datenbank ab.
    public async Task ArtikelLoeschenAsync(int id, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Wissensartikel");
        var artikel = await FindeAsync(db.KbArticles.Include(a => a.Tags), a => a.Id == id, "Diesen Artikel");

        var vorschlaege = await db.KbVorschlaege.Where(v => v.ArticleId == id).ToListAsync();
        db.KbVorschlaege.RemoveRange(vorschlaege);
        db.KbArticles.Remove(artikel);
        await db.SaveChangesAsync();
    }

    public async Task<KbArticle> SpeichernAsync(
        int? id, string title, string content, int? kategorieId,
        bool published, int reviewIntervallTage, IReadOnlyList<int> tagIds, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Wissensartikel");

        var jetzt = DateTime.UtcNow;
        var tags = await db.KbTags.Where(tg => tagIds.Contains(tg.Id)).ToListAsync();

        KbArticle artikel;
        if (id is null)
        {
            artikel = new KbArticle { CreatedAt = jetzt, Owner = akteur.Name, OwnerId = akteur.Id };
            db.KbArticles.Add(artikel);
        }
        else
        {
            artikel = await FindeAsync(db.KbArticles.Include(a => a.Tags), a => a.Id == id, "Diesen Artikel");
        }

        artikel.Title = Pflicht(title, "Titel");
        artikel.Content = Pflicht(content, "Inhalt");
        artikel.KategorieId = kategorieId;
        artikel.Published = published;
        artikel.ReviewIntervallTage = reviewIntervallTage < 1 ? KbArticle.StandardPruefzyklusTage : reviewIntervallTage;
        // Nur wenn noch keiner dasteht: Wer einen fremden Artikel ändert, wird
        // nicht sein Eigentümer. Beide Felder zusammen, denn Altbestand trägt
        // einen Namen ohne Kennung.
        if (artikel.Owner is null)
        {
            artikel.Owner = akteur.Name;
            artikel.OwnerId = akteur.Id;
        }
        artikel.Tags.Clear();
        artikel.Tags.AddRange(tags);
        artikel.UpdatedAt = jetzt;

        await FassungAblegenAsync(artikel, id is null ? Fassungsanlass.Angelegt : Fassungsanlass.Bearbeitet, akteur);
        await db.SaveChangesAsync();
        return artikel;
    }

    // Setzt nur Stand und Eigentümer; damit erlischt der Prüfhinweis, ohne
    // dass eine Fassung entsteht.
    public async Task AlsGeprueftMarkierenAsync(int id, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Wissensartikel");
        var artikel = await FindeAsync(db.KbArticles, a => a.Id == id, "Diesen Artikel");
        artikel.UpdatedAt = DateTime.UtcNow;
        artikel.Owner = akteur.Name;
        artikel.OwnerId = akteur.Id;
        await db.SaveChangesAsync();
    }

    public async Task<KbAenderungsvorschlag> VorschlagEinreichenAsync(
        int? articleId, string title, string content, int? kategorieId, Akteur akteur)
    {
        if (!akteur.IstMitarbeiter)
        {
            throw new InvalidOperationException("Wissensartikel bearbeiten dürfen nur Mitarbeiter.");
        }

        var vorschlag = new KbAenderungsvorschlag
        {
            ArticleId = articleId,
            Title = Pflicht(title, "Titel"),
            Content = Pflicht(content, "Inhalt"),
            KategorieId = kategorieId,
            VorgeschlagenVon = akteur.Name,
            VorgeschlagenVonId = akteur.Id,
            VorgeschlagenAm = DateTime.UtcNow
        };

        db.KbVorschlaege.Add(vorschlag);
        await db.SaveChangesAsync();
        return vorschlag;
    }

    public Task<List<KbAenderungsvorschlag>> VorschlaegeAsync(Akteur akteur)
    {
        RequireTeamleitung(akteur, "Änderungsvorschläge");
        return db.KbVorschlaege.OrderBy(v => v.VorgeschlagenAm).ToListAsync();
    }

    public async Task<KbArticle> VorschlagUebernehmenAsync(int vorschlagId, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Änderungsvorschläge");
        var vorschlag = await FindeAsync(db.KbVorschlaege, v => v.Id == vorschlagId, "Diesen Vorschlag");

        var jetzt = DateTime.UtcNow;
        KbArticle artikel;
        // Ein neuer Vorschlag geht unveröffentlicht in den Bestand: Übernehmen
        // heißt „Text ist angekommen", nicht „steht im Nachschlagewerk". Bei
        // einem bestehenden Artikel bleibt sein Stand.
        if (vorschlag.ArticleId is null)
        {
            artikel = new KbArticle
            {
                CreatedAt = jetzt,
                Published = false,
                Owner = akteur.Name,
                OwnerId = akteur.Id
            };
            db.KbArticles.Add(artikel);
        }
        else
        {
            // Mit Tags, denn die Fassung hält sie als Namen fest; ohne das Include
            // sähe sie aus, als hätte der Vorschlag alle Tags entfernt.
            artikel = await FindeAsync(db.KbArticles.Include(a => a.Tags), a => a.Id == vorschlag.ArticleId, "Diesen Artikel");
        }

        artikel.Title = vorschlag.Title;
        artikel.Content = vorschlag.Content;
        artikel.KategorieId = vorschlag.KategorieId;
        artikel.Owner = akteur.Name;
        artikel.OwnerId = akteur.Id;
        artikel.UpdatedAt = jetzt;

        await FassungAblegenAsync(artikel, Fassungsanlass.VorschlagUebernommen(vorschlag.VorgeschlagenVon), akteur);
        db.KbVorschlaege.Remove(vorschlag);
        await db.SaveChangesAsync();
        return artikel;
    }

    public Task<List<KbArtikelFassung>> FassungenAsync(int articleId, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Versionen");
        return db.KbFassungen
            .AsNoTracking()
            .Where(f => f.ArticleId == articleId)
            .OrderByDescending(f => f.Nummer)
            .ToListAsync();
    }

    // Der Rückweg legt eine neue Fassung an, statt die Geschichte
    // umzuschreiben. Der Veröffentlichungsstand bleibt, weil er keine
    // Eigenschaft des Textes ist; Kategorie und Tags, die es nicht mehr gibt,
    // bleiben weg, denn ein Rückweg erfindet kein Vokabular.
    public async Task<KbArticle> ZurueckAufFassungAsync(int articleId, int nummer, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Versionen");
        var artikel = await FindeAsync(db.KbArticles.Include(a => a.Tags), a => a.Id == articleId, "Diesen Artikel");
        var fassung = await FindeAsync(db.KbFassungen.AsNoTracking(),
            f => f.ArticleId == articleId && f.Nummer == nummer, $"Version {nummer}");

        var juengste = await db.KbFassungen.Where(f => f.ArticleId == articleId).MaxAsync(f => f.Nummer);
        if (nummer == juengste)
        {
            throw new InvalidOperationException($"Version {nummer} ist schon der aktuelle Stand.");
        }

        artikel.Title = fassung.Title;
        artikel.Content = fassung.Content;
        artikel.KategorieId = fassung.Kategorie is null
            ? null
            : (await db.KbKategorien.SingleOrDefaultAsync(k => k.Name == fassung.Kategorie))?.Id;
        var tagNamen = TagNamen(fassung.Tags);
        var tags = await db.KbTags.Where(t => tagNamen.Contains(t.Name)).ToListAsync();
        artikel.Tags.Clear();
        artikel.Tags.AddRange(tags);
        artikel.UpdatedAt = DateTime.UtcNow;

        await FassungAblegenAsync(artikel, Fassungsanlass.Zurueck(nummer), akteur);
        await db.SaveChangesAsync();
        return artikel;
    }

    // Legt den Stand als nächste Fassung ab, sofern er sich von der jüngsten
    // unterscheidet; eine gleiche Fassung trüge nichts. Muss laufen, nachdem
    // alle Felder gesetzt sind, und vor SaveChanges; ein neuer Artikel (Id 0)
    // bekommt die 1.
    private async Task FassungAblegenAsync(KbArticle artikel, string anlass, Akteur akteur)
    {
        var juengste = artikel.Id == 0
            ? null
            : await db.KbFassungen.AsNoTracking()
                .Where(f => f.ArticleId == artikel.Id)
                .OrderByDescending(f => f.Nummer)
                .FirstOrDefaultAsync();
        var kategorie = artikel.KategorieId is int kategorieId
            ? (await db.KbKategorien.FindAsync(kategorieId))?.Name
            : null;
        var tags = string.Join(", ", artikel.Tags.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

        if (juengste is not null
            && juengste.Title == artikel.Title
            && juengste.Content == artikel.Content
            && juengste.Kategorie == kategorie
            && juengste.Tags == tags
            && juengste.Published == artikel.Published)
        {
            return;
        }

        artikel.Fassungen.Add(new KbArtikelFassung
        {
            Nummer = (juengste?.Nummer ?? 0) + 1,
            Title = artikel.Title,
            Content = artikel.Content,
            Kategorie = kategorie,
            Tags = tags,
            Published = artikel.Published,
            Anlass = anlass,
            GespeichertVon = akteur.Name,
            GespeichertVonId = akteur.Id,
            GespeichertAm = artikel.UpdatedAt
        });
    }

    private static string[] TagNamen(string tags) =>
        tags.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task<bool> VorschlagAblehnenAsync(int vorschlagId, Akteur akteur)
    {
        RequireTeamleitung(akteur, "Änderungsvorschläge");
        var vorschlag = await db.KbVorschlaege.SingleOrDefaultAsync(v => v.Id == vorschlagId);
        if (vorschlag is null)
        {
            return false;
        }

        db.KbVorschlaege.Remove(vorschlag);
        await db.SaveChangesAsync();
        return true;
    }

    private static void RequireTeamleitung(Akteur akteur, string was)
    {
        if (!akteur.IstMindestens(RoleLevel.Teamleitung))
        {
            throw new InvalidOperationException($"{was} pflegen dürfen Teamleitung und Administration.");
        }
    }

    private static string Pflicht(string wert, string feld)
    {
        var sauber = (wert ?? string.Empty).Trim();
        return sauber.Length == 0
            ? throw new InvalidOperationException($"{feld} darf nicht leer sein.")
            : sauber;
    }

    // Ein fehlender Datensatz ist Alltag (zweites Fenster, Doppelklick).
    // SingleAsync wirft dafür eine englische Rahmenwerksmeldung, die die
    // Fenster unverändert anzeigen würden.
    private static async Task<T> FindeAsync<T>(
        IQueryable<T> menge, System.Linq.Expressions.Expression<Func<T, bool>> bedingung, string was)
        where T : class
    {
        var treffer = await menge.SingleOrDefaultAsync(bedingung);
        return treffer ?? throw new InvalidOperationException(
            $"{was} gibt es nicht mehr, vermutlich wurde es zwischenzeitlich entfernt.");
    }
}
