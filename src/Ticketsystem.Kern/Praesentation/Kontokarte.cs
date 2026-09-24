using Microsoft.AspNetCore.Identity;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

// ZahlIstVollstaendig sagt, ob OffeneVorgaenge alle Vorgänge der Person
// zählt: Ein Bearbeiter sieht nur die, die er selbst sehen darf.
public sealed record Kontokarte(
    string Anzeigename,
    string VollerName,
    string Email,
    string Rolle,
    string? PausiertBis,
    int OffeneVorgaenge,
    bool ZahlIstVollstaendig,
    string KontoId);

public sealed class KontokartePresenter(UserManager<AppUser> users, TicketService tickets)
{
    public async Task<Kontokarte?> LadenAsync(string kontoId, Akteur akteur, DateTime jetzt)
    {
        var konto = await users.FindByIdAsync(kontoId);
        if (konto is null)
        {
            return null;
        }

        var rollen = await users.GetRolesAsync(konto);
        // Die höchste Rolle zählt; Rollen sind kumulativ.
        var rolle = rollen.Contains(Rollen.Admin) ? RoleLevel.Administration.Anzeige()
            : rollen.Contains(Rollen.TeamLead) ? RoleLevel.Teamleitung.Anzeige()
            : rollen.Contains(Rollen.Editor) ? RoleLevel.Bearbeiter.Anzeige()
            : "Ohne Rolle";

        var offene = await tickets.ListAsync(akteur, nurOffene: true, bearbeiterId: konto.Id);

        var vollstaendig = akteur.IstMindestens(RoleLevel.Teamleitung) || akteur.Id == konto.Id;

        return new Kontokarte(
            Kontenname.Anzeige(konto),
            Kontenname.Voll(konto),
            konto.Email ?? konto.UserName ?? "",
            rolle,
            konto.IstPausiert(jetzt) ? konto.PausedUntil.Anzeige() : null,
            offene.Gesamt,
            vollstaendig,
            konto.Id);
    }
}
