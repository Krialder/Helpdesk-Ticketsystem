# Referenz

Diese Referenz richtet sich an alle Anwender des Ticketsystems, gleich welcher Rolle. Sie schlagen hier nach, was ein Status, eine Priorität, ein Fristzeichen oder eine Rolle genau bedeutet, welche Tasten es gibt und wie ein Wissensartikel ausgezeichnet wird. Anleitungen mit Schritten stehen nicht hier, sondern in den Handbüchern je Rolle: [Einstieg](einstieg.md), [Bearbeiter](bearbeiter.md), [Teamleitung](teamleitung.md), [Administration](administration.md).

## Status

Jeder Vorgang hat genau einen Status. Erlaubt sind nur die Wechsel in der Tabelle; jeder andere wird mit einer Meldung abgewiesen.

| Status | Bedeutung | Erlaubte Wechsel |
|---|---|---|
| **Neu** | erfasst, niemand kümmert sich | nach Zugewiesen (üblich durch eine Zuweisung, auch als Statusknopf ohne Person), nach Geschlossen |
| **Zugewiesen** | eine Person ist verantwortlich, hat aber noch nicht begonnen | nach In Arbeit, nach Geschlossen |
| **In Arbeit** | die Person arbeitet daran | nach Gelöst, nach Geschlossen |
| **Gelöst** | die Lösung ist eingetragen, der Kunde hat noch nicht bestätigt | zurück nach In Arbeit (Wiedereröffnung), nach Geschlossen |
| **Geschlossen** | endgültig; keine Änderung, kein Kommentar, keine Zuweisung mehr | keine |

Was die Wechsel nebenbei tun:

- Verlässt ein Vorgang den Status Neu zum ersten Mal, gilt die Reaktionsfrist als erfüllt; war sie in diesem Moment schon abgelaufen, als verspätet erfüllt. Das gilt auch beim Schließen aus Neu.
- Der Wechsel auf Gelöst setzt den Lösungszeitpunkt und entscheidet damit die Lösungsfrist. Ein Text im Feld **Lösung** wird als Kommentar „Lösung: …" gespeichert.
- Der Wechsel von Gelöst zurück auf In Arbeit löscht den Lösungszeitpunkt; die Lösungsfrist läuft wieder.
- Wer einen Vorgang schließt, ohne dass er gelöst war, lässt die noch offenen Fristen entfallen (Zustand „Entfällt"), etwa bei einem Duplikat oder einer Fehlmeldung. Eine schon erfüllte Reaktionsfrist bleibt erfüllt; wird ein Vorgang erst nach Ablauf der Reaktionsfrist aus Neu geschlossen, zählt sie als verspätet erfüllt und damit in der Auswertung als Fristriss.
- Das Aufheben einer Zuweisung setzt einen Vorgang von Zugewiesen zurück auf Neu; ein Vorgang in Arbeit bleibt in Arbeit.

Als offen gelten Neu, Zugewiesen und In Arbeit. Gelöst zählt im Lagebild als unerledigt, in der Ansicht **Offen** aber nicht mehr.

## Prioritäten und Fristen

Die Priorität legt zwei Fristen fest, beide ab dem Anlegen des Vorgangs in Kalenderstunden, also auch über Nacht und am Wochenende.

| Priorität | Reaktion bis | Lösung bis | Gemeint für |
|---|---|---|---|
| **Kritisch** | 1 Stunde | 4 Stunden | viele Personen können nicht arbeiten, etwa ein Serverausfall |
| **Hoch** | 4 Stunden | 8 Stunden | eine Person kann nicht arbeiten |
| **Mittel** | 8 Stunden | 24 Stunden | die Arbeit geht eingeschränkt weiter; die Vorgabe bei der Erfassung |
| **Niedrig** | 24 Stunden | 72 Stunden | Wunsch, Frage, kein Ausfall |

Die Stunden sind Vorgaben, die die Administration in der Einstellungsdatei ändern kann, siehe [Konfigurationsreferenz](../betrieb/betriebshandbuch.md#konfigurationsreferenz).

Zwei Regeln verschieben Fristen:

- Ruhezeiten (Ferien, Betriebsruhe) zählen nicht mit: Die Fälligkeit rückt genau um die Überschneidung nach hinten. Eine Frist, die schon gerissen war, bleibt gerissen.
- Ändert jemand die Priorität, rechnet das System beide Fristen ab dem Anlegen neu, nicht ab der Änderung. Die Uhr des Kunden läuft seit dem Eingang.

Die Prioritäten Hoch und Kritisch stehen in der Liste farbig, Mittel und Niedrig in der Textfarbe: Wer alles hervorhebt, hebt nichts hervor.

## Fristzustände

Jede der beiden Fristen hat einen Zustand. Die Liste zeigt den dringlicheren von beiden, das Detailfenster beide. Das Zeichen trägt den Zustand auch ohne Farbe, für den Schwarz-Weiß-Druck und für Menschen mit Farbsehschwäche.

| Zeichen | Zustand | Bedeutung | Farbe |
|---|---|---|---|
| ○ | Läuft | mehr als ein Viertel der Frist ist übrig | neutral |
| ● | Bald fällig | weniger als ein Viertel der Frist ist übrig | Warnfarbe |
| ! | Überfällig | die Frist ist abgelaufen, der Vorgang ist offen | Gefahrfarbe |
| ✓ | Erfüllt | rechtzeitig reagiert oder gelöst | Erfolgsfarbe |
| ✓! | Verspätet erfüllt | reagiert oder gelöst, aber nach der Frist | Warnfarbe |
| – | Entfällt | geschlossen, ohne gelöst zu werden | neutral |

„Bald fällig" ist relativ zur Fristlänge: Bei einer Frist von vier Stunden beginnt die Warnung nach drei Stunden, bei 72 Stunden nach 54.

Die Frist steht als Restzeit: „noch 7 h 59 min", „noch 2 Tage 3 h" oder, nach Ablauf, „seit 42 min". Das Detailfenster nennt zusätzlich den Zeitpunkt: für heute nur die Uhrzeit, sonst mit Datum. Unter der Restzeit füllt sich ein dünner Balken mit dem verstrichenen Anteil; bei Überschreitung ist er voll, die Überschreitung selbst zeigt nur der Text.

Als Fristriss zählen in der Auswertung die Zustände Überfällig und Verspätet erfüllt.

Unten rechts im Hauptfenster steht der Fristenstand aller Vorgänge, die Sie sehen dürfen: „Fristen: 2 überfällig, 1 bald fällig, 0 Wiedervorlagen", in Gefahrfarbe, sobald etwas überfällig ist, sonst in Warnfarbe. Gibt es nichts zu tun, steht dort „Keine Frist überfällig".

## Rollen und Rechte

Drei Rollen bauen aufeinander auf: Die Teamleitung darf alles, was ein Bearbeiter darf, die Administration alles, was die Teamleitung darf. Ein Konto hat genau eine Rolle; ein Konto ohne Rolle kommt nicht durch die Anmeldung. Die Prüfung liegt im Programm selbst, nicht nur in den Fenstern: Ein Fenster blendet Knöpfe aus, die Regel gilt auch ohne Fenster. Zwei Ausnahmen gibt es: Ob jemand das Lagebild öffnen darf, entscheidet nur das Fenster (das Programm selbst verlangt dort nur ein Mitarbeiterkonto), und das Anlegen der Einstellungsdatei sperrt nur das Fenster. Das Lagebild zeigt aber ohnehin nur, was die Person sehen darf, und die Einstellungsdatei enthält keine Geheimnisse.

| Recht | Bearbeiter | Teamleitung | Administration |
|---|---|---|---|
| Vorgänge sehen | selbst angelegte, sich zugewiesene und unzugewiesene | alle | alle |
| Vorgang anlegen (Telefon, E-Mail) | ja | ja | ja |
| Kommentieren | jeden sichtbaren Vorgang | alle | alle |
| Status, Priorität, Angaben, Wiedervorlage ändern | selbst angelegte und sich zugewiesene | alle | alle |
| Zuweisen | nur sich selbst, nur unzugewiesene Vorgänge | jede Person, auch umverteilen | wie Teamleitung |
| Zuweisung aufheben | nur sich zugewiesene | alle | alle |
| Ansicht **Pausierte Zuweisungen** | nein | ja | ja |
| **Lagebild** | nein | ja | ja |
| Wissensdatenbank lesen, Artikel und Änderungen vorschlagen | ja | ja | ja |
| Artikel anlegen, bearbeiten, veröffentlichen, löschen; Versionen; als geprüft markieren; Freigaben; Kategorien und Tags | nein | ja | ja |
| **Verwaltung**: Auswertung, CSV-Export, Ruhezeiten | nein | ja | ja |
| **Verwaltung**: Konten, Stammdaten löschen, Sicherung, Zurückspielen, JSON-Export und Import, Einstellungsdatei | nein | nein | ja |
| Eigenes Konto: Anzeigename, Passwort, Farbschema, Darstellungsgröße, Hinweise | ja | ja | ja |
| Fristmeldung per E-Mail erhalten | nein | ja | ja |

Ein pausiertes Konto kann sich weiter anmelden und arbeiten; es bekommt nur keine neuen Zuweisungen und keine Fristmeldungen.

## Konten und Passwörter

Ein Passwort braucht mindestens sechs Zeichen, darunter eine Ziffer, einen Kleinbuchstaben, einen Großbuchstaben und ein Sonderzeichen. Nach fünf falschen Passwörtern ist das Konto fünf Minuten gesperrt; die Sperre löst sich von selbst. Eine Selbstregistrierung und einen Knopf für vergessene Passwörter gibt es nicht: Konten legt die Administration an, und sie setzt auch ein neues Passwort.

## Die Vorgangsliste

Welche Ansichten es gibt, steht in den [Anleitungen für Bearbeiter](bearbeiter.md#die-richtige-ansicht-wählen). Jede Zeile zeigt von links nach rechts: Nummer (**Nr.**), Fristzeichen, **Titel**, **Priorität**, **Kunde**, **Ort**, **Geändert**, **Bearbeiter**, **Frist** als Restzeit mit Balken und **Status**.

Die Liste zeigt höchstens 200 Vorgänge und sagt es, wenn sie kürzt. Was nicht unter den ersten 200 liegt, erreichen Sie über Ansicht, Prioritätsfilter oder Suche; die Liste ist nicht zum Blättern gedacht. Der Zähler unten links unterscheidet „47 Vorgänge" (alles sichtbar) von „12 von 47 Vorgängen" (ein Filter greift).

## Eingaberegeln

| Feld | Regel |
|---|---|
| **Adresse** | Pflichtfeld: ein Großbuchstabe für das Gebäude, Bindestrich, ein bis vier Ziffern für den Raum (`A-101`, `B-12`), oder genau das Wort `extern` |
| **Rückrufnummer** | Pflichtfeld bei der Quelle Telefon: eine Durchwahl mit 2 bis 6 Ziffern, eine Festnetznummer mit Vorwahl mit 8 bis 12 Ziffern oder eine Mobilnummer (Beginn 015, 016 oder 017) mit 10 bis 12 Ziffern. Sonderrufnummern, die mit 01 beginnen und keine Mobilnummern sind, werden abgewiesen. Nur die Ziffern werden gespeichert; `+49` und `0049` werden zur führenden Null. |
| **Name des Anrufers oder Absenders** | Pflichtfeld; die Schreibweise „Nachname, Vorname" hält die Stammdaten und die Sortierung einheitlich |
| **Absenderadresse** | Pflichtfeld bei der Quelle E-Mail; an diese Adresse geht die Eingangsbestätigung, wenn der Mailversand eingerichtet ist |
| **Titel**, **Beschreibung** | Pflichtfelder; der Titel hat höchstens 200 Zeichen |
| **Anzeigename** | muss im Team eindeutig sein; leer heißt: der volle Name |

## Tastatur

| Wo | Taste | Wirkung |
|---|---|---|
| Hauptfenster | Strg+N | neuen Vorgang erfassen |
| Hauptfenster | Strg+F oder F3 | in das Suchfeld springen |
| Suchfeld | Enter | suchen |
| Suchfeld | Pfeil ab | in die Liste springen, erste Zeile markiert |
| Liste | Enter oder Doppelklick | markierten Vorgang öffnen |
| Hauptfenster | F5 | Liste und Fristenstand neu laden |
| Erfassung | Strg+Enter | Vorgang anlegen |
| Erfassung | Escape | Fenster schließen; die Eingaben bleiben als Entwurf erhalten |
| Detailfenster | Strg+Enter | Kommentar senden |
| Detailfenster | Escape | Fenster schließen; ein getippter, nicht gesendeter Kommentar geht dabei verloren |
| Bearbeiten-Dialog der Wissensdatenbank | Escape | Dialog schließen; ungespeicherter Text geht ohne Rückfrage verloren, nur **Abbrechen** fragt nach |
| Wissensdatenbank, Suchfeld | Enter | suchen |
| jedes Neben- und Dialogfenster | Escape | schließen |

Escape ist die Taste oben links auf der Tastatur. In der Erfassung ist sie gefahrlos, weil der Entwurf gesichert wird; im Detailfenster und im Bearbeiten-Dialog prüfen Sie vor Escape, ob noch ungesendeter oder ungespeicherter Text steht.

## Auszeichnung in Artikeln

Wissensartikel sind reiner Text mit sechs Schreibweisen. Der Text bleibt so gespeichert, wie er getippt ist; die Lesefläche setzt ihn um. Andere Markdown-Zeichen bleiben Text.

| Schreibweise | Wirkung |
|---|---|
| `## Überschrift` am Zeilenanfang (ein bis sechs `#`, dann ein Leerzeichen) | Überschrift |
| `**fett**` | fett |
| `` `Befehl` `` | Festbreitenschrift im Satz |
| `- Punkt` oder `* Punkt` am Zeilenanfang | Aufzählung |
| `1. Schritt` oder `1) Schritt` am Zeilenanfang | nummerierte Liste; die erste Zahl bestimmt den Beginn |
| eine Zeile mit drei Rückwärtsapostrophen vor und nach dem Text | Befehlsblock, wortgetreu mit Zeilenumbrüchen |

Eine Leerzeile trennt Absätze. Ein Wechsel der Listenart beendet die Liste.

## Automatik im Hintergrund

| Was | Wann |
|---|---|
| Sicherung der Datenbank | beim Start, sobald mindestens ein Vorgang existiert, und danach alle vier Stunden, solange das Programm läuft |
| Prüfung auf gerissene Fristen und Fristmeldung per E-Mail | alle fünf Minuten |
| Lagebild | frischt jede Minute auf |
| Aufräumen alter Protokolldateien | beim Start; behalten werden vierzehn Tage |

Die Takte sind Vorgaben und in der [Konfigurationsreferenz](../betrieb/betriebshandbuch.md#konfigurationsreferenz) einstellbar. Was das System bewusst nicht tut, steht in der [README](../../README.md#bekannte-einschränkungen).
