using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Was passiert, wenn der Bestand wächst. Gemessen mit 5000 Vorgängen: 2,8 MB
// Seite, 649 Millisekunden für die Ticketliste, kein einziger Index auf
// Tickets. Ein Helpdesk erzeugt leicht 1000 Vorgänge im Jahr.
public sealed class WachstumTests : IDisposable
{
    private readonly string _ordner;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public WachstumTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), $"wachstum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_ordner);
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite($"Data Source={Path.Combine(_ordner, "app.db")};Pooling=false")
            .Options;
        _db = new TicketsystemContext(options);
        _db.Database.Migrate();
        _service = new TicketService(_db);
    }

    [Fact]
    public async Task Die_Liste_endet_bei_der_Obergrenze_und_sagt_es()
    {
        await VorgaengeAnlegenAsync(TicketService.Obergrenze + 5);

        var liste = await _service.ListAsync(TestDaten.Teamleitung);

        Assert.Equal(TicketService.Obergrenze, liste.Zeilen.Count);
        Assert.True(liste.Gekuerzt, "Die Liste müsste sich als gekürzt melden.");
        Assert.Equal(TicketService.Obergrenze + 5, liste.Gesamt);
    }

    [Fact]
    public async Task Eine_kurze_Liste_meldet_keine_Kuerzung()
    {
        await VorgaengeAnlegenAsync(3);

        var liste = await _service.ListAsync(TestDaten.Teamleitung);

        Assert.Equal(3, liste.Zeilen.Count);
        Assert.False(liste.Gekuerzt);
        Assert.Equal(3, liste.Gesamt);
    }

    // Abgeschnitten wird hinten, nicht vorne: Wer die Liste öffnet, sucht den
    // jüngsten Vorgang, nicht den ältesten.
    [Fact]
    public async Task Die_Obergrenze_nimmt_die_neuesten_Vorgaenge()
    {
        await VorgaengeAnlegenAsync(TicketService.Obergrenze + 2);

        var liste = await _service.ListAsync(TestDaten.Teamleitung);

        var neuester = await _db.Tickets.AsNoTracking().OrderByDescending(t => t.Id).FirstAsync();
        Assert.Equal(neuester.Id, liste.Zeilen[0].Id);
    }

    // Der Abfrageplan lautete „SCAN Tickets" plus „USE TEMP B-TREE FOR ORDER
    // BY". Geprüft wird die Zusage, nicht das Verhalten von SQLite.
    [Fact]
    public async Task Auf_den_gefilterten_Spalten_liegen_Indizes()
    {
        var namen = await IndexSpaltenAsync();

        Assert.Contains("Status", namen);
        Assert.Contains("CreatedAt", namen);
        Assert.Contains("Address", namen);
        Assert.Contains("CustomerName", namen);
    }

    // Der Dienst lebt eine Anfrage lang, und innerhalb dieser Anfrage genügt
    // eine Ermittlung. Die Kehrseite ist hier ausdrücklich festgehalten: Wer im
    // selben Aufruf einen Vorgang anlegt, sieht ihn im zweiten Fristenstand
    // nicht mehr. Für die Kopfzeile ist das richtig, für eine Auswertung wäre
    // es falsch.
    [Fact]
    public async Task Der_Fristenstand_wird_je_Anfrage_nur_einmal_ermittelt()
    {
        await VorgaengeAnlegenAsync(1);
        var erster = await _service.FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);

        await UeberfaelligenVorgangAnlegenAsync();
        var zweiter = await _service.FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);

        Assert.Equal(erster, zweiter);

        var naechsteAnfrage = await new TicketService(_db)
            .FristenstandAsync(TestDaten.Teamleitung, DateTime.UtcNow);
        Assert.Equal(1, naechsteAnfrage.Ueberfaellig);
    }

    // Zwei Quellen derselben Wahrheit driften: Die Vorgaben leben nur im Code,
    // Abweichungen in der Datei im Datenordner, die ein Mensch bewusst anlegt.
    [Fact]
    public void Die_Auslieferung_traegt_keine_Einstellungsdatei_mehr()
    {
        var datei = Path.Combine(Quellordner(), "src", "Ticketsystem.App", "appsettings.json");

        Assert.False(File.Exists(datei), "Die appsettings.json neben der Programmdatei ist zurück.");
    }

    private static string Quellordner()
    {
        var ordner = AppContext.BaseDirectory;
        while (ordner is not null && !File.Exists(Path.Combine(ordner, "Ticketsystem.sln")))
        {
            ordner = Directory.GetParent(ordner)?.FullName;
        }

        return ordner ?? throw new InvalidOperationException("Die Projektwurzel wurde nicht gefunden.");
    }

    private async Task<HashSet<string>> IndexSpaltenAsync()
    {
        var spalten = new HashSet<string>(StringComparer.Ordinal);
        await using var verbindung = new SqliteConnection(_db.Database.GetConnectionString() + ";Pooling=false");
        await verbindung.OpenAsync();

        await using var liste = verbindung.CreateCommand();
        liste.CommandText = "SELECT name FROM pragma_index_list('Tickets')";
        var indizes = new List<string>();
        await using (var leser = await liste.ExecuteReaderAsync())
        {
            while (await leser.ReadAsync())
            {
                indizes.Add(leser.GetString(0));
            }
        }

        foreach (var index in indizes)
        {
            await using var befehl = verbindung.CreateCommand();
            befehl.CommandText = $"SELECT name FROM pragma_index_info('{index}')";
            await using var leser = await befehl.ExecuteReaderAsync();
            while (await leser.ReadAsync())
            {
                spalten.Add(leser.GetString(0));
            }
        }

        return spalten;
    }

    private async Task VorgaengeAnlegenAsync(int anzahl)
    {
        for (var i = 0; i < anzahl; i++)
        {
            await _service.CreateAsync(
                $"Vorgang {i}", "Beschreibung", TicketPriority.Medium, "Test", TicketSource.Phone);
        }
    }

    private async Task UeberfaelligenVorgangAnlegenAsync()
    {
        var ticket = await _service.CreateAsync(
            "Überfällig", "Beschreibung", TicketPriority.Critical, "Test", TicketSource.Phone);
        ticket.ReactionDueAt = DateTime.UtcNow.AddHours(-3);
        ticket.ResolutionDueAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_ordner, recursive: true);
    }
}
