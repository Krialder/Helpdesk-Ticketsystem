# Entwicklerhandbuch

Dieses Handbuch richtet sich an Entwicklerinnen und Entwickler, die den Quelltext bauen, testen, ändern oder erweitern wollen. Danach können Sie das Projekt auf Ihrem Rechner bauen und testen, wissen, warum es so aufgebaut ist, wie es ist, wo eine Änderung hingehört und welche Regeln ein Beitrag einhalten muss.

## Entwicklungsumgebung

Voraussetzungen:

- .NET SDK 8 (geprüft mit 8.0.131). Unter Windows, Linux oder macOS; die Oberfläche lässt sich überall bauen und ohne Bildschirm rendern, weil das Projekt keinen Windows-Zielrahmen setzt.
- Git.
- Für das Auslieferungspaket und für die Bedienung mit Fenstern: Windows 10 oder 11, 64 Bit. Unter Linux laufen Bau, Tests, Sichtprobe und Startprobe, aber kein sichtbares Fenster.

Ein Editor mit C#- und AXAML-Unterstützung hilft (Visual Studio 2022, Rider, VS Code mit C# Dev Kit), ist aber keine Voraussetzung; alles unten läuft auf der Befehlszeile.

### Bauen und testen

```bash
git clone https://github.com/Krialder/Ticketsystem.git
cd Ticketsystem
dotnet restore
dotnet build --configuration Release
dotnet test --no-build --configuration Release
```

Der Bau muss ohne Warnung durchlaufen: Alle drei Projekte setzen `TreatWarningsAsErrors`, eine Warnung ist ein Baufehler. Der Testlauf braucht keinen Bildschirm; die Fenstertests laufen über Avalonia.Headless.

Beim Bauen sendet das Avalonia-Paket `Avalonia.BuildServices` anonyme Telemetrie an `avaloniaui.net`. In einem abgeschotteten Netz sehen Sie deshalb abgewiesene Verbindungen; abschalten lässt sich das mit der Umgebungsvariablen `AVALONIA_TELEMETRY_OPTOUT=1`.

### Die Anwendung starten

```bash
dotnet run --project src/Ticketsystem.App
```

Ohne weitere Angaben legt der Start eine Datenbank im Profil des angemeldeten Kontos an und schreibt das Passwort des Startkontos ins Protokoll. Für eine Wegwerfdatenbank mit bekanntem Passwort setzen Sie vorher Umgebungsvariablen:

```bash
export ConnectionStrings__Default="Data Source=/tmp/ticketsystem-dev/app.db"
export SeedAgent__Password='Entwicklung-1!'
export Daten__SicherungBeimStart=false
dotnet run --project src/Ticketsystem.App
```

Welche Schlüssel es gibt, steht in der [Konfigurationsreferenz](../betrieb/betriebshandbuch.md#konfigurationsreferenz); die Schichtenfolge (Vorgaben, Einstellungsdatei, Umgebungsvariablen, Befehlszeile) gilt auch für `dotnet run`. Setzen Sie die Variablen nur für den Start, nicht für `dotnet test`: Zwei Tests in `EinstellungenTests` prüfen die Vorgaben gegen die echte Umgebung und schlagen fehl, solange `ConnectionStrings__Default` oder `Daten__SicherungBeimStart` gesetzt sind. Unter Windows startet `Start.cmd` im Projektordner dasselbe und installiert vorher bei Bedarf ein lokales .NET 8 in den Ordner `.dotnet`, ohne Administratorrechte.

### Sichtprobe und Startprobe

Zwei Probeläufe fahren die Anwendung ohne Bildschirm hoch und sind dieselben, die das CI ausführt.

```bash
dotnet run --project src/Ticketsystem.App -- --sichtprobe bilder agent@ticketsystem.local 'Entwicklung-1!'
dotnet run --project src/Ticketsystem.App -- --startprobe 8
```

Die Sichtprobe rendert jedes Fenster über Skia in den angegebenen Ordner (Vorgabe `sichtprobe-app`): ohne Anmeldedaten nur das Anmeldefenster, mit Anmeldedaten alle 17 Bilder von `anmeldefenster.png` bis `wissen-bearbeiten.png`, mit Beispieldaten in einer leeren Datenbank. Die Startprobe fährt die Anwendung mit Desktop-Lebensdauer und Nachrichtenschleife hoch, wartet die angegebenen Sekunden (Vorgabe 5), gibt „Startprobe: Hauptfenster steht" aus und fährt herunter. Beide enden mit Exit-Code 0, die Startprobe auch dann, wenn kein Hauptfenster stand; ob sie gelungen ist, sagt allein ihre Ausgabe. Das CI prüft deshalb zusätzlich zum Exit-Code die Ausgabe. Die Bilder im Anwenderhandbuch stammen aus der Sichtprobe.

### Migrationen

Das Datenbankschema verwaltet EF Core über Migrationen im Ordner `src/Ticketsystem.Kern/Migrations`; beim Start bringt `Database.Migrate()` jede vorhandene Datenbank auf den aktuellen Stand. Ändern Sie eine Entität, erzeugen Sie eine Migration:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Ticketsystem.Kern
```

Das Werkzeug kommt aus `.config/dotnet-tools.json`. Es baut den Kontext über `EntwurfszeitFabrik` mit der Datei `entwurfszeit.db`, die im Betrieb nie benutzt wird. Prüfen Sie die erzeugte Migration, bevor Sie sie einchecken, und ergänzen Sie in `MigrationTests` einen Fall, wenn bestehende Daten umgeschrieben werden.

### Auslieferungspaket bauen

```bash
dotnet publish src/Ticketsystem.App --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none --output veroeffentlicht
```

Unter Windows tut `Veroeffentlichen.cmd` dasselbe und packt das Ergebnis zusätzlich als `Ticketsystem-windows.zip`. Das Paket bringt die Laufzeit mit, damit auf dem Zielrechner kein .NET installiert sein muss. Das kostet über 100 MB, erspart aber die Vorbedingung, an der eine Verteilung sonst als Erstes scheitert.

## Architektur

### Zwei Projekte und ein Testprojekt

| Projekt | Inhalt |
|---|---|
| `src/Ticketsystem.Kern` | alles, was entscheidet: Domäne, Dienste, Presenter, Datenzugriff, Start, Mailversand, Migrationen. Keine Abhängigkeit auf Avalonia. |
| `src/Ticketsystem.App` | die Avalonia-Oberfläche: Fenster, Stil, Programmstart, Sicht- und Startprobe |
| `tests/Ticketsystem.Tests` | xUnit-Tests gegen beide Projekte |

Die Fenster folgen dem Muster Humble View: Sie binden Werte, leiten Klicks weiter und blenden nach Flaggen ein und aus. Was angezeigt wird und was erlaubt ist, entscheiden Presenter und Dienste im Kern, damit jede Regel ohne Fenster testbar ist. Der Kern verweigert einen Verstoß mit einer Ausnahme, auch wenn ein Fenster den Knopf gar nicht anbietet. Drei Stellen halten das noch nicht ein; sie stehen unter den offenen Punkten: die Wissensfenster (`DarfPflegen`), das Lagebild (`FristenuebersichtAsync` verlangt nur ein Mitarbeiterkonto) und das Anlegen der Einstellungsdatei (nur das Fenster sperrt).

### Entscheidungen und ihre Gründe

| Entscheidung | Grund |
|---|---|
| Ein Arbeitsplatz, SQLite-Datei, kein Server | Der Zweck ist ein einzelner Helpdesk-Rechner. Eine Datei braucht keine Installation, keinen Dienst und keine Netzfreigabe; Sicherung ist Kopieren. Mehrplatzbetrieb wäre ein Umbau, kein Schalter. |
| Zweiter Start wird beendet | Ein Mutex `Local\Ticketsystem.Desktop` lässt je Windows-Sitzung einen Prozess zu. Zwei Prozesse auf derselben SQLite-Datei wären zwei Wahrheiten. |
| Datenbank im Profil, mit Rückfallkette | `%LOCALAPPDATA%` ist auf manchen Rechnern leer; deshalb die Kette Konfiguration, Profil, Benutzerordner, Programmordner. Neben dem Programm zu liegen wäre bei einem Update tödlich, darum steht der Programmordner am Ende der Kette und mit Warnung im Protokoll. |
| Einstellungsdatei nur als Vorlage aus der Verwaltung | Eine Datei, die von selbst entsteht, wird von selbst falsch. Die Vorlage schreibt die geltenden Werte und wird nie überschrieben. Geheimnisse gehören in Umgebungsvariablen. |
| ASP.NET Core Identity, nur der Kern | Passwort-Hashing, Rollen, Sperrzähler und Passwortregeln sind geprüfter Code; der `SignInManager` und alles mit Cookies fehlen, weil es keinen Browser gibt. Den Sperrzähler führt `AnmeldeDienst` selbst. |
| Zwei Namen je Konto | Der volle Name (Nachname, Vorname) ist für den Inhaber unveränderlich, nur die Administration setzt ihn; er wird in Historie und Bearbeiterfeld gespeichert. Der Anzeigename gehört dem Inhaber und wird überall angezeigt. Stünde der Anzeigename in der Akte, schriebe jede Umbenennung die Vergangenheit um. Die Anzeige löst gespeicherte Kennungen über das `Namensverzeichnis` auf den heutigen Anzeigenamen auf. |
| Sichtbarkeit an genau einer Stelle | `TicketService.Sichtbar` filtert jede Leseabfrage nach der Rechtematrix. Nicht sichtbar und nicht vorhanden geben dieselbe Antwort, damit sich aus ihr keine Nummern ableiten lassen. |
| Historie in der Fachschicht | Jede Änderung schreibt ihren Eintrag im Dienst, nicht im Fenster, damit kein Fenster die Historie vergessen kann. Die eine Lücke im Dienst selbst (`CreateAsync`) steht unter den offenen Punkten. |
| Fristen als reine Funktionen | `SlaEvaluation` und `SlaRechner` bekommen die Jetzt-Zeit hereingereicht. Jeder Randfall (Ruhezeit über Mitternacht, Prioritätswechsel) ist ohne Uhr testbar. |
| Eigene Auszeichnung statt Markdown-Bibliothek | Sechs Schreibweisen reichen für Anleitungen. Eine Bibliothek zöge acht Pakete nach und renderte, was niemand meint. Gespeichert bleibt reiner Text, deshalb bleiben Suche, Versionen und Sicherung unberührt. |
| Mail nur senden, kein Postfachabruf | Der Abruf bräuchte `Mail.Read` auf das ganze Postfach und einen Regelapparat für Spam und Antworten. Senden mit `Mail.Send` und einer ApplicationAccessPolicy ist die kleinste Berechtigung, die den Nutzen bringt. Ohne `Email:Enabled` schreibt ein `LoggingTicketMailer` ins Protokoll statt zu senden. |
| Datenbank unverschlüsselt | Eine Verschlüsselung in der Anwendung hätte einen Schlüssel, der auf demselben Rechner liegt. Der Schutz gegen Diebstahl ist BitLocker, dokumentiert als Voraussetzung im Betriebshandbuch. |
| Eine Farbquelle, zwei Schriften | `Palette` (mit den beiden Schemata in `Farbschema`) und `Schriften` sind die einzigen Stellen, an denen Farben und Schriften stehen; XAML verweist nur auf Ressourcen. `PaletteTests` rechnet für jede Paarung das Kontrastverhältnis nach. |
| Kein Windows-Zielrahmen | `net8.0` statt `net8.0-windows`, damit Bau, Tests und Sichtprobe unter Linux laufen und das CI die Fenster wirklich zeichnet. |
| Konten und Vorgänge im Export getrennt | Der JSON-Export enthält keine Konten und keine Passwort-Hashes; ein Import ersetzt den fachlichen Bestand und lässt die Konten stehen. So kann ein Export weitergegeben werden, ohne Zugänge zu verraten. |

### Der Start

`Programm.Main` hängt sich zuerst an eine vorhandene Konsole, damit `--sichtprobe` und `--startprobe` aus einem Terminal Ausgabe zeigen. Danach läuft der Start in dieser Reihenfolge ab: Er setzt die Kultur `de-DE`, prüft den Mutex, hängt die Einstellungsdatei an die Konfiguration, richtet die Protokolldatei ein, verdrahtet die Dienste über `KernDienste.Registrieren`, bereitet mit `Startablauf.DatenbankVorbereitenAsync` die Datenbank vor (Migrationen, Rollen und Startkonto, Sicherung beim Start), startet den Host und geht dann in die Sichtprobe, die Startprobe oder die Avalonia-Schleife. Die Migrationen laufen still; das Protokoll nennt sie nicht. Exit-Codes: 0 bei Erfolg und bei einer zweiten Instanz, 1 bei einem Fehler nach dem Einrichten des Protokolls (der Grund steht dort), 2 davor (der Grund steht nur auf der Konsole).

`KernDienste.Registrieren` ist die eine Dienstverdrahtung für Anwendung und Tests. Dort hängen auch die zwei Hintergrunddienste: `SlaMelderService` prüft im konfigurierten Takt auf gerissene Fristen, `SicherungsDienst` fragt alle 15 Minuten, ob eine Sicherung fällig ist.

## Struktur

| Ordner | Inhalt |
|---|---|
| `src/Ticketsystem.Kern/Domain` | Entitäten (`Ticket`, `KbArticle`, `Kontakt`, `AppUser`, `Ruhezeit`), Aufzählungen mit deutschen Anzeigenamen (`TicketEnums`), Rollen, Fristlogik (`Sla`), Eingaberegeln (`AdressRegel`, `RufnummerRegel`), Namensregeln (`Kontenname`) |
| `src/Ticketsystem.Kern/Services` | die Dienste mit der Rechtematrix: `TicketService`, `KnowledgeBaseService`, `KontenDienst`, `AnmeldeDienst`, `StammdatenService`, `RuhezeitService`, `AuswertungService`, `SicherungService`, `AustauschService` (JSON und CSV), `SlaMelder`, `BestaetigungsMelder`, `Namensverzeichnis`, `ZustandService` |
| `src/Ticketsystem.Kern/Praesentation` | Presenter, die Daten für Fenster aufbereiten und Darf-Flaggen liefern: `TicketlistenPresenter`, `TicketdetailPresenter`, `Erfassung`, `Fristanzeige`, `Fristenblick` (Lagebild), `KontokartePresenter` (Datei `Kontokarte.cs`), `WissensPresenter`, `FassungenPresenter`, `Wissenstext` (Auszeichnung) |
| `src/Ticketsystem.Kern/Data` | `TicketsystemContext` (EF Core), `Datenablage` (wo die Datenbank liegt), `IdentitySeed` (Rollen, Startkonto), `EntwurfszeitFabrik` (nur für `dotnet ef`) |
| `src/Ticketsystem.Kern/Start` | `KernDienste`, `Startablauf`, `Einstellungsablage`, `Protokolldatei`, `Protokollstufen`, `Elternkonsole`, `Demodaten` |
| `src/Ticketsystem.Kern/Email` | `ITicketMailer` mit `GraphTicketMailer` und `LoggingTicketMailer` |
| `src/Ticketsystem.Kern/Migrations` | 27 EF-Core-Migrationen und der Schema-Schnappschuss |
| `src/Ticketsystem.App/Fenster` | ein Paar aus `.axaml` und `.axaml.cs` je Fenster: `AnmeldeFenster`, `Grundfenster` (Hauptfenster), `ErfassungsFenster`, `DetailFenster`, `LagebildFenster`, `WissensFenster` mit `WissensBearbeitenDialog`, `FreigabenDialog`, `FassungenDialog` und `OrdnungDialog`, `VerwaltungsFenster`, `KontoFenster`, `VorgangslisteFenster`, `EinstellungenDialog`; dazu `Vorgangsfilter` |
| `src/Ticketsystem.App/Stil` | `Palette`, `Farbschema`, `Schriften`, `Darstellung` (Größenstufe und Einpassen), `Hinweise` (Tooltips), `Nebenfenster` (Escape schließt), `Bestaetigung` (Knopf mit Rückfrage), `Fristmesser` (Balken), `Wissenstextanzeige`, `Befehl`, `Farbwandler` |
| `src/Ticketsystem.App` | `Programm`, `App.axaml`, `Sichtprobe`, `Startprobe`, `Entwurfsablage` (Erfassungsentwurf als JSON), `Fehlerdialog` (Win32-Dialog, wenn Avalonia nicht hochkommt), `app.manifest` |
| `tests/Ticketsystem.Tests` | die Tests, dazu `KernWirt` (Host mit Temp-Datenbank), `TestDaten` (ein Akteur je Rolle), `Messen` (Geometrie im Fensterkoordinatensystem) |
| `scripts`, `Start.cmd`, `Veroeffentlichen.cmd` | Start und Auslieferung unter Windows per Doppelklick |
| `.github/workflows/ci.yml` | das CI |
| `docs` | Anwender-, Betriebs- und Entwicklerdokumentation |

## Tests

Die Tests laufen mit xUnit; Fenstertests nutzen `Avalonia.Headless.XUnit` und das Attribut `[AvaloniaFact]`. `KernWirt` baut je Test einen Host mit derselben Verdrahtung wie die Anwendung über einer eigenen Temp-Datenbank samt Migrationen und Seed, damit ein Test dieselben Dienste sieht wie ein Nutzer.

Vier Arten von Tests:

- Diensttests belegen die Regeln: Rechtematrix (`RollenRechteTests` und weitere; nicht jede Zeile der Matrix hat schon einen eigenen Fall, siehe offene Punkte), Statusübergänge (`TicketLifecycleTests`), Fristen und Ruhezeiten (`SlaTests`, `SlaRuhezeitTests`), Konten (`KontenDienstTests`, `BetriebshaertungTests`), Austausch (`DatenTests`), Nebenläufigkeit zweier Fenster auf demselben Vorgang (`NebenlaeufigkeitTests`).
- Fenstertests öffnen ein Fenster headless, setzen Eingaben und lesen Meldungen (`ErfassungsFensterTests`, `DetailFensterTests`, `VerwaltungsFensterTests`, `WissensFensterTests`). Dafür sind die per `x:Name` erzeugten Felder der Fenster für das Testprojekt sichtbar (`InternalsVisibleTo`).
- Wächtertests halten Zusagen der Oberfläche fest, die sonst schleichend kippen: Jedes bedienbare Element hat einen Hinweis (`HinweiseUeberallTests`), jede Farbpaarung hält WCAG AA (`PaletteTests`), Farbe bedeutet nur Druck (`GestaltTests`), gleiche Wirkung sieht gleich aus (`KnopfgewichtTests`, `GefahrknopfTests`), Maße folgen einer Leiter (`LeiternTests`), was sichtbar sein muss, ist im gerenderten Bild sichtbar (`SichtbarkeitTests`), eine Farbquelle (`FarbquelleTests`).
- Bestandstests: `MigrationTests` bringt eine echte Alt-Datenbank auf den aktuellen Stand, `WachstumTests` misst die Liste bei 5000 Vorgängen, `DsgvoAnleitungTests` führt die SQL-Blöcke aus `docs/betrieb/datenschutz.md` gegen eine Datenbank mit der Beispielperson aus. Die Datenschutzanleitung ist damit Teil der Testabdeckung; ändern Sie die SQL-Blöcke nur zusammen mit dem Test.

Einen einzelnen Test starten Sie mit einem Filter:

```bash
dotnet test --filter "FullyQualifiedName~RollenRechteTests"
```

Jede Fehlerbehebung bekommt einen Test, der ohne die Behebung rot ist. Wer eine Regel ändert, ändert den Test, der sie belegt, im selben Commit.

## Das CI

`.github/workflows/ci.yml` läuft bei jedem Push und Pull Request mit zwei Aufgaben:

1. Bauen und Fachtests unter Linux: Restore, Build, `dotnet test` mit Abdeckung, dann die Sichtprobe (prüft, dass alle 17 Bilder entstehen) und die Startprobe (prüft die Zeile „Startprobe: Hauptfenster steht"). Testergebnisse und Bilder liegen als Artefakte sieben Tage bereit.
2. Auslieferung unter Windows: `dotnet publish` als eigenständige Einzeldatei, Prüfung, dass `Ticketsystem.App.exe` im Paket liegt und die Reste einer früheren Fassung (`appsettings.json`, `appsettings.Development.json`, `wwwroot`, `Ticketsystem.Desktop.exe`) fehlen, dann Startprobe und Sichtprobe der veröffentlichten Exe. Das Paket liegt als Artefakt `ticketsystem-windows` bereit.

Beide Proben bekommen über Umgebungsvariablen eine Wegwerfdatenbank und das offen sichtbare Passwort `Sichtprobe-CI1!`. Das ist Absicht: Die Datenbank lebt nur im Lauf, und ein Secret wäre ein zweiter Ort, an dem etwas kaputtgehen kann.

## Abhängigkeiten

Direkte Pakete und ihre Lizenzen:

| Paket | Zweck | Lizenz |
|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Headless | Oberfläche, Thema, Schrift, Rendern ohne Bildschirm | MIT |
| Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design | Datenzugriff und Migrationen | MIT |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | Konten, Rollen, Passwort-Hashing | MIT |
| Microsoft.Extensions.Hosting, Configuration.*, Options.ConfigurationExtensions | Host, Konfiguration, Protokoll | MIT |
| Microsoft.Graph, Microsoft.Kiota.Abstractions | Mailversand über Microsoft 365 | MIT |
| Azure.Identity | Anmeldung der App-Registrierung mit Client-Geheimnis | MIT |
| xunit, xunit.runner.visualstudio | Testrahmen | Apache-2.0 |
| Microsoft.NET.Test.Sdk, coverlet.collector, Avalonia.Headless.XUnit | Testlauf, Abdeckung, Fenstertests | MIT |

`Microsoft.Kiota.Abstractions` ist direkt referenziert, obwohl nur Microsoft.Graph es braucht: Die von Graph gezogene Fassung trägt die bekannte Schwachstelle GHSA-7j59-v9qr-6fq9, die direkte Referenz hebt sie auf eine bereinigte. Prüfen Sie den Stand vor jeder Veröffentlichung:

```bash
dotnet list package --vulnerable --include-transitive
```

Die Ausgabe muss für alle drei Projekte „has no vulnerable packages" melden.

## Beitragen

Was ein Beitrag einhält:

- Keine Warnung: `TreatWarningsAsErrors` gilt in allen Projekten. Eine Unterdrückung braucht einen Kommentar mit dem Grund, wie `AVLN3001` in der App.
- Deutsch im Kern: Klassen, Methoden und Meldungen der Anwendung tragen deutsche Namen, weil Nutzer, Meldungen und Handbuch deutsch sind. Englisch bleibt, wo ein Rahmen es vorgibt (`Ticket.Status`, EF-Konventionen).
- Rechte im Dienst, nicht im Fenster. Ein Fenster darf einen Knopf ausblenden, die Regel steht im Kern und wirft bei Verstoß.
- Jede Änderung an einem Vorgang schreibt Historie über `AddHistory` im Dienst.
- Farben nur über die `Palette`, Schriften nur über `Schriften`, Maße nach den Leitern in `LeiternTests`. Jedes bedienbare Element bekommt ein `ToolTip.Tip`; `HinweiseUeberallTests` meldet sonst das Fenster.
- Schemaänderung heißt Migration, und `MigrationTests` bekommt einen Fall, wenn Daten umgeschrieben werden.
- Kommentare erklären das Warum, die Invariante oder die Stolperfalle, nie das Was; ein Klassenkopf darf den Zweck der Klasse nennen. Sie sind für Menschen geschrieben, in ganzen Sätzen, nur mit `//`. Ist der Code ohne Kommentar unklar, wird er umgebaut, nicht kommentiert.
- Commit-Betreff als Infinitiv- oder Imperativsatz, höchstens 72 Zeichen, ohne Punkt; der Body erklärt, was sich ändert und warum. Eine Fehlerbehebung nennt das Fehlbild und bringt ihren Test mit.
- Vor dem Pull Request: `dotnet build`, `dotnet test`, Sichtprobe. Das CI muss grün sein.

Was ein Beitrag nicht tut: Funktionen für ein vermutetes künftiges Bedürfnis anlegen, Abhängigkeiten ohne Grund hinzufügen, das Schema oder eine Schnittstelle ohne Migration und Test ändern.

Bekannte offene Punkte, an denen Sie ansetzen können:

- Der Mailversand ist gegen kein echtes Microsoft-365-Postfach geprüft.
- Ein zweiter Start endet ohne sichtbaren Hinweis; die Meldung steht nur auf der Konsole, die ein Doppelklick nicht hat.
- Die Erfassung bietet bei der Quelle Telefon kein Feld für eine E-Mail-Adresse.
- Im Detailfenster steht in der Karte **Status** auch ohne zugewiesene Person der Knopf **Zugewiesen**.
- Der Verweis auf einen früheren Vorgang wird gespeichert und exportiert, aber im Detailfenster nicht angezeigt.
- `TicketService.CreateAsync` schreibt keinen Historieneintrag „Angelegt"; nur Tests nutzen diesen Weg. Entweder `Anlegen` aufrufen oder die Methode entfernen.
- Ein neuer Anzeigename erscheint unten rechts im Hauptfenster erst nach erneuter Anmeldung; das Fenster setzt den Text nur beim Aufbau.
- Escape schließt den Bearbeiten-Dialog der Wissensdatenbank und das Detailfenster ohne Rückfrage, auch wenn dort ungespeicherter Text steht; nur **Abbrechen** fragt nach.
- Die Ansicht **Unzugewiesen** und die Ansicht **Pausierte Zuweisungen** filtern nicht nach Status und zeigen deshalb auch erledigte Vorgänge; die Lagebild-Kachel **Nicht zugewiesen** zählt nur offene.
- Beim Schließen aus dem Status Neu setzt `ApplyStatusEffects` den Reaktionszeitpunkt; eine abgelaufene Reaktionsfrist zählt dann als verspätet erfüllt und in der Auswertung als Fristriss, obwohl der Vorgang nur weggeschlossen wurde.
- Vor einem JSON-Import legt `AustauschService.ImportJsonAsync` keine Sicherung an; das Zurückspielen tut es.
- `FristenuebersichtAsync` prüft nur auf Mitarbeiter, nicht auf Teamleitung, und `Einstellungsablage.Anlegen` prüft keine Rolle; die Wissensfenster werten `DarfPflegen` selbst aus statt über einen Presenter.
- Nicht jede Zeile der Rechtematrix hat einen Test: Administration in `ListAsync`, Kommentieren fremder Vorgänge durch Bearbeiter, Pflegeverbote der Wissensdatenbank für Bearbeiter, `VorschlagAblehnenAsync`, CSV-Export und Zurückspielen durch Nicht-Berechtigte, Fristmeldung an die Administration.
- Die Testsuite lief vor dem Commit dieser Fassung unter Last sporadisch rot: `SqliteConnection.ClearAllPools()` in `Dispose` leerte den prozessweiten Pool, während xUnit andere Klassen parallel laufen ließ (`ObjectDisposedException` auf `SQLitePCL.sqlite3`). Die Testklassen öffnen ihre Datenbanken jetzt mit `Pooling=false`; bleibt ein Lauf trotzdem rot, wiederholen Sie ihn und melden Sie den Testnamen.
- `SlaMelderService.ExecuteAsync` fängt die Abbruchausnahme von `Task.Delay` nicht ab; bei einem Fehlstart nach `host.Start()` steht deshalb hinter dem eigentlichen Fehler noch „BackgroundService failed" im Protokoll. `SicherungsDienst` macht es richtig.
- `TicketService.FindForUserAsync` lädt Historie und Kommentare ohne `AsSplitQuery()`; EF Core schreibt bei jedem Öffnen eines Detailfensters die Warnung `MultipleCollectionIncludeWarning`.
- Der Fristenstand in der Statusleiste sagt „1 Wiedervorlagen".
- Einige Hinweise und Meldungen duzen („für dich sichtbar"), die Dokumentation siezt.
- `TicketSource.Web` ist ein Rest der abgelösten Web-Fassung; nur Tests benutzen den Wert.
- Der Mutex heißt `Local\Ticketsystem.Desktop` nach der abgelösten Fassung.
- Die Sperrmeldung nach fünf Fehlversuchen verrät, dass das Konto existiert, obwohl `AnmeldeDienst` sonst dieselbe Meldung für unbekanntes Konto und falsches Passwort gibt.
- Das Startskript `scripts/start.ps1` lädt `dotnet-install.ps1` ohne Signaturprüfung herunter.
