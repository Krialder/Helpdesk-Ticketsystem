# Anleitungen für die Administration

Diese Anleitungen richten sich an Personen mit der Rolle Administration. Sie können alles, was Teamleitung und Bearbeiter können (siehe [Anleitungen für die Teamleitung](teamleitung.md)), und zusätzlich die Konten des Teams pflegen, Stammdaten löschen und die Daten des Systems sichern, zurückspielen, exportieren und importieren. Danach können Sie ein Konto anlegen, ein Passwort neu setzen und eine Abwesenheit eintragen, und Sie wissen, was jeder Knopf auf dem Reiter **Daten** tut.

Alle drei Aufgaben liegen im Fenster **Verwaltung**, das Sie über den gleichnamigen Knopf im Hauptfenster öffnen. Die Reiter **Konten**, **Stammdaten** und **Daten** sehen nur Sie; die Reiter **Auswertung** und **Ruhezeiten** teilen Sie sich mit der Teamleitung. Wer das System erstmals einrichtet und noch kein eigenes Konto hat, beginnt im [Betriebshandbuch](../betrieb/betriebshandbuch.md#erste-anmeldung-und-konten).

## Konten

Der Reiter **Konten** zeigt oben alle Konten, je Zeile Anzeigename, E-Mail-Adresse und in Klammern die Rolle, bei pausierten Konten auch das Ende der Pause. Darunter liegen zwei Karten: **Neues Konto** für das Anlegen und **Gewähltes Konto** für alles, was ein bestehendes Konto betrifft. Die zweite Karte bleibt grau, bis Sie in der Liste ein Konto anklicken; ihr Titel wechselt dann auf den Namen des Kontos, damit ein Passwort nicht am falschen Konto landet.

### Ein Konto anlegen

1. Tragen Sie unter **E-Mail-Adresse** die Anmeldeadresse der Person ein. Sie ist zugleich der Anmeldename und muss eindeutig sein.
2. Tragen Sie unter **Startpasswort** ein erstes Passwort ein. Es muss die Regeln unter [Konten und Passwörter](referenz.md#konten-und-passwörter) erfüllen; bei einem Verstoß nennt die Meldung, was fehlt.
3. Wählen Sie unter **Rolle** Bearbeiter, Teamleitung oder Administration. Was jede Rolle darf, steht in der [Referenz](referenz.md#rollen-und-rechte).
4. Klicken Sie auf **Konto anlegen**. Die Meldung „Konto … angelegt." erscheint, und das Konto steht in der Liste.
5. Wählen Sie das neue Konto in der Liste; der Kartenkopf **Gewähltes Konto** zeigt jetzt Namen und Adresse des Kontos. Tragen Sie **Nachname** und **Vorname** ein und klicken Sie auf **Namen setzen**. Die Liste zeigt den Namen.

Aus jeder Kontokarte führt der Knopf **In der Verwaltung öffnen** direkt zu diesem Reiter mit dem Konto vorausgewählt.

Übergeben Sie das Startpasswort persönlich und bitten Sie die Person, es bei der ersten Anmeldung unter **Einstellungen** zu ändern. Eine E-Mail mit dem Passwort landet in Postfächern, die Sie nicht kontrollieren.

Der volle Name aus Schritt 5 wird in jeder Historie und in jedem Bearbeiterfeld gespeichert. Deshalb setzen Sie ihn, nicht die Person selbst: Könnte jeder seinen Namen jederzeit ändern, ließe sich die Historie nicht mehr nachvollziehen. Angezeigt wird überall der Anzeigename, den jede Person selbst unter **Einstellungen** wählt. Sind voller Name und Anzeigename beide leer, zeigt das Programm die E-Mail-Adresse.

### Die Rolle ändern

1. Wählen Sie das Konto in der Liste.
2. Wählen Sie in der Auswahl neben **Rolle setzen** die neue Rolle und klicken Sie auf **Rolle setzen**. Die Meldung bestätigt die Rolle.

Die neue Rolle gilt ab der nächsten Anmeldung der Person. Das letzte Administrationskonto lässt sich nicht herabstufen: Ohne ein solches Konto gäbe es niemanden mehr, der Konten anlegen könnte, denn eine Selbstregistrierung gibt es nicht.

### Ein Passwort neu setzen

Wer sein Passwort vergessen hat, bekommt von Ihnen ein neues. Einen Knopf „Passwort vergessen" gibt es nicht.

1. Wählen Sie das Konto in der Liste.
2. Tragen Sie unter **Neues Passwort** und **Wiederholung** dasselbe neue Passwort ein.
3. Klicken Sie auf **Passwort setzen**. Die Meldung bestätigt den Wechsel und erinnert daran, das Passwort persönlich mitzuteilen.

Weichen die beiden Eingaben voneinander ab, ändert das Programm nichts und sagt das. Trägt ein Konto aus einem älteren Bestand noch eine Zwei-Faktor-Anmeldung, entfernt das Setzen des Passworts sie mit; die Anwendung bietet keine Codeeingabe mehr, und das Konto wäre sonst unbenutzbar.

Ist ein Konto nach falschen Passwörtern gesperrt, löst sich die Sperre von selbst; die Dauer steht unter [Konten und Passwörter](referenz.md#konten-und-passwörter).

### Eine Abwesenheit eintragen

Ein pausiertes Konto kann sich weiter anmelden, bekommt aber keine Vorgänge zugewiesen und keine Fristmeldungen per E-Mail.

1. Wählen Sie das Konto in der Liste.
2. Wählen Sie im Datumsfeld neben **Pausieren bis** den Tag, an dem die Person zurückkommt; vorgeschlagen ist der Tag in zwei Wochen. Klicken Sie auf **Pausieren bis**. Die Liste zeigt hinter der Rolle „pausiert bis" mit dem Datum.
3. Kommt die Person früher zurück, wählen Sie das Konto und klicken Sie auf **Wieder aktivieren**. Sonst endet die Pause von selbst um Mitternacht zu Beginn des gewählten Tags.

Die Vorgänge der Person bleiben ihr zugewiesen. Die Teamleitung findet sie in der Ansicht **Pausierte Zuweisungen** im Hauptfenster und weist sie um; wie, steht in den [Anleitungen für die Teamleitung](teamleitung.md#vorgänge-pausierter-kolleginnen-und-kollegen-finden).

### Ein Konto entfernen

1. Wählen Sie das Konto in der Liste.
2. Klicken Sie auf **Gewähltes Konto entfernen** und bestätigen Sie mit **Ja, Konto entfernen**.

Die Vorgänge, Kommentare und Historieneinträge der Person bleiben bestehen und tragen weiter ihren vollen Namen; entfernt wird nur der Zugang. Ihr eigenes Konto und das letzte Administrationskonto lassen sich nicht entfernen. Wollen Sie eine Person nur vorübergehend aussperren, ist eine Pause der bessere Weg, denn eine Pause lässt sich zurücknehmen.

## Stammdaten

Bei jeder Telefonerfassung merkt sich das System den Namen des Anrufers mit der zuletzt genannten Rufnummer und dem zuletzt genutzten Raum, damit die nächste Erfassung derselben Person die Werte vorschlagen kann. Der Reiter **Stammdaten** zeigt diese Einträge, je Zeile Name, Rufnummer und Raum.

Zum Löschen wählen Sie den Eintrag und klicken Sie auf **Gewählte Stammdaten löschen**, dann auf **Ja, Stammdaten löschen**. Die Vorgänge der Person bleiben davon unberührt; es verschwindet nur der Vorschlag mit Rufnummer und Raum. Bei der nächsten Telefonerfassung mit demselben Namen entsteht der Eintrag neu.

Verlangt eine Person die vollständige Löschung ihrer Daten, reicht dieser Knopf nicht: Die Vorgänge selbst, die Sicherungen und das Protokoll enthalten die Daten weiter. Das vollständige Verfahren steht in der Anleitung [Datenschutz](../betrieb/datenschutz.md).

## Daten

Der Reiter **Daten** zeigt oben fünf Zeilen:

| Zeile | Inhalt |
|---|---|
| **Datenbank:** | Pfad der Datenbankdatei |
| **Sicherungen:** | Sicherungsordner |
| **Zweites Ziel:** | zweites Sicherungsziel oder der Hinweis, dass keines eingerichtet ist |
| **Einstellungen:** | Pfad der Einstellungsdatei und ob sie vorhanden ist |
| **Bestand:** | Zahl der Vorgänge (dort „Tickets"), Artikel und Stammdaten (dort „Kontakte") |

Ist die letzte automatische Sicherung gescheitert, steht das als weitere Zeile darunter. Dann folgt die Liste der vorhandenen Sicherungen, neueste zuerst.

| Knopf | Was er tut |
|---|---|
| **Einstellungsdatei anlegen** | schreibt eine Vorlage der Einstellungsdatei mit den geltenden Werten nach `%LOCALAPPDATA%\Ticketsystem` und zeigt den Pfad an; eine vorhandene Datei bleibt unangetastet. Welche Schlüssel es gibt, steht in der [Konfigurationsreferenz](../betrieb/betriebshandbuch.md#konfigurationsreferenz). |
| **Jetzt sichern** | legt sofort eine Sicherung der Datenbank an, zusätzlich zu den automatischen; die neue Datei erscheint oben in der Liste |
| **Export als JSON speichern** | schreibt den fachlichen Bestand in eine Datei, die Sie im Speichern-Dialog wählen; was genau darin steht, sagt das [Betriebshandbuch](../betrieb/betriebshandbuch.md#export-und-import) |
| **Gewählte Sicherung zurückspielen** | ersetzt den gesamten Bestand durch die in der Liste gewählte Sicherung; der jetzige Stand wird vorher automatisch weggeschrieben |
| **Import aus JSON (ersetzt den Bestand)** | ersetzt den fachlichen Bestand durch den Inhalt einer Exportdatei; er führt nicht zusammen und legt vorher keine Sicherung an. Klicken Sie zuerst auf **Jetzt sichern**. |

Zurückspielen und Import fragen vor dem Ausführen nach, weil beide den Bestand ersetzen. Die Schritte im Einzelnen, was die automatischen Sicherungen tun und wie ein Umzug auf einen anderen Rechner geht, stehen im [Betriebshandbuch](../betrieb/betriebshandbuch.md#sicherung-und-wiederherstellung).

Zum Beenden schließen Sie das Hauptfenster; die Anwendung fährt dabei geordnet herunter. Einen eigenen Knopf dafür gibt es nicht.
