# Ticketsystem

Ein Helpdesk-Ticketsystem für einen einzelnen Arbeitsplatz. Diese Datei ist für alle, die das System zum ersten Mal sehen. Sie erklärt, was es kann, wie Sie es in zehn Minuten starten und wo die weitere Dokumentation liegt.

## Was das System ist

Kunden melden Störungen per Telefon oder E-Mail. Der Helpdesk erfasst daraus einen Vorgang mit Nummer, Priorität, Fristen, Verantwortlichem und lückenloser Historie und arbeitet ihn bis zum Schließen ab. Dazu kommen ein internes Nachschlagewerk (Wissensdatenbank), Stammdaten wiederkehrender Anrufer, eine Auswertung, ein Lagebild für die Leitung und automatische Sicherungen.

Technisch ist das System ein Windows-Programm aus einer einzigen Datei. Es braucht keinen Server, keine Datenbankinstallation, keinen Browser und keine Netzverbindung. Der gesamte Datenbestand liegt in einer SQLite-Datei im Profil des angemeldeten Windows-Kontos.

Es ist für einen Arbeitsplatz gebaut: ein Rechner, ein Bestand. Mehrere Arbeitsplätze mit gemeinsamem Bestand sind nicht vorgesehen.

## Funktionsumfang

| Bereich | Was das System tut |
|---|---|
| Vorgänge | Erfassung aus Anruf oder E-Mail, Status Neu bis Geschlossen, vier Prioritäten, Zuweisung, Kommentare, Wiedervorlage, Verweis auf frühere Vorgänge (nur in Erfassung und Export, siehe Bekannte Einschränkungen), Historie jeder Änderung |
| Fristen | Reaktions- und Lösungsfrist je Priorität, Anzeige als Restzeit, Ruhezeiten für Ferien und Betriebsruhe, Fristenstand in der Statusleiste, optional Meldung per E-Mail |
| Rollen | Bearbeiter, Teamleitung, Administration mit abgestuften Rechten; Anmeldung mit E-Mail-Adresse und Passwort |
| Wissensdatenbank | Artikel mit Kategorien, Schlagworten, Versionen, Prüfzyklus und Freigabe von Vorschlägen |
| Stammdaten | Name, Rufnummer und Raum wiederkehrender Anrufer als Vorschlag bei der Erfassung |
| Leitung | Lagebild mit Kennzahlen, Auswertung über vier oder zwölf Wochen, CSV-Export |
| Betrieb | Sicherung beim Start und alle vier Stunden, Export und Import als JSON, Protokoll, Einstellungsdatei |

## Schnellstart

Das Repository enthält den Quelltext, keine fertige Programmdatei. Die Programmdatei `Ticketsystem.App.exe` bauen Sie einmal selbst; danach braucht der Rechner, auf dem sie läuft, kein installiertes .NET.

Voraussetzungen: Windows 10 oder 11, 64 Bit, und zum Bauen das [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) auf demselben oder einem anderen Windows-Rechner.

1. Laden Sie das Repository herunter (auf GitHub **Code**, **Download ZIP**, oder `git clone`) und entpacken Sie es in einen Ordner.
2. Starten Sie in diesem Ordner `Veroeffentlichen.cmd` per Doppelklick. Das Skript baut die eigenständige Programmdatei; das dauert einige Minuten. Am Ende liegen im Ordner `veroeffentlicht` die Datei `Ticketsystem.App.exe` und daneben `Ticketsystem-windows.zip`.
3. Kopieren Sie `Ticketsystem.App.exe` (oder das Archiv) auf den Helpdesk-Rechner in einen eigenen Ordner, zum Beispiel `C:\Programme\Ticketsystem`, und starten Sie die Datei. Das Anmeldefenster öffnet sich; die Warnung von Windows SmartScreen beim ersten Start erklärt das Betriebshandbuch.
4. Das Passwort des ersten Kontos steht im Protokoll. Wo genau, und wie Sie danach die Konten Ihres Teams anlegen, steht unter [Erste Anmeldung und Konten](docs/betrieb/betriebshandbuch.md#erste-anmeldung-und-konten).

Ohne eigenes SDK kommen Sie an die Programmdatei über das CI: Jeder Lauf unter **Actions** legt das Artefakt `ticketsystem-windows` ab, sieben Tage lang und nur für angemeldete GitHub-Konten. Wer den Quelltext nur ausprobieren will, startet stattdessen `Start.cmd` im Projektordner; was das Skript tut, steht im [Entwicklerhandbuch](docs/entwicklung/entwicklerhandbuch.md#die-anwendung-starten).

## Dokumentation

| Für wen | Dokument |
|---|---|
| Helpdesk, erster Tag | [Einstieg: vom ersten Anmelden bis zum geschlossenen Vorgang](docs/anwender/einstieg.md) |
| Bearbeiter | [Anleitungen für Bearbeiter](docs/anwender/bearbeiter.md) |
| Teamleitung | [Anleitungen für die Teamleitung](docs/anwender/teamleitung.md) |
| Administration | [Anleitungen für die Administration](docs/anwender/administration.md) |
| Alle Anwender | [Referenz](docs/anwender/referenz.md) (Status, Prioritäten, Rollen und Rechte, Tastatur) und [Glossar](docs/anwender/glossar.md) |
| Betrieb (Administration) | [Betriebshandbuch](docs/betrieb/betriebshandbuch.md), [Datenschutz](docs/betrieb/datenschutz.md), [E-Mail-Versand einrichten](docs/betrieb/e-mail.md) |
| Entwicklung | [Entwicklerhandbuch](docs/entwicklung/entwicklerhandbuch.md) |
| Alle | [Änderungen je Version](CHANGELOG.md) |

## Bekannte Einschränkungen

- Ein Arbeitsplatz, ein Bestand. Zwei Rechner oder zwei Windows-Konten am selben Rechner haben getrennte Bestände. Mehrere Arbeitsplätze mit gemeinsamem Bestand ließen sich nicht einschalten, sondern erforderten einen Umbau (Datenbankserver, zentrale Anmeldung).
- Kein Kundenportal und kein automatischer Postfachabruf. Kunden melden sich nicht an; das Support-Postfach wird von Hand gelesen und die E-Mail als Vorgang erfasst.
- Der Mailversand (Eingangsbestätigung, Fristmeldung) ist eingebaut, aber bisher nicht gegen ein echtes Microsoft-365-Postfach geprüft.
- Die Datenbankdatei ist nicht verschlüsselt. Betreiben Sie das System nur auf einem Rechner mit Festplattenverschlüsselung (BitLocker). Warum das so entschieden ist, steht im [Betriebshandbuch](docs/betrieb/betriebshandbuch.md#sicherheit).
- Die Programmdatei ist nicht signiert; es gibt keinen Installer und keine Selbstaktualisierung.
- Vorgänge können keine Anhänge haben. Nach außen gibt es nur den Export und Import als Datei.
- Vorgänge lassen sich nicht löschen, Kommentare und Historie nicht ändern. Ein Fehlvorgang wird ohne Lösung geschlossen und bleibt in der Ansicht **Alle** und in der Auswertung sichtbar.
- Keine Queues und keine Eskalationsstufen. Zuständigkeit läuft über die Zuweisung an Personen und die Ansichten **Unzugewiesen** und **Offen**; eskaliert wird durch Priorität erhöhen und umverteilen.
- Der Verweis auf einen früheren Vorgang wird gespeichert und exportiert, aber im Vorgang nicht angezeigt.

## Lizenz

Für dieses Projekt ist noch keine Lizenz festgelegt. Ohne Lizenz gilt das Urheberrecht; eine Weitergabe oder Änderung braucht die Zustimmung des Autors. Die verwendeten Bibliotheken stehen unter MIT- und Apache-2.0-Lizenz; die Liste steht im [Entwicklerhandbuch](docs/entwicklung/entwicklerhandbuch.md#abhängigkeiten).
