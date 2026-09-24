using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Die Erwartungswerte sind von Hand gerechnet: Eine Auswertung, deren
// Erwartungswerte der Code selbst liefert, prüft nur, dass der Code sich
// selbst gleicht. Der Datensatz bei festem Jetzt:
//   A: vor 1 Tag,   Raum A-1, Mittel, gelöst vor der Frist      (Erfüllt)
//   B: vor 1 Tag,   Raum A-1, Hoch,   offen, Fristen in Zukunft (Läuft)
//   C: vor 8 Tagen, Raum B-2, Mittel, offen, Lösungsfrist vorbei (Überfällig)
//   D: vor 40 Tagen, außerhalb des Vier-Wochen-Zeitraums
// A und B liegen am selben Tag, C genau sieben Tage früher, also immer in
// einer anderen Kalenderwoche.
public sealed class AuswertungTests : IDisposable
{
    private static readonly DateTime Jetzt = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly AuswertungService _service;

    public AuswertungTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new AuswertungService(_db);

        _db.Tickets.AddRange(
            Ticket("A", Jetzt.AddDays(-1), "A-1", TicketPriority.Medium,
                t => { t.Status = TicketStatus.Resolved; t.FirstReactionAt = Jetzt.AddDays(-1); t.ResolvedAt = Jetzt.AddHours(-20); }),
            Ticket("B", Jetzt.AddDays(-1), "A-1", TicketPriority.High, t => { }),
            Ticket("C", Jetzt.AddDays(-8), "B-2", TicketPriority.Medium,
                t => { t.ReactionDueAt = Jetzt.AddDays(-7); t.ResolutionDueAt = Jetzt.AddDays(-1); }),
            Ticket("D", Jetzt.AddDays(-40), "C-3", TicketPriority.Low, t => { }));
        _db.SaveChanges();
    }

    private static Ticket Ticket(string titel, DateTime erstellt, string adresse,
        TicketPriority prioritaet, Action<Ticket> anpassen)
    {
        var ticket = new Ticket
        {
            Title = titel,
            Description = "x",
            CustomerName = "Weber, Sabine",
            Source = TicketSource.Phone,
            Priority = prioritaet,
            Address = adresse,
            CreatedAt = erstellt,
            UpdatedAt = erstellt,
            ReactionDueAt = Jetzt.AddDays(1),
            ResolutionDueAt = Jetzt.AddDays(2)
        };
        anpassen(ticket);
        return ticket;
    }

    [Fact]
    public async Task Die_Auswertung_zaehlt_Wochen_Raeume_und_Prioritaeten_wie_von_Hand_gerechnet()
    {
        var auswertung = await _service.ErstellenAsync(TestDaten.Teamleitung, wochen: 4, Jetzt);

        Assert.Equal(3, auswertung.Gesamt);
        Assert.Equal([2, 1], auswertung.JeWoche.Select(w => w.Anzahl));
        Assert.Equal([("A-1", 2), ("B-2", 1)],
            auswertung.JeRaum.Select(z => (z.Schluessel, z.Anzahl)));
        Assert.Equal([("Mittel", 2), ("Hoch", 1)],
            auswertung.JePrioritaet.Select(z => (z.Schluessel, z.Anzahl)));
    }

    [Fact]
    public async Task Die_SLA_Quote_zaehlt_Ueberfaellig_und_Verspaetet_als_Riss()
    {
        var auswertung = await _service.ErstellenAsync(TestDaten.Teamleitung, wochen: 4, Jetzt);

        Assert.Equal(2, auswertung.OhneRiss);
        Assert.Contains(auswertung.JeZustand, z => z.Schluessel == "Überfällig" && z.Anzahl == 1);
        Assert.Contains(auswertung.JeZustand, z => z.Schluessel == "Erfüllt" && z.Anzahl == 1);
        Assert.Contains(auswertung.JeZustand, z => z.Schluessel == "Läuft" && z.Anzahl == 1);
    }

    [Fact]
    public async Task Der_Zeitraum_schneidet_alte_Vorgaenge_ab()
    {
        var zwoelf = await _service.ErstellenAsync(TestDaten.Teamleitung, wochen: 12, Jetzt);

        Assert.Equal(4, zwoelf.Gesamt);
    }

    [Fact]
    public async Task Die_Auswertung_ist_der_Teamleitung_vorbehalten()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ErstellenAsync(TestDaten.Bearbeiter1, wochen: 4, Jetzt));
    }

    // Der CSV-Export war genau der Rolle versperrt, die auswertet, und schützte
    // Daten, die sie in der Anwendung ohnehin sieht.
    [Fact]
    public async Task Der_CSV_Export_ist_ab_Teamleitung_erlaubt()
    {
        var austausch = new AustauschService(_db, NullLogger<AustauschService>.Instance);

        var datei = await austausch.ExportCsvAsync(TestDaten.Teamleitung);

        Assert.NotEmpty(datei);
    }

    // Der Import verändert den Bestand, der JSON-Export ist das Umzugsformat.
    [Fact]
    public async Task JSON_Export_und_Import_bleiben_bei_der_Administration()
    {
        var austausch = new AustauschService(_db, NullLogger<AustauschService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            austausch.ExportJsonAsync(TestDaten.Teamleitung));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            austausch.ImportJsonAsync(TestDaten.Teamleitung, new MemoryStream()));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
