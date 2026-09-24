using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Tests;

// Der Presenter übersetzt die gewählte Ansicht in Dienstparameter. Ein
// Fehler hier zeigte in der Liste falsche Vorgänge, und kein Fenster-Test
// sähe ihn, denn die Fenster binden nur.
public sealed class TicketlistenPresenterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TicketsystemContext _db;
    private readonly TicketService _service;
    private readonly TicketlistenPresenter _presenter;

    public TicketlistenPresenterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new TicketsystemContext(new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new TicketService(_db);
        _presenter = new TicketlistenPresenter(_service, new Namensverzeichnis(_db));
        TestDaten.KontoAnlegen(_db, TestDaten.Bearbeiter1);
    }

    private Task<Ticket> TicketAsync(string titel, TicketPriority prio = TicketPriority.Medium) =>
        _service.CreatePhoneAsync(titel, "Beschreibung.", prio, "Weber, Sabine",
            "0221123456", DateTime.UtcNow, null,
            createdById: TestDaten.Bearbeiter1.Id, createdByName: TestDaten.Bearbeiter1.Name,
            address: "A-1");

    [Fact]
    public async Task Die_Zeile_traegt_dieselbe_Semantik_wie_die_Web_Liste()
    {
        await TicketAsync("Server piept", TicketPriority.Critical);

        var ergebnis = await _presenter.LadenAsync(
            TestDaten.Teamleitung, "Offen", null, nurMeine: false, suche: null, DateTime.UtcNow);

        var zeile = Assert.Single(ergebnis.Zeilen);
        Assert.Equal("Server piept", zeile.Titel);
        Assert.Equal("Kritisch", zeile.Prioritaet);
        Assert.Equal("prio-kritisch", zeile.PrioKlasse);
        // Die Zeile nennt die Restzeit statt des Zustands: „noch 59 min" löst eine
        // Handlung aus, „Läuft" verlangt erst eine Rechnung.
        Assert.StartsWith("noch ", zeile.Sla);
        Assert.Equal("○", zeile.SlaZeichen);
        Assert.Equal("badge-frist-neutral", zeile.SlaKlasse);
    }

    [Fact]
    public async Task Die_Ansicht_Offen_zeigt_Geschlossenes_nicht()
    {
        await TicketAsync("Offen bleibt");
        var zu = await TicketAsync("Geschlossen faellt");
        await _service.ChangeStatusAsync(zu.Id, TicketStatus.Closed, TestDaten.Teamleitung);

        var offen = await _presenter.LadenAsync(TestDaten.Teamleitung, "Offen", null, false, null, DateTime.UtcNow);
        var alle = await _presenter.LadenAsync(TestDaten.Teamleitung, "Alle", null, false, null, DateTime.UtcNow);

        Assert.Equal(["Offen bleibt"], offen.Zeilen.Select(z => z.Titel));
        Assert.Equal(2, alle.Zeilen.Count);
    }

    [Fact]
    public async Task Die_Ansicht_eines_echten_Status_filtert_genau_diesen()
    {
        var zu = await TicketAsync("Geschlossen");
        await _service.ChangeStatusAsync(zu.Id, TicketStatus.Closed, TestDaten.Teamleitung);
        await TicketAsync("Neu bleibt");

        var ergebnis = await _presenter.LadenAsync(
            TestDaten.Teamleitung, TicketStatus.Closed.Anzeige(), null, false, null, DateTime.UtcNow);

        Assert.Equal(["Geschlossen"], ergebnis.Zeilen.Select(z => z.Titel));
    }

    [Fact]
    public async Task Wiedervorlage_Ansicht_zeigt_nur_Faelliges()
    {
        var faellig = await TicketAsync("Faellig");
        await _service.WiedervorlageSetzenAsync(faellig.Id, DateTime.UtcNow.AddMinutes(-5), "jetzt", TestDaten.Bearbeiter1);
        var spaeter = await TicketAsync("Spaeter");
        await _service.WiedervorlageSetzenAsync(spaeter.Id, DateTime.UtcNow.AddDays(2), "Freitag", TestDaten.Bearbeiter1);

        var ergebnis = await _presenter.LadenAsync(
            TestDaten.Teamleitung, "Wiedervorlage fällig", null, false, null, DateTime.UtcNow);

        Assert.Equal(["Faellig"], ergebnis.Zeilen.Select(z => z.Titel));
    }

    // Dieselbe Schwelle wie im Dienst: Ein Bearbeiter, der die Ansicht wählen
    // könnte, bekäme nur dessen Ausnahme zu sehen.
    [Fact]
    public void Die_Pausierten_Ansicht_gibt_es_nur_ab_Teamleitung()
    {
        Assert.Contains("Pausierte Zuweisungen", TicketlistenPresenter.AnsichtenFuer(TestDaten.Teamleitung));
        Assert.DoesNotContain("Pausierte Zuweisungen", TicketlistenPresenter.AnsichtenFuer(TestDaten.Bearbeiter1));
        Assert.Equal("Offen", TicketlistenPresenter.AnsichtenFuer(TestDaten.Bearbeiter1)[0]);
    }

    // Drei Wahrheiten: Ein leerer Bestand lädt zum ersten Ticket ein, eine
    // leere Ansicht benennt die Ansicht, und ein leeres Filterergebnis verweist
    // auf die Filter, denn ein neues Ticket erschiene dort gar nicht.
    [Fact]
    public async Task Der_Leerzustand_unterscheidet_Bestand_Ansicht_und_Filter()
    {
        var leererBestand = await _presenter.LadenAsync(
            TestDaten.Teamleitung, "Alle", null, nurMeine: false, suche: null, DateTime.UtcNow);
        Assert.Contains("noch keine Vorgänge", leererBestand.Leertext);

        var leereAnsicht = await _presenter.LadenAsync(
            TestDaten.Teamleitung, "Offen", null, nurMeine: false, suche: null, DateTime.UtcNow);
        Assert.Equal("Keine offenen Vorgänge.", leereAnsicht.Leertext);

        await TicketAsync("Vorhanden, aber Suche trifft nicht");
        var gefiltert = await _presenter.LadenAsync(
            TestDaten.Teamleitung, "Alle", null, nurMeine: false, suche: "xyzzy", DateTime.UtcNow);
        Assert.Empty(gefiltert.Zeilen);
        Assert.Contains("Filter", gefiltert.Leertext);
        Assert.DoesNotContain("noch keine", gefiltert.Leertext);
    }

    [Fact]
    public async Task Der_Kuerzungshinweis_nennt_die_Gesamtzahl()
    {
        for (var i = 0; i < 201; i++)
        {
            await TicketAsync($"Vorgang {i}");
        }

        var ergebnis = await _presenter.LadenAsync(TestDaten.Teamleitung, "Alle", null, false, null, DateTime.UtcNow);

        Assert.Equal(200, ergebnis.Zeilen.Count);
        Assert.NotNull(ergebnis.Kuerzungshinweis);
        Assert.Contains("201", ergebnis.Kuerzungshinweis);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
