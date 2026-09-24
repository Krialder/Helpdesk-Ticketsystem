using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Praesentation;

public sealed record FassungsZeile(
    int Nummer,
    string Zeit,
    string Anlass,
    string Wer,
    string? WerKontoId,
    string Titel,
    string Inhalt,
    string Kategorie,
    string Tags,
    bool Veroeffentlicht,
    bool IstAktuell)
{
    public string Kopf => $"Version {Nummer} · {Zeit} · {Anlass} · {Wer}";

    public string Angaben =>
        $"{Kategorie}, Tags: {(Tags.Length == 0 ? "-" : Tags)}, {(Veroeffentlicht ? "veröffentlicht" : "Entwurf")}";
}

// Die Versionen eines Artikels für den Dialog, neueste zuerst; die erste
// Zeile ist der aktuelle Stand, auf den es keinen Rückweg gibt.
public sealed class FassungenPresenter(KnowledgeBaseService kb, Namensverzeichnis namen)
{
    public async Task<IReadOnlyList<FassungsZeile>> LadenAsync(int articleId, Akteur akteur)
    {
        var fassungen = await kb.FassungenAsync(articleId, akteur);
        var buch = await namen.LadenAsync();

        return fassungen
            .Select((f, i) => new FassungsZeile(
                f.Nummer,
                f.GespeichertAm.Anzeige(),
                f.Anlass,
                buch.Anzeige(f.GespeichertVonId, f.GespeichertVon),
                buch.KontoId(f.GespeichertVonId, f.GespeichertVon),
                f.Title,
                f.Content,
                f.Kategorie ?? "Ohne Kategorie",
                f.Tags,
                f.Published,
                IstAktuell: i == 0))
            .ToList();
    }
}
