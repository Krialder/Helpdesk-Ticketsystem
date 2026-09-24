# Glossar

Dieses Glossar richtet sich an alle, die im Handbuch oder im Programm auf ein Wort stoßen, das sie nicht kennen. Jeder Eintrag erklärt das Wort in ein bis drei Sätzen; die Zahlen und Regeln dahinter stehen in der [Referenz](referenz.md).

**Administration**: Die höchste der drei Rollen. Sie darf zusätzlich zur Teamleitung Konten pflegen, Stammdaten löschen und die Daten sichern, zurückspielen, exportieren und importieren.

**Adresse**: Der Ort im Haus, an dem das Problem liegt, als Raumbezeichnung wie `A-101`, oder das Wort `extern` für Orte außerhalb. Im Programm heißt das Feld Adresse, in der Liste Ort. Die E-Mail-Adresse eines Kontos oder Absenders heißt in diesem Handbuch immer E-Mail-Adresse oder Absenderadresse.

**Agent**: In anderen Ticketsystemen die Person, die Tickets bearbeitet; hier Bearbeiter.

**Akte**: Der gespeicherte Bestand eines Vorgangs (Beschreibung, Kommentare, Historie), der sich nicht mehr ändert. Die Anzeige darüber kann sich ändern, etwa der Anzeigename einer Person.

**Angaben nachtragen**: Kundennamen, Adresse und Rückrufnummer eines bestehenden Vorgangs ändern. Jede Änderung landet in der Historie.

**Anrufer**: Die Person, die ein Problem meldet, gleich ob per Telefon oder E-Mail. Das Programm nennt sie in der Liste Kunde.

**Ansicht**: Die Auswahl im Hauptfenster, die festlegt, welche Vorgänge die Liste zeigt, etwa Offen, Überfällig oder Unzugewiesen.

**Anzeigename**: Der Name, den jede Person für sich selbst wählt und den andere auf dem Bildschirm sehen, auch in Verlauf und Historie. Gespeichert wird dort der volle Name; ändert sich der Anzeigename, ändert sich nur die Anzeige.

**Artikel**: Ein Eintrag in der Wissensdatenbank, etwa eine Anleitung zu einem wiederkehrenden Problem. Ein Artikel hat Titel, Text, Kategorie, Schlagworte, Versionen und einen Prüfzyklus.

**Auswertung**: Der erste Reiter der Verwaltung. Er zählt die Vorgänge der letzten vier oder zwölf Wochen nach Woche, Raum, Priorität und Fristzustand.

**Bearbeiter**: Die Grundrolle. Bearbeiter erfassen Vorgänge, übernehmen unzugewiesene, bearbeiten ihre eigenen und schlagen Wissensartikel vor. Das Wort bezeichnet zugleich die Person, der ein Vorgang zugewiesen ist.

**Datenordner**: Der Ordner, in dem die Datenbankdatei liegt, in der Regel `%LOCALAPPDATA%\Ticketsystem`. Daneben liegen die Sicherungen, das Protokoll und der Entwurf; die Einstellungsdatei liegt immer im Profilordner `%LOCALAPPDATA%\Ticketsystem`.

**Eingangsbestätigung**: Die E-Mail mit der Vorgangsnummer, die ein Kunde nach der Erfassung eines E-Mail-Vorgangs bekommt. Sie geht nur hinaus, wenn der Mailversand eingerichtet ist.

**Einstellungsdatei**: Die Datei `einstellungen.json` im Profilordner `%LOCALAPPDATA%\Ticketsystem`, mit der die Administration Vorgaben wie Sicherungsordner oder Fristen ändert. Ohne die Datei gelten die Vorgaben.

**Entwurf**: Zwei Bedeutungen. In der Erfassung: eine angefangene, nicht abgeschickte Erfassung, die das Programm beim Schließen des Fensters aufbewahrt und beim nächsten Öffnen anbietet. In der Wissensdatenbank: ein Artikel, der noch nicht veröffentlicht ist, aber schon lesbar.

**Erfassung**: Das Fenster, in dem aus einem Anruf oder einer E-Mail ein Vorgang wird. Es öffnet sich über **Neues Ticket** oder Strg+N.

**Escape**: Die Taste oben links auf der Tastatur. Sie schließt das aktuelle Neben- oder Dialogfenster, siehe [Tastatur](referenz.md#tastatur).

**Eskalation**: Eine eigene Eskalationsstufe gibt es nicht. Der Weg ist, die Priorität zu erhöhen, den Vorgang umzuverteilen und den Grund zu kommentieren, siehe [Anleitungen für die Teamleitung](teamleitung.md#eskalation).

**Export**: Das Schreiben des Bestands in eine Datei: als JSON für Umzug und Wiederherstellung (Administration) oder als ZIP mit CSV-Tabellen für die Tabellenkalkulation (Teamleitung).

**Fehlvorgang**: Ein versehentlich oder doppelt angelegter Vorgang. Weil Löschen nicht vorgesehen ist, wird er ohne Lösung geschlossen.

**Freigabe**: Die Entscheidung der Teamleitung über einen Vorschlag zur Wissensdatenbank: übernehmen oder verwerfen.

**Frist**: Der Zeitpunkt, bis zu dem etwas passiert sein muss. Jeder Vorgang hat eine Reaktionsfrist (bis jemand den Vorgang übernimmt) und eine Lösungsfrist (bis er gelöst ist). Beide legt die Priorität fest.

**Fristenstand**: Die Zeile unten rechts im Hauptfenster, die zählt, wie viele Vorgänge überfällig, bald fällig oder zur Wiedervorlage fällig sind.

**Fristmeldung**: Die E-Mail an Teamleitung und Administration, wenn eine Frist gerissen ist. Jeder Vorgang wird höchstens einmal gemeldet; ohne eingerichteten Mailversand gibt es sie nicht.

**Fristriss**: Eine Frist, die abgelaufen ist, bevor die Reaktion oder Lösung kam. Die Auswertung zählt die Zustände Überfällig und Verspätet erfüllt als Fristriss.

**Gelöst**: Der Status, in dem die Lösung eingetragen ist und der Kunde noch bestätigen kann. Von hier geht es zurück nach In Arbeit oder weiter nach Geschlossen.

**Geschlossen**: Der letzte Status. Ein geschlossener Vorgang nimmt keine Änderung mehr an. Meldet sich der Kunde später erneut, legen Sie einen neuen Vorgang mit Verweis auf den alten an.

**Historie**: Die Liste aller Änderungen eines Vorgangs mit Zeitpunkt, handelnder Person, altem und neuem Wert. Sie lässt sich nicht bearbeiten.

**Import**: Das Einlesen einer JSON-Exportdatei. Der Import ersetzt den fachlichen Bestand, er fügt nichts hinzu; Konten bleiben unberührt.

**Karte**: Ein umrandeter Kasten mit Überschrift, etwa die Karten **Status** oder **Bearbeiter** rechts im Detailfenster. Jede Karte zeigt eine Angabe und die Knöpfe, die sie ändern.

**Kategorie**: Die Ordnung der Wissensdatenbank in Themen. Jeder Artikel hat höchstens eine Kategorie; Schlagworte ergänzen sie.

**Kommentar**: Ein Beitrag zum Verlauf eines Vorgangs, für das Team gedacht. Kommentare lassen sich nach dem Senden nicht ändern und nicht löschen.

**Konto**: Der Zugang einer Person: E-Mail-Adresse, Passwort, voller Name, Anzeigename und Rolle. Konten legt die Administration an.

**Kontokarte**: Das Fenster, das sich beim Klick auf einen Namen öffnet und die Person mit Rolle, Pause und ihren offenen Vorgängen zeigt.

**Lagebild**: Das Fenster für Teamleitung und Administration mit Kennzahlen zu den offenen Vorgängen: Kacheln, Ring nach Priorität, Balken nach Status und Alter.

**Lösung**: Der Satz, der beim Setzen auf Gelöst festhält, was das Problem behoben hat. Er wird als Kommentar „Lösung: …" gespeichert und ist der Rohstoff für einen Wissensartikel.

**Mitarbeiterrolle**: Jede der drei Rollen Bearbeiter, Teamleitung und Administration. Ein Konto ohne eine davon kommt nicht durch die Anmeldung.

**Nur meine**: Der Umschalter im Hauptfenster, der die Liste auf die Vorgänge einschränkt, die Ihnen zugewiesen sind.

**Pause**: Eine eingetragene Abwesenheit eines Kontos bis zu einem Datum. Ein pausiertes Konto bekommt keine Zuweisungen und keine Fristmeldungen, kann sich aber anmelden.

**Pflichtangabe**: Ein Feld, ohne das ein Vorgang nicht angelegt wird; welche das sind, steht unter [Eingaberegeln](referenz.md#eingaberegeln).

**Priorität**: Die Dringlichkeit eines Vorgangs in vier Stufen: Niedrig, Mittel, Hoch, Kritisch. Sie legt die beiden Fristen fest.

**Protokoll**: Die Textdatei, in die das Programm schreibt, was es tut und wo es scheitert. Sie liegt im Ordner `Protokoll` im Datenordner, eine Datei je Tag; mehrere Starts an einem Tag schreiben in dieselbe Datei.

**Prüfzyklus**: Die Zahl der Tage, nach denen ein Wissensartikel die Marke Prüfung fällig bekommt, damit jemand nachsieht, ob er noch stimmt. Vorgabe sind 180 Tage.

**Quelle**: Der Weg, auf dem eine Anfrage kam: Telefon oder E-Mail. Die Quelle entscheidet, welche Felder die Erfassung verlangt.

**Queue**: In anderen Ticketsystemen eine Warteschlange oder Gruppe als Ablageort und Zuständigkeit. Hier gibt es keine Queues; Zuständigkeit läuft über die Zuweisung an eine Person, und die Ansichten **Unzugewiesen** und **Offen** ersetzen den Blick in die Warteschlange.

**Requester**: In anderen Ticketsystemen die Person, die eine Anfrage stellt; hier Anrufer oder Kunde.

**Restzeit**: Die Zeit, die bis zur Frist noch bleibt, in der Liste als „noch 7 h 59 min"; nach Ablauf steht dort, seit wann die Frist gerissen ist.

**Rolle**: Die Rechtestufe eines Kontos: Bearbeiter, Teamleitung oder Administration. Jede Stufe schließt die Rechte der vorigen ein.

**Ruhezeit**: Ein Zeitraum wie Ferien oder Betriebsruhe, in dem die Fristen stillstehen. Ruhezeiten legt die Teamleitung in der Verwaltung an.

**Schlagwort (Tag)**: Ein frei vergebbares Stichwort an einem Wissensartikel. Ein Artikel kann mehrere Schlagworte tragen; die Wissensdatenbank filtert danach.

**Sicherung**: Eine Kopie der Datenbankdatei im Ordner `Sicherungen`. Das Programm legt sie beim Start und alle vier Stunden an; die Administration kann sie zurückspielen.

**SLA**: Kurz für Service Level Agreement, die Zusage, in welcher Zeit reagiert und gelöst wird. Im Programm steht das Wort für die Fristen je Priorität und ihre Zustände.

**Stammdaten**: Name, zuletzt genannte Rufnummer und zuletzt genutzter Raum wiederkehrender Anrufer. Die Erfassung schlägt sie beim Tippen des Namens vor. Auf dem Reiter **Daten** der Verwaltung heißen sie Kontakte.

**Startkonto**: Das Administrationskonto `agent@ticketsystem.local`, das die Anwendung beim ersten Start anlegt, damit sich überhaupt jemand anmelden kann. Nach dem Anlegen eigener Konten entfernt es die Administration von Hand.

**Status**: Die Stufe, auf der ein Vorgang steht: Neu, Zugewiesen, In Arbeit, Gelöst oder Geschlossen.

**Statusleiste**: Die Zeile am unteren Rand des Hauptfensters mit dem Zähler der Vorgänge links und Name, **Abmelden** und Fristenstand rechts.

**Teamleitung**: Die mittlere Rolle. Sie sieht alle Vorgänge, weist zu, nutzt das Lagebild und die Auswertung, pflegt Ruhezeiten und gibt die Wissensdatenbank frei.

**Ticket**: Ein anderes Wort für Vorgang. Das Programm benutzt beide.

**Überfällig**: Der Fristzustand eines offenen Vorgangs, dessen Frist abgelaufen ist. In der Liste als `!` in Gefahrfarbe.

**Unzugewiesen**: Ein Vorgang ohne Bearbeiter. Die gleichnamige Ansicht zeigt auch gelöste und geschlossene; die Lagebild-Kachel **Nicht zugewiesen** zählt nur offene. Einen offenen unzugewiesenen Vorgang sieht jeder Bearbeiter und kann ihn sich zuweisen.

**UTC**: Die Weltzeit, in der Protokoll und Auswertung rechnen; die deutsche Zeit ist ihr eine (Winter) oder zwei (Sommer) Stunden voraus: 0:00 UTC ist 1:00 oder 2:00 Uhr.

**Verlauf**: Die linke Seite des Detailfensters: die Beschreibung als erster Beitrag, dann Kommentare und Änderungen in Zeitfolge.

**Version**: Ein gespeicherter Stand eines Wissensartikels. Jedes Speichern, das Titel, Text, Kategorie, Tags oder Veröffentlichungsstand ändert, legt eine neue an; die Teamleitung kann auf eine ältere zurückgehen.

**Verwaltung**: Das Fenster mit den Reitern Auswertung, Ruhezeiten, Konten, Stammdaten und Daten. Es lässt sich erst ab der Rolle Teamleitung öffnen; die Reiter Konten, Stammdaten und Daten sieht nur die Administration.

**Verweis**: Die Nummer eines früheren Vorgangs, die bei der Erfassung eingetragen wird, wenn dasselbe Problem wiederkommt. Der Verweis wird am neuen Vorgang gespeichert und steht im CSV-Export; das Detailfenster zeigt ihn bisher nicht an.

**Voller Name**: Nachname und Vorname einer Person, wie die Administration sie setzt. Er wird in jeder Historie gespeichert; die Person selbst kann ihn nicht ändern.

**Vorgang**: Eine erfasste Anfrage mit Nummer, Anrufer, Titel, Beschreibung, Priorität, Status, Bearbeiter, Fristen, Kommentaren und Historie. Das Programm nennt ihn auch Ticket.

**Vorschlag**: Ein neuer Artikel oder eine Änderung, die ein Bearbeiter für die Wissensdatenbank einreicht. Die Teamleitung übernimmt oder verwirft ihn unter **Freigaben**.

**Wiedervorlage**: Ein Termin mit Grund an einem Vorgang, damit er zu diesem Zeitpunkt wieder auffällt. Fällige Wiedervorlagen stehen in der gleichnamigen Ansicht und im Fristenstand.

**Wissensdatenbank**: Das Nachschlagewerk des Helpdesks mit Artikeln, Kategorien und Schlagworten. Es öffnet sich über **Wissen** im Hauptfenster.

**Workflow**: Es gibt genau einen festen Statusablauf, siehe [Status](referenz.md#status); eigene Abläufe lassen sich nicht anlegen.

**Zugewiesen**: Der Status, sobald eine Person für den Vorgang verantwortlich ist, aber noch nicht begonnen hat. Zugleich das Wort für den Vorgang, der jemandem gehört.

**Zuweisung**: Die Verbindung zwischen einem Vorgang und der Person, die ihn bearbeitet. Bearbeiter weisen sich selbst zu, die Teamleitung weist jede Person zu oder hebt die Zuweisung auf.

**Zweites Sicherungsziel**: Ein zweiter Ordner, in den jede Sicherung zusätzlich kopiert wird, etwa ein verschlüsselter Stick oder ein Netzlaufwerk. Er wird über die Einstellungsdatei eingerichtet.
