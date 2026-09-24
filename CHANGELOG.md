# Änderungen je Version

Diese Datei richtet sich an alle, die wissen wollen, was sich zwischen zwei Versionen des Ticketsystems geändert hat. Sie folgt dem Format von [Keep a Changelog](https://keepachangelog.com/de/1.1.0/) und der [semantischen Versionierung](https://semver.org/lang/de/): Die erste Zahl steigt bei Änderungen, die bestehende Daten oder Abläufe brechen, die zweite bei neuen Funktionen, die dritte bei Fehlerbehebungen.

## [1.0.0] - 2026-09-24

Erste Veröffentlichung; der Eintrag beschreibt den vollständigen Funktionsumfang. Die Versionsnummer steht in den Dateieigenschaften von `Ticketsystem.App.exe`.

### Hinzugefügt

- Vorgänge aus Anruf oder E-Mail erfassen mit Titel, Beschreibung, Adresse, Priorität, Rückrufnummer oder Absenderadresse, Gesprächsnotiz und Verweis auf einen früheren Vorgang. Eine unterbrochene Erfassung bleibt als Entwurf erhalten.
- Status Neu, Zugewiesen, In Arbeit, Gelöst und Geschlossen mit festen Übergängen; Geschlossen ist endgültig. Lösungstext beim Setzen auf Gelöst.
- Vier Prioritäten mit Reaktions- und Lösungsfrist, Anzeige als Restzeit mit Zeichen, Farbe und Balken; Ruhezeiten lassen Fristen stillstehen; Fristenstand in der Statusleiste.
- Zuweisung und Aufhebung der Zuweisung; eine Ansicht zeigt die Vorgänge pausierter Konten. Kommentare, Wiedervorlage und Nachtragen von Angaben; jede Änderung steht mit der handelnden Person in der Historie.
- Vorgangsliste mit Ansichten, Prioritätsfilter, Suche, Sortierung und Merken der Einstellungen je Konto; Fenster für die Vorgänge einer Person oder eines Raums; Kontokarte.
- Drei Rollen (Bearbeiter, Teamleitung, Administration) mit Rechteprüfung in der Fachschicht; Anmeldung mit E-Mail-Adresse und Passwort, Sperre nach fünf Fehlversuchen; Startkonto mit zufälligem Passwort beim ersten Start.
- Wissensdatenbank mit Artikeln, Kategorien, Schlagworten, Auszeichnung, Versionen mit Rückweg, Prüfzyklus, Vorschlägen der Bearbeiter und Freigabe durch die Teamleitung; Artikelentwurf aus einem Vorgang.
- Stammdaten wiederkehrender Anrufer mit Vorschlag bei der Erfassung und Löschweg in der Verwaltung.
- Lagebild mit Kennzahlen, Verteilung nach Priorität, Status und Alter; Auswertung über vier oder zwölf Wochen mit CSV-Export.
- Verwaltung von Konten (Anlegen, Namen, Rolle, Passwort, Pause, Entfernen), Ruhezeiten, Stammdaten und Daten.
- Die Datenbank wird beim Start und alle vier Stunden gesichert, auf Wunsch zusätzlich an ein zweites Ziel; Sicherungen lassen sich zurückspielen, der Bestand als JSON exportieren und importieren. Die Verwaltung schreibt eine Einstellungsdatei als Vorlage; das Protokoll entsteht je Tag.
- Optionaler Mailversand über Microsoft 365 für Eingangsbestätigungen und Fristmeldungen.
- Farbschema Dunkel und Hell, drei Darstellungsgrößen, Hinweise beim Überfahren an jedem Bedienelement, Tastenkürzel; eigenes Passwort und Anzeigename unter Einstellungen.
- Auslieferung als eigenständige Windows-Programmdatei; Start aus dem Quelltext per Doppelklick; CI mit Fachtests, Sichtprobe und Startprobe unter Linux und Windows.
- Dokumentation: README, Anwenderhandbuch je Rolle mit Referenz und Glossar, Betriebshandbuch mit Datenschutzanleitung und Mailanleitung, Entwicklerhandbuch.

### Bekannte Einschränkungen

- Ein Arbeitsplatz, ein Bestand; kein Mehrplatzbetrieb.
- Der Mailversand ist gegen kein echtes Microsoft-365-Postfach geprüft.
- Kein Kundenportal, kein Postfachabruf, keine Anhänge, keine Verschlüsselung der Datenbankdatei, keine signierte Programmdatei.
- Vorgänge lassen sich nicht löschen, Kommentare nicht ändern; keine Queues, keine Eskalationsstufen.
- Vor einem JSON-Import legt die Anwendung keine Sicherung an.
- Der Verweis auf einen früheren Vorgang wird gespeichert und exportiert, aber nicht angezeigt.
