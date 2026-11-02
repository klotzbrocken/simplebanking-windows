# Microsoft To Do – Windows 0.20.0

## Verwendung

Unter **Einstellungen → Extras → Microsoft To Do** mit dem eigenen Microsoft-Konto anmelden, eine vorhandene Liste wählen oder eine neue Liste erstellen. Erst der Schalter **Erinnerungen automatisch synchronisieren** erlaubt die Übertragung der Buchungserinnerungen. Er ist standardmäßig aus, auch nach einer erneuten Anmeldung.

Übertragen werden Händlername im Aufgabentitel, Erinnerungszeit und Erledigt-Status sowie eine zufällige technische Zuordnung in der Aufgabenbeschreibung. Betrag, IBAN, Verwendungszweck, Buchungsnotizen und Anhänge werden nicht übertragen. Der Aufgabenbeschreibungseintrag ermöglicht die Wiedererkennung nach einem Verbindungsabbruch und sollte erhalten bleiben.

- Neue lokale Erinnerungen werden als Aufgaben angelegt. Änderungen an Erinnerungszeit und Erledigt-Status werden in beide Richtungen abgeglichen. Änderungen an Titel oder Beschreibung in To Do überschreibt simplebanking nicht.
- Das Entfernen einer lokalen verknüpften Erinnerung löscht die zugehörige Microsoft-Aufgabe. Eine entfernte Microsoft-Aufgabe entfernt die lokale Erinnerung, sofern diese inzwischen nicht lokal geändert wurde. Bei gleichzeitigen Änderungen an Zeit/Status hat die lokale Änderung Vorrang.
- Der automatische Abgleich läuft bei entsperrter App ungefähr jede Minute, nach lokalem Speichern früher. **Jetzt abgleichen** stößt ihn direkt an. Einträge im Archiv nicht zugeordneter Anreicherungen werden nicht automatisch verändert.
- Sperren oder Beenden bricht den laufenden Abgleich ab. Ein bereits bei Microsoft angenommener Vorgang kann dadurch nicht zurückgenommen werden. Nach dem Entsperren werden offene Zustände erneut geprüft.
- Nach einem unklaren Erstellungsausgang wird nicht automatisch erneut angelegt. **Verbindung prüfen** erklärt die Wiedererkennung und erlaubt ein ausdrücklich bestätigtes Zurücksetzen, nachdem die Microsoft-Liste kontrolliert wurde. Andere Erinnerungen können weiter synchronisieren.
- **Trennen** entfernt den lokalen Anmeldecache und deaktiviert den Abgleich. Aufgaben und Erinnerungen bleiben erhalten; erneutes Anmelden mit demselben Konto erhält Zuordnungen. Danach erlaubt **Anderes Konto** einen bewussten Wechsel. Ein Listen-/Kontowechsel belässt alte Aufgaben und legt aktuelle Erinnerungen nach Aktivierung in der neuen Liste an.
- Der Demo-Modus meldet sich nicht bei Microsoft an und führt keine Graph-Anfragen aus.

## Einmalige Einrichtung durch den Herausgeber

Es liegt noch keine Microsoft-App-Registrierung für simplebanking vor. Deshalb enthält diese Beta keine produktive Client-ID. Die Implementierung ist gebaut und mit simulierten Antworten geprüft; eine echte Anmeldung ist erst nach dieser Einrichtung möglich.

1. Im [Microsoft Entra Admin Center](https://entra.microsoft.com/) unter **App-Registrierungen → Neue Registrierung** eine Anwendung namens **simplebanking Windows** erstellen. Dafür benötigt der Herausgeber Zugriff auf ein Entra-Verzeichnis mit Registrierungsberechtigung.
2. Unterstützte Kontotypen: **Konten in einem beliebigen Organisationsverzeichnis und persönliche Microsoft-Konten**.
3. Unter **Authentifizierung → Plattform hinzufügen → Mobile Anwendungen und Desktopanwendungen** die Umleitungs-URI **http://localhost** registrieren. Die App nutzt MSAL und den Systembrowser; kein Client-Secret anlegen.
4. Unter **API-Berechtigungen → Microsoft Graph → Delegierte Berechtigungen** **Tasks.ReadWrite** hinzufügen. Ein automatisch angelegtes `User.Read` wird für diese Integration nicht benötigt. Organisationsrichtlinien können eine Administratorzustimmung verlangen.
5. Die **Anwendungs-ID (Client-ID)** kopieren. Für diese Beta kann sie über **App-ID hinterlegen** im To-Do-Dialog eingetragen werden. Es handelt sich um eine öffentliche GUID, kein Geheimnis. Beim regulären Herausgeber-Build kann sie über die MSBuild-Eigenschaft `MicrosoftTodoClientId` eingebettet werden; dann benötigen Nutzer nur den normalen Anmeldeknopf.

Beispiel für einen Build mit bereits registrierter, öffentlicher ID:

```powershell
dotnet publish src/SimpleBanking.Windows -c Release -r win-x64 --self-contained true -p:MicrosoftTodoClientId=EIGENE-REGISTRIERTE-GUID
```

Der MSAL-Cache einschließlich erneuerbarer Anmeldung liegt ausschließlich im verschlüsselten Banktresor. Im unverschlüsselten Einstellungsfile und im öffentlichen Quellcode werden keine Microsoft-Zugangstokens gespeichert. Die delegierte Microsoft-Berechtigung umfasst To-Do-Aufgaben; die App ändert nur ihre verknüpften Aufgaben. Es gibt keine serverseitige Anmeldung und keinen eigenen Token-/Ticketdienst.

## Umsetzung und Prüfung

MSAL.NET 4.90.1, Microsoft Graph v1.0, persönliche und organisatorische Microsoft-Konten. Lokale Regressionen prüfen Nutzdatenbegrenzung, Zeitzonenrundlauf, Anlegen/Ändern/Erledigen/Reaktivieren/Entfernen, verlorene Antworten ohne doppelte Erstellung, Sperren, parallele Änderungen, Kontozuordnungen und verschlüsselte Persistenz. Fremde Paging-Adressen werden vor dem Senden eines Tokens abgelehnt. UI-Rendering umfasst den Offline-Demo-Dialog.

Grenzen: keine Live-Anmeldung ohne App-Registrierung; kein Abgleich bei geschlossener/gesperrter App; keine Übernahme fremder Aufgaben als Bankbuchungen; Aufgaben, die in Microsoft in eine andere Liste verschoben werden, gelten in der ursprünglichen Liste als entfernt. Ein vollständiger Listenabruf ist auf 100 Seiten begrenzt. Kein unbeaufsichtigtes Wiederanlegen bei unklarem Ausgang.

## Microsoft-Referenzen

- [App registrieren](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [Desktop-Anmeldung mit Systembrowser und localhost](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers)
- [MSAL-Cache serialisieren](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)
- [Aufgabe erstellen und Tasks.ReadWrite](https://learn.microsoft.com/en-us/graph/api/todotasklist-post-tasks?view=graph-rest-1.0)
- [To-Do-Aufgabe und Erinnerungsfelder](https://learn.microsoft.com/en-us/graph/api/resources/todotask?view=graph-rest-1.0)
