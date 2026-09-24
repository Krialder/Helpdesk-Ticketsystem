using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Macht die DSGVO-Anleitung ausführbar: Die Tests lesen die SQL-Blöcke aus
// docs/betrieb/datenschutz.md und führen sie gegen eine Datenbank aus, in der
// genau die Person steckt, die dort als Beispiel steht. Eine Anleitung hat
// sonst nichts, was rot werden könnte, und war deshalb über Monate falsch,
// ohne dass es jemand merkte.
public sealed class DsgvoAnleitungTests : IDisposable
{
    // Dieselben Werte wie in der Checkliste. Weichen sie ab, findet die
    // Anleitung nichts, und genau das sollen diese Tests melden.
    private const string Name = "Weber, Sabine";
    private const string Mail = "s.weber@example.com";
    private const string NameNormalisiert = "weber, sabine";

    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;

    public DsgvoAnleitungTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    private static string ChecklistePfad()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var kandidat = Path.Combine(ordner.FullName, "docs", "betrieb", "datenschutz.md");
            if (File.Exists(kandidat))
            {
                return kandidat;
            }

            ordner = ordner.Parent;
        }

        throw new FileNotFoundException("docs/betrieb/datenschutz.md wurde oberhalb von " + AppContext.BaseDirectory + " nicht gefunden.");
    }

    // Abschnittsweise, damit ein Test die Auskunft prüfen kann, ohne die
    // Löschung auszuführen.
    private static IReadOnlyList<string> AnweisungenAus(params string[] abschnitte)
    {
        var text = File.ReadAllText(ChecklistePfad());
        var anweisungen = new List<string>();

        foreach (var abschnitt in abschnitte)
        {
            var beginn = text.IndexOf("## " + abschnitt, StringComparison.Ordinal);
            Assert.True(beginn >= 0, $"Abschnitt „{abschnitt}“ steht nicht in der Checkliste.");

            var ende = text.IndexOf("\n## ", beginn + 3, StringComparison.Ordinal);
            var inhalt = ende < 0 ? text[beginn..] : text[beginn..ende];

            foreach (Match block in Regex.Matches(inhalt, "```sql\\s*(.*?)```", RegexOptions.Singleline))
            {
                anweisungen.AddRange(block.Groups[1].Value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(a => a.Length > 0));
            }
        }

        Assert.NotEmpty(anweisungen);
        return anweisungen;
    }

    private long Zaehle(string sql)
    {
        using var befehl = _connection.CreateCommand();
        befehl.CommandText = $"SELECT count(*) FROM ({sql})";
        return Convert.ToInt64(befehl.ExecuteScalar());
    }

    private void Ausfuehren(string sql)
    {
        using var befehl = _connection.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    // Legt genau die Spuren an, die eine Person in diesem System hinterlässt:
    // ein Telefon-Ticket, ein Web-Ticket aus ihrem Konto (dort ist der Name die
    // Mailadresse), einen Kommentar, einen Historieneintrag, einen Stammsatz
    // und das Konto. Kundenkommentar und Kontobindung sind Portal-Altbestand,
    // den der Dienst nicht mehr anlegt; sie kommen direkt in die Datenbank,
    // denn echte Datenbanken tragen sie noch.
    private async Task SpurenAnlegenAsync()
    {
        var tickets = new TicketService(_db);
        var stammdaten = new StammdatenService(_db);
        var mitarbeiter = TestDaten.Bearbeiter1;
        TestDaten.KontoAnlegen(_db, mitarbeiter);

        _db.Users.Add(new AppUser
        {
            Id = "kunde-weber", UserName = Mail, Email = Mail,
            NormalizedEmail = Mail.ToUpperInvariant(), NormalizedUserName = Mail.ToUpperInvariant()
        });
        await _db.SaveChangesAsync();

        var telefonTicket = await tickets.CreatePhoneAsync(
            "Drucker klemmt", "Papierstau gemeldet.", TicketPriority.Medium, Name,
            "0221333444", DateTime.UtcNow, "Rückruf am Nachmittag zugesagt",
            createdById: mitarbeiter.Id, createdByName: mitarbeiter.Name,
            address: "A-9", customerEmail: Mail);
        // Der Wiedervorlage-Grund ist Freitext und trägt hier den Namen der Person:
        // Die Löschung muss auch dieses Feld leeren.
        await tickets.WiedervorlageSetzenAsync(telefonTicket.Id, DateTime.UtcNow.AddDays(2),
            "Rückruf Weber, Sabine", mitarbeiter);

        var eigenes = await tickets.CreateAsync(
            "Zugang fehlt", "Kein Zugriff auf das Laufwerk.", TicketPriority.Low, Mail,
            TicketSource.Web, customerEmail: Mail,
            createdById: "kunde-weber", createdByName: Mail);
        eigenes.CustomerId = "kunde-weber";
        await _db.SaveChangesAsync();

        _db.TicketComments.Add(new TicketComment
        {
            TicketId = eigenes.Id,
            AuthorId = "kunde-weber",
            AuthorName = Mail,
            Text = "Gibt es dazu schon etwas Neues?",
            CreatedAt = DateTime.UtcNow
        });
        _db.TicketHistory.Add(new TicketHistoryEntry
        {
            TicketId = eigenes.Id,
            Field = "Kommentar",
            NewValue = "Gibt es dazu schon etwas Neues?",
            ChangedBy = Mail,
            ChangedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await stammdaten.ErfasseAsync(Name, "0221333444", "A-9", mitarbeiter);
    }

    // Für ein Telefon-Ticket lieferte die dokumentierte Abfrage null Treffer,
    // weil sie nur den Namen prüfte und dort ein Personenname steht statt
    // einer Adresse.
    [Fact]
    public async Task Die_dokumentierte_Auskunft_findet_jede_Spur_der_Person()
    {
        await SpurenAnlegenAsync();

        foreach (var abfrage in AnweisungenAus("3. Auskunft"))
        {
            var treffer = Zaehle(abfrage);
            Assert.True(treffer > 0,
                $"Diese Abfrage aus der Checkliste findet nichts:\n{abfrage}");
        }
    }

    [Fact]
    public async Task Nach_der_dokumentierten_Loeschung_bleibt_keine_Spur()
    {
        await SpurenAnlegenAsync();

        // Eine vollständige Anfrage braucht beide Abschnitte: Die Stammdaten
        // stehen getrennt, weil sie getrennt gelöscht werden.
        foreach (var anweisung in AnweisungenAus("2a. Stammdaten", "4. Löschung"))
        {
            Ausfuehren(anweisung);
        }

        foreach (var (tabelle, spalten) in new[]
                 {
                     ("Tickets", new[] { "CustomerName", "CustomerEmail", "CreatedByName", "CallbackNumber", "CallNote", "FollowUpNote" }),
                     ("TicketComments", new[] { "AuthorName", "AuthorId" }),
                     ("TicketHistory", new[] { "ChangedBy" }),
                     ("Kontakte", new[] { "Name", "NameNormalisiert" }),
                     ("AspNetUsers", new[] { "UserName", "Email", "NormalizedEmail" })
                 })
        {
            foreach (var spalte in spalten)
            {
                var rest = Zaehle(
                    $"SELECT 1 FROM {tabelle} WHERE {spalte} LIKE '%Weber%' " +
                    $"OR {spalte} LIKE '%weber%' OR {spalte} LIKE '%0221333444%'");
                Assert.True(rest == 0, $"Nach der Löschung steht die Person noch in {tabelle}.{spalte}.");
            }
        }

        // Die Vorgänge bleiben erhalten, nur ohne Personenbezug: Anonymisierung
        // statt Löschung.
        Assert.Equal(2, Zaehle("SELECT 1 FROM Tickets"));
    }

    // Die Abschnitte für Mitarbeitende nennen Tabellen und Spalten, die die
    // Beispielperson als Kundin nie füllt. Ausgeführt werden sie trotzdem:
    // Eine umbenannte Spalte würde die Anleitung sonst still ungültig machen.
    [Fact]
    public async Task Die_Abschnitte_fuer_Mitarbeitende_laufen_gegen_das_Schema()
    {
        await SpurenAnlegenAsync();

        foreach (var abfrage in AnweisungenAus("3a. Auskunft"))
        {
            Assert.Equal(0, Zaehle(abfrage));
        }

        foreach (var anweisung in AnweisungenAus("4a. Löschung"))
        {
            Ausfuehren(anweisung);
        }

        Assert.Equal(2, Zaehle("SELECT 1 FROM Tickets"));
    }

    // Der Ablageort steht in einer vierstufigen Kette. Wer die erstbeste app.db
    // öffnet, kann an einer Kopie arbeiten und glauben, gelöscht zu haben.
    [Fact]
    public void Die_Anleitung_nennt_keinen_festen_Dateinamen_mehr()
    {
        var text = File.ReadAllText(ChecklistePfad());
        var auskunft = text[text.IndexOf("## 3. Auskunft", StringComparison.Ordinal)..];
        auskunft = auskunft[..auskunft.IndexOf("\n## ", StringComparison.Ordinal)];

        Assert.DoesNotContain("app.db", auskunft);
        Assert.Contains("Verwaltung", auskunft);
    }

    [Fact]
    public void Die_Datenuebersicht_kennt_die_Protokolldatei()
    {
        Assert.Contains("Protokoll", File.ReadAllText(ChecklistePfad()));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
