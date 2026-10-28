# Abgleich mit macOS, 12.10.2026

Windows 0.17.0. Verglichen wurden die 23 Commits von `933e158b0e0d87558943eeef21c97feea3e1423a` bis `9be7a2505e773c3aef138881c8a29822be0fe187` im [Mac-Repository](https://github.com/klotzbrocken/simplebanking/compare/933e158b0e0d87558943eeef21c97feea3e1423a...9be7a2505e773c3aef138881c8a29822be0fe187). Der ursprüngliche Herkunftsnachweis bleibt in provenance.json erhalten; der Review-Stand ist dort zusätzlich vermerkt.

## Übernommen

- **Banklogos** (`a7c2d59a`): Die Institutsgruppe der BLZ hat Vorrang vor dem regionalen Präfix. Die Hamburger Sparkasse bekommt kein comdirect-Logo mehr; Volksbanken ebenso gruppengerecht.
- **Bankfreigaben** (`fecc91ff`): Timer, Programmstart und Entsperren dürfen keine Bankdialoge oder Weiterleitungen starten. Bei nötiger Freigabe wird die Verbindung im verschlüsselten Tresor als pausiert markiert; ein Tray-Hinweis nennt die Bank. Nur bewusstes Aktualisieren oder ein bewusst angeforderter Historienabruf dürfen interaktiv werden. Erst ein vollständig erfolgreicher Abruf hebt die Pause auf. Windows öffnete den Browser bereits nur über eine Schaltfläche, zeigte aber bisher ungefragt den Freigabedialog.
- **Fixkosten** (`55e88665`, `815f5628`): Nur Fälligkeiten innerhalb des aktuellen Gehaltszyklus zählen zu „offen“. Innerhalb des Zyklus überfällige Zahlungen bleiben enthalten. Unerkannte Zahlungsdienstleister sind keine Händlergruppe; zwei Beobachtungen benötigen nahezu gleiche Beträge. Vorgemerkte Umsätze sind keine Buchungsbelege für die Wiederkehr. Zahlungsdienstleister dürfen außerdem nicht vorher durch Marken-Aliase verkürzt werden.
- **Zuordnungen** (`ed14f220`, `ecd3ea60`): Korrekturen gelten engineübergreifend für bekannte Händler. Bei „Unbekannt“ bleibt der unterscheidende Zusatz erhalten. Kontoübergreifende Vermischung ist ausgeschlossen. Ausschließen benennt die Wirkung auf Fixkosten ausdrücklich; vorhandene exakte Zuordnungen bleiben lesbar.
- **Sicherungen** (`a0c33a03`): Windows speichert die vollständigen Tresordaten einschließlich Kontozugängen, Anhängen und eigenen Logos gemeinsam verschlüsselt, ohne stilles Überspringen von Dateien. Zusätzlich wird eine exportierte Sicherung nun zunächst vollständig in eine temporäre Datei geschrieben und erst dann ersetzt; die Größenbegrenzung wird auch beim Export geprüft. Ein Schreibfehler ersetzt die bestehende Sicherung nicht.
- **CLI/MCP-Auswertungen** (`ead22ef8`, `407f2e14`): `get_fixed_costs`, `get_subscriptions`, CLI-Aliase `fixed` und `subs`. Die Berechnungen verwenden dieselben Funktionen, Gehaltseinstellungen und Nutzerkorrekturen wie die App. Konten/Währungen bleiben getrennt. Ausgabe enthält Berechnungszeit und Zeitpunkt des letzten Saldoabrufs, keine eigene Bankaktualisierung. Die vorhandene Entsperr-/Opt-in-/Benutzerprüfung bleibt bestehen. Unterstützte Parameter der neuen Abfragen: `account_id`, `currency`.

## Bereits anders gelöst / keine direkte Übernahme

- **Buchungsidentität** (`3f80be09`): Windows speichert und verwendet bereits `Entry.Id` einschließlich Vorkommenszähler. Anzeige, Anreicherung und OFX verwenden diese Kennung. Neuer Regressionstest prüft getrennte Notizen und Anhänge für zwei identische Buchungen nach Sicherung und Abgleich.
- **Eigene Händlerlogos** (`a0c33a03`): Eigene Logos liegen im verschlüsselten Tresor, Internetlogos in einem getrennten Cache. Die Anzeige priorisiert eigene Logos; der Downloader schreibt nicht in den Tresor. Der Mac-Race beim gemeinsamen Cache trifft diese Architektur nicht.
- **Freigabewache** (`3f80be09`): Windows serialisiert Abrufe über `bankBusy` und gibt diesen Zustand synchron im `finally` frei. Kein losgelöster Task zur Freigabe der Sperre.
- **Menüleistenformatierung** (`75690330`): Windows verwendet bereits .NET-Zahlenformatierung mit `de-DE`; die fehlerhafte manuelle Swift-Ziffernumkehr existiert hier nicht. Der Tray zeigt das Bankicon statt eines Mac-Menüleistentextes.
- **Settings-Breitenfehler und Reverts** (`ed14f220`, `c20d7135`, `05afdd59`): Ursache ist SwiftUI-spezifische Zentrierung eines zu breiten Inhalts. Keine Übernahme des zwischenzeitlich zurückgenommenen Fixes. Windows nutzt eigene Forms-/Panel-Layouts.
- **Diagnoseänderungen einschließlich Rücknahme** (`e37077fd`, `10ec118e`, `9266ea02`, `d4f06fb5`): Die zuletzt wieder entfernte Fixkosten-Aufschlüsselung wird nicht nachgebaut.
- **Release-Metadaten** (`587768c6`, `611a2546`): Windows führt seine eigene Version 0.17.0 und diesen Änderungsnachweis.
- **Toter Swift-Code / REWE-Prüfstand / Build-Ignore** (`5ea22f6f`, `6a37a87b`, `9be7a250`): Mac-Buildstruktur und unbenutzte Swift-Deklarationen existieren im Windows-Projekt nicht. Händleranbindungen und Aufrunden bleiben wie beauftragt geparkt.

## Bewusst nicht übernommen

`prepare_transfer` (`e62db31e`) bleibt unter Windows nicht verfügbar. Die neue Mac-Funktion benötigt einen eigenen, standardmäßig ausgeschalteten Client-Berechtigungsbereich, einen separaten Schalter und eine zeitlich gültige Lizenzbestätigung. Windows besitzt bisher eine ausschließlich lesende lokale Schnittstelle, ohne das dazugehörige Client-/Tokenmodell. Ein bloßes Ergänzen eines schreibenden Befehls würde diese Bedingungen umgehen. Das bestehende manuelle simplesend bleibt unverändert; die spätere vollständige Portierung des Berechtigungsmodells ist ein eigener Arbeitspunkt.

## Prüfung

Regressionen für BLZ-Gruppen, abgelaufene/gezahlte/überfällige Fixkosten, Zahlungsdienstleister und echte Händler, Zwei-Buchungs-Toleranz, vorgemerkte Umsätze, unbekannte Gruppen, Hintergrundfreigaben, gemeinsame API-/UI-Auswertung, Sicherung und doppelte Buchungen. Native Bankkommunikation, echte TANs, Windows-Hello-Anmeldung und reale Zahlungen sind nicht Teil dieser Tests.

Lokale Validierung am 12.10.2026: 79 Tests erfolgreich, Tray-Lebenszyklus erfolgreich, Demo-Start und UI-Rendering erfolgreich, CLI-/MCP-Abfragen geprüft. Das selbständige Windows-Paket und der unsignierte Installer wurden erstellt. Die Installation auf einem zweiten Rechner und die echten Bank-/Hello-Abläufe bleiben offen (siehe P1-BETA.md).
