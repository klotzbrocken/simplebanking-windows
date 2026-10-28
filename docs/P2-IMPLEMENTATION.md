# Prio 2: Windows 0.19.0

Diese Runde setzt die fünf Entwicklungsbereiche der Prio-2-Liste um. Geparkte Belege/Händleranbindungen, Aufrunden und die ausgeschlossene Mac-Datenmigration bleiben außerhalb des Auftrags.

## Windows-Updater

Unter **Einstellungen → Über → Nach Updates suchen** liest die App die Releases des Windows-Repositorys. Der Nutzer kann Beta-Releases einbeziehen. Neuere numerische Programmversionen werden angeboten; ältere und gleiche Versionen werden nicht installiert.

Die App lädt ausschließlich Setup-Assets dieses Repositorys und verlangt den von GitHub bereitgestellten SHA-256-Digest. Größe und Hash werden vor dem Bereitstellen und erneut vor dem Start geprüft. Abbruch/Fehler entfernt den unvollständigen Download. Erst **App schließen und installieren** startet den normalen Setup-Assistenten. Es gibt keine stille Installation und keine automatische Veröffentlichung eines Builds. Die Beta bleibt unsigniert. Ein Release ohne Digest wird nicht angeboten.

## PDF- und Bildscanner

Alle Seiten einer Rechnung werden vollständig gerendert, einschließlich Vektortext, Seitenrotationen und eingebetteter Bilder. QR-Erkennung und lokale deutsche/englische OCR untersuchen die gerenderte Seite; Deskew und bis zu vier Orientierungen verbessern die Erkennung. Ein gültiger GiroCode hat Vorrang.

Die Oberfläche zeigt Fortschritt und erlaubt Abbruch. Grenzen: 30 MB, maximal 20 PDF-Seiten, Rendering bis 3200 Pixel an der längeren Kante, drei Minuten für einen Scan. Größere Dokumente werden mit einer Erklärung abgelehnt und nicht still abgeschnitten. OCR-Ergebnisse müssen weiterhin im normalen Überweisungsdialog geprüft werden.

## CAMT-Sammelbuchungen

Mehrere `TxDtls` einer `Ntry` ergeben getrennte Umsätze mit Betrag, Richtung, Gegenpartei, Zweck, Referenz und Banktypcodes. Einzelbeträge in der Kontowährung müssen exakt den gebuchten Gesamtbetrag ergeben. Fehlende Einzelbeträge, nicht auflösbare Fremdwährungen oder Differenzen führen zum Abbruch vor dem Import. Die Vorschau und der bestehende Dublettenabgleich bleiben erhalten. Fehlgeschlagenes Speichern stellt den vorherigen Zustand im Speicher wieder her.

## KI

- Antworten von Anthropic, Mistral und OpenAI erscheinen als Textstrom. Abbruch, unterbrochene Streams, erneutes Fragen, Folgefragen, neues Gespräch, Kopieren und verwendete Ergebnisdaten sind integriert.
- Für Freitextfragen erstellt das Modell eine SELECT-Abfrage. Diese läuft in einer getrennten In-Memory-Datenbank, die ausschließlich bereinigte, gebuchte Umsätze des gewählten Kontos enthält. Sie hat keinen Zugriff auf Tresor, andere Konten, Dateien oder Netzwerk. SQLite-Authorizer, feste Funktionsliste, Leseprüfung und VM-/Zeitbudget sind unabhängig vom Modell-Prompt. Höchstens 200 Zeilen und 100 KB Ergebnisdaten werden weitergegeben.
- Unter Labs lässt sich die automatische Kategorisierung neuer Umsätze nach Bankabruf/Import ausdrücklich einschalten. Standard ist aus. Manuelle Kategorien und Regelzuordnungen bleiben erhalten. Pro Anfrage maximal 50 Umsätze; aufeinanderfolgende Jobs werden gesammelt. Sperren und Beenden brechen laufende Kategorisierung ab; veraltete Ergebnisse werden verworfen.
- Der Demo-Chat bleibt vollständig lokal. Persönliche Anbieter-Keys liegen wie bisher im verschlüsselten Tresor. Tests verwenden simulierte HTTP-Antworten; es wurden keine echten Kontodaten an KI-Anbieter gesendet.

## MCP und CLI

Unter **Einstellungen → Extras**: CLI/MCP einschalten und **Clients verwalten** öffnen. Jeder Client erhält einen Namen, explizit ausgewählte Konten, eine Gültigkeit von 1–365 Tagen und einzelne Bereiche: Konten/Salden, Umsätze, Auswertungen, optional Überweisungsentwürfe. Der Token wird einmal angezeigt; gespeichert wird nur sein SHA-256-Hash im verschlüsselten Tresor. Zugriff lässt sich sofort widerrufen.

Der stdio-Client startet `simplebanking.exe --mcp`; der Token wird als Umgebungsvariable `SIMPLEBANKING_TOKEN` hinterlegt. Dasselbe gilt für `--cli`. Frühere Live-Aufrufe ohne Token benötigen jetzt einen eingerichteten Client. `--demo` bleibt ohne Token nutzbar und ausschließlich lesend. Beispielkonfiguration (Platzhalter ersetzen):

```json
{
  "mcpServers": {
    "simplebanking": {
      "command": "C:\\Users\\NAME\\AppData\\Local\\Programs\\simplebanking\\simplebanking.exe",
      "args": ["--mcp"],
      "env": {"SIMPLEBANKING_TOKEN": "EINMALIG_ANGEZEIGTER_TOKEN"}
    }
  }
}
```

`tools/list` zeigt nur freigegebene Bereiche. Jeder Aufruf prüft Token, Ablauf, Widerruf, Kontobereich, entsperrten App-Zustand und globale Freigabe erneut. Transport bleibt eine Named Pipe für denselben Windows-Benutzer, ohne Netzwerklistener.

`prepare_transfer` erlaubt nur gültige EUR-Entwürfe auf freigegebenen Konten. Pflichtfelder sind `account_id`, `recipient`, `iban`, `amount`, `purpose`, `request_id`; `date` ist optional. Wiederholungen mit derselben Request-ID liefern denselben Entwurf; geänderte Inhalte werden abgelehnt. Der Aufruf erzeugt **keinen Zahlungsauftrag**. Unter **MCP-Entwürfe prüfen** muss der Nutzer Empfänger, IBAN, Betrag, Termin und Konto ansehen und den Entwurf freigeben. Anschließend gelten die normalen getrennten simplesend-Prüf- und Sendeschritte. Maximal 100 ungeprüfte Entwürfe.

Hinweis zum Mac-Abgleich: Der aktuelle Mac-MCP ist ausschließlich lesend. Die Windows-Entwurfsfunktion folgt der ausdrücklich beauftragten Prio-2-Liste und besitzt deshalb einen zusätzlichen, standardmäßig ausgeschalteten Berechtigungsbereich.

## Validierung

97 Selbsttests: unter anderem Summen-/Währungsprüfung bei CAMT, Soll-/Haben-Beträge und Dubletten, Update-Auswahl/Prüfsummen/Manipulation, Streams der drei Anbieter einschließlich Fehler/Abbruch, SQL-Isolation und Ressourcenbudget, Token-/Kontoberechtigungen und idempotente Entwürfe sowie eine vollständig gerenderte, gedrehte Rechnung auf der vierten PDF-Seite.

Die UI-Prüfung verwendet echte WinForms-Steuerelemente mit synthetischen Daten. Sie prüft Folgefragen im Demo-Chat und den vollständigen MCP-Entwurfs-Freigabeweg ohne Zahlungsaufruf. Screenshots decken auch neue Client-, Updater- und Entwurfsdialoge ab. Echte Bank-, Hello- und KI-Anbieterabnahmen bleiben gesonderte Prüfungen.

## Technische Quellen

- [GitHub Releases API und Asset-Digests](https://docs.github.com/en/rest/releases/releases?apiVersion=latest)
- [OpenAI Streaming](https://developers.openai.com/api/docs/guides/streaming-responses)
- [Anthropic Streaming](https://platform.claude.com/docs/en/build-with-claude/streaming)
- [Mistral Chat Completions](https://docs.mistral.ai/studio/conversations/chat-completion)
- [SQLite Authorizer](https://www.sqlite.org/c3ref/set_authorizer.html) und [VM-Fortschrittsgrenze](https://www.sqlite.org/c3ref/progress_handler.html)
- [MCP Tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)

Mac-Vorlagen: `SQLGuard.swift`, `LLMChatModels.swift`, `AIProviderService.swift`, `MCPClientStore.swift`, `TransferDocumentScanner.swift` sowie der CAMT-Importer im bereits dokumentierten Referenzstand.
