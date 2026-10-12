# Prio 1 vor einer belastbar abgenommenen Banking-Beta

Stand: 12.10.2026. Dies sind offene Abnahmen, keine bereits bestätigten Produktfehler. Automatische Tests ersetzen keine reale Bank- oder Geräteprüfung.

| Priorität | Aufgabe | Erledigt, wenn | Mitwirkung |
|---|---|---|---|
| P1.1 | Reale Bank-/SCA-Matrix | Kontostand und Umsätze stimmen; Zustimmung wird wiederverwendet; Ablauf, Abbruch und erneute Freigabe funktionieren; Hintergrundabrufe öffnen keine Dialoge/Bankseiten und pausieren bis zur bewussten Aktion | Nutzer gibt Bankfreigaben/TAN ausschließlich selbst ein |
| P1.2 | Installer und Upgrade auf zweitem Windows-Rechner | Erstinstallation, Tray-Start, Upgrade, Reparatur, Deinstallation geprüft; vorhandener Tresor bleibt erhalten | Zweiter Rechner oder isolierte Windows-Testumgebung; bisherigen Codex-AppData-Sonderfall nicht als Erfolg werten |
| P1.3 | Sicherung/Wiederherstellung als vollständiger Benutzerablauf | Frisches Testprofil erhält Konten, doppelte Buchungen, Notizen, Anhänge, eigene Logos und Zugangsdaten; falsches Passwort und Schreibfehler erhalten Altbestand | Mit synthetischen Daten möglich; keine echten Daten veröffentlichen |
| P1.4 | Windows Hello | Einrichtung, automatische Entsperrung, Abbruch, Passwort-Fallback, Sperren und Passwortwechsel mit echtem Windows-Hello-Schlüssel geprüft | Nutzer bedient persönliche PIN/Biometrie |
| P1.5, nur bei Zahlungs-Beta | simplesend | Separater Transfer-Schlüssel, Lizenzprüfung, Empfänger-/Betragsprüfung, Freigabe/Abbruch und unklarer Bankausgang praktisch abgenommen; kein doppelter Auftrag durch Wiederholung | Testlizenz/Transfer-Konfiguration und persönliche Freigabe eines ausdrücklich beauftragten Testauftrags |

Die normale Lese-Beta benötigt P1.5 nicht, solange Überweisungen klar als nicht abgenommen ausgewiesen bleiben. Ohne echten Transfer-Test keine Behauptung vollständiger Zahlungsbereitschaft.

Der aktuelle Mac-Abgleich (Windows 0.17.0) wird separat mit lokalen Tests, UI-Rendering, CLI/MCP-Protokollprüfung und GitHub CI validiert. Details: [MAC-SYNC-2026-10.md](MAC-SYNC-2026-10.md).

Nicht P1 dieser Runde: ausgeschlossene Mac-Datenmigration, geparkte Händleranbindungen/Belege und Aufrunden, MCP-Überweisungsentwürfe ohne eigenes Berechtigungsmodell, zusätzliche Designs/Themes. Die unsignierte Auslieferung ist für diese Beta ausdrücklich gewählt.
