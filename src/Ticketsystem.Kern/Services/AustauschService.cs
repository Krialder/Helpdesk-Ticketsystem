using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

// Das Austauschformat: der gesamte fachliche Bestand als eine JSON-Datei.
// Eigene Sätze statt der EF-Klassen, weil die Klassen gegenseitige
// Verweise tragen, an denen ein Serialisierer im Kreis läuft, und das
// Dateiformat sonst am Datenmodell hinge. Kennung und Version stehen im
// Kopf, damit der Import eine fremde Datei erkennt, bevor er löscht.
public sealed class Datenbuendel
{
    public const string Kennung = "Ticketsystem";

    // Gelesen werden auch die Versionen 1 und 2, neuere weist der Import ab.
    // Fehlt in einer alten Datei eine Liste, bleibt sie leer; trägt ein Kontakt
    // noch die Nummernliste, gilt die jüngste Nummer; fehlen die Fassungen,
    // wird der Stand Fassung 1.
    public const int AktuelleVersion = 3;

    public string Anwendung { get; set; } = Kennung;

    public int Version { get; set; } = AktuelleVersion;

    public DateTime ErstelltAm { get; set; }

    public string ErstelltVon { get; set; } = string.Empty;

    public List<TicketSatz> Tickets { get; set; } = [];

    public List<KontaktSatz> Kontakte { get; set; } = [];

    public List<ArtikelSatz> Wissensartikel { get; set; } = [];

    public List<RuhezeitSatz> Ruhezeiten { get; set; } = [];

    public List<VorschlagSatz> Vorschlaege { get; set; } = [];

    // Eigene Listen für Kategorien und Tags: Nur aus den Artikeln abgeleitet
    // gingen die Einträge verloren, die noch an keinem hängen.
    public List<string> Kategorien { get; set; } = [];

    public List<string> Tags { get; set; } = [];
}

// Der Bezug auf den Artikel läuft über den Titel, nicht über die Nummer:
// Artikel bekommen beim Import neue Nummern, Titel überstehen den Umzug.
// Findet sich kein Artikel, bleibt der Vorschlag ohne Bezug stehen.
public sealed class VorschlagSatz
{
    public string? ArtikelTitel { get; set; }

    public string Titel { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string? Kategorie { get; set; }

    public string VorgeschlagenVon { get; set; } = string.Empty;

    public DateTime VorgeschlagenAm { get; set; }
}

public sealed class TicketSatz
{
    public int Id { get; set; }

    public string Titel { get; set; } = string.Empty;

    public string Beschreibung { get; set; } = string.Empty;

    public TicketPriority Prioritaet { get; set; }

    public TicketStatus Status { get; set; }

    public TicketSource Quelle { get; set; }

    public string KundeName { get; set; } = string.Empty;

    public string? KundeEmail { get; set; }

    public string? KundeId { get; set; }

    public string? ErstellerId { get; set; }

    public string? ErstellerName { get; set; }

    public string? BearbeiterId { get; set; }

    public string? BearbeiterName { get; set; }

    public string? Adresse { get; set; }

    public string? Rueckrufnummer { get; set; }

    // Trägt bei Mails den Eingang der Mail, und die CSV-Spalte heißt
    // „Eingang (UTC)"; der JSON-Name bleibt, damit alte Dateien lesbar bleiben.
    public DateTime? Anrufzeit { get; set; }

    public string? Gespraechsnotiz { get; set; }

    public DateTime? Wiedervorlage { get; set; }

    public string? WiedervorlageGrund { get; set; }

    public int? VerweisAufTicket { get; set; }

    public DateTime Erstellt { get; set; }

    public DateTime Geaendert { get; set; }

    public DateTime ReaktionFaellig { get; set; }

    public DateTime LoesungFaellig { get; set; }

    public DateTime? ErsteReaktion { get; set; }

    public DateTime? Geloest { get; set; }

    public DateTime? SlaGemeldet { get; set; }

    public List<HistorieSatz> Historie { get; set; } = [];

    public List<KommentarSatz> Kommentare { get; set; } = [];
}

public sealed class HistorieSatz
{
    public string Feld { get; set; } = string.Empty;

    public string? Alt { get; set; }

    public string? Neu { get; set; }

    public string Von { get; set; } = string.Empty;

    public DateTime Am { get; set; }
}

public sealed class KommentarSatz
{
    public string AutorId { get; set; } = string.Empty;

    public string AutorName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime Am { get; set; }
}

public sealed class KontaktSatz
{
    public string Name { get; set; } = string.Empty;

    public string? LetzteAdresse { get; set; }

    public string? Rufnummer { get; set; }

    public DateTime Erstellt { get; set; }

    public DateTime Geaendert { get; set; }

    // Die Nummernliste der Versionen 1 und 2, nur zum Lesen alter Dateien;
    // beim Schreiben bleibt sie null und fällt aus der Datei heraus.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RufnummerSatz>? Rufnummern { get; set; }
}

public sealed class RufnummerSatz
{
    public string Nummer { get; set; } = string.Empty;

    public RufnummerArt Art { get; set; }

    public DateTime Erstellt { get; set; }
}

public sealed class ArtikelSatz
{
    public string Titel { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string? Kategorie { get; set; }

    public List<string> Tags { get; set; } = [];

    public bool Veroeffentlicht { get; set; }

    public string? Eigentuemer { get; set; }

    public int PruefzyklusTage { get; set; }

    public DateTime Erstellt { get; set; }

    public DateTime Geaendert { get; set; }

    public List<FassungSatz> Fassungen { get; set; } = [];
}

public sealed class FassungSatz
{
    public int Nummer { get; set; }

    public string Titel { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string? Kategorie { get; set; }

    public string Tags { get; set; } = string.Empty;

    public bool Veroeffentlicht { get; set; }

    public string Anlass { get; set; } = string.Empty;

    public string GespeichertVon { get; set; } = string.Empty;

    public string? GespeichertVonId { get; set; }

    public DateTime GespeichertAm { get; set; }
}

public sealed class RuhezeitSatz
{
    public string Bezeichnung { get; set; } = string.Empty;

    public DateTime Von { get; set; }

    public DateTime Bis { get; set; }

    public string AngelegtVon { get; set; } = string.Empty;

    public DateTime Erstellt { get; set; }
}

// Export und Import des fachlichen Bestands: JSON als verlustfreies
// Umzugsformat, CSV in einer ZIP-Datei zur Auswertung. Anders als die
// Sicherung enthält der Export keine Konten und keine Passwort-Hashes;
// nach einem Import auf einem frischen Rechner zeigen Ticket und Historie
// deshalb Namen, deren Konten fehlen.
public sealed class AustauschService(TicketsystemContext db, ILogger<AustauschService> log)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // Ohne diesen Encoder stünden Umlaute als \u00e4 in der Datei: gültig,
        // aber unlesbar, und lesbar zu sein ist der halbe Zweck des Exports.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<byte[]> ExportJsonAsync(Akteur akteur)
    {
        RequireAdministration(akteur);

        var buendel = new Datenbuendel
        {
            ErstelltAm = DateTime.UtcNow,
            ErstelltVon = akteur.Name,
            Tickets = await TicketsaetzeAsync(),
            Kontakte = await KontaktsaetzeAsync(),
            Wissensartikel = await ArtikelsaetzeAsync(),
            Ruhezeiten = await RuhezeitsaetzeAsync(),
            Vorschlaege = await VorschlagsaetzeAsync(),
            Kategorien = await db.KbKategorien.AsNoTracking()
                .OrderBy(k => k.Name).Select(k => k.Name).ToListAsync(),
            Tags = await db.KbTags.AsNoTracking()
                .OrderBy(t => t.Name).Select(t => t.Name).ToListAsync()
        };

        return JsonSerializer.SerializeToUtf8Bytes(buendel, Json);
    }

    // Ab Teamleitung, nicht erst Administration: Die Tabellen sind der
    // Auswertungsersatz und enthalten weder Konten noch Passwörter. Getrennte
    // Dateien statt einer breiten Tabelle, weil ein Ticket viele
    // Historieneinträge hat.
    public async Task<byte[]> ExportCsvAsync(Akteur akteur)
    {
        RequireTeamleitung(akteur);

        var tickets = await TicketsaetzeAsync();
        var kontakte = await KontaktsaetzeAsync();
        var artikel = await ArtikelsaetzeAsync();

        using var speicher = new MemoryStream();
        using (var archiv = new ZipArchive(speicher, ZipArchiveMode.Create, leaveOpen: true))
        {
            Eintrag(archiv, "tickets.csv",
                ["Nummer", "Titel", "Beschreibung", "Status", "Prioritaet", "Quelle", "Kunde", "E-Mail",
                 "Adresse", "Bearbeiter", "Ersteller", "Erstellt (UTC)", "Geaendert (UTC)",
                 "ReaktionFaellig (UTC)", "LoesungFaellig (UTC)", "ErsteReaktion (UTC)",
                 "Geloest (UTC)", "Verweis", "Rueckrufnummer", "Eingang (UTC)",
                 "Wiedervorlage (UTC)", "Wiedervorlage-Grund"],
                tickets.Select(t => new[]
                {
                    t.Id.ToString(CultureInfo.InvariantCulture), t.Titel, t.Beschreibung,
                    t.Status.Anzeige(), t.Prioritaet.Anzeige(),
                    t.Quelle.Anzeige(), t.KundeName, t.KundeEmail, t.Adresse,
                    t.BearbeiterName, t.ErstellerName, Zeit(t.Erstellt), Zeit(t.Geaendert),
                    Zeit(t.ReaktionFaellig), Zeit(t.LoesungFaellig), Zeit(t.ErsteReaktion),
                    Zeit(t.Geloest), t.VerweisAufTicket?.ToString(CultureInfo.InvariantCulture),
                    t.Rueckrufnummer, Zeit(t.Anrufzeit),
                    Zeit(t.Wiedervorlage), t.WiedervorlageGrund
                }));

            Eintrag(archiv, "historie.csv",
                ["Nummer", "Feld", "Alt", "Neu", "Von", "Am (UTC)"],
                tickets.SelectMany(t => t.Historie.Select(h => new[]
                {
                    t.Id.ToString(CultureInfo.InvariantCulture), h.Feld, h.Alt, h.Neu, h.Von, Zeit(h.Am)
                })));

            Eintrag(archiv, "kommentare.csv",
                ["Nummer", "Autor", "Text", "Am (UTC)"],
                tickets.SelectMany(t => t.Kommentare.Select(k => new[]
                {
                    t.Id.ToString(CultureInfo.InvariantCulture), k.AutorName, k.Text, Zeit(k.Am)
                })));

            Eintrag(archiv, "kontakte.csv",
                ["Name", "LetzteAdresse", "Rufnummer"],
                kontakte.Select(k => new[]
                {
                    k.Name, k.LetzteAdresse, k.Rufnummer
                }));

            Eintrag(archiv, "wissen.csv",
                ["Titel", "Kategorie", "Tags", "Veroeffentlicht", "Eigentuemer", "Geaendert (UTC)", "Inhalt"],
                artikel.Select(a => new[]
                {
                    a.Titel, a.Kategorie, string.Join(", ", a.Tags),
                    a.Veroeffentlicht ? "ja" : "nein", a.Eigentuemer, Zeit(a.Geaendert), a.Inhalt
                }));

            Hinweis(archiv);
        }

        return speicher.ToArray();
    }

    // Kein Zusammenführen: Der alte Bestand wird vollständig ersetzt, weil
    // zwei Stände desselben Tickets sich ohne Abgleichregeln nicht auflösen
    // lassen. Alles in einer Transaktion; bricht der Import ab, steht der
    // alte Bestand unverändert da.
    public async Task<Bestand> ImportJsonAsync(Akteur akteur, Stream datei)
    {
        RequireAdministration(akteur);

        var buendel = Lesen(datei);

        // Der Verfolger kennt womöglich noch Zeilen, die gleich gelöscht werden;
        // das Einfügen stolperte sonst über dieselbe Ticketnummer.
        db.ChangeTracker.Clear();

        await using var transaktion = await db.Database.BeginTransactionAsync();

        await LeerenAsync();
        await SchreibenAsync(buendel);
        await transaktion.CommitAsync();
        db.ChangeTracker.Clear();

        log.LogInformation(
            "Import durch {Akteur}: {Tickets} Tickets, {Kontakte} Kontakte, {Artikel} Artikel, " +
            "{Vorschlaege} Vorschläge, {Ruhezeiten} Ruhezeiten (Datei-Version {Version})",
            akteur.Name, buendel.Tickets.Count, buendel.Kontakte.Count, buendel.Wissensartikel.Count,
            buendel.Vorschlaege.Count, buendel.Ruhezeiten.Count, buendel.Version);

        return new Bestand(
            await db.Tickets.CountAsync(),
            await db.TicketHistory.CountAsync(),
            await db.TicketComments.CountAsync(),
            await db.Kontakte.CountAsync(),
            await db.KbArticles.CountAsync(),
            await db.Ruhezeiten.CountAsync(),
            await db.KbVorschlaege.CountAsync(),
            await db.KbKategorien.CountAsync(),
            await db.KbTags.CountAsync());
    }

    private static Datenbuendel Lesen(Stream datei)
    {
        Datenbuendel? buendel;
        try
        {
            buendel = JsonSerializer.Deserialize<Datenbuendel>(datei, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Die Datei ist keine gültige JSON-Datei. Der Bestand wurde nicht angefasst.", ex);
        }

        // Die Kennung ist der eigentliche Schutz: Ohne sie ginge eine fremde
        // JSON-Datei als leeres Bündel durch und löschte den Bestand.
        if (buendel is null || buendel.Anwendung != Datenbuendel.Kennung)
        {
            throw new InvalidOperationException(
                "Diese Datei stammt nicht aus dem Ticketsystem. Der Bestand wurde nicht angefasst.");
        }

        // Ältere Versionen werden gelesen, neuere nicht: Was in einer alten Datei
        // fehlt, kennen wir; was in einer neuen zusätzlich steht, kennen wir
        // nicht, und es still zu verwerfen wäre Verlust.
        if (buendel.Version > Datenbuendel.AktuelleVersion)
        {
            throw new InvalidOperationException(
                $"Diese Datei hat Format-Version {buendel.Version}, gelesen wird bis " +
                $"{Datenbuendel.AktuelleVersion}. Der Bestand wurde nicht angefasst.");
        }

        return buendel;
    }

    // Von den Kindern zu den Eltern, sonst greifen die Fremdschlüssel; die
    // Verknüpfung Artikel/Tag und die Fassungen räumt die Kaskade ab.
    private async Task LeerenAsync()
    {
        await db.TicketComments.ExecuteDeleteAsync();
        await db.TicketHistory.ExecuteDeleteAsync();
        await db.Tickets.ExecuteDeleteAsync();
        await db.Kontakte.ExecuteDeleteAsync();
        await db.KbVorschlaege.ExecuteDeleteAsync();
        await db.KbArticles.ExecuteDeleteAsync();
        await db.KbTags.ExecuteDeleteAsync();
        await db.KbKategorien.ExecuteDeleteAsync();
        await db.Ruhezeiten.ExecuteDeleteAsync();
    }

    private async Task SchreibenAsync(Datenbuendel buendel)
    {
        foreach (var satz in buendel.Tickets)
        {
            db.Tickets.Add(new Ticket
            {
                // Die Ticketnummer wird übernommen, nicht neu vergeben: Sie steht
                // in Mails, auf Notizzetteln und in Verweisen anderer Tickets.
                Id = satz.Id,
                Title = satz.Titel,
                Description = satz.Beschreibung,
                Priority = satz.Prioritaet,
                Status = satz.Status,
                Source = satz.Quelle,
                CustomerName = satz.KundeName,
                CustomerEmail = satz.KundeEmail,
                CustomerId = satz.KundeId,
                CreatedById = satz.ErstellerId,
                CreatedByName = satz.ErstellerName,
                AgentId = satz.BearbeiterId,
                AgentName = satz.BearbeiterName,
                Address = satz.Adresse,
                CallbackNumber = satz.Rueckrufnummer,
                CallTime = satz.Anrufzeit,
                CallNote = satz.Gespraechsnotiz,
                FollowUpAt = satz.Wiedervorlage,
                FollowUpNote = satz.WiedervorlageGrund,
                RelatedTicketId = satz.VerweisAufTicket,
                CreatedAt = satz.Erstellt,
                UpdatedAt = satz.Geaendert,
                ReactionDueAt = satz.ReaktionFaellig,
                ResolutionDueAt = satz.LoesungFaellig,
                FirstReactionAt = satz.ErsteReaktion,
                ResolvedAt = satz.Geloest,
                SlaBreachNotifiedAt = satz.SlaGemeldet,
                History = satz.Historie.Select(h => new TicketHistoryEntry
                {
                    Field = h.Feld,
                    OldValue = h.Alt,
                    NewValue = h.Neu,
                    ChangedBy = h.Von,
                    ChangedAt = h.Am
                }).ToList(),
                Comments = satz.Kommentare.Select(k => new TicketComment
                {
                    AuthorId = k.AutorId,
                    AuthorName = k.AutorName,
                    Text = k.Text,
                    CreatedAt = k.Am
                }).ToList()
            });
        }

        foreach (var satz in buendel.Kontakte)
        {
            db.Kontakte.Add(new Kontakt
            {
                Name = satz.Name,
                NameNormalisiert = satz.Name.Trim().ToLowerInvariant(),
                LetzteAdresse = satz.LetzteAdresse,
                CreatedAt = satz.Erstellt,
                UpdatedAt = satz.Geaendert,
                // Version 3 nennt die Nummer direkt, eine ältere Datei trägt die
                // Liste; dann gilt die zuletzt genannte.
                LetzteRufnummer = satz.Rufnummer
                    ?? satz.Rufnummern?.OrderByDescending(r => r.Erstellt).FirstOrDefault()?.Nummer
            });
        }

        foreach (var satz in buendel.Ruhezeiten)
        {
            db.Ruhezeiten.Add(new Ruhezeit
            {
                Bezeichnung = satz.Bezeichnung,
                Von = satz.Von,
                Bis = satz.Bis,
                AngelegtVon = satz.AngelegtVon,
                CreatedAt = satz.Erstellt
            });
        }

        // Namen aus den Listen des Bündels und von den Artikeln laufen durch
        // dieselben zwei Wörterbücher, deshalb entsteht jeder Name genau einmal.
        var kategorien = new Dictionary<string, KbKategorie>(StringComparer.OrdinalIgnoreCase);
        var tags = new Dictionary<string, KbTag>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in buendel.Kategorien)
        {
            Kategorie(name);
        }

        foreach (var name in buendel.Tags)
        {
            Schlagwort(name);
        }

        // Nach Titel, damit die Vorschläge unten ihren Bezug finden: Die Nummern
        // der Quelldatenbank sind wertlos, das Einfügen vergibt neue.
        var artikelNachTitel = new Dictionary<string, KbArticle>(StringComparer.OrdinalIgnoreCase);

        foreach (var satz in buendel.Wissensartikel)
        {
            var artikel = new KbArticle
            {
                Title = satz.Titel,
                Content = satz.Inhalt,
                Kategorie = Kategorie(satz.Kategorie),
                Published = satz.Veroeffentlicht,
                Owner = satz.Eigentuemer,
                ReviewIntervallTage = satz.PruefzyklusTage <= 0 ? KbArticle.StandardPruefzyklusTage : satz.PruefzyklusTage,
                CreatedAt = satz.Erstellt,
                UpdatedAt = satz.Geaendert
            };

            foreach (var name in satz.Tags)
            {
                var tag = Schlagwort(name);
                if (tag is not null)
                {
                    artikel.Tags.Add(tag);
                }
            }

            // Eine Datei ohne Fassungen (vor Version 3) bekommt ihren Stand als
            // Fassung 1, denn eine leere Geschichte sähe aus wie ein Fehler.
            if (satz.Fassungen.Count == 0)
            {
                artikel.Fassungen.Add(new KbArtikelFassung
                {
                    Nummer = 1,
                    Title = satz.Titel,
                    Content = satz.Inhalt,
                    Kategorie = satz.Kategorie,
                    Tags = string.Join(", ", satz.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase)),
                    Published = satz.Veroeffentlicht,
                    Anlass = Fassungsanlass.AusSicherung,
                    GespeichertVon = satz.Eigentuemer ?? "unbekannt",
                    GespeichertAm = satz.Geaendert
                });
            }
            else
            {
                artikel.Fassungen.AddRange(satz.Fassungen.Select(f => new KbArtikelFassung
                {
                    Nummer = f.Nummer,
                    Title = f.Titel,
                    Content = f.Inhalt,
                    Kategorie = f.Kategorie,
                    Tags = f.Tags,
                    Published = f.Veroeffentlicht,
                    Anlass = f.Anlass,
                    GespeichertVon = f.GespeichertVon,
                    GespeichertVonId = f.GespeichertVonId,
                    GespeichertAm = f.GespeichertAm
                }));
            }

            db.KbArticles.Add(artikel);
            artikelNachTitel.TryAdd(satz.Titel, artikel);
        }

        await db.SaveChangesAsync();

        // Erst speichern, dann die Vorschläge: Ihr Fremdschlüssel braucht die
        // Nummer, die der Artikel beim Einfügen bekommt.
        foreach (var satz in buendel.Vorschlaege)
        {
            KbArticle? artikel = null;
            if (!string.IsNullOrWhiteSpace(satz.ArtikelTitel))
            {
                artikelNachTitel.TryGetValue(satz.ArtikelTitel, out artikel);
            }

            db.KbVorschlaege.Add(new KbAenderungsvorschlag
            {
                ArticleId = artikel?.Id,
                Title = satz.Titel,
                Content = satz.Inhalt,
                KategorieId = Kategorie(satz.Kategorie)?.Id,
                VorgeschlagenVon = satz.VorgeschlagenVon,
                VorgeschlagenAm = satz.VorgeschlagenAm
            });
        }

        await db.SaveChangesAsync();
        return;

        KbKategorie? Kategorie(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (!kategorien.TryGetValue(name, out var vorhanden))
            {
                vorhanden = new KbKategorie { Name = name.Trim() };
                kategorien[name] = vorhanden;
                db.KbKategorien.Add(vorhanden);
            }

            return vorhanden;
        }

        KbTag? Schlagwort(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (!tags.TryGetValue(name, out var vorhanden))
            {
                vorhanden = new KbTag
                {
                    Name = name.Trim(),
                    NameNormalisiert = name.Trim().ToLowerInvariant()
                };
                tags[name] = vorhanden;
                db.KbTags.Add(vorhanden);
            }

            return vorhanden;
        }
    }

    private async Task<List<TicketSatz>> TicketsaetzeAsync() =>
        await db.Tickets
            .AsNoTracking()
            .Include(t => t.History)
            .Include(t => t.Comments)
            // Zwei Sammlungen in einer Abfrage ergäben ein kartesisches Produkt:
            // zehn Historieneinträge mal drei Kommentare wären dreißig Zeilen
            // je Ticket.
            .AsSplitQuery()
            .OrderBy(t => t.Id)
            .Select(t => new TicketSatz
            {
                Id = t.Id,
                Titel = t.Title,
                Beschreibung = t.Description,
                Prioritaet = t.Priority,
                Status = t.Status,
                Quelle = t.Source,
                KundeName = t.CustomerName,
                KundeEmail = t.CustomerEmail,
                KundeId = t.CustomerId,
                ErstellerId = t.CreatedById,
                ErstellerName = t.CreatedByName,
                BearbeiterId = t.AgentId,
                BearbeiterName = t.AgentName,
                Adresse = t.Address,
                Rueckrufnummer = t.CallbackNumber,
                Anrufzeit = t.CallTime,
                Gespraechsnotiz = t.CallNote,
                Wiedervorlage = t.FollowUpAt,
                WiedervorlageGrund = t.FollowUpNote,
                VerweisAufTicket = t.RelatedTicketId,
                Erstellt = t.CreatedAt,
                Geaendert = t.UpdatedAt,
                ReaktionFaellig = t.ReactionDueAt,
                LoesungFaellig = t.ResolutionDueAt,
                ErsteReaktion = t.FirstReactionAt,
                Geloest = t.ResolvedAt,
                SlaGemeldet = t.SlaBreachNotifiedAt,
                Historie = t.History
                    .OrderBy(h => h.ChangedAt)
                    .Select(h => new HistorieSatz
                    {
                        Feld = h.Field,
                        Alt = h.OldValue,
                        Neu = h.NewValue,
                        Von = h.ChangedBy,
                        Am = h.ChangedAt
                    }).ToList(),
                Kommentare = t.Comments
                    .OrderBy(k => k.CreatedAt)
                    .Select(k => new KommentarSatz
                    {
                        AutorId = k.AuthorId,
                        AutorName = k.AuthorName,
                        Text = k.Text,
                        Am = k.CreatedAt
                    }).ToList()
            })
            .ToListAsync();

    private async Task<List<KontaktSatz>> KontaktsaetzeAsync() =>
        await db.Kontakte
            .AsNoTracking()
            .OrderBy(k => k.Name)
            .Select(k => new KontaktSatz
            {
                Name = k.Name,
                LetzteAdresse = k.LetzteAdresse,
                Erstellt = k.CreatedAt,
                Geaendert = k.UpdatedAt,
                Rufnummer = k.LetzteRufnummer
            })
            .ToListAsync();

    private async Task<List<ArtikelSatz>> ArtikelsaetzeAsync() =>
        await db.KbArticles
            .AsNoTracking()
            .OrderBy(a => a.Title)
            .Select(a => new ArtikelSatz
            {
                Titel = a.Title,
                Inhalt = a.Content,
                Kategorie = a.Kategorie != null ? a.Kategorie.Name : null,
                Tags = a.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
                Veroeffentlicht = a.Published,
                Eigentuemer = a.Owner,
                PruefzyklusTage = a.ReviewIntervallTage,
                Erstellt = a.CreatedAt,
                Geaendert = a.UpdatedAt,
                Fassungen = a.Fassungen.OrderBy(f => f.Nummer).Select(f => new FassungSatz
                {
                    Nummer = f.Nummer,
                    Titel = f.Title,
                    Inhalt = f.Content,
                    Kategorie = f.Kategorie,
                    Tags = f.Tags,
                    Veroeffentlicht = f.Published,
                    Anlass = f.Anlass,
                    GespeichertVon = f.GespeichertVon,
                    GespeichertVonId = f.GespeichertVonId,
                    GespeichertAm = f.GespeichertAm
                }).ToList()
            })
            .ToListAsync();

    private async Task<List<RuhezeitSatz>> RuhezeitsaetzeAsync() =>
        await db.Ruhezeiten
            .AsNoTracking()
            .OrderBy(r => r.Von)
            .Select(r => new RuhezeitSatz
            {
                Bezeichnung = r.Bezeichnung,
                Von = r.Von,
                Bis = r.Bis,
                AngelegtVon = r.AngelegtVon,
                Erstellt = r.CreatedAt
            })
            .ToListAsync();

    // Die LIESMICH erklärt dem Auswerter das Hochkomma vor Formelzeichen, die
    // einzige Stelle, an der die Tabelle den Text verändert.
    private static void Hinweis(ZipArchive archiv)
    {
        var eintrag = archiv.CreateEntry("LIESMICH.txt", CompressionLevel.Optimal);
        using var strom = eintrag.Open();
        using var schreiber = new StreamWriter(strom, new UTF8Encoding(true)) { NewLine = "\r\n" };

        schreiber.Write(
            """
            Tabellen aus dem Ticketsystem

            Trennzeichen ist das Semikolon, die Kodierung UTF-8 mit
            Byte-Order-Mark. Beides erwartet Excel in deutscher Einstellung.

            Zeiten stehen in Weltzeit (UTC)
            -------------------------------
            Die Spalten mit "(UTC)" im Kopf tragen Weltzeit, nicht die
            Ortszeit vom Bildschirm. In Deutschland sind das eine Stunde im
            Winter und zwei im Sommer weniger als dort. Der Grund: Diese
            Dateien sollen sich wiedereinlesen und zwischen Rechnern bewegen
            lassen, und eine Zeit ohne Bezug ist dann nicht mehr zu retten.

            Warum manche Felder mit einem Hochkomma beginnen
            ------------------------------------------------
            Ein Feld, das mit = + - oder @ anfängt, liest eine
            Tabellenkalkulation als Formel. Ein Ticket mit dem Titel
            "=cmd|..." wäre damit ein Angriff gegen den, der die Auswertung
            öffnet. Deshalb steht vor solchen Feldern ein Hochkomma; das ist
            die von OWASP empfohlene Entschärfung.

            Der Preis: Auch harmlose Texte bekommen es, etwa eine
            Gesprächsnotiz, die mit einem Aufzählungsstrich beginnt. Aus
            "- Drucker piept" wird "'- Drucker piept". Das Hochkomma gehört
            nicht zum Text.

            Nur den Strich zu verschonen wäre keine Lösung: In einer Formel
            darf zwischen Rechenzeichen und Operand ein Leerzeichen stehen,
            "- cmd|..." wäre also weiterhin eine Formel.

            Wer den Text unverändert braucht
            --------------------------------
            Dafür ist der Weg "Export als JSON" da. Die JSON-Datei ist das
            verlustfreie Format zum Weitergeben und Wiedereinlesen, diese
            Tabellen sind das Format für die Auswertung.
            """);
    }

    private static void Eintrag(ZipArchive archiv, string name, string[] kopf, IEnumerable<string?[]> zeilen)
    {
        var eintrag = archiv.CreateEntry(name, CompressionLevel.Optimal);
        using var strom = eintrag.Open();

        // UTF-8 mit BOM, weil Excel eine Datei ohne BOM als Windows-1252 liest
        // und aus „Müller" dann „MÃ¼ller" macht. Fest CRLF statt
        // Environment.NewLine: RFC 4180 verlangt es, und die Datei soll auf jedem
        // Rechner gleich aussehen.
        using var schreiber = new StreamWriter(strom, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            NewLine = "\r\n"
        };
        schreiber.WriteLine(string.Join(';', kopf.Select(Feld)));
        foreach (var zeile in zeilen)
        {
            schreiber.WriteLine(string.Join(';', zeile.Select(Feld)));
        }
    }

    // Semikolon als Trenner, weil Excel in deutscher Einstellung genau das
    // erwartet; Anführungszeichen im Text werden verdoppelt (RFC 4180).
    private static string Feld(string? wert)
    {
        if (string.IsNullOrEmpty(wert))
        {
            return string.Empty;
        }

        var text = Entschaerfen(wert);
        if (text.AsSpan().IndexOfAny(';', '"', '\n') >= 0 || text.Contains('\r'))
        {
            return '"' + text.Replace("\"", "\"\"") + '"';
        }

        return text;
    }

    // Ein Feld, das mit = + - oder @ beginnt, führt Excel als Formel aus; ein
    // Titel „=cmd|..." wäre ein Angriff auf den, der die Auswertung öffnet.
    // Das Hochkomma ist die von OWASP empfohlene Entschärfung.
    private static string Entschaerfen(string wert) =>
        wert.Length > 0 && wert[0] is '=' or '+' or '-' or '@' ? "'" + wert : wert;

    private async Task<List<VorschlagSatz>> VorschlagsaetzeAsync() =>
        await db.KbVorschlaege
            .AsNoTracking()
            .OrderBy(v => v.Id)
            .Select(v => new VorschlagSatz
            {
                ArtikelTitel = v.ArticleId == null
                    ? null
                    : db.KbArticles.Where(a => a.Id == v.ArticleId).Select(a => a.Title).FirstOrDefault(),
                Titel = v.Title,
                Inhalt = v.Content,
                Kategorie = v.KategorieId == null
                    ? null
                    : db.KbKategorien.Where(k => k.Id == v.KategorieId).Select(k => k.Name).FirstOrDefault(),
                VorgeschlagenVon = v.VorgeschlagenVon,
                VorgeschlagenAm = v.VorgeschlagenAm
            })
            .ToListAsync();

    // Zeiten bleiben UTC, anders als auf dem Bildschirm: Die Datei ist zum
    // Wiedereinlesen da, und eine Zeit ohne Bezug ist bei einem Umzug nicht
    // mehr zu retten. Die Spaltenköpfe tragen deshalb „(UTC)".
    private static string? Zeit(DateTime? wert) =>
        wert?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Zeit(DateTime wert) =>
        wert.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static void RequireAdministration(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Administration))
        {
            throw new InvalidOperationException("Export und Import darf nur die Administration.");
        }
    }

    private static void RequireTeamleitung(Akteur akteur)
    {
        if (!akteur.IstMindestens(RoleLevel.Teamleitung))
        {
            throw new InvalidOperationException("Der Tabellen-Export ist ab Teamleitung erlaubt.");
        }
    }
}
