using System.Text;
using System.Text.RegularExpressions;

namespace Ticketsystem.Kern.Praesentation;

// Die Lesefläche der Wissensartikel: eine kleine Teilmenge von Markdown
// (Überschrift, Fett, Festbreite, Listen, Befehlsblock), gelesen zu Blöcken
// und Läufen. Gespeichert bleibt der reine Text; deshalb ändern sich
// Versionen, Suche und Sicherung nicht. Keine Markdown-Bibliothek: Die
// zöge acht Pakete nach und renderte auch, was hier niemand meint.
public sealed record Lauf(string Text, bool Fett = false, bool Festbreite = false);

public abstract record Wissensblock;

public sealed record Absatz(IReadOnlyList<IReadOnlyList<Lauf>> Zeilen) : Wissensblock;

public sealed record Ueberschrift(string Text) : Wissensblock;

public sealed record Aufzaehlung(bool Nummeriert, int Beginn, IReadOnlyList<IReadOnlyList<Lauf>> Eintraege) : Wissensblock;

public sealed record Befehlsblock(string Text) : Wissensblock;

public static partial class Wissenstext
{
    [GeneratedRegex(@"^#{1,6}\s+(\S.*)$")]
    private static partial Regex UeberschriftMuster();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(\S.*)$")]
    private static partial Regex SchrittMuster();

    [GeneratedRegex(@"^[-*]\s+(\S.*)$")]
    private static partial Regex PunktMuster();

    private const string Zaun = "```";

    // Zeilenweise: Eine Leerzeile trennt Absätze, ein Wechsel der Listenart
    // beendet die Liste, ein Zaun aus drei Rückwärtsapostrophen rahmt einen
    // Befehlsblock wortgetreu.
    public static IReadOnlyList<Wissensblock> Lesen(string? text)
    {
        var bloecke = new List<Wissensblock>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return bloecke;
        }

        var zeilen = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var absatz = new List<IReadOnlyList<Lauf>>();
        var eintraege = new List<IReadOnlyList<Lauf>>();
        bool? nummeriert = null;
        var beginn = 1;

        void AbsatzSchliessen()
        {
            if (absatz.Count > 0)
            {
                bloecke.Add(new Absatz(absatz.ToList()));
                absatz.Clear();
            }
        }

        void ListeSchliessen()
        {
            if (nummeriert is bool art)
            {
                bloecke.Add(new Aufzaehlung(art, beginn, eintraege.ToList()));
                eintraege.Clear();
                nummeriert = null;
            }
        }

        for (var i = 0; i < zeilen.Length; i++)
        {
            var zeile = zeilen[i].Trim();

            if (zeile == Zaun)
            {
                AbsatzSchliessen();
                ListeSchliessen();
                var befehl = new List<string>();
                // Ein Zaun ohne Ende schluckt den Rest des Textes als Befehl; besser als
                // ein halber Befehl, der als Absatz ausgezeichnet würde.
                for (i++; i < zeilen.Length && zeilen[i].Trim() != Zaun; i++)
                {
                    befehl.Add(zeilen[i]);
                }

                bloecke.Add(new Befehlsblock(string.Join("\n", befehl)));
                continue;
            }

            if (zeile.Length == 0)
            {
                AbsatzSchliessen();
                ListeSchliessen();
                continue;
            }

            if (UeberschriftMuster().Match(zeile) is { Success: true } kopf)
            {
                AbsatzSchliessen();
                ListeSchliessen();
                bloecke.Add(new Ueberschrift(kopf.Groups[1].Value.TrimEnd()));
                continue;
            }

            if (SchrittMuster().Match(zeile) is { Success: true } schritt)
            {
                AbsatzSchliessen();
                if (nummeriert != true)
                {
                    ListeSchliessen();
                    nummeriert = true;
                    beginn = int.Parse(schritt.Groups[1].Value);
                }

                eintraege.Add(Laeufe(schritt.Groups[2].Value.TrimEnd()));
                continue;
            }

            if (PunktMuster().Match(zeile) is { Success: true } punkt)
            {
                AbsatzSchliessen();
                if (nummeriert != false)
                {
                    ListeSchliessen();
                    nummeriert = false;
                    beginn = 1;
                }

                eintraege.Add(Laeufe(punkt.Groups[1].Value.TrimEnd()));
                continue;
            }

            ListeSchliessen();
            absatz.Add(Laeufe(zeile));
        }

        AbsatzSchliessen();
        ListeSchliessen();
        return bloecke;
    }

    // Ein einzelner Stern oder Unterstrich bleibt Text, weil „*.docx“ und
    // „datei_name“ in Helpdesk-Texten vorkommen; nur ein Paar aus zwei Sternen
    // macht fett, nur ein Paar aus Rückwärtsapostrophen macht Festbreite.
    public static IReadOnlyList<Lauf> Laeufe(string zeile)
    {
        var laeufe = new List<Lauf>();
        var puffer = new StringBuilder();

        void Leeren()
        {
            if (puffer.Length > 0)
            {
                laeufe.Add(new Lauf(puffer.ToString()));
                puffer.Clear();
            }
        }

        var i = 0;
        while (i < zeile.Length)
        {
            if (zeile[i] == '`')
            {
                var ende = zeile.IndexOf('`', i + 1);
                if (ende > i + 1)
                {
                    Leeren();
                    laeufe.Add(new Lauf(zeile[(i + 1)..ende], Festbreite: true));
                    i = ende + 1;
                    continue;
                }
            }
            else if (i + 1 < zeile.Length && zeile[i] == '*' && zeile[i + 1] == '*')
            {
                var ende = zeile.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (ende > i + 2)
                {
                    Leeren();
                    laeufe.Add(new Lauf(zeile[(i + 2)..ende], Fett: true));
                    i = ende + 2;
                    continue;
                }
            }

            puffer.Append(zeile[i]);
            i++;
        }

        Leeren();
        return laeufe;
    }
}
