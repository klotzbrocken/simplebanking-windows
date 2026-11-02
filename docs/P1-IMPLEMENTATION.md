# Prio 1: Entwicklungsrunde 0.18.0

Stand: 12.10.2026. Diese Runde behandelt die drei offenen Entwicklungsbereiche; die getrennte Liste der echten Bank- und Geräteabnahmen steht in [P1-BETA.md](P1-BETA.md).

## Darstellung und Themes

- Theme-Farben und Schriften gelten auch für Einstellungen, Regeln, Buchungsdetails und Auswertungen. Dynamisch angelegte Dialogelemente übernehmen das aktive Theme. Wiederholte Wechsel erhalten Schriftgröße und ursprüngliche Texte.
- BTX verwendet VT323, Großschreibung, eckige Schalter, Textsteuerung, Kategorie-Mosaike, gepunktete Zeilen und die gestreckte Kontostandtypografie des Originals. Doppelklick auf das blinkende Telefonsymbol schaltet den durchklickbaren Röhreneffekt ein oder aus. Das Overlay folgt Sichtbarkeit und Lebensdauer seines Fensters.
- Theme-Pakete unterstützen PNG, eigenständige SVG-Pfade, die erste PDF-Seite sowie private TTF/OTF-Schriften ohne Installation im System. Vektorbilder werden beim Import in PNG umgewandelt und mit dem Theme exportiert. Größenlimits und lokale Referenzen werden geprüft; aktive/externe SVG-Inhalte sind ausgeschlossen.
- Breite Hintergrundbilder, Banklogo-Schalter und der Vorrang eines eigenen Ringbilds entsprechen dem Theme-Vertrag. Dunkle Auswertungen und native Auswahlfelder haben angepasste Kontraste.

## Händler und Regeln

- Automatische Händlerauflösung speichert Herkunft und Confidence; Einzelkorrekturen und Benutzerregeln behalten Vorrang. PayPal, Klarna, Kartenprozessoren, ABWA und Bankgegenparteien folgen der bestehenden Mac-Reihenfolge.
- Der Original-Kategorienkatalog erkennt deutsche Wortstämme und ganze Händlernamen. Bestehende und manuell gesetzte Kategorien werden erhalten.
- Aus Umsatz-Kontextmenü oder Buchungsdetails lässt sich eine Händlerregel aus dem Originaltext vorbefüllen. Der Regelassistent bietet passende Operatoren und feste Werte für Intervall/Buchungsart, Kontobereich und Vorher-/Nachher-Vorschau. Fehlgeschlagenes Speichern setzt Änderungen im Speicher zurück.

## Auswertungen

- Strukturierte YAXI-Banktypcodes (ISO, SWIFT und nationale Codes) bleiben beim Abruf und Speichern erhalten. CAMT übernimmt ISO/GVC. Daueraufträge und Bankgebühren verwenden diese Informationen zusätzlich zum Banktext.
- Die eigene Fixkostenansicht zeigt Betrag, Rhythmus, nächsten Termin und Status im Gehaltszyklus. Details enthalten die zugehörigen Buchungen und erlauben den Ausschluss aus Fixkosten und Abos.
- Kostenstatistiken normalisieren bestätigte monatliche, vierteljährliche und jährliche Wiederholungen. Geldalter zeigt Spanne, Einordnung und Veränderung. Vorgemerkte beziehungsweise zukünftige Umsätze verfälschen MMI und Kontoring nicht.

## Prüfung und Auslieferung

- 89 lokale Regressionstests einschließlich SVG/PDF-Rasterung, privater Schriften, Import-/Export-Rundlauf, Banktypcodes, Händlerregeln, Kategorien, Geldalter und Theme-Wechseln.
- Automatisches Rendering der echten WinForms-Steuerelemente mit Demodaten: Einstellungen, Details, Regeln und sechs Dashboard-Bereiche; zusätzliche dunkle und BTX-Ansichten. Interaktionsprüfung für Overlay-Sichtbarkeit und Theme-Wechsel.
- Selbstständiges Windows-x64-Paket und unsignierte Beta-Setup.exe, Version 0.18.0. Bibliothekslizenzen liegen dem Paket bei. Lokale Banking-Konfiguration wird ausschließlich beim lokalen Build eingebettet; sie gehört nicht ins öffentliche Repository.

Referenzen: Mac-Stand `9be7a250`, insbesondere `ThemeSupport.swift`, `BTXCRT.metal`, `BTXMosaic.swift`, `MerchantResolver.swift`, `TransactionCategorizer.swift`, `BookingType.swift`, `Bankgebuehren.swift`, `MoneyAge.swift` und `SubscriptionStats.swift`. Der YAXI-Codevertrag wurde mit den JSON-Fixtures des C++-Clients abgeglichen.

Nicht Teil dieser Runde: geparkte Belege/Händleranbindungen und Aufrunden, ausgeschlossene Mac-Datenmigration sowie Updater, zusätzliche KI- und MCP-Funktionen. Echte Bankfreigaben, Zahlungen und persönliche Windows-Hello-Abnahmen wurden nicht ausgeführt.
