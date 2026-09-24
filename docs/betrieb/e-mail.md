# E-Mail-Versand einrichten

Diese Anleitung richtet sich an die Administration mit Zugriff auf einen Microsoft-365-Mandanten. Danach verschickt das Ticketsystem Eingangsbestätigungen an die Absender von E-Mail-Vorgängen und meldet gerissene Fristen an Teamleitung und Administration. Mehr tut die Anwendung mit dem Postfach nicht; gelesen wird es weiterhin von Hand. Jede gesendete Mail liegt danach im Postfach unter „Gesendete Elemente".

Ohne diese Einrichtung läuft das System vollständig; statt einer Mail schreibt es eine Zeile ins Protokoll. Der Versand ist eingebaut und getestet, aber bisher nicht gegen ein echtes Postfach geprüft.

Sie brauchen Administratorrechte im Mandanten, weil die Berechtigung eine Administratorzustimmung erfordert.

## 1. App-Registrierung anlegen

1. Öffnen Sie im Azure-Portal **Microsoft Entra ID**, **App registrations**, **New registration**.
2. Vergeben Sie einen Namen, zum Beispiel `Ticketsystem-Versand`, wählen Sie „Konten nur in diesem Organisationsverzeichnis" und lassen Sie die Redirect-URI leer.
3. Notieren Sie nach dem Anlegen die **Application (client) ID** und die **Directory (tenant) ID**.

## 2. Berechtigung erteilen

1. Öffnen Sie **API permissions**, **Add a permission**, **Microsoft Graph**, **Application permissions**.
2. Wählen Sie genau eine Berechtigung: `Mail.Send`. Ein Leserecht braucht der Versand nicht.
3. Klicken Sie auf **Grant admin consent** und bestätigen Sie.

## 3. Geheimnis erzeugen

1. Öffnen Sie **Certificates & secrets**, **New client secret** und wählen Sie eine kurze Laufzeit, höchstens sechs Monate. Der Dialog zeigt das neue Geheimnis mit Wert und Ablaufdatum. Die Erneuerung vor dem Ablauf steht in den [wiederkehrenden Aufgaben](betriebshandbuch.md#wiederkehrende-aufgaben) des Betriebshandbuchs.
2. Kopieren Sie den **Wert** sofort. Er ist später nicht mehr einsehbar.

## 4. Konfiguration setzen

Setzen Sie die Werte als Umgebungsvariablen des Windows-Kontos, unter dem die Anwendung läuft, nicht systemweit. Das Geheimnis gehört nicht in die Einstellungsdatei und nicht in ein Repository. Bedenken Sie: Eine Benutzer-Umgebungsvariable liegt im Klartext in der Registrierung dieses Kontos und ist für jedes Programm lesbar, das unter ihm läuft.

```text
Email__Enabled=true
Email__TenantId=<Directory (tenant) ID>
Email__ClientId=<Application (client) ID>
Email__ClientSecret=<Geheimnis>
Email__MailboxAddress=support@example.org
```

Starten Sie die Anwendung danach neu.

## 5. Senden auf das eine Postfach begrenzen

Anwendungsberechtigungen (Application permissions) gelten ohne weitere Einschränkung für alle Postfächer des Mandanten; mit `Mail.Send` dürfte die App also im Namen jedes Postfachs senden. Begrenzen Sie das mit einer ApplicationAccessPolicy in der Exchange Online PowerShell:

```powershell
New-ApplicationAccessPolicy -AppId <ClientId> `
  -PolicyScopeGroupId <mail-aktivierte Sicherheitsgruppe mit dem Support-Postfach> `
  -AccessRight RestrictAccess `
  -Description "Ticketsystem sendet nur als Support-Postfach"
```

## 6. Prüfen

1. Legen Sie in der Anwendung einen Vorgang mit Quelle **E-Mail** und einer Absenderadresse an, die Sie selbst lesen können.
2. Prüfen Sie das Postfach dieser Adresse: Die Eingangsbestätigung trägt `[Ticket #n]` im Betreff.

Ist `Email__Enabled` nicht auf `true` gesetzt, kommt keine Mail; im Protokoll steht stattdessen eine Meldung, die `E-Mail-Versand deaktiviert` enthält.

Telefon-Vorgänge tragen keine Adresse und lösen keine Bestätigung aus.

## Fehlerbilder

| Beobachtung | Ursache |
|---|---|
| 403 oder `ErrorAccessDenied` beim Senden | die Administratorzustimmung fehlt (Schritt 2), oder die ApplicationAccessPolicy schließt das Postfach aus |
| 401 oder `InvalidAuthenticationToken` | Tenant-ID, Client-ID oder Geheimnis stimmen nicht, oder das Geheimnis ist abgelaufen |
| keine Mail und keine Fehlermeldung | `Email__Enabled` steht nicht auf `true`; das Protokoll zeigt die Zeile des deaktivierten Versands |
