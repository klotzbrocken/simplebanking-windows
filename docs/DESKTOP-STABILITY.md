# Desktop und große Tresore – 0.19.1

## Fenster

Das Desktop-Widget korrigiert seine Position beim Aufklappen von Quick Send sowie nach Änderungen an Monitoren, Skalierung und Arbeitsfläche. Negative Monitor-Koordinaten bleiben gültig; ein entfernter Monitor lässt das Widget auf einem vorhandenen Bildschirm erscheinen. Während des Ziehens erfolgt keine störende Positionskorrektur.

Die Umsatzliste behält zwei feste Breiten und lässt sich weiterhin nicht frei skalieren. Bei kleinen Arbeitsflächen wird die Fenstergröße begrenzt, damit Titelleiste und Footer erreichbar bleiben. Beim Wechsel auf einen größeren Bildschirm werden die vorgesehenen Maße wiederhergestellt. DPI-Wechsel erneuern die festen Größenbegrenzungen.

Geometrietests decken 100, 125, 150 und 200 Prozent Skalierung, negative Koordinaten, entfernte Bildschirme und vier Taskbar-Positionen ab. Der UI-Test prüft das aufgeklappte Widget am unteren Bildschirmrand und die Rückkehr eines außerhalb aller Bildschirme positionierten Widgets. Das ersetzt keinen physischen Test mit mehreren Monitoren unterschiedlicher Skalierung.

## Verschlüsselter Tresor

Der neue Test mit 100.000 Buchungen auf zehn Konten hat die bisherige 64-MB-Grenze überschritten. Ursache waren auch bei jeder Buchung mitgespeicherte leere optionale Felder. Diese Standardwerte werden jetzt ausgelassen und beim Laden durch die unveränderten Konstruktorvorgaben rekonstruiert. Pflichtfelder und gesetzte Anreicherungen bleiben gespeichert. Dateiformat v1, Verschlüsselung und Größenlimit bleiben erhalten; bestehende Tresore benötigen keine Migration.

Der Test speichert und lädt alle Buchungen, vergleicht sie vollständig, verändert eine Notiz und prüft anschließend sowohl die aktuelle als auch die vorherige Tresorgeneration. Auf dem Entwicklungsrechner: 29.598.131 Bytes, initiales Speichern 628 ms, Laden 751 ms, erneutes Speichern 492 ms. Das sind Einzelmessungen mit synthetischen Daten ohne große Bildanhänge, keine allgemeine Leistungsgarantie. Der Prozess belegte in der Spitze etwa 378 MiB; der Tresor bleibt vollständig im Arbeitsspeicher.

Wiederholbar mit `simplebanking.exe --benchmark-vault <report.json>`. Es werden ausschließlich temporäre synthetische Daten verwendet und anschließend entfernt. GitHub Actions führt den Test ebenfalls aus und archiviert den Messbericht.

101 Selbsttests sowie Tray-Lebenszyklus und gerenderte UI-Prüfung. Keine realen Bank- oder Zahlungsaufrufe. Geparkte Funktionen bleiben außerhalb dieser Runde.
