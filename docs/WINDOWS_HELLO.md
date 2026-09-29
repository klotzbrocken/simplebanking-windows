# Windows Hello — 0.13.0

## Bedienung

Einstellungen → Sicherheit → Windows Hello einrichten. Zuerst das Masterpasswort, anschließend die Windows-Freigabe bestätigen. Windows muss PIN/Biometrie eingerichtet haben. Die App aktiviert Hello erst nach einer erfolgreichen privaten Schlüsseloperation. Beim nächsten Öffnen des gesperrten Tresors erfolgt Hello automatisch. Abbruch oder Fehler öffnet den Masterpasswort-Dialog; dessen Abbrechen lässt alles gesperrt. Deaktivieren im Sicherheitsbereich entfernt die Entsperrhilfe. Nach Passwortwechsel erneut einrichten.

## Schutzmodell

Der vorhandene AES-GCM-/PBKDF2-Tresor bleibt unverändert. Eine separate lokale hello.json enthält ausschließlich einen verschlüsselten Passwortumschlag, keine Klartext-PIN und kein Klartext-Masterpasswort. Eine zufällige 256-Bit-AES-Kennung schützt das Passwort mit AES-GCM. Dieser Zufallsschlüssel wird mit RSA-2048/PKCS#1 v1.5 unter einem eigenen Schlüssel des Microsoft Passport Key Storage Provider verpackt. Es wird kein beliebiger Klartext mittels RSA verarbeitet.

Schlüsselname enthält Windows-SID, Hash des Tresorpfads und zufällige Registrierungs-ID. Pfad/Name sind zusätzlich authentifizierte AES-Zusatzdaten. Öffnen prüft die nicht exportierbare Schlüsselrichtlinie, Decryption-Berechtigung, ausgeschlossene Import-Berechtigung, verpflichtende NGC-Authentifizierung und öffentlichen Schlüsselfingerabdruck. Vor jeder Entschlüsselung wird PinCacheIsGestureRequired gesetzt. NCryptDecrypt wird mit einem ausreichend großen Ausgabepuffer einmalig ausgeführt. Kein Rückfall auf Software-KSP, DPAPI oder eine reine UserConsentVerifier-Abfrage. TPM-Hardware wird nicht pauschal behauptet: die konkrete Passport-Implementierung hängt vom Gerät ab.

Enrollment erstellt einen neuen Schlüssel, prüft dessen tatsächliche Entschlüsselung, schreibt atomar den Umschlag und entfernt erst danach die vorherige Registrierung. Abbruch entfernt den neuen Schlüssel und erhält eine etwaige bisherige Registrierung. Schlüsselbytes und temporäre Passwortbytes werden nach Gebrauch genullt. Wie bisher hält die entsperrte App das Masterpasswort als verwaltete Zeichenfolge im Prozess; Hello beseitigt diese bestehende Speicherarchitektur nicht.

Deaktivieren entfernt zuerst den Umschlag und anschließend nach Möglichkeit den app-eigenen Windows-Schlüssel. Ein Windows-Löschfehler kann einen ungenutzten Schlüssel hinterlassen; der normale App-Zugriff bleibt widerrufen. Kopien alter Umschläge sind keine App-Backups und werden nicht exportiert. Vor Passwortwechsel wird Hello deaktiviert, sodass kein veraltetes Passwort automatisch versucht wird. Auch bei fehlgeschlagenem Passwortwechsel kann Hello deshalb neu eingerichtet werden müssen. Normale Windows-sbw-Sicherungen benötigen weiterhin ihr Masterpasswort. Mac-Migration gehört nicht zum Projektumfang.

## Prüfung und Grenzen

Lokale RSA-Testimplementierung prüft Umschlag-Rundlauf, obligatorischen Freigabeaufruf, Abbruch, beschädigten Tag, Ordnerbindung, erneutes Einrichten, Löschen und Klartextfreiheit. UI-Harness prüft automatische Entsperrung sowie Abbruch mit Passwort-Fallback und weiterhin gesperrten Daten. Diese Tests erzeugen keine echten Passport-Schlüssel. Die echte PIN-/Fingerabdruck-/Gesichtsbestätigung muss beim Einrichten auf dem Zielgerät erfolgen. Bei nicht unterstützten NGC-Eigenschaften scheitert die Einrichtung geschlossen; das Masterpasswort bleibt verfügbar.

## Quellen

- Microsoft NCryptDecrypt: https://learn.microsoft.com/en-us/windows/win32/api/ncrypt/nf-ncrypt-ncryptdecrypt
- Microsoft CNG-Schlüsseleigenschaften: https://learn.microsoft.com/en-us/windows/win32/seccng/key-storage-property-identifiers
- Lokales Windows-SDK 10.0.26100.0, ncrypt.h (Passport-Provider und PinCacheIsGestureRequired).
- Provider-spezifische NGC-Cache-Konvention gegen die Implementierung des Projekts KeePassWinHello abgeglichen: https://github.com/sirAndros/KeePassWinHello/blob/master/src/AuthProviders/WinHelloProvider.cs . Eigene Implementierung über .NET CngKey, kein Einbinden der Bibliothek; NgcCacheType ist provider-spezifisch und kann auf nicht unterstützten Systemen fehlen.
