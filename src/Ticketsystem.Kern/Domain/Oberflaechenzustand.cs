namespace Ticketsystem.Kern.Domain;

// Als Enum und nicht als Spaltenname: Ein String aus der Oberfläche in
// einer Sortierklausel wäre eine offene Tür.
public enum Sortierung
{
    Nummer,
    Frist,
    Prioritaet,
    Aenderung
}

// Was ein Konto beim nächsten Start wieder vorfinden soll: eine Zeile je
// Konto, keine Fachdaten, die Tabelle darf jederzeit leer sein. Der Filter
// „Nur meine“ steht absichtlich nicht hier: Er versteckt Vorgänge, und am
// Morgen denkt niemand daran, dass er noch steht.
public sealed class Oberflaechenzustand
{
    public string KontoId { get; set; } = string.Empty;

    public string Ansicht { get; set; } = "Offen";

    // Index der Auswahl, 0 heißt „alle“.
    public int Prioritaet { get; set; }

    public Sortierung Sortierung { get; set; } = Sortierung.Nummer;

    public bool Absteigend { get; set; }

    public int VerwaltungsReiter { get; set; }

    // 0, solange nie gespeichert; die Oberfläche prüft, ob die Maße auf den
    // Schirm passen.
    public double FensterBreite { get; set; }

    public double FensterHoehe { get; set; }

    public bool Hinweise { get; set; } = true;

    // „dunkel“ oder „hell“ als Text: Der Kern kennt keine Farben, nur die Wahl.
    public string Farbschema { get; set; } = "dunkel";

    // In Prozent; welche Stufen es gibt, weiß die Oberfläche.
    public int Darstellung { get; set; } = 100;
}
