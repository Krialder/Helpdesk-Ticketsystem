# Datenschutz: Daten, Auskunft, Löschung

Diese Anleitung richtet sich an die Administration und an die Person, die im Betrieb für den Datenschutz verantwortlich ist. Danach wissen Sie, welche personenbezogenen Daten das System wo verarbeitet, und können eine Auskunft nach Art. 15 DSGVO und eine Löschung nach Art. 17 DSGVO ausführen.

Die Verfahren sind mit Absicht manuell: Anfragen sind bei einem kleinen Helpdesk selten, und eine Löschfunktion in der Anwendung wäre Code mit eigenen Rechten und eigener Pflege. Damit die Anleitung nicht veraltet, führt ein Test des Projekts die SQL-Blöcke der Abschnitte 2a, 3 und 4 gegen eine Datenbank mit der Beispielperson aus; die Blöcke der Abschnitte 3a und 4a führt er nur gegen das Schema aus. Ändern Sie die Blöcke deshalb nur zusammen mit den Tests.

## 1. Welche personenbezogenen Daten, wo, wozu

| Ort | Daten | Zweck |
|---|---|---|
| Vorgänge | Name des Kunden, bei E-Mail-Vorgängen die Absenderadresse, bei Telefon-Vorgängen Rückrufnummer und Gesprächsnotiz, dazu Raum, Titel und Beschreibung | Bearbeitung der Anfrage, Rückruf |
| Kommentare und Historie | voller Name und Kontokennung der handelnden Person je Eintrag. Dieselben Angaben stehen im Bearbeiterfeld des Vorgangs, an Wissensartikeln, Versionen und Vorschlägen (wer sie angelegt oder bearbeitet hat) und an jeder Ruhezeit (dort nur der Name) | Nachvollziehbarkeit des Vorgangs |
| Konten | E-Mail-Adresse, Passwort-Hash, voller Name, Anzeigename, Rolle, Pause bis | Anmeldung, Rechte, Abwesenheit |
| Stammdaten | Name des Anrufers, zuletzt genannte Rufnummer, zuletzt genutzter Raum | Vorschlag bei der nächsten Telefonerfassung |
| Sicherungen | vollständige Kopie der Datenbank, also alle Daten dieser Tabelle samt Konten; zehn Stände je Ziel | Wiederanlauf nach Fehlgriff oder Rechnerschaden |
| Protokolldatei | Zeitpunkte, Pfade, beim Import der volle Name der handelnden Person, beim ersten Start das Startpasswort; Empfängeradressen von Mails maskiert, bei ausgeschaltetem Versand aber Ticketnummer und Titel jeder Mail, die gesendet worden wäre | Fehlersuche; eine Datei je Tag, vierzehn Tage |
| Einstellungsdatei `einstellungen.json` | wenn eingetragen: Pfade (meist mit dem Windows-Kontonamen), die Adresse des Startkontos und des Support-Postfachs | Konfiguration |
| Oberflächenzustand je Konto | Kontokennung mit Ansicht, Sortierung, Farbschema und Fenstergröße | Merken der Einstellungen |
| Entwurfsdatei `entwurf.json` | Name, Rufnummer und Notiz einer angefangenen, nicht abgeschickten Erfassung | damit ein unterbrochener Anruf nicht neu erfasst werden muss; verschwindet mit dem Anlegen oder Verwerfen |
| Exportdateien (JSON, CSV) | Vorgänge, Historie, Kommentare, Stammdaten, Artikel; keine Konten und keine Passwörter | Auswertung, Weitergabe, Umzug |
| Eingangsbestätigung | Absenderadresse eines E-Mail-Vorgangs | Bestätigung mit Ticketnummer, nur bei eingerichtetem Mailversand |
| Support-Postfach (Microsoft 365) | E-Mails der Kunden; in „Gesendete Elemente" die Eingangsbestätigungen und Fristmeldungen mit Adresse, Ticketnummer und Titel | wird von Hand gelesen; die Anwendung greift nicht lesend zu, sondern sendet nur; Auftragsverarbeitung über den Microsoft-365-Vertrag des Betreibers |

Nicht erhoben: Geburtsdaten, Privatanschriften, Zahlungsdaten, Nutzungsverfolgung. Die erfasste Adresse ist ein Raum im Haus (`A-101`) oder das Wort `extern`. Es gibt keinen Browser und keine Cookies; die Anmeldung lebt im Prozess und endet mit ihm.

## 2. Datensparsamkeit

- Erfasst wird nur, was die Bearbeitung braucht (Tabelle oben).
- Das Protokoll enthält keine vollständigen Kundenadressen; der Versand maskiert den Empfänger.
- Die App-Registrierung für den Mailversand hat nur das Recht `Mail.Send` und wird per ApplicationAccessPolicy auf das eine Support-Postfach begrenzt, siehe [E-Mail-Versand einrichten](e-mail.md).

## 2a. Stammdaten

Bei jeder Telefonerfassung entsteht oder aktualisiert sich ein Stammsatz. Die Schutzmaßnahmen liegen in der Fachschicht und sind durch Tests abgesichert:

- Vorschläge erscheinen nur im Erfassungsfenster, also nach der Anmeldung mit einer Mitarbeiterrolle; der Dienst prüft die Rolle zusätzlich selbst.
- Vorschläge gibt es erst ab drei getippten Zeichen und höchstens acht je Abfrage. Ohne Begriff gibt es nichts.
- Gespeichert werden nur Name, die zuletzt genannte Rufnummer und der zuletzt genutzte Raum. Eine neue Nummer ersetzt die alte; der Bestand ist ein Stand, kein Archiv.
- Eine ungültige Rufnummer landet nicht im Bestand und verdrängt keine gültige.

Löschen: Die Administration wählt in der Verwaltung auf dem Reiter **Stammdaten** den Eintrag und klickt auf **Gewählte Stammdaten löschen**. Der Weg über SQL bleibt für den Fall, dass die Anwendung nicht startet:

```sql
DELETE FROM Kontakte WHERE NameNormalisiert = 'weber, sabine';
```

Die Vorgänge der Person bleiben davon unberührt; sie werden nach Abschnitt 4 getrennt anonymisiert. So zerstört ein gelöschter Stammsatz nicht die Nachvollziehbarkeit abgeschlossener Vorgänge.

## 3. Auskunft

Beispielperson dieser Anleitung: Sabine Weber, im System als Anruferin „Weber, Sabine" und mit der Adresse `s.weber@example.com`. Ersetzen Sie beide Werte an jeder Stelle.

Wo abfragen: gegen die Datenbankdatei, deren Pfad in der Verwaltung auf dem Reiter **Daten** steht und im Protokoll in der Zeile `Datenbank:`. Schließen Sie die Anwendung, bevor Sie die Datei mit einem SQLite-Werkzeug öffnen. Verlassen Sie sich nicht auf einen festen Dateinamen: Der Ort wird über eine vierstufige Kette ermittelt, und wer die erstbeste Datei öffnet, arbeitet womöglich an einer Kopie.

Eine Person kann auf drei Arten im Bestand stehen: als Name im Namensfeld (Anruf), als E-Mail-Adresse im Namensfeld (E-Mail-Vorgänge aus einem älteren Bestand) und als Adresse im Feld Absenderadresse. Die Abfragen unten decken alle drei ab.

```sql
SELECT * FROM Tickets
  WHERE CustomerName = 'Weber, Sabine' OR CustomerName = 's.weber@example.com' OR CustomerEmail = 's.weber@example.com';
SELECT * FROM Tickets
  WHERE CreatedByName = 'Weber, Sabine' OR CreatedByName = 's.weber@example.com';
SELECT * FROM TicketComments
  WHERE AuthorName = 'Weber, Sabine' OR AuthorName = 's.weber@example.com';
SELECT * FROM TicketHistory
  WHERE ChangedBy = 'Weber, Sabine' OR ChangedBy = 's.weber@example.com';
SELECT Id, UserName, Email FROM AspNetUsers WHERE NormalizedEmail = 'S.WEBER@EXAMPLE.COM';
SELECT * FROM Kontakte WHERE NameNormalisiert = 'weber, sabine';
```

Was diese Abfragen nicht finden: einen Namen in einem Freitext, also in Titel, Beschreibung, Gesprächsnotiz, Kommentar oder Wiedervorlage-Grund. Verlangt die Anfrage Vollständigkeit, sehen Sie diese Felder der gefundenen Vorgänge durch. Hatte die Person ein Konto im Ticketsystem, kommen die Abfragen aus Abschnitt 3a hinzu.

## 3a. Auskunft für Mitarbeitende

Eine Person mit Konto steht zusätzlich als Bearbeiterin in Vorgängen, als Eigentümerin, Bearbeiterin und Vorschlagende in der Wissensdatenbank und als Anlegerin von Ruhezeiten. Die Beispielperson kommt an diesen Orten nicht vor; der Test führt die Blöcke deshalb nur gegen das Schema aus und prüft keine Treffer.

```sql
SELECT Id, Title FROM Tickets WHERE AgentName = 'Weber, Sabine';
SELECT Id, Title FROM KbArticles WHERE Owner = 'Weber, Sabine';
SELECT ArticleId, Nummer FROM KbFassungen WHERE GespeichertVon = 'Weber, Sabine';
SELECT Id, Title FROM KbVorschlaege WHERE VorgeschlagenVon = 'Weber, Sabine';
SELECT Id, Bezeichnung FROM Ruhezeiten WHERE AngelegtVon = 'Weber, Sabine';
```

## 4. Löschung und Anonymisierung

Empfohlen ist die Anonymisierung der Vorgänge statt ihrer Löschung: Der Vorgang selbst (was war kaputt, wie wurde es gelöst) bleibt als Wissen erhalten, der Personenbezug verschwindet. Eine vollständige Anfrage braucht beide Schritte: die Stammdaten aus Abschnitt 2a und die Vorgangsdaten hier.

```sql
UPDATE Tickets SET CustomerName = 'entfernt', CustomerEmail = NULL, CustomerId = NULL,
  CallbackNumber = NULL, CallNote = NULL, FollowUpAt = NULL, FollowUpNote = NULL
  WHERE CustomerName = 'Weber, Sabine' OR CustomerName = 's.weber@example.com' OR CustomerEmail = 's.weber@example.com';
UPDATE Tickets SET CreatedByName = 'entfernt', CreatedById = NULL
  WHERE CreatedByName = 'Weber, Sabine' OR CreatedByName = 's.weber@example.com';
UPDATE TicketComments SET AuthorName = 'entfernt', AuthorId = 'entfernt'
  WHERE AuthorName = 'Weber, Sabine' OR AuthorName = 's.weber@example.com';
UPDATE TicketHistory SET ChangedBy = 'entfernt', ChangedById = NULL
  WHERE ChangedBy = 'Weber, Sabine' OR ChangedBy = 's.weber@example.com';
DELETE FROM AspNetUsers WHERE NormalizedEmail = 'S.WEBER@EXAMPLE.COM';
```

Mit diesen Anweisungen ist es nicht getan; hatte die Person ein Konto, folgt außerdem Abschnitt 4a. An fünf weiteren Orten liegen dieselben Daten:

1. Die Sicherungen neben der Datenbank und auf dem zweiten Ziel. Je Ziel bleiben zehn Stände; die Person steht dort weiter, bis die Stände durchgelaufen sind. Wer sofort löschen muss, löscht die Sicherungsdateien an beiden Orten und legt eine neue Sicherung an.
2. Exportdateien, die jemand erzeugt hat. Sie liegen dort, wohin sie gespeichert wurden.
3. Die Protokolldatei im Ordner `Protokoll`, vierzehn Tage lang.
4. Die Entwurfsdatei `entwurf.json`, falls gerade eine Erfassung dieser Person angefangen war.
5. Das Support-Postfach, denn dort liegt die Kopie der Anfragen und in „Gesendete Elemente" jede Eingangsbestätigung.

## 4a. Löschung für Mitarbeitende

Der Name einer ausgeschiedenen Mitarbeiterin bleibt in Bearbeiterfeldern, Wissensdatenbank und Ruhezeiten stehen, auch nachdem ihr Konto entfernt ist. Verteilen Sie offene Vorgänge der Person vorher in der Anwendung um oder heben Sie die Zuweisung auf, denn die erste Anweisung macht jeden Vorgang ohne Bearbeiterkennung zu einem unzugewiesenen; sie ist für abgeschlossene Vorgänge gedacht. Danach anonymisieren Sie diese Stellen genauso wie die Historie; die Nachvollziehbarkeit der Vorgänge leidet nicht, weil der Eintrag selbst bleibt.

```sql
UPDATE Tickets SET AgentName = 'entfernt', AgentId = NULL WHERE AgentName = 'Weber, Sabine';
UPDATE KbArticles SET Owner = 'entfernt', OwnerId = NULL WHERE Owner = 'Weber, Sabine';
UPDATE KbFassungen SET GespeichertVon = 'entfernt', GespeichertVonId = NULL WHERE GespeichertVon = 'Weber, Sabine';
UPDATE KbVorschlaege SET VorgeschlagenVon = 'entfernt', VorgeschlagenVonId = NULL WHERE VorgeschlagenVon = 'Weber, Sabine';
UPDATE Ruhezeiten SET AngelegtVon = 'entfernt' WHERE AngelegtVon = 'Weber, Sabine';
```

## 5. Betrieb auf einem Arbeitsplatzrechner

Der gesamte Bestand liegt auf einem Arbeitsplatzrechner, nicht auf einem Server. Daraus ergeben sich andere Risiken als bei einem Server:

| Risiko | Maßnahme | Wer |
|---|---|---|
| Rechner wird gestohlen oder verloren | Festplattenverschlüsselung ist Voraussetzung (BitLocker mit PIN vor dem Start). Ohne sie ist jeder Diebstahl eine Offenlegung des ganzen Bestands samt Sicherungen | Betreiber, vor der ersten Nutzung |
| Ein zweites Windows-Konto am selben Rechner liest mit | Die Datenbank liegt unter `%LOCALAPPDATA%` des jeweiligen Kontos; Rechneradministratoren kommen trotzdem heran | Betreiber |
| Exportdateien wandern weiter | Jede Exportdatei ist eine eigenständige Kopie personenbezogener Daten; den Tabellen-Export (CSV) darf auch die Teamleitung speichern. Sie gehört auf ein verschlüsseltes Laufwerk, wird nicht unverschlüsselt per Mail versandt und wird gelöscht, sobald ihr Zweck erfüllt ist | wer exportiert |
| Sicherungen häufen sich an | Zehn Stände je Ziel; eine Löschanfrage muss beide Ziele erfassen | Administration |
| Sicherung nur auf derselben Festplatte | Zweites Sicherungsziel auf einem verschlüsselten Stick, Schlüssel `Daten:ZweitSicherungsOrdner` | Betreiber |

## 6. Sicherheit

- Keine Netzschnittstelle: Fenster und Datenzugriff laufen im selben Prozess. Hinaus geht nur der Mailversand (TLS zu Microsoft Graph), und der ist ohne Einrichtung aus.
- Passwörter ausschließlich als Hash; die Anmeldung sperrt nach wiederholten Fehlversuchen vorübergehend (Zahlen in der [Referenz](../anwender/referenz.md#konten-und-passwörter)).
- Die Rechte an Vorgängen, Wissensdatenbank, Konten, Stammdaten, Sicherung und Austausch prüft die Fachschicht (Ausnahmen in der [Referenz](../anwender/referenz.md#rollen-und-rechte)): Ein Konto ohne Rolle kommt nicht durch die Anmeldung, Stammdaten und Artikel sind Mitarbeitern vorbehalten.
- Geheimnisse (Client-Geheimnis, Startpasswort) nur über Umgebungsvariablen.
- Sichern, Zurückspielen, JSON-Export und Import darf nur die Administration; den Tabellen-Export (CSV) mit allen Kundenangaben darf zusätzlich die Teamleitung. Die Prüfung liegt im Dienst und zusätzlich im Fenster.
- Der Export enthält keine Passwort-Hashes.
