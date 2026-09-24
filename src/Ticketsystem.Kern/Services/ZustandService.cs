using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Services;

public sealed record Oberflaechenstand(
    string Ansicht,
    int Prioritaet,
    Sortierung Sortierung,
    bool Absteigend,
    int VerwaltungsReiter,
    double FensterBreite,
    double FensterHoehe,
    bool Hinweise,
    string Farbschema,
    int Darstellung)
{
    // Die Sortierung hängt an der Rolle: Teamleitung verteilt und will das
    // Kritische oben, Bearbeiter arbeiten ab und wollen das Neueste.
    // Fenstermaße 0 heißen Entwurfsgröße.
    public static Oberflaechenstand VorgabeFuer(Akteur akteur) => new(
        "Offen", 0,
        akteur.IstMindestens(RoleLevel.Teamleitung) ? Sortierung.Prioritaet : Sortierung.Nummer,
        false, 0, 0, 0, Hinweise: true, Farbschema: "dunkel", Darstellung: 100);
}

// Liest und schreibt den Oberflächenzustand je Konto, ohne Rechteprüfung:
// Ein Konto erreicht nur seinen eigenen, der Schlüssel ist die Kennung.
public sealed class ZustandService(TicketsystemContext db)
{
    public async Task<Oberflaechenstand> LadenAsync(Akteur akteur)
    {
        var zeile = await db.Oberflaechenzustaende
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.KontoId == akteur.Id);

        return zeile is null
            ? Oberflaechenstand.VorgabeFuer(akteur)
            : new Oberflaechenstand(zeile.Ansicht, zeile.Prioritaet, zeile.Sortierung,
                zeile.Absteigend, zeile.VerwaltungsReiter,
                zeile.FensterBreite, zeile.FensterHoehe, zeile.Hinweise, zeile.Farbschema, zeile.Darstellung);
    }

    public async Task SpeichernAsync(Akteur akteur, Oberflaechenstand stand)
    {
        var zeile = await db.Oberflaechenzustaende.FirstOrDefaultAsync(z => z.KontoId == akteur.Id);
        if (zeile is null)
        {
            zeile = new Oberflaechenzustand { KontoId = akteur.Id };
            db.Oberflaechenzustaende.Add(zeile);
        }

        zeile.Ansicht = stand.Ansicht;
        zeile.Prioritaet = stand.Prioritaet;
        zeile.Sortierung = stand.Sortierung;
        zeile.Absteigend = stand.Absteigend;
        zeile.VerwaltungsReiter = stand.VerwaltungsReiter;
        zeile.FensterBreite = stand.FensterBreite;
        zeile.FensterHoehe = stand.FensterHoehe;
        zeile.Hinweise = stand.Hinweise;
        zeile.Farbschema = stand.Farbschema;
        zeile.Darstellung = stand.Darstellung;

        await db.SaveChangesAsync();
    }
}
