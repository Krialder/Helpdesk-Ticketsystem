# Anleitungen für Bearbeiter

Diese Anleitungen richten sich an Mitarbeiterinnen und Mitarbeiter des Helpdesks mit der Rolle Bearbeiter. Sie finden hier je Aufgabe die Schritte, die Sie im Alltag brauchen. Wer das System noch nicht kennt, beginnt mit dem [Einstieg](einstieg.md); was Zeichen, Ansichten und Tasten bedeuten, steht in der [Referenz](referenz.md).

Alles, was sich anklicken oder ausfüllen lässt, erklärt sich auch im Programm: Halten Sie den Mauszeiger darauf, und ein Hinweis sagt, was passiert. Wer die Hinweise kennt, schaltet sie unter **Einstellungen** ab.

## Vorgänge finden

### Die richtige Ansicht wählen

1. Öffnen Sie im Hauptfenster oben die Auswahl rechts neben dem Suchfeld, die zu Beginn **Offen** zeigt. Das Handbuch nennt sie die Ansicht; im Programm trägt sie keine Beschriftung.
2. Wählen Sie einen Eintrag. Die Liste zeigt sofort nur noch die passenden Vorgänge:

| Ansicht | Zeigt |
|---|---|
| **Offen** | Vorgänge mit Status Neu, Zugewiesen oder In Arbeit. Gelöste stehen nicht darin. |
| **Alle** | jeden Vorgang, den Sie sehen dürfen, auch geschlossene |
| **Überfällig** | offene Vorgänge, deren Frist abgelaufen ist |
| **Unzugewiesen** | Vorgänge ohne Bearbeiter, auch gelöste und geschlossene; die offenen darunter kann jeder übernehmen |
| **Wiedervorlage fällig** | Vorgänge, deren Wiedervorlagetermin erreicht ist |
| **Neu**, **Zugewiesen**, **In Arbeit**, **Gelöst**, **Geschlossen** | Vorgänge mit genau diesem Status |

Als Bearbeiter sehen Sie in jeder Ansicht nur Vorgänge, die Ihnen zugewiesen sind, die Sie angelegt haben oder die niemandem zugewiesen sind. Teamleitung und Administration sehen alle.

### Nach Priorität filtern und nur eigene zeigen

1. Wählen Sie in der Auswahl **Alle Prioritäten** eine Stufe. Die Liste zeigt nur Vorgänge dieser Priorität.
2. Klicken Sie auf **Nur meine**, um nur die Vorgänge zu sehen, die Ihnen zugewiesen sind. Der Knopf bleibt gedrückt, bis Sie erneut klicken; bei jeder Anmeldung ist er aus.

Unten links steht, wie viele Vorgänge die Liste zeigt und wie viele es gibt, zum Beispiel „12 von 47 Vorgängen". Weichen die Zahlen ab, greift ein Filter.

### Suchen

1. Klicken Sie in das Feld **Suchen (Strg+F)** oder drücken Sie Strg+F.
2. Tippen Sie eine Vorgangsnummer, ein Wort aus Titel oder Beschreibung oder einen Kundennamen und drücken Sie Enter. Die Liste zeigt die Treffer.
3. Drücken Sie Pfeil ab, um in die Trefferliste zu springen, und Enter, um den markierten Vorgang zu öffnen.

Die Suche greift nicht in Kommentare.

### Sortieren

1. Klicken Sie auf einen der Spaltenköpfe **Nr.**, **Priorität**, **Geändert** oder **Frist**. Der Pfeil im Spaltenkopf zeigt die Sortierung.
2. Klicken Sie ein zweites Mal, um die Richtung umzudrehen.

Das Programm merkt sich Ansicht, Priorität, Sortierung und Fenstergröße für Ihr Konto, auch über die Abmeldung hinaus; nur **Nur meine** ist bei jeder Anmeldung aus. In der Verwaltung merkt es sich den zuletzt offenen Reiter.

## Einen Anruf erfassen

1. Klicken Sie auf **Neues Ticket** oder drücken Sie Strg+N. Das Erfassungsfenster öffnet sich mit der Quelle **Telefon**.
2. Füllen Sie die Felder in der Reihenfolge aus, in der ein Anrufer sie nennt:

| Feld | Eingabe |
|---|---|
| **Name des Anrufers oder Absenders** | „Nachname, Vorname". Ab drei Zeichen schlägt das Programm einen bekannten Anrufer vor, sobald nur noch einer passt oder der Name vollständig stimmt. Die Zeile „Bekannt:" zeigt dann Rufnummer und Raum; ein Klick übernimmt den Wert. |
| **Rückrufnummer** | Durchwahl, Festnetznummer mit Vorwahl oder Mobilnummer; die genaue Regel steht in den [Eingaberegeln](referenz.md#eingaberegeln). |
| **Titel** | ein kurzer Satz für die Liste |
| **Beschreibung** | was gemeldet wurde, mit allem, was die nächste Person braucht |
| **Adresse** | Raum als `A-101` oder `extern`, siehe [Eingaberegeln](referenz.md#eingaberegeln); Kleinschreibung und Leerzeichen am Rand werden korrigiert |
| **Priorität** | Niedrig, Mittel, Hoch oder Kritisch; Vorgabe Mittel. Sie bestimmt die Fristen. |
| **Anrufzeit** | vorbelegt mit jetzt; klicken Sie auf **ändern**, wenn der Anruf früher war |
| **Gesprächsnotiz (freiwillig)** | was sonst gesagt wurde |
| **Verweis auf Ticketnummer (freiwillig)** | die Nummer eines früheren Vorgangs, auf den sich dieser bezieht, als `123` oder `#123`. Der Verweis wird gespeichert und exportiert, im Vorgang selbst aber bisher nicht angezeigt; die früheren Vorgänge einer Person finden Sie über den Klick auf den Kundennamen. |
| **Mir zuweisen** | Häkchen, wenn Sie den Vorgang selbst bearbeiten |

3. Klicken Sie auf **Ticket anlegen (Strg+Enter)**. Das Fenster schließt sich, die Statusleiste meldet die Nummer, und der Vorgang ist in der Liste markiert.

Fehlt etwas oder stimmt ein Format nicht, steht die Begründung unten im Fenster, und der Cursor springt in das erste betroffene Feld.

Schließen Sie das Fenster mit Escape oder **Abbrechen**, merkt sich das Programm Ihre Eingaben. Beim nächsten Öffnen fragt es mit **Weiter bearbeiten** und **Neu beginnen**, ob Sie den Entwurf fortsetzen wollen.

Bei jedem Anruf merkt sich das Programm Name, Rufnummer und Raum als Stammdaten, damit der nächste Anruf derselben Person schneller geht.

## Eine E-Mail erfassen

Das Support-Postfach wird von Hand gelesen. Aus einer E-Mail machen Sie so einen Vorgang:

1. Klicken Sie auf **Neues Ticket** und wählen Sie oben die Quelle **E-Mail**. Die Felder ändern sich.
2. Tragen Sie unter **Name des Anrufers oder Absenders** den Namen der Person und unter **Absenderadresse** ihre E-Mail-Adresse ein. Dorthin geht die Eingangsbestätigung, falls der Mailversand eingerichtet ist.
3. Tragen Sie unter **Eingegangen am (Zeitpunkt aus der Mail)** Datum und Uhrzeit aus der E-Mail ein; vorbelegt ist jetzt.
4. Übernehmen Sie Betreff und Text der E-Mail als **Titel** und **Beschreibung**, tragen Sie **Adresse** und **Priorität** ein und klicken Sie auf **Ticket anlegen (Strg+Enter)**.

Die Fristen laufen ab der Erfassung, nicht ab dem Eingang der E-Mail; der Eingangszeitpunkt steht als Angabe im Vorgang.

## Einen Vorgang übernehmen

1. Öffnen Sie einen Vorgang ohne Bearbeiter mit Doppelklick.
2. Klicken Sie in der Karte **Bearbeiter** auf **Mir zuweisen**. Ihr Name erscheint als Bearbeiter, der Status wechselt von **Neu** auf **Zugewiesen**, und die Reaktionsfrist gilt als erfüllt.

Wollen Sie einen eigenen Vorgang wieder abgeben, klicken Sie in derselben Karte auf **Zuweisung aufheben**. Der Vorgang ist dann wieder für alle sichtbar; was das mit dem Status macht, steht in der [Referenz](referenz.md#status).

## Den Status ändern

Die Karte **Status** zeigt den aktuellen Status und je einen Knopf für jeden erlaubten nächsten Status. Bei Neu, Zugewiesen und In Arbeit ist der Knopf hervorgehoben, der den Vorgang weiterbringt.

| Von | Knopf | Was passiert |
|---|---|---|
| Neu | **Zugewiesen** | Status ohne Person; üblicher ist **Mir zuweisen** in der Karte Bearbeiter |
| Zugewiesen | **In Arbeit** | die Bearbeitung beginnt |
| In Arbeit | **Gelöst** | tragen Sie vorher unter **Lösung (wird beim Setzen auf Gelöst als Kommentar gespeichert)** ein, was geholfen hat; im Verlauf steht der Text dann als „Lösung: …", und die Lösungsfrist gilt als erfüllt |
| Gelöst | **In Arbeit** | Wiedereröffnung, wenn das Problem weiter besteht; die Lösungszeit wird gelöscht, die Frist bleibt |
| jeder | **Vorgang schließen** | endgültig; der Knopf fragt nach, bestätigen Sie mit **Endgültig schließen** |

Geschlossen ist endgültig: Der Vorgang nimmt danach keine Kommentare, Nachträge oder Statuswechsel mehr an. Meldet sich der Kunde später wieder, legen Sie einen neuen Vorgang mit Verweis auf den alten an. Löschen lässt sich ein Vorgang nie. Einen versehentlich angelegten schließen Sie ohne Lösung, und zwar sofort: Ist die Reaktionsfrist schon abgelaufen, zählt auch ein so geschlossener Vorgang als Fristriss. Er bleibt in der Ansicht **Alle** und in der Auswertung sichtbar.

## Einen Kommentar schreiben

1. Tragen Sie im Detailfenster unten links in das Feld **Kommentar (Strg+Enter sendet)** ein, was das Team wissen muss: was Sie versucht haben, was der Kunde gesagt hat, was als Nächstes ansteht.
2. Drücken Sie Strg+Enter oder klicken Sie auf **Senden**. Der Kommentar erscheint im Verlauf mit Ihrem Namen und der Uhrzeit.

Kommentare lassen sich nicht ändern oder löschen; sie sind Teil der Akte. Berichtigen Sie einen Irrtum mit einem neuen Kommentar.

## Eine Wiedervorlage setzen

Wenn ein Vorgang warten muss, etwa auf ein bestelltes Ersatzteil:

1. Wählen Sie in der Karte **Wiedervorlage** Tag und Uhrzeit. Vorbelegt ist der nächste Tag um 9:00 Uhr.
2. Tragen Sie in das Feld darunter den Grund ein, zum Beispiel `Netzteil da? Einbau`.
3. Klicken Sie auf **Merken**. Der Vorgang bleibt in seinem Status; am Termin erscheint er in der Ansicht **Wiedervorlage fällig**, in der Statusleiste und im Lagebild. Ein gelöster Vorgang erscheint am Termin nur noch in der Ansicht, nicht mehr in Statusleiste und Lagebild. Besteht schon ein Termin, heißt der Knopf **Termin ändern**.

Die Wiedervorlage hält die Fristen nicht an. Nach dem Nachfassen klicken Sie auf **Erledigt, entfernen**, sonst meldet sich der Vorgang jeden Tag wieder.

## Die Priorität ändern

Wählen Sie in der Karte **Priorität** die neue Stufe und klicken Sie auf **Ändern**. Die Karte zeigt die neue Stufe, und beide Fristen werden neu berechnet; wie, steht unter [Prioritäten und Fristen](referenz.md#prioritäten-und-fristen).

## Angaben nachtragen

Was am Telefon nicht sauber zu bekommen war, tragen Sie in der Karte **Anrufer** nach: **Name**, **Adresse**, **Rückrufnummer**.

1. Ändern Sie in den Feldern **Name**, **Adresse** und **Rückrufnummer** nur, was falsch ist; sie zeigen den aktuellen Stand.
2. Klicken Sie auf **Nachtragen**. Jede Änderung steht mit altem und neuem Wert in der Historie.

Ein geleertes Feld **Adresse** oder **Rückrufnummer** entfernt den Wert; der **Name** darf nicht leer werden.

## Frühere Vorgänge einer Person oder eines Raums sehen

- Klicken Sie in der Liste auf den Kundennamen oder im Detailfenster in der Karte **Anrufer** auf **Vorgänge** neben dem Namen. Ein Fenster zeigt alle Vorgänge dieser Person, auch geschlossene. Die Auswahl ist exakt: „Weber, Sabine" bringt nicht „Weber, Sabine (Vertretung)".
- Klicken Sie in der Liste auf den Ort oder im Detailfenster auf **Vorgänge** neben der Adresse. Das Fenster zeigt alle Vorgänge in diesem Raum.
- Klicken Sie auf einen Bearbeiter, einen Ersteller oder einen Namen im Verlauf. Die Kontokarte der Person öffnet sich: Anzeigename, voller Name, E-Mail-Adresse, Rolle, eine etwaige Pause, die Zahl ihrer offenen Vorgänge und der Knopf **Vorgänge dieser Person**. Als Bearbeiter sehen Sie von den Vorgängen einer Kollegin nur die, die Sie selbst angelegt haben; die Karte kennzeichnet die Zahl dann mit „für dich sichtbar".

Ein Name, der kein Knopf ist, gehört zu keinem Konto mehr.

## Die Wissensdatenbank benutzen

Der Knopf **Wissen** im Hauptfenster öffnet das Nachschlagewerk des Helpdesks: links die Artikel, rechts der gewählte Artikel.

### Einen Artikel finden

1. Tippen Sie in das Feld **Suchbegriff (Enter)** ein Wort aus Titel, Text, Kategorie oder Schlagwort und drücken Sie Enter, oder wählen Sie eine Kategorie oder ein Schlagwort aus den Auswahlfeldern daneben. Die Liste zeigt die Treffer. Der Umschalter **Nur Prüfung fällig** zeigt nur Artikel, deren Prüfung ansteht.
2. Klicken Sie einen Artikel an. Rechts erscheinen Titel, Stand, Eigentümer und Text. Absätze, Listen und Befehle lassen sich markieren und kopieren.

Ein Artikel mit der Marke **Entwurf** ist noch nicht freigegeben; ein Artikel mit der Marke **Prüfung fällig** wurde länger nicht bestätigt. Beide bleiben lesbar.

### Einen Artikel vorschlagen oder eine Änderung vorschlagen

Als Bearbeiter schreiben Sie nicht direkt in die Wissensdatenbank; Sie reichen Vorschläge ein, die die Teamleitung freigibt.

1. Klicken Sie auf **Artikel vorschlagen** für einen neuen Artikel oder wählen Sie einen Artikel und klicken Sie auf **Änderung vorschlagen**. Der Bearbeiten-Dialog öffnet sich mit einer Vorschau rechts.
2. Tragen Sie **Titel**, **Inhalt** und **Kategorie** ein; Schlagworte, Prüfzyklus und Veröffentlichung setzt die Teamleitung nach dem Übernehmen über **Bearbeiten**. Für den Text gelten die Schreibweisen aus der [Referenz](referenz.md#auszeichnung-in-artikeln).
3. Klicken Sie auf **Vorschlag einreichen**. Die Teamleitung sieht den Vorschlag unter **Freigaben**.

### Aus einem Vorgang einen Artikel entwerfen

1. Klicken Sie im Detailfenster in der Karte **Wissensdatenbank** auf **Wissensartikel aus diesem Vorgang entwerfen**. Der Bearbeiten-Dialog öffnet sich mit der Beschreibung und, falls vorhanden, der Lösung als Text.
2. Kürzen Sie den Text auf die Anleitung und reichen Sie den Vorschlag ein.

## Einstellungen für das eigene Konto

Klicken Sie im Hauptfenster auf **Einstellungen**. Das Fenster **Einstellungen** öffnet sich und bietet:

| Einstellung | Wirkung |
|---|---|
| **Farbschema** | Dunkel oder Hell, wirkt sofort auf alle Fenster |
| **Darstellungsgröße** | 100, 115 oder 130 Prozent; Schrift und Abstände wachsen gemeinsam; gilt für jedes Fenster, das danach aufgeht |
| **Hinweise beim Überfahren zeigen** | die Erklärungen unter dem Mauszeiger ein- oder ausschalten |
| **Anzeigename** und **Anzeigename speichern** | der Name, den andere auf dem Bildschirm sehen; muss im Team eindeutig sein; leer heißt: der volle Name |
| **Passwort ändern** mit **Bisheriges Passwort**, **Neues Passwort**, **Neues Passwort wiederholen** und **Ändern** | die Regeln stehen unter [Konten und Passwörter](referenz.md#konten-und-passwörter) |

Farbschema, Darstellungsgröße und Hinweise gelten für Ihr Konto. Beim Abmelden fällt das Programm auf die Vorgaben zurück, damit die nächste Person am selben Rechner ihre eigenen Einstellungen bekommt.

## Abmelden

Klicken Sie unten rechts auf **Abmelden**, wenn Sie den Platz verlassen. Das Programm zeigt wieder das Anmeldefenster.
