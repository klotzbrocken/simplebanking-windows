# Stand der Windows-Portierung — 0.16.0, 29.09.2026

Referenz: Mac-Quellcode 2.0.7, Commit 933e158 (vollständige Herkunft unter provenance.json), ursprüngliche Assets und Yaxi SDK 0.2.0. Designreferenz zusätzlich: Desktop/export, insbesondere kombinierte Filter und Passwort-/Hello-Dialog (0.15–0.16, einschließlich Details, Tastenkappen und About-Karten). Diese aktuelle Matrix ersetzt die früheren Fortschrittslisten. **Vollständige Funktions- und Designparität ist nicht erreicht.**

| Bereich | Implementiert | Grenze / noch offen |
|---|---|---|
| Windows-App | Selbstständige x64-EXE, Tray/Flyout, Single-Instance-Aktivierung, Benutzer-MSI, Startmenü und Desktop | Signierung, Updater, saubere zweite Testmaschine |
| Einrichten / Entsperren | Assistent, Masterpasswort, Banksuche, Zugang, Kontenauswahl; automatisches Hello-Entsperren mit Passwort-Fallback, Einrichten/Deaktivieren; getestetes Sperren | Live-Bank-/TAN-Abnahme, persönliche Hello-PIN-/Biometrie-Abnahme; nicht jedes Mac-Detail visuell identisch |
| Darstellung | Original-Bank-/Händlerassets plus bekannte Remote-Händlerlogos mit Cache, gemeinsame Balance-Karte, MoneyMood-Verläufe, Ripple, acht Einstellungsbereiche mit farbiger Seitenleiste, Footer-Iconbar, ziehbares/persistentes Widget, vier Original-Themepaletten, PNG/ZIP-Themes mit Vorschau und dunkle Kontenansicht | SVG/PDF-Themegrafiken, externe Schriften, vollständige BTX-Spezialansichten und anwendungsweite Themes, vollständige Typografie/Animationen/DPI-Abnahme |
| Umsatzliste | Konto-/Gesamtansicht, Suche, Filter, virtualisierte Zeilen, temporäre Scrollleiste, feste 348/840-Breiten, Scrollpositionserhalt, Pfeiltasten/Pos1/Ende, Details | 100.000 synthetische Buchungen in der UI geprüft; Speicherung weiterhin verschlüsseltes JSON statt SQLite, kein kompletter Tresor-Benchmark |
| Buchungsdetails | Notizen, Kategorie, gelesen/markiert, eigene Logos, Reminder, drei PDF-/Bildanhänge à 3 MB | Logos pro Buchung oder Händler/Konto vorhanden; E-Mail-/Quittungsablauf, Windows-To-Do-Sync, vollständige Vorschauen fehlen |
| Datenabgleich | Eindeutige Vormerkung→Buchung-/Referenz-Zuordnung, Mehrdeutigkeit wird nicht geraten; getrenntes Archiv erhält nicht zuordenbare Anreicherungen | Nicht alle bankabhängigen Referenzänderungen abgenommen |
| Regeln | Priorität, UND/ODER, Kategorien, wiederkehrende Zuordnung, Händlerregeln mit Regex-Zeitlimit, Vorher-/Nachher-Vorschau, Originalnamen und Einzelbuchungs-Override; lokale Provider-/Karten-/ABWA-Erkennung | Eigene Namen/IBANs und gruppenweite Logos umgesetzt; nicht sämtliche Auto-Regelvarianten/Confidence-Metadaten portiert |
| Analyse | MMI, FIFO-Geldalter, Abos/Fixkosten, Monatskalender mit Händlerlogos und Mindestzeilenhöhe, Kategorie-Ring, Kündigungslinks, manuelle Zuordnungen, Gehalt/Dispo/Toleranz | Genaue Detailgestaltung, Sonderfälle und Banktypcodes nicht vollständig portiert |
| Attention Inbox | Doppelte/ungewöhnliche/neue Abbuchungen, Preisänderung, Kategorienanstieg, Gehaltshinweis, Reminder, Gelesen-Snooze | Einfache regelbasierte Hinweise, keine Garantie auf korrekte Erkennung |
| Datenimport / Export | CAMT 053 und OFX/QFX mit Vorschau/Dublettenabgleich; CSV, OFX und simple.report-PDF; ältere Bankumsätze mit Datumsauswahl | CAMT-Sammelbuchungen werden abgelehnt; Mac-Datenmigration auf Nutzerwunsch ausgeschlossen |
| simplesend | Separater Schlüsselpfad, öffentliche Polar-Lizenzprüfung, Prüfdialog, Vorlagen, Termin und native Überweisung, E2E-ID, persistentes Journal, keine automatische Wiederholung unklarer Aufträge | Transfer-Key/-Secret fehlt; Lizenz und echte Bank-/VoP-/TAN-Flows nicht abgenommen; Quick-Send-Flyout mit vier kontogebundenen Favoriten vorhanden; Vorlagen-/Symbol-Editor und lokale kontogebundene Empfängervorschläge vorhanden; Inline-Prüfung, Zurück, explizites Senden, Abbruch und Ergebnis im Flyout vorhanden |
| Scanner | Lokale OCR Deutsch/Englisch, QR/GiroCode, PDF-Text und eingebettete Scanbilder, manuelle Prüfung der Felder | Keine vollständige PDF-Seitenrasterung; komplexe oder gedrehte Scans können scheitern |
| Aufrunden (Weiterentwicklung geparkt) | 10/50 ct, 1/2/5/10 €, heute/gestern/vorgestern/Monat, virtuelle Rücklage, vorbereitete Auszahlung, reservierte/unklare Tage ausgeschlossen | Vollständiger Mac-Tagesledger, Hero-/Streak-Oberfläche und Nachbuchungslogik fehlen |
| KI | Drei Original-Anbieter, persönliche verschlüsselte Keys, Chat-Opt-in, aktives Konto, begrenzter Gesprächsverlauf, Folgefragen/Abbruch, Kategorienvorschau; lokale Demo | Kein Live-Nachweis, keine SQL-Sandbox/volle Chat-UX, keine automatische Kategorisierung |
| CLI / MCP | Sechs lokale Nur-Lese-Befehle mit Datum/Währung/Filtern und Pagination, stdio-MCP, Named Pipe nur aktueller Windows-Benutzer, entsperrt + Opt-in erforderlich | Nur Teilmenge des Mac-MCP; kein Raycast-Pendant, keine SQL-/Tokenverwaltung |
| Demo | Getrennte Daten und Einstellungen, Abos/Historie, Reminder/Anhang, Vorlagen/Journal, simulierte Zahlungen, lokale Chat-Zusammenfassung, Zurücksetzen | Simulation ersetzt keine Bankabnahme |
| REWE / dm / Amazon / PayPal (geparkt) | Noch nicht implementiert | Händler-Login, eBon-Abruf/Abgleich und zugehörige UX fehlen |

## Ausgeführte Prüfungen

- 70 lokale Selbsttests, null Fehler und null Compilerwarnungen: Fachlogik, Konto/Währung, Ticket-Signatur, native ABI, Import/Export, OCR/QR, Anreicherungsabgleich, verschlüsselter Rundlauf, KI-Vertrags-Fixtures ohne Netzwerk, CLI-Schreibsperre, Zahlungs-/Spartopfdaten.
- Gerenderte UI-Prüfung mit synthetischem Tresor: Passwortabfrage genau einmal, Abbrechen/falsch/richtig, erneutes Sperren, geschlossene Unterfenster, Flyout-Doppelklick, sämtliche Einstellungsbereiche und neue Dialoge; Widget-Fokusverlust, Positionswiederherstellung, Sperren, Footer-Aktionen und Theme-Rendering. Physisches Ziehen und Multi-Monitor-/DPI-Verhalten sind noch nicht manuell abgenommen.
- Aktueller Installations- und Prüfnachweis unter release-checks.json; historische Versionsangaben in den folgenden Abschnitten beziehen sich auf frühere Pakete.
- Keine echte Bankanmeldung, keine reale Zahlung, keine externe KI-Anfrage. Eine authentische Bankabnahme kann nicht durch lokale Fixtures ersetzt werden.

## Angaben / Mitwirkung für die noch nicht extern prüfbaren Teile

1. Separates simplesend-Transfer-Key/-Secret über die lokale Schlüsselkonfiguration; nicht in öffentliche Dateien oder Tickets schreiben.
2. Bestätigung, ob die Polar-Organisation und bestehenden Mac-simplesend-Lizenzen für Windows verwendet werden sollen; eine eigene Testlizenz für die Aktivierungsprüfung.
3. Bankzugang/TAN durch den Nutzer direkt in der App für die Ende-zu-Ende-Abnahme. Zugangsdaten müssen nicht an den Assistenten gesendet werden.
4. Für signierte öffentliche Verteilung: Herausgebername, Signierzugang und später Update-Adresse.
5. Persönliche KI-Keys können Nutzer selbst unter Labs hinterlegen. Ohne Key bleibt die lokale Demo verfügbar.

Die übrigen offenen Tabellenpunkte sind Entwicklungsarbeit, keine vom Nutzer zu beantwortenden Fragen. Insbesondere vollständige Theme-/BTX-Ansichten und weitere Mac-UX-Details sind mit diesem Stand nicht als fertig zu behandeln.

- Installiertes Demo-Fenster per Windows-Fensterliste erkannt. Direkte native Screenshot-/Bedienprüfung wurde vom Computer-Use-Werkzeug nicht freigegeben; kein manueller UI-End-to-End-Nachweis. Die gerenderten UI-Prüfungen liefen erfolgreich.

## UX-Korrekturen — 0.14.0

Native Vergrößern-Aktion schaltet die beiden Mac-Breiten 348/840 bei fester Höhe. Kein dynamisches Resize. Icon-Fokusrahmen rund und nur bei Tastatur-Fokushinweisen; Filterpillen ohne Rahmen. Die virtualisierte Liste scrollt über eigenen Offset und zeigt ihre Scrollleiste nur während Aktivität.

Desktop-Flyout verbirgt Werte außerhalb des Mausbereichs (eigene lokale Maskierung, kein Ändern der globalen Saldenpräferenz). Schließen-Symbol entfernt das Widget einschließlich gespeichertem Aktivierungszustand; laufende Zahlungen verhindern dessen Schließen. Hover, Schließen, Breitenwechsel, Scrollaktivität und Ausblendung sind im UI-Harness geprüft.

Remote-Logos folgen der originalen Marken-Domain-Whitelist, Logo.dev-Konfiguration und dem Budget von drei Abrufen/Tag, 30 Tage Cache und Mindestkante 64 Pixel. Nur bekannte Domains, keine Buchungstexte oder Namen privater Gegenparteien. Kein Google-Favicon-Fallback; unbekannte oder fehlende Logos bleiben beim Kategorie-Symbol. Cache/Budget/404/Formatgrenzen sind lokal getestet, ein öffentlicher Testabruf lieferte HTTP 200 mit PNG256. Ohne echte Bankdaten getestet. Mac-Datenmigration bleibt ausdrücklich ausgeschlossen.

## Umfang und Windows Hello — 0.13.0

**Mac-Datenmigration entfällt ausdrücklich auf Nutzerwunsch.** Historische Nennungen weiter unten sind keine offenen Aufgaben mehr. Die unabhängige Entscheidung über SQLite als internen Speicher bleibt davon getrennt.

Windows Hello ist implementiert: eigener Passport-Schlüssel mit verpflichtender Freigabe, RSA-geschützter Zufallsschlüssel und AES-GCM-verschlüsselte Entsperrhilfe, benutzer-/ordnergebunden. Einrichtung nach Masterpasswortprüfung und erfolgreicher Hello-Entschlüsselung, automatischer Aufruf, Passwort-Fallback, Deaktivieren, Widerruf vor Passwortwechsel. Kein DPAPI-/Boolean-Ersatzpfad. Bei nicht unterstütztem Provider oder fehlender Schutzrichtlinie bleibt die Einrichtung aus. 68 Selbsttests; Hardware/PIN-Abnahme benötigt die persönliche Einrichtung.

## Runde 0.12.0

Die Umsatzansicht erzeugt nur Zeilen im Sichtbereich plus kleinem Puffer. Ein Test mit 100.000 Buchungen prüft begrenzte Control-Anzahl, Scrollsprung und Position nach Aktualisierung. Datenmodell und Tresor bleiben vollständig im Speicher; dies ist keine SQLite-Migration.

Der KI-Dialog hält einen flüchtigen Gesprächsverlauf, sendet maximal sechs gekürzte Paare mit und erlaubt Abbruch. Demo-Verlauf wird mit zwei Fragen geprüft. Live-Anbieterzugriff und Streaming bleiben offen. CLI/MCP ergänzt Händler/Kategorien, gemeinsame Filter und Offset/Limit; große Eingaben werden beim Lesen abgewiesen. Theme-Export schreibt zunächst ein temporäres Archiv.

## Historische Änderungsnotizen

Die folgenden Abschnitte dokumentieren frühere Stände. Ihre damaligen Offen-Punkte werden durch die aktuelle Matrix oben ersetzt.

## Startkorrektur 0.3.1
Der ObjectDisposedException beim Wiederöffnen nach geschlossenem/freigegebenem Hauptfenster wurde im isolierten Tray-Test reproduziert (vorher Exit 1) und behoben (nachher Exit 0). ApplicationContext bindet jetzt das Hauptfenster und beendet Timer/Tray beim Freigeben. Normales Schließen und Wiederöffnen sind mitgeprüft. 53 Selbsttests und UI-Review bestanden, MSI-Upgrade und installierter Regressionstest Exit 0.

## UX-Priorität — 0.4.0

Flyout-Pin aktiviert jetzt TopMost, und der zweite Klick löst die Anheftung ohne sofortiges Schließen. Gespeicherte Position und Sperrverhalten bleiben erhalten. Händler-/Banklogos werden mit hochwertigem Resampling und Seitenverhältniserhalt gezeichnet. Die Umsatzliste hat keine redundante Dauerstatuszeile und keine horizontale Scrollleiste. Einstellungen sind kompakter und nutzen durchgehend anklickbare Aktionszeilen. Kalenderzeilen passen Betrag und Logos auf 54 logische Pixel an. Quick Send nutzt den existierenden validierten Überweisungsdialog; es wurde keine Zahlung ausgelöst.

Automatisierte UI-Prüfungen: tatsächlicher Pin-Button-Klick, Fokusverlust, zweiter Klick, Wiederherstellung; Quick-Send-Favorit, Kontowechsel löscht Eingaben, Gesamtansicht schließt Quick Send; verschlüsselte Favoriten-Serialisierung und Logo-Geometrie/Transparenz. 54 Selbsttests plus Tray-Lebenszyklustest. Keine manuelle Multi-DPI-/Monitor-Abnahme.

Aktiv offen bleiben insbesondere Windows Hello, Mac-/SQLite-Migration, vollständige Händlererkennung, externe Theme-Pakete/BTX-Spezialansichten, weitergehende KI/MCP-Funktionen, Signierung/Updater und echte Bank-/Lizenz-/Transfer-Abnahme. Die beiden geparkten Bereiche werden nicht weiterentwickelt, vorhandene Funktionen bleiben erhalten.

## Vorlagen und Farbthemes — 0.5.1

Vorlagen lassen sich direkt erstellen und bearbeiten; Symbol, Kontobezug und Favoritenstatus bleiben im verschlüsselten Tresor. Empfängervorschläge verwenden nur gebuchte EUR-Ausgaben des ausgewählten Kontos mit gültiger IBAN; Gruppierung nach Bankname und IBAN, häufigster Betrag statt Durchschnitt, letzter Zweck. Die Auswahl löst keine Zahlung aus. Datenänderungen im Vorlageneditor werden bei Speicherfehlern zurückgenommen.

Import/Export von Farbtheme-CFGs mit Größenlimit und Validierung von Farben/Schaltern; eine eigene Palette wird in den Benutzereinstellungen gespeichert. Vollständige Theme-Pakete, externe Assets und alle Mac-Theme-Effekte bleiben offen. 56 Selbsttests, Tray-Lebenszyklustest und UI-Review einschließlich Vorlageneditor. Geparkte Bereiche unverändert.
Zusätzliche Regression 0.5.1: Das Öffnen der Darstellungseinstellungen ändert das ausgewählte Theme nicht mehr durch verzögerte ComboBox-Datenbindung; Theme-Änderungen werden nur nach einer bewussten Auswahl übernommen.

## Händlererkennung und Regel-UX — 0.6.0

Die lokale Auflösung ergänzt Provider-/Kartenzahlungsextraktion, ABWA, Filialnummern, Rechtsformen, Alias-Kanonisierung und Bargelderkennung. OriginalMerchant und BankCounterpartyName bleiben erhalten. Gleiche eigene Konto-IBAN wird als Gegenseite verworfen; ein vollständiges OwnPartyRegistry mit Namenslernen ist noch offen. Keine Händleranmeldung und kein externer Abruf: die geparkten Anbindungen bleiben geparkt.

Priorität: manueller Anzeigename pro Buchung → aktive Benutzerregel → automatische Erkennung. Einzelbuchungsnamen bleiben bei eindeutiger Reconciliation erhalten; nicht zuordenbare Anreicherungen landen im bestehenden Archiv. Regeln werden mit konkreten Vorher-/Nachher-Zeilen geprüft; die Vorschau ist auf die letzten 2.000 Umsätze und ein Zeitbudget begrenzt. Bei Speicherfehlern werden Regeln und Buchungsnamen gemeinsam zurückgesetzt.

58 Selbsttests, Tray-Lebenszyklustest und gerenderte UI-Prüfung. Noch keine reale Bank-/TAN-Abnahme, keine manuelle Multi-DPI-Prüfung; vollständige Mac-Parität weiterhin offen.

## Eigene Händlerlogos — 0.7.0

Buchungsdetails bieten die Wahl zwischen Einzelbuchung und Händler im ausgewählten Konto. Gemeinsame Logos werden einmal im verschlüsselten Tresor gespeichert und beim Laden sowie bei neuen Umsätzen anhand des aufgelösten Händlernamen angewendet. Andere Konten und individuelle Logos anderer Buchungen bleiben unverändert. Umbenennen eines Händlers ändert die Zuordnung; unbekannte Empfänger und Intermediär-Platzhalter bieten nur die Einzelbuchung. Zurücksetzen wirkt auf den ausgewählten Umfang. Speicherfehler rollen gemeinsame Änderungen zurück.

Bildimport nutzt transparente, proportionale 128-Pixel-PNGs und hochwertige Skalierung. Kategorie-Fallback wird nach Bildauswahl nicht mehr darüber gezeichnet. Leere Buchungstext-/IBAN-/BIC-Felder sind ausgeblendet. 59 Selbsttests, gerenderte UI-Prüfung, installierter Tray- und Demo-Starttest erfolgreich. Weiterhin keine reale Bank-/TAN-Abnahme oder manuelle Multi-DPI-Prüfung. Übrige aktive und geparkte Restpunkte unverändert.

## Fenster, Tray und Buchungsaktionen — 0.8.0

Die Umsatzliste hat eine feste Fenstergröße, eigene verschiebbare Titelleiste und Icons für Aktualisieren, Einstellungen, TopMost, Minimieren und Schließen. Schließen blendet weiterhin ins Tray aus. Such-/Filter-/Kategorieicons sind 24 statt 28 Pixel groß und sitzen vier Pixel vom rechten Rand entfernt. Der Money-Mood-Verlauf wird über die gesamte Fensterfläche gezeichnet; Header, Buchungszeilen, Datumstrenner und Footer verwenden denselben Hintergrund. Theme-Paletten bleiben berücksichtigt.

Das Systray-Icon wird bei Kontowechsel aus dem Banklogo erzeugt, einschließlich Freigabe alter Icon-Handles. Bei Sperre bleibt das zuletzt ausgewählte Logo erhalten; beim Start ohne entsperrtes Konto sowie bei Aggregat/unbekannter Bank wird ein neutrales Banksymbol verwendet. About zeigt die Assembly-Version, Maik Klotz als Entwickler/Herausgeber entsprechend Mac-Quellcode, YAXI PSD2 Banking API / Routex, Website, Support und Lizenzhinweise.

Buchungsdetails haben Glocke und Gelesen-Status neben Schließen. Die Glocke öffnet einen kompakten Datumsdialog inklusive Erledigen/Entfernen bei bestehenden Erinnerungen. Das Händlerbild ist ein tastaturbedienbarer Button mit Bildauswahl/Reset für Einzelbuchung und Händler im Konto; keine separaten Bildbuttons oder Erinnerungssektion mehr. Anhänge bleiben unverändert geparkt.

Prüfung: 59 Selbsttests, Tray-Regression einschließlich tatsächlichem Iconvergleich nach Kontowechsel, UI-Review mit Main-TopMost und Erinnerungsspeicherung. Screenshots von Umsatzliste, Buchungsdetails, Erinnerung und About geprüft. Physisches Verschieben, Windows-Snap und Multi-DPI sind nicht manuell geprüft. Übrige in früheren Abschnitten ausgewiesene Funktionslücken bleiben offen; kein vollständiger Port behauptet.

## Abrufzustimmung und UI-Korrekturen — 0.9.0

Die eigene Titelleiste aus 0.8.0 wurde entfernt. Wieder native feste Windows-Titelleiste ohne zusätzliche App-Icons; Fenstergröße bleibt fest. Gelesen-Markierung in Umsatzzeilen mit 10 Pixel Randabstand und 6 Pixel Durchmesser. Footer-Icons einheitlich 24 × 24 Pixel wie Suche/Filter; Senden bleibt innerhalb des rechten Randes.

SCA: ConnectionData wurden bereits verschlüsselt gespeichert, der separate nichtinteraktive SDK-Zugang wurde jedoch nicht genutzt und Sessions wurden ignoriert. BankReadAccess nutzt nun für Balance/Umsätze zuerst ConnectionData als SDK-Access-Variante. Nur InterruptError, Unauthorized oder InvalidCredentials wechseln einmal mit neuem Ticket in den bestehenden interaktiven Freigabeablauf; andere Fehler werden nicht automatisch wiederholt. Zustimmung wird dabei nicht gelöscht. Neue ConnectionData werden sofort, auch vor nachfolgendem Mapping, gespeichert. Fehlende ConnectionData im Ergebnis erhalten den bisherigen Wert. Sessions sind pro Verbindung/Dienst getrennt, nur im Speicher, einmalig konsumierbar und nach maximal fünf Minuten verworfen; Sperren leert den Cache. Der Schnellabruf verwendet keine Sitzung. Keine Überweisungsänderung.

Automatische Umsatzabfrage umfasst genau 90 Kalendertage inklusive heute (statt drei Monaten mit ggf. 92 Tagen); explizites Nachladen älterer Umsätze bleibt bestehen. Bestehende ältere Buchungen werden nicht dadurch gelöscht. Quellen: Mac YaxiService.swift SessionStore/schnellUmsaetze, C++ SDK Access-Variante, https://docs.yaxi.tech/sessions.html und https://docs.yaxi.tech/transactions.html. Diese Codefehler sind korrigiert; ob die betreffende Bank jetzt ohne erneute SCA abruft, ist ohne echten Bankabruf noch nicht bestätigt. Bankseitig erforderliche Freigaben bleiben möglich.

Offenen OwnPartyRegistry-Punkt ergänzt: Account.OwnerName aus Accounts-Ergebnis, sonst konservatives Lernen anhand eindeutiger Gegenrichtung und passender eigener IBAN. Abweichende Namen werden nicht geraten. Resolver prüft alle Konten dieses Tresors anhand eigener IBAN und normalisiertem Zweiwort-Namen wie im Mac-Original; keine globale Vermischung zwischen Demo und echtem Tresor. Original-Bankfelder und manuelle Händlerregeln bleiben erhalten.

62 Selbsttests plus Tray-/UI-Prüfung, einschließlich simulierter Abrufzustimmung, frischer Fallback-Tickets, Sitzungstrennung/-verbrauch, Fehler ohne Wiederholung, Zeitraumgrenzen und Kontoinhaberlernen. Echte Bank-/TAN-Abnahme weiterhin offen. Andere aktive Punkte: Windows Hello, SQLite/Mac-Migration, Theme-Pakete/BTX, vollständige Inline-QuickSend-UX, weitergehende KI/MCP-Funktionen, Signierung und Updater. Händleranbindungen/Belege und Aufrunden bleiben geparkt.

## QuickSend direkt im Flyout — 0.10.0

QuickSendDrawerView.swift dient als Ablaufvorlage: Eingabe → Prüfung → ausdrückliches Senden → Ergebnis. Der Windows-Flyout öffnet beim Prüfen keinen zweiten Transfereditor mehr. Die Prüfung zeigt Quellkonto, Empfänger, vollständige IBAN, Betrag und Zweck. Zurück bewahrt Eingaben. Ein Kontowechsel verwirft den geprüften Entwurf. Die Höhe richtet sich nach dem Text; das Ergebnis benötigt keine leere Eingabefläche.

Erst Jetzt senden bzw. Demo simulieren ruft den gemeinsamen Zahlungsablauf auf. Der große Transfereditor behält seine bestehende Abschlussbestätigung. Inline wird diese durch die vorherige Prüfung plus separaten Sendeklick ersetzt. Journal, End-to-End-ID, Lizenz-/Transfer-Key-Prüfung und Schutz gegen Wiederholung unklarer Aufträge bleiben zentral. Während des Versands ist der Sendeknopf gesperrt, das Flyout verschwindet nicht beim Fokuswechsel zu Bankdialogen, Warten beenden fordert Abbruch an; ein bereits übermittelter Auftrag wird dabei nicht als rückgängig gemacht dargestellt. Rückgelieferte ConnectionData werden auch nach Transfer im verschlüsselten Tresor aktualisiert. Eine nachfolgende Sperre überschreibt den zurückgemeldeten Ausgang nicht.

Demo: lokale Simulation erzeugt genau einen Journal-Eintrag ohne Bank-/Lizenzaufruf. UI-Prüfung deckt nichtsendende Prüfung, Zurück, Quellkontowechsel und genau einen simulierten Auftrag ab. 62 Selbsttests plus Tray- und gerenderte UI-Prüfung erfolgreich. Echte Überweisung/VoP/TAN weiterhin nicht abgenommen; separates simplesend-Transfer-Schlüsselpaar weiterhin erforderlich. Keine vollständige Mac-Parität behauptet. Offen bleiben u.a. Windows Hello, SQLite/Mac-Migration, externe Theme-Pakete/volle BTX-Ansichten, weitergehendes AI/MCP, Signierung/Updater und reale Abnahme. Geparkte Bereiche unverändert.

## PNG-Theme-Pakete — 0.11.0

CFG- und ZIP-Import mit Hell-/Dunkelvorschau vor Übernahme. ThemePackage liest ZIP-Einträge direkt und extrahiert keine Pfade. Eine CFG, einfache Bildreferenzen, Prüfung auf doppelte Dateinamen/Pfad-Ausbruch/Symlink, Archive höchstens 40 MB und 128 Einträge, entpackte Dateien höchstens 24 MB, CFG 64 KB, PNG 4 MB / 4096 Pixel / 8 Megapixel. PNG-Signatur und Maße werden vor Bilddekodierung geprüft. Nicht referenzierte Dateien werden nicht gespeichert.

PNG-Wallpaper für Umsatzliste und Flyout, Theme-Logo im Saldoheader sowie Ringgrafik mit Hell-/Dunkelvarianten. Key-Schreibweise ist wie im Mac-Parser nicht case-sensitiv. Bilder werden zusammen mit der Palette in den Benutzereinstellungen gehalten; der Umsatz-Hintergrund wird pro Theme-Wechsel geladen statt pro Buchungszeile neu dekodiert. ZIP-Export enthält CFG und Bilder. Vorschau ist unverbindlich, Abbrechen verändert das aktive Theme nicht. Standard aktiviert das Standardtheme und behält das importierte Paket zur erneuten Auswahl. Es gibt weiterhin einen eigenen Theme-Speicherplatz, keine Verwaltung mehrerer importierter Pakete.

64 Selbsttests einschließlich ZIP-Rundlauf, Bildpersistenz, Groß-/Kleinschreibung, unzulässiger Pfade, mehrerer CFGs, fehlender/unsupported Grafiken. UI-Review enthält Vorschau, tatsächliche Umsatzansicht mit Wallpaper und Standard-Reset sowie ein synthetisches sample-theme.zip. Kein echter Bank-/Zahlungsaufruf.

Grenzen: PNG unterstützt; referenzierte SVG/PDF werden mit Erklärung abgelehnt. Externe Schriften und vollständige BTX/CRT-/Theme-Spezialansichten bleiben offen. Die Fenstergröße der Umsatzliste bleibt fest; Wide-Wallpaper wird gespeichert, aber derzeit nicht dargestellt. Keine vollständige Mac-Parität behauptet. Weitere Restpunkte und geparkte Bereiche gelten unverändert.
CFG-Export bleibt für Farbthemes ohne Grafiken zusätzlich zum ZIP-Export verfügbar.
