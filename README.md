# simplebanking für Windows

Native Windows-Version von [simplebanking für macOS](https://github.com/klotzbrocken/simplebanking), von Maik Klotz. Kontostand im Systray-Flyout, Umsatzliste und Money Mood, Bankanbindung über YAXI.

**Quellstand: 0.21.0 Beta.** Neu: [Verwaltung mehrerer eigener Themes](docs/THEME-LIBRARY.md). Neu: [Microsoft-To-Do-Anbindung und einmalige Registrierung](docs/MICROSOFT-TODO.md). Neu: [Desktop-Stabilität und große Tresore](docs/DESKTOP-STABILITY.md). Neu: [Prio 2 – Updater, Scanner, CAMT, KI und MCP](docs/P2-IMPLEMENTATION.md). Außerdem: [Prio-1-Umsetzung für Themes, Händlerregeln und Auswertungen](docs/P1-IMPLEMENTATION.md). Änderungen gegenüber der ersten Beta: [Mac-Abgleich Oktober 2026](docs/MAC-SYNC-2026-10.md). Die vollständige Funktionsparität zur Mac-Version ist noch nicht erreicht. Echte Bankabläufe und Windows Hello müssen auf den jeweiligen Geräten und mit den jeweiligen Banken abgenommen werden. Details: [Funktionsstand](docs/PARITY.md).

## Installation

Die `simplebanking-Beta-Setup.exe` unter [Releases](https://github.com/klotzbrocken/simplebanking-windows/releases) enthält die Anwendung und die .NET-Laufzeit für Windows 10/11 x64. Das Beta-Setup ist **nicht signiert**; Windows kann eine Warnung anzeigen.

1. Eine laufende Version über das Tray-Menü beenden.
2. Setup.exe aus dem Windows-Explorer starten und installieren.
3. Im Startmenü **simplebanking** oder **simplebanking Demo** öffnen.

Installation pro Benutzer. Der verschlüsselte Tresor liegt getrennt von den Programmdateien. Der Demo-Modus verwendet synthetische Daten; Zahlungen werden darin simuliert.

## Entwicklung

Voraussetzungen: PowerShell 7, .NET SDK 10.0.401, Visual Studio 2022/Build Tools mit C++-Desktopentwicklung und Windows SDK. In einer **x64 Developer PowerShell**:

```powershell
./publish.ps1 -Test
./build-installer.ps1
./build-setup.ps1
```

Der Build lädt NuGet-Abhängigkeiten, das geprüfte YAXI-C++-Paket 0.2.0 und WiX 4.0.6. Die Ausgabe liegt in `app/`; der Installer im Projektverzeichnis. Ohne YAXI-Schlüssel eignet sich das Paket für Demo und Entwicklung. Es werden keine Zugangsdaten aus dem Quellrepository bezogen.

Für einen eigenen Live-Build eine lokale, nicht eingecheckte JSON-Datei mit `KeyId` und `SecretBase64` bereitstellen und `./publish.ps1 -Test -YaxiKeysFile <Dateipfad>` verwenden. Optional sind `TransferKeyId` und `TransferSecretBase64` für simplesend möglich. Schlüssel werden dabei in das Programm eingebettet und sind aus einer ausgelieferten Desktop-App grundsätzlich extrahierbar.

GitHub Actions baut bei Push und Pull Requests ohne Live-Schlüssel. Ein manuell gestarteter Workflow kann mit `live_keys` die Repository-Secrets `YAXI_KEY_ID` und `YAXI_SECRET_BASE64` einbetten. Workflow-Artefakte sind keine automatische Release-Freigabe.

## Beta-Prüfung

112 lokale Selbsttests, Tray-Lebenszyklus und Demo-Start des Entwicklungsstands wurden erfolgreich geprüft. Bestehende Installation und Tresor-Erhalt wurden lokal geprüft. Eine saubere Erstinstallation auf einem weiteren Rechner, vollständige Bankmatrix, persönliche Windows-Hello-Anmeldung und echte Zahlungen sind noch nicht abgenommen.

Der Reparaturtest des MSI aus dem Projektordner war erfolgreich. Die EXE-Reparatur aus der paketierten Codex-Umgebung scheiterte an deren AppData-Umleitung; die Wiederholung aus dem normalen Explorer-Kontext steht aus.

## Lizenz und Herkunft

MIT, siehe [LICENSE](LICENSE). Mac-Vorlage, SDK-Version und Referenz-Commits: [provenance.json](docs/provenance.json). Lizenzen der übernommenen Inhalte liegen unter `reference/` und `native/vendor/`. Marken und Händlerlogos gehören ihren jeweiligen Inhabern.
