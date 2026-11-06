# Windows-Korrekturen und Mac-Abgleich, 06.11.2026

Geprüfter Windows-Ausgangspunkt: `09a0ed6331184da1f05c97c4f516d7e7771cfadf`.
Geprüfter Mac-Stand: `ba249b403f66b8a28f3f138d7d6ffeb200992772` (2.0.9), am 06.11. erneut gegen GitHub bestätigt.

## Buchungen und Windows Hello

Der Umsatzabruf gleicht neue Daten jetzt gegen den vollständigen aktuellen Kontobestand ab. Zuvor wurden alte zukünftige Buchungen beim Matching ausgeschlossen, konnten danach aber durch dieselbe neue ID ersetzt werden. Dabei gingen Kategorien und Gelesen-Markierungen verloren. Auch eindeutig erkannte Pending-zu-Booked-Wechsel außerhalb des Abruffensters ersetzen ihren Vorgänger, statt eine Dublette zu hinterlassen. Nicht zuordenbare Gelesen-Markierungen, Flags und andere lokale Ergänzungen bleiben in den nicht zugeordneten Details erhalten. Änderungen während des Abrufs werden erst nach dessen Abschluss aus dem aktuellen Bestand übernommen. Fehlgeschlagenes Speichern stellt Buchungen, Ergänzungen, Zuordnungen und Verbindungsstatus wieder her.

ISO-Datumswerte werden weiterhin unverändert übernommen. Das Modell merkt sich nun, ob die Anzeige mangels Buchungstag aus Wertstellung oder Transaktionsdatum stammt; Details erklären diesen Ersatz. Zukünftige Tage sind in der Liste gekennzeichnet und zählen in MCP-Auswertungen, Monatsberichten, Geld-Alter, Aufrundungen und der Wiederkehr-/Abo-Erkennung nicht als bereits gebuchte Ausgaben. Eine zukünftige Bankangabe wird weder gelöscht noch auf heute umgeschrieben. Steffis Original-Bankantwort liegt nicht vor; ob ihr Datum von der Bank stammt, bleibt deshalb unbestätigt.

Die Hello-Schlüsselerzeugung setzt Export Policy und Key Usage als Eigenschaften ohne Persistenzflags. Die .NET-Komforteigenschaften setzen diese Flags implizit, was beim Passport-Provider zu NTE_BAD_FLAGS führen kann. Nicht exportierbarer Schlüssel, Decryption-Nutzung, Cache-Schutzprüfung, echte private Schlüsseloperation und Enrollment-Verifikation bleiben bestehen. Fehlermeldungen ergänzen eine technische Phase ohne Passwort, Token oder Schlüsselmaterial. Die Änderung ist durch Parametertests und die vorhandenen Envelope-/Abbruchtests abgesichert; die Bestätigung auf einem persönlichen Hello-Gerät steht aus.

Technische Referenzen: [Microsoft NCryptSetProperty](https://learn.microsoft.com/en-us/windows/win32/api/ncrypt/nf-ncrypt-ncryptsetproperty), [.NET CngKey.Create](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/CngKey.Create.cs).

## Fünf weitere Mac-Commits

Verglichen wurde `9be7a250...ba249b40`:

- `f561fede`: Windows hat die Trennung zwischen bewusster Nutzeraktion und Hintergrundabruf bereits. Neue Regressionen prüfen die erneute Freigabe für Saldo und Umsatz/Historie; bestehende Tests schützen stille Hintergrundabrufe. Keine Swift-spezifische Freigabemechanik übernommen.
- `c521bc81`: Saldoauswahl bevorzugt Booked/Available ohne eingeschlossene Kreditlinie, danach den jüngsten nicht zukünftigen Stichtag, dann Booked bei Gleichstand. Währung und Konto bleiben getrennt. Undatierte Salden bleiben nutzbar; ausschließlich zukünftige/ungeeignete Werte ergeben keinen aktuellen Saldo. Windows verwendete das Standard-Überweisungsprodukt mit Echtzeitversuch und SEPA-Fallback bereits. Ergänzt wird die explizite Option „Nur als Echtzeitüberweisung senden“, einschließlich nativer Produktwahl und Validierung gegen Terminüberweisungen.
- `ed327a98`: Swift-Schalter und Swift-Testpfade sind nicht auf Windows übertragbar.
- `45886f42`, `ba249b40`: Mac-Releasehinweise und Appcast werden nicht in Windows-Releasedateien kopiert. Diese Dokumentation beschreibt die Windows-Änderungen; die Mac-Releasedateien werden nicht verwendet.

## MCP einrichten

App entsperren, unter Einstellungen → Extras CLI/MCP aktivieren und einen Client mit ausgewählten Konten/Bereichen anlegen. Die einmalige Token-Anzeige bietet nun eine vollständige kopierbare JSON-Konfiguration mit absolutem Programmpfad und `--mcp`. Der Token kann später nicht erneut angezeigt werden; bei Verlust einen neuen Client anlegen und den alten widerrufen.

`SIMPLEBANKING_TOKEN` bleibt der Windows-Name. `SIMPLEBANKING_MCP_TOKEN` wird als Mac-kompatibler Alias akzeptiert. Unterschiedliche gleichzeitig gesetzte Werte werden abgelehnt. Nach einem Update bleibt eine Konfiguration am normalen Installationspfad gültig. App-Sperre, globale Freigabe, Konto-/Toolbereiche, Ablauf und Widerruf werden weiterhin bei jeder Live-Abfrage geprüft. Entwürfe führen keine Zahlung aus.

stdio verarbeitet zeilengetrenntes JSON-RPC, antwortet nicht auf Notifications, unterscheidet Parse-/Request-/Parameter-/Toolfehler und verhandelt die unterstützten Versionen 2024-11-05, 2025-03-26 und 2025-06-18. Unbekannte Versionen erhalten 2025-06-18 als unterstützte Alternative. Interne Fehlermeldungen und Geheimnisse werden nicht an den Client weitergegeben. Protokollreferenzen: [Lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle), [stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports).

`scripts/test-mcp.ps1` startet den tatsächlichen Serverprozess, prüft Initialisierung, Toolliste, Demo-Abfrage, verweigerten Live-Zugriff ohne Token, reines Protokoll auf stdout und Beenden bei EOF. Der Workflow führt diesen Test gegen die veröffentlichbare EXE aus. Ein persönlicher Claude-/Codex-Client und echte Bank-/Hello-Abläufe müssen zusätzlich mit den jeweils eingerichteten Konten abgenommen werden.

## Validierung und verbleibende Abnahme

Lokal vorhandene installierte App: 0.16.0; sie wurde weder aktualisiert noch ersetzt. Der untersuchte Quellstand ist 0.21.0. Die neue MCP-Konfiguration richtet sich an einen Build dieses Quellstands, nicht an die ältere installierte Beta.

Die native x64-Bridge wurde mit MSVC 14.44 und dem vorhandenen Windows SDK gegen YAXI 0.2.0 gebaut. Der Release-Build hat keine Warnungen. 123 Selbsttests, Tray-Lebenszyklus und MCP-Unterprozesse (dotnet-Host sowie Windows-EXE; Demo und verweigerter Zugriff ohne Token) wurden geprüft. Der UI-Review rendert die Dialoge mit synthetischen Daten und prüft insbesondere, dass ein Termin die strikte Echtzeitoption ausschaltet. Die neue Option wurde zusätzlich anhand ihrer gerenderten Darstellung visuell geprüft.

Offen bleiben persönliche Windows-Hello-PIN/Biometrie, Steffis originale Bankantwort, echte Bankfreigaben und Zahlungen, ein persönlich eingerichteter MCP-Zielclient sowie die Abnahme eines aktualisierten Installationspakets. Die ursprüngliche Entwicklung wurde deshalb als Entwurf vorbereitet, ohne Bankzahlung, Produktiv-Upgrade oder Veröffentlichung. Für das angeforderte Windows-Release wird dieser Stand jetzt als 0.22.0 Beta 1 paketiert; die genannten persönlichen Abnahmen bleiben offen.
