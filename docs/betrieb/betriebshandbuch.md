# Betriebshandbuch

Dieses Handbuch richtet sich an die Person, die das Ticketsystem auf einem Rechner einrichtet und betreibt. Danach können Sie es installieren, konfigurieren, sichern, wiederherstellen, aktualisieren, auf einen anderen Rechner umziehen und bei Störungen den Grund finden. Die Bedienung der Anwendung selbst steht in den [Anleitungen je Rolle](../../README.md#dokumentation); der Umgang mit personenbezogenen Daten in der [Datenschutzanleitung](datenschutz.md).

Inhalt:

1. [Voraussetzungen](#voraussetzungen)
2. [Installation](#installation)
3. [Erste Anmeldung und Konten](#erste-anmeldung-und-konten)
4. [Wo die Daten liegen](#wo-die-daten-liegen)
5. [Konfiguration](#konfiguration)
6. [Sicherung und Wiederherstellung](#sicherung-und-wiederherstellung)
7. [Update](#update)
8. [Umzug auf einen anderen Rechner](#umzug-auf-einen-anderen-rechner)
9. [Protokolle](#protokolle)
10. [Wiederkehrende Aufgaben](#wiederkehrende-aufgaben)
11. [Fehlerbehebung](#fehlerbehebung)
12. [Sicherheit](#sicherheit)

## Voraussetzungen

| Was | Anforderung |
|---|---|
| Betriebssystem | Windows 10 oder 11, 64 Bit. Die Anwendung ist als eigenständiges Paket für `win-x64` gebaut; auf dem Rechner muss kein .NET installiert sein. |
| Festplatte | Verschlüsselt mit BitLocker (Windows Pro oder Enterprise; Home hat kein BitLocker). Die Datenbankdatei ist nicht verschlüsselt, siehe [Sicherheit](#sicherheit). |
| Platz | rund 150 MB für das Programm. Für die Daten rechnen Sie mit dem Elffachen der Datenbankgröße: die Datei selbst und zehn Sicherungsstände. Auf dem zweiten Sicherungsziel brauchen Sie denselben Platz noch einmal. |
| Zweites Sicherungsziel | ein verschlüsselter USB-Stick (BitLocker To Go) oder ein Netzlaufwerk, auf das das Windows-Konto schreiben darf. |
| Für den Mailversand (optional) | ein Microsoft-365-Mandant mit Administratorrechten für eine App-Registrierung, siehe [E-Mail-Versand einrichten](e-mail.md). |
| Zum Bauen des Pakets | ein Rechner mit .NET SDK 8.0, siehe [Entwicklerhandbuch](../entwicklung/entwicklerhandbuch.md). |

## Installation

Die Anwendung wird als ZIP-Archiv verteilt, das genau eine Datei enthält: die Programmdatei `Ticketsystem.App.exe` mit rund 150 MB; sie enthält die .NET-Laufzeit.

### Paket bauen

Wenn Sie kein fertiges Archiv haben, bauen Sie es auf einem Rechner mit .NET SDK 8.0:

1. Öffnen Sie den Projektordner und starten Sie `Veroeffentlichen.cmd` per Doppelklick. Das Skript ruft `dotnet publish` für `win-x64` als eigenständige Fassung auf.
2. Warten Sie, bis das Fenster die Hinweise zur Installation ausgibt; es bleibt offen, bis Sie eine Taste drücken. Im Projektordner liegen jetzt der Ordner `veroeffentlicht` und daneben `Ticketsystem-windows.zip`.

### Paket installieren

1. Entpacken Sie `Ticketsystem-windows.zip` in einen eigenen Ordner, zum Beispiel `C:\Programme\Ticketsystem`. Der Ordner enthält danach nur `Ticketsystem.App.exe`.
2. Starten Sie `Ticketsystem.App.exe` per Doppelklick. Windows SmartScreen meldet „Der Computer wurde durch Windows geschützt", weil die Datei nicht signiert ist. Wählen Sie **Weitere Informationen** und dann **Trotzdem ausführen**.
3. Warten Sie, bis das Anmeldefenster erscheint. Beim ersten Start legt die Anwendung im Hintergrund die Datenbank an, führt die Datenbankmigrationen aus, erzeugt die Rollen und das Startkonto und schreibt das Protokoll.

Ein zweiter Doppelklick auf die Programmdatei startet keine zweite Anwendung: Ein Sperrobjekt je Windows-Sitzung verhindert das, und der zweite Start endet ohne Meldung. Das laufende Fenster finden Sie in der Taskleiste.

Zum Ausprobieren aus dem Quelltext genügt `Start.cmd` im Projektordner. Das Skript sucht ein installiertes .NET SDK 8, lädt bei Bedarf das Installationsskript von `dot.net` und installiert ein lokales .NET in den Unterordner `.dotnet` ohne Administratorrechte, baut das Projekt und startet die Anwendung. Weil das Skript ein Installationsskript aus dem Internet ausführt, ist dieser Weg zum Ausprobieren gedacht, nicht für den Helpdesk-Rechner.

## Erste Anmeldung und Konten

Beim ersten Start legt die Anwendung das Startkonto `agent@ticketsystem.local` an, weil es sonst kein Administrationskonto gäbe. Sein Passwort ist ein Zufallswert, der genau einmal ins Protokoll geschrieben wird.

1. Öffnen Sie im Ordner `%LOCALAPPDATA%\Ticketsystem\Protokoll` die neueste Datei.
2. Suchen Sie die Zeile, die `Startkonto agent@ticketsystem.local angelegt. Passwort:` enthält. Sie steht eingerückt unter einer Zeile mit Zeitstempel und Stufe, wie alle Meldungen im Protokoll (siehe [Protokolle](#protokolle)). Das Passwort steht hinter `Passwort:` und endet vor dem nächsten Leerzeichen; kopieren Sie es.
3. Melden Sie sich im Anmeldefenster mit `agent@ticketsystem.local` und diesem Passwort an. Das Hauptfenster öffnet sich.
4. Klicken Sie auf **Einstellungen**, tragen Sie unter **Passwort ändern** das bisherige und zweimal ein neues Passwort ein und klicken Sie auf **Ändern**.
5. Klicken Sie auf **Verwaltung** und wechseln Sie auf den Reiter **Konten**. Legen Sie für jede Person ein Konto an: E-Mail-Adresse, Startpasswort, Rolle, dann **Konto anlegen**. Setzen Sie anschließend über **Namen setzen** Nachname und Vorname; unter diesem Namen wird die Person in jeder Historie gespeichert.
6. Melden Sie sich mit Ihrem eigenen Administrationskonto neu an und entfernen Sie das Startkonto über **Gewähltes Konto entfernen**. Es wird nicht wieder angelegt, solange ein Administrationskonto existiert.

Wollen Sie das Startpasswort vor dem ersten Start festlegen, setzen Sie die Umgebungsvariablen `SeedAgent__Email` und `SeedAgent__Password`, bevor Sie die Anwendung starten.

Die Passwortregeln und die Sperre nach Fehlversuchen stehen in der [Referenz](../anwender/referenz.md#konten-und-passwörter). Eine Selbstregistrierung und einen Weg für vergessene Passwörter gibt es nicht: Die Administration setzt ein neues Passwort über **Passwort setzen** und übergibt es persönlich.

## Wo die Daten liegen

Der gesamte Bestand liegt in einer SQLite-Datei. Ohne Konfiguration sucht die Anwendung ihren Ort in dieser Reihenfolge:

1. Der Wert von `ConnectionStrings:Default`, wenn gesetzt.
2. `%LOCALAPPDATA%\Ticketsystem\app.db`, also im Profil des angemeldeten Windows-Kontos. Das ist der Normalfall.
3. `%USERPROFILE%\Ticketsystem\app.db`, falls sich das Profil nicht ermitteln lässt.
4. Neben der Programmdatei, mit einer Warnung im Protokoll.

Welcher Ort gilt, steht in der Verwaltung auf dem Reiter **Daten** in der Zeile „Datenbank:" und im Protokoll in der Zeile, die `Datenbank:` enthält; dort steht in Klammern auch, welche Stufe gegriffen hat.

Dazu gehören:

| Datei oder Ordner | Inhalt |
|---|---|
| `Sicherungen\` | die automatischen Sicherungen, Dateiname `app-JJJJ-MM-TT-HHmm-ss.db`, dazu `vor-wiederherstellung-…db` vor jedem Zurückspielen |
| `Protokoll\` | eine Protokolldatei je Tag, `ticketsystem-JJJJ-MM-TT.log`; jeder Start hängt an |
| `einstellungen.json` | die optionale Einstellungsdatei, siehe [Konfiguration](#konfiguration). Die Einstellungsdatei liegt immer im Profilordner `%LOCALAPPDATA%\Ticketsystem`, auch wenn sie die Datenbank über `ConnectionStrings:Default` an einen anderen Ort legt: Die Anwendung muss die Datei finden, bevor sie weiß, wo die Datenbank liegt. Protokoll und Sicherungen folgen dagegen der Datenbank. |
| `entwurf.json` | eine angefangene, nicht abgeschickte Erfassung mit Name, Rufnummer und Notiz; verschwindet, sobald der Vorgang angelegt oder der Entwurf verworfen wird |

Die Daten liegen mit Absicht nicht im Programmordner: Der darf bei einem Update vollständig ersetzt werden, und unter `C:\Programme` darf ein normales Konto nicht schreiben. Die Folge ist, dass zwei Windows-Konten am selben Rechner zwei getrennte Bestände haben. Wollen Sie einen gemeinsamen Ort, setzen Sie `ConnectionStrings:Default` auf einen Pfad, den alle Konten schreiben dürfen.

## Konfiguration

Die Anwendung läuft ohne Einstellungsdatei mit den Vorgabewerten. Wollen Sie etwas ändern:

1. Klicken Sie in der Verwaltung auf dem Reiter **Daten** auf **Einstellungsdatei anlegen**. Die Anwendung schreibt `einstellungen.json` mit den Sektionen `ConnectionStrings`, `Daten` und `Desktop` und den geltenden Werten in den Profilordner `%LOCALAPPDATA%\Ticketsystem` (siehe [Wo die Daten liegen](#wo-die-daten-liegen)) und zeigt den Pfad an. Eine vorhandene Datei wird nicht überschrieben.
2. Öffnen Sie die Datei in einem Texteditor und ändern Sie die Werte. Die Sektionen `Sla`, `Email` und `SeedAgent` fehlen in der Vorlage; tragen Sie sie bei Bedarf nach der Tabelle unten ein.
3. Beenden Sie die Anwendung (Hauptfenster schließen) und starten Sie sie neu. Ob die Datei gelesen wird, steht im Protokoll in der Zeile `Einstellungen:` und auf dem Reiter **Daten**.

Vier Schichten überschreiben einander in dieser Reihenfolge: Vorgaben im Code, die Einstellungsdatei, Umgebungsvariablen, Befehlszeilenargumente. In Umgebungsvariablen wird der Doppelpunkt zu zwei Unterstrichen (`Daten__ZweitSicherungsOrdner`). Geheimnisse wie das Client-Geheimnis des Mailversands gehören in Umgebungsvariablen, nicht in die Datei.

### Konfigurationsreferenz

| Schlüssel | Bedeutung | Vorgabe | Beispiel |
|---|---|---|---|
| `ConnectionStrings:Default` | Verbindung zur SQLite-Datei | leer, dann die Ablagekette oben | `Data Source=D:\Helpdesk\app.db` |
| `Daten:SicherungsOrdner` | Ordner der automatischen Sicherungen | leer, dann `Sicherungen` neben der Datenbank | `D:\Helpdesk\Sicherungen` |
| `Daten:ZweitSicherungsOrdner` | zweites Sicherungsziel außerhalb des Rechners; jede gelungene Sicherung wird zusätzlich dorthin kopiert | leer, dann keine Kopie | `E:\Ticketsystem-Sicherung` |
| `Daten:AufbewahrteSicherungen` | Zahl der aufbewahrten Stände je Ziel; ältere werden beim nächsten Sichern gelöscht | `10` | `20` |
| `Daten:SicherungBeimStart` | Sicherung bei jedem Start, sobald mindestens ein Vorgang existiert | `true` | `false` |
| `Daten:SicherungIntervallStunden` | Abstand der automatischen Sicherungen im laufenden Betrieb; `0` schaltet sie ab | `4` | `2` |
| `Desktop:ProtokollTage` | Tage, die Protokolldateien aufbewahrt werden | `14` | `30` |
| `SeedAgent:Email` | Anmeldeadresse des Startkontos; wird nur angelegt, solange kein Administrationskonto existiert | `agent@ticketsystem.local` | `admin@example.org` |
| `SeedAgent:Password` | Passwort des Startkontos | Zufallswert, einmalig im Protokoll | nur als Umgebungsvariable |
| `Sla:Critical:ReactionHours`, `Sla:Critical:ResolutionHours` | Fristen der Priorität Kritisch in Stunden | `1`, `4` | `0.5`, `2` |
| `Sla:High:ReactionHours`, `Sla:High:ResolutionHours` | Fristen der Priorität Hoch | `4`, `8` | `2`, `6` |
| `Sla:Medium:ReactionHours`, `Sla:Medium:ResolutionHours` | Fristen der Priorität Mittel | `8`, `24` | `4`, `16` |
| `Sla:Low:ReactionHours`, `Sla:Low:ResolutionHours` | Fristen der Priorität Niedrig | `24`, `72` | `24`, `120` |
| `Sla:PruefIntervallMinuten` | Takt, in dem gerissene Fristen geprüft und gemeldet werden | `5` | `15` |
| `Email:Enabled` | schaltet den Mailversand ein | `false` | `true` |
| `Email:TenantId` | Verzeichnis-ID des Microsoft-365-Mandanten | leer | siehe [E-Mail-Versand einrichten](e-mail.md) |
| `Email:ClientId` | Anwendungs-ID der App-Registrierung | leer | `00000000-0000-0000-0000-000000000000` |
| `Email:ClientSecret` | Client-Geheimnis der App-Registrierung | leer | nur als Umgebungsvariable |
| `Email:MailboxAddress` | Postfach, in dessen Namen gesendet wird | leer | `support@example.org` |

Eine vollständige Beispieldatei mit Platzhaltern liegt unter [einstellungen.beispiel.json](einstellungen.beispiel.json).

## Sicherung und Wiederherstellung

### Was automatisch passiert

Die Anwendung legt eine Sicherung an:

- bei jedem Start, sobald mindestens ein Vorgang existiert;
- im laufenden Betrieb, sobald die jüngste Sicherung im Sicherungsordner vier Stunden alt ist; ob das so ist, prüft die Anwendung alle fünfzehn Minuten.

Eine Sicherung ist eine vollständige Kopie der Datenbankdatei, einschließlich der Konten und Passwort-Hashes. Je Ziel bleiben zehn Stände; ältere werden beim nächsten Sichern gelöscht. Ist `Daten:ZweitSicherungsOrdner` gesetzt, kopiert die Anwendung jede gelungene Sicherung zusätzlich dorthin. Fehlt der Stick, bleibt die Sicherung im Hauptordner gültig, der Reiter **Daten** zeigt unter der Zeile „Zweites Ziel" den Hinweis „Die Spiegelkopie nach … ist fehlgeschlagen", und der nächste Lauf versucht es wieder.

Eine Sicherung neben der Datenbank schützt vor einem Fehlgriff, nicht vor dem Verlust des Rechners. Richten Sie deshalb das zweite Ziel ein, bevor der Betrieb beginnt.

### Sicherung von Hand

Klicken Sie in der Verwaltung auf dem Reiter **Daten** auf **Jetzt sichern**. Die neue Datei erscheint oben in der Liste der Sicherungen, und der Reiter meldet den Pfad.

### Wiederherstellen

1. Wählen Sie auf dem Reiter **Daten** in der Liste die Sicherung, die Sie zurückspielen wollen.
2. Klicken Sie auf **Gewählte Sicherung zurückspielen** und bestätigen Sie die Rückfrage. Die Anwendung schreibt zuerst den aktuellen Stand als `vor-wiederherstellung-…db` in den Sicherungsordner, ersetzt dann die Datenbank durch die Sicherung und führt fehlende Migrationen aus, falls die Sicherung von einer älteren Version stammt.
3. Melden Sie sich neu an. Die Konten sind die aus der Sicherung.

Die Liste zeigt nur Dateien im Sicherungsordner. Eine Sicherung von einem Stick spielen Sie zurück, indem Sie sie zuerst in diesen Ordner kopieren. Die Sicherheitskopie vor dem Zurückspielen zählt zu den zehn aufbewahrten Ständen.

### Export und Import

| Knopf auf dem Reiter Daten | Was passiert |
|---|---|
| **Export als JSON speichern** | schreibt den fachlichen Bestand in eine Datei: Vorgänge mit Historie und Kommentaren, Stammdaten, Artikel mit Versionen, Kategorien, Tags und offenen Vorschlägen, Ruhezeiten. Konten und Passwörter sind nicht enthalten. |
| **Import aus JSON (ersetzt den Bestand)** | liest eine solche Datei ein und ersetzt den gesamten fachlichen Bestand. Eine Sicherung davor legt die Anwendung nicht an: Klicken Sie vor dem Import auf **Jetzt sichern**, sonst ist alles seit der letzten automatischen Sicherung verloren, wenn Sie die falsche Datei erwischen. Konten bleiben, wie sie sind. |

Der Import ist kein Abgleich: Tauschen zwei Personen Exportdateien, gewinnt der letzte Import. Der Export ist keine Sicherung: Nach einem Import auf einem frischen Rechner legt die Administration die Konten neu an. Für den Umzug eines ganzen Arbeitsplatzes nehmen Sie deshalb die Sicherung, nicht den Export.

Auf dem Reiter **Auswertung** schreibt **Tabellen als ZIP (CSV) speichern** jede Tabelle des Bestands als CSV-Datei für die Tabellenkalkulation. Felder, die mit `=`, `+`, `-` oder `@` beginnen, tragen dort ein vorangestelltes Hochkomma, damit ein Tabellenprogramm sie nicht als Formel ausführt; die Datei `LIESMICH.txt` im Archiv erklärt es.

## Update

Migrationen laufen beim ersten Start einer neuen Version von selbst und nicht rückwärts. Sichern Sie deshalb vorher.

1. Klicken Sie in der Verwaltung auf dem Reiter **Daten** auf **Jetzt sichern** und kopieren Sie die neueste Datei aus dem Sicherungsordner an einen Ort außerhalb des Rechners.
2. Schließen Sie das Hauptfenster. Damit endet die Anwendung.
3. Löschen Sie den Programmordner vollständig, statt das Archiv darüber zu entpacken: Sonst bleiben Dateien einer älteren Installation liegen (aus Vorabständen etwa `Ticketsystem.Desktop.exe` oder `appsettings.json`), und solche Reste sind die häufigste Ursache für einen Fehlstart.
4. Entpacken Sie das neue Archiv in den Ordner und starten Sie `Ticketsystem.App.exe`. Das Anmeldefenster erscheint; die Migrationen laufen davor still durch, das Protokoll nennt sie nicht. Die Daten im Profil bleiben erhalten.

Wollen Sie nach einem fehlgeschlagenen Update zur alten Version zurück, installieren Sie das alte Paket und spielen Sie die Sicherung aus Schritt 1 zurück.

## Umzug auf einen anderen Rechner

1. Klicken Sie auf dem alten Rechner in der Verwaltung auf dem Reiter **Daten** auf **Jetzt sichern** und kopieren Sie die neueste Datei aus `%LOCALAPPDATA%\Ticketsystem\Sicherungen` auf einen verschlüsselten Stick. Liegt das zweite Sicherungsziel auf dem Stick, ist sie schon dort.
2. Installieren Sie die Anwendung auf dem neuen Rechner und starten Sie sie einmal, damit der Datenordner entsteht. Schließen Sie sie wieder.
3. Legen Sie im Datenordner den Unterordner `Sicherungen` an, falls er noch fehlt, und kopieren Sie die Datei vom Stick hinein.
4. Starten Sie die Anwendung und melden Sie sich mit dem Startkonto an (Passwort aus dem Protokoll des neuen Rechners).
5. Wählen Sie in der Verwaltung auf dem Reiter **Daten** die kopierte Datei in der Liste und klicken Sie auf **Gewählte Sicherung zurückspielen**. Die Konten kommen mit.
6. Melden Sie sich mit Ihrem eigenen Konto neu an, entfernen Sie das Startkonto und tragen Sie das zweite Sicherungsziel in die Einstellungsdatei des neuen Rechners ein.

## Protokolle

Die Anwendung schreibt je Tag eine Datei `ticketsystem-JJJJ-MM-TT.log` in den Ordner `Protokoll` neben der Datenbank. Der Dateiname trägt das UTC-Datum des Starts; jeder weitere Start am selben Tag hängt an und beginnt mit der Startzeile `# Ticketsystem gestartet`. Läuft die Anwendung über Mitternacht, schreibt sie in der Datei des Starttags weiter. Dateien, die älter sind als `Desktop:ProtokollTage`, werden beim Start gelöscht.

Jede Meldung belegt zwei Zeilen: oben Zeitstempel, Stufe und Quelle (`2026-09-24 05:12:04 warn: Ticketsystem.Start[0]`), darunter eingerückt der Text. Nur die Startzeile `# Ticketsystem gestartet` und die beiden Zeilen `Protokoll:` und `Einstellungen:` beginnen nicht mit einem Zeitstempel und stehen am Zeilenanfang. Suchen Sie deshalb nach dem Text, nicht nach dem Zeilenanfang.

Die wichtigsten Meldungen:

| Zeile enthält | Bedeutung |
|---|---|
| `# Ticketsystem gestartet` | Startzeile mit der Startzeit in UTC (Weltzeit) |
| `Protokoll:` | Pfad dieser Protokolldatei |
| `Einstellungen:` | Pfad der Einstellungsdatei; fehlt sie, folgt der Zusatz „(nicht vorhanden, es gelten die Vorgaben)" |
| `Datenbank:` | Pfad der Datenbank, in Klammern die Stufe der Ablagekette, danach der Sicherungsordner |
| `Startkonto` | nur beim allerersten Start: Adresse und Passwort des Startkontos |
| `Sicherung beim Start abgelegt:` oder `Laufende Sicherung abgelegt:` | eine Sicherung wurde geschrieben |
| `Spiegelkopie in das zweite Sicherungsziel` | das zweite Ziel war nicht erreichbar; die Sicherung selbst ist gelungen |
| `Stand vor der Wiederherstellung gesichert:` | ein Zurückspielen hat begonnen |
| `gerissene SLA-Fristen gemeldet` | der Fristmelder hat Mails verschickt oder protokolliert |
| `E-Mail-Versand deaktiviert` | eine Mail wäre gesendet worden, der Versand ist aber nicht eingeschaltet |

Das Protokoll enthält keine vollständigen Kundenadressen (der Empfänger wird maskiert), aber das Startpasswort beim ersten Start und beim Import den vollen Namen der handelnden Person. Alle Zeitangaben stehen in UTC.

## Wiederkehrende Aufgaben

| Wann | Was |
|---|---|
| einmal vor dem Betrieb | BitLocker prüfen, zweites Sicherungsziel eintragen, Startkonto entfernen, Mailversand einrichten oder bewusst auslassen |
| bei einer neuen Person | Konto anlegen, Namen setzen, Passwort persönlich übergeben |
| bei Abwesenheit | Konto über **Pausieren bis** pausieren; die Pause endet am Datum von selbst, **Wieder aktivieren** beendet sie früher. Anmelden kann die Person weiterhin, sie bekommt nur keine Zuweisungen und keine Fristmeldungen |
| bei Ferien oder Betriebsruhe | Ruhezeit anlegen (Reiter **Ruhezeiten**, ab Teamleitung), damit die Fristen stillstehen |
| monatlich | prüfen, ob auf dem zweiten Sicherungsziel ein Stand von heute liegt und ob die Liste auf dem Reiter **Daten** eine Sicherung von heute zeigt |
| vor Ablauf des Client-Geheimnisses (Laufzeit im Portal) | neues Geheimnis erzeugen, `Email__ClientSecret` ändern, Anwendung neu starten, altes Geheimnis im Portal löschen, siehe [E-Mail-Versand einrichten](e-mail.md) |
| bei Weggang einer Person | Konto entfernen; die Historie behält den Namen |
| bei einer Anfrage nach Auskunft oder Löschung | [Datenschutzanleitung](datenschutz.md) |
| wenn ein Rechner den Betrieb verlässt | Programmordner, `%LOCALAPPDATA%\Ticketsystem` und das zweite Sicherungsziel löschen |

## Fehlerbehebung

| Beobachtung | Ursache | Abhilfe |
|---|---|---|
| Kein Fenster nach dem Start | Fehlstart; der Grund steht im Protokoll | Öffnen Sie die Datei des Starttags unter `%LOCALAPPDATA%\Ticketsystem\Protokoll`; die letzten Zeilen nennen den Fehler |
| Doppelklick, aber kein zweites Fenster | die Anwendung läuft schon | Suchen Sie das Fenster in der Taskleiste |
| Fehlstart nach einem Update | Reste einer älteren Installation im Programmordner, oder die alte Programmdatei war beim Entpacken noch geöffnet | Beenden Sie die Anwendung, löschen Sie den Programmordner vollständig und entpacken Sie neu |
| Anmeldung wird abgewiesen, das Passwort ist richtig | Konto nach Fehlversuchen gesperrt (Dauer in der [Referenz](../anwender/referenz.md#konten-und-passwörter)); oder das Konto hat keine Rolle | Warten; die Administration vergibt eine Rolle auf dem Reiter **Konten** |
| Das Startpasswort steht nicht mehr im Protokoll | Protokoll älter als die Aufbewahrung | Setzen Sie `SeedAgent__Password` als Umgebungsvariable, löschen Sie das Konto `agent@ticketsystem.local` aus der Tabelle `AspNetUsers` der Datenbank (Anwendung geschlossen) und starten Sie neu. Das greift nur, solange kein weiteres Administrationskonto existiert; sonst setzt ein anderes Administrationskonto das Passwort über **Passwort setzen** |
| Keine Mail kommt an | `Email:Enabled` ist nicht `true`, oder die Einrichtung ist fehlerhaft | Steht `E-Mail-Versand deaktiviert` im Protokoll, ist der Versand aus; bei 401 und 403 siehe [E-Mail-Versand einrichten](e-mail.md#fehlerbilder) |
| Fristen wirken falsch | Uhr oder Zeitzone des Rechners, oder eine Ruhezeit greift | Prüfen Sie Uhr und Zone; sehen Sie die Ruhezeiten in der Verwaltung an |
| Keine Sicherung auf dem Stick | Stick fehlte beim Sichern, oder der Pfad stimmt nicht | Der Reiter **Daten** zeigt den Fehlschlag; prüfen Sie `Daten:ZweitSicherungsOrdner` und klicken Sie auf **Jetzt sichern** |
| „disk I/O error" oder gesperrte Datenbank | zweiter Prozess, Virenscanner oder Netzlaufwerk | Die Datenbank gehört auf eine lokale Platte; SQLite auf einem Netzlaufwerk führt zu Beschädigungen |
| Fenster größer als der Bildschirm | Darstellungsgröße 130 Prozent auf einem kleinen Bildschirm | Die Person stellt unter **Einstellungen** die Darstellung auf 100 Prozent |

Für Änderungen direkt an der Datenbank eignen sich „DB Browser for SQLite" oder das Kommandozeilenwerkzeug `sqlite3`. Schließen Sie die Anwendung vorher; sie hält die Datei offen.

## Sicherheit

Was das System mitbringt: Passwörter liegen nur als Hash in der Datenbank (ASP.NET Core Identity). Es gibt keine Netzschnittstelle und keinen offenen Port; der einzige Weg nach außen ist der Mailversand über Microsoft Graph, und der ist ohne Einrichtung aus. Die Rechte an Vorgängen, Wissensdatenbank, Konten, Stammdaten, Sicherung und Austausch prüft die Fachschicht, nicht die Oberfläche; die zwei Ausnahmen stehen in der [Referenz](../anwender/referenz.md#rollen-und-rechte). Gefährliche Aktionen (Zurückspielen, Import, Konto entfernen, Stammdaten löschen) sind zweistufig, und vor jedem Zurückspielen wird der Stand davor gesichert; vor einem JSON-Import nicht, dort sichern Sie von Hand. Der Export enthält keine Konten und keine Passwort-Hashes.

Was Sie sicherstellen müssen:

- Die Datenbankdatei ist nicht verschlüsselt. Wer sie in die Hand bekommt, hat den ganzen Bestand samt Passwort-Hashes. Eine Verschlüsselung durch die Anwendung wurde geprüft und verworfen: Ein Schlüssel neben der Datei schützt nichts; ein an das Windows-Konto gebundener Schlüssel macht die Sicherungen auf einem Ersatzgerät unlesbar; eine Passphrase führt einen Totalverlust ein, den niemand zurücksetzen kann. Deshalb ist BitLocker mit PIN vor dem Systemstart Voraussetzung, und der Wiederherstellungsschlüssel gehört an einen Ort, der nicht auf dem Gerät liegt. Für den Stick des zweiten Sicherungsziels gilt dasselbe (BitLocker To Go).
- Das Startpasswort steht im Klartext im Protokoll, solange die Protokolldatei aufbewahrt wird. Ändern Sie es sofort und entfernen Sie das Startkonto.
- Das Client-Geheimnis des Mailversands ist das gefährlichste Geheimnis auf dem Rechner: Wer es hat, sendet im Namen des Postfachs. Legen Sie es nur als Umgebungsvariable ab und begrenzen Sie die App-Registrierung auf das eine Postfach, wie in [E-Mail-Versand einrichten](e-mail.md) beschrieben.
- Sicherungen, Exportdateien und die Entwurfsdatei sind Kopien personenbezogener Daten ohne eigene Verschlüsselung. Sie gehören auf verschlüsselte Datenträger und werden gelöscht, sobald ihr Zweck erfüllt ist.
- Es gibt keine Zwei-Faktor-Anmeldung und kein Protokoll über Lesezugriffe. Die Historie hält fest, wer etwas geändert hat, nicht, wer etwas angesehen hat.
- Die Programmdatei ist nicht signiert. Prüfen Sie vor der Installation, woher das Archiv stammt.

Schadsoftware unter dem angemeldeten Windows-Konto oder ein Rechneradministrator mit Speicherzugriff lassen sich in der Anwendung nicht abwehren. Dagegen helfen nur die üblichen Maßnahmen am Rechner: Standardbenutzer im Alltag, Endpunktschutz, Updates.
