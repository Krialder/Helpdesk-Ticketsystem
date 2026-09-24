using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Eine Adresse ist Großbuchstabe, Bindestrich und ein bis vier Ziffern, oder
// das Wort „extern". Groß- und Kleinschreibung und Randleerzeichen räumt die
// Regel vor der Prüfung auf, denn wer im Gespräch mittippt, trifft die
// Schreibweise nicht; gespeichert wird nur die strenge Form, damit die
// Raum-Historie eindeutig bleibt.
public sealed class AdressRegelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;

    public AdressRegelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TicketsystemContext(options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
    }

    [Theory]
    [InlineData("A-101")]
    [InlineData("C-7")]
    [InlineData("B-1204")]
    [InlineData("extern")]
    [InlineData("a-101")]
    [InlineData("Extern")]
    public void Gueltige_Adressen_bestehen_die_Regel(string adresse) =>
        Assert.True(AdressRegel.IstGueltig(adresse));

    [Theory]
    [InlineData("AB-12")]
    [InlineData("A101")]
    [InlineData("A-12345")]
    [InlineData("Raum 5")]
    [InlineData("A-101 B-102")]
    public void Ungueltige_Adressen_fallen_durch(string adresse) =>
        Assert.False(AdressRegel.IstGueltig(adresse));

    [Fact]
    public async Task Ticket_mit_gueltiger_Adresse_wird_gespeichert()
    {
        var ticket = await _service.CreateAsync(
            "Titel", "Text", TicketPriority.Medium, "A. Beispiel", TicketSource.Web, address: "A-101");

        Assert.Equal("A-101", (await _db.Tickets.SingleAsync(t => t.Id == ticket.Id)).Address);
    }

    [Fact]
    public async Task Ticket_mit_ungueltiger_Adresse_wird_abgewiesen()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync("Titel", "Text", TicketPriority.Medium, "A. Beispiel",
                TicketSource.Web, address: "Raum 5"));

        Assert.Empty(await _db.Tickets.ToListAsync());
    }

    // Ein leeres Feld kommt als null an, und die Regex warf damit eine
    // ArgumentNullException, bevor die Meldung „bitte Adresse angeben" erschien.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Eine_fehlende_Adresse_ist_ungueltig_statt_ein_Absturz(string? adresse)
    {
        Assert.False(AdressRegel.IstGueltig(adresse));
    }

    [Theory]
    [InlineData(" A-101 ", "A-101")]
    [InlineData("a-101", "A-101")]
    [InlineData(" Extern ", "extern")]
    [InlineData("EXTERN", "extern")]
    public void Randleerzeichen_und_Grossschreibung_kosten_kein_Ticket(string eingabe, string erwartet)
    {
        Assert.True(AdressRegel.IstGueltig(eingabe));
        Assert.Equal(erwartet, AdressRegel.Normalisieren(eingabe));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
