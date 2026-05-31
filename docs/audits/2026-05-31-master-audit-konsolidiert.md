# Konsolidierter Master-Audit SewerStudio / AuswertungPro

Datum: 2026-05-31  
Basis: Codex-Audit, Claude-Audit, Gemini-Audit-Vergleich  
Arbeitsweise: keine Codeaenderungen, kein Commit, kein Push

## Kurzfazit

Das Gemini-Audit wird fuer die Bewertung nicht verwendet. Es hat groesstenteils alte PowerShell-Dateien im Projektwurzelordner bewertet, nicht die echte .NET-App unter `src/AuswertungPro.Next.*`.

Das Codex-Audit und das Claude-Audit ergaenzen sich. Codex hat den kritischsten Datenintegritaets-Bug gefunden: Die Import-Vorschau ist nicht wirklich read-only. Claude hat den zweitwichtigsten fachlichen Bug gefunden: KI-Befunde koennen automatisch als akzeptiert gespeichert werden.

Die echte Prioritaet ist deshalb klar: zuerst Datenverlust verhindern, dann KI-Auto-Uebernahme stoppen, dann Crash-Risiken und gefaehrliche UI-Aktionen absichern.

## Audit-Bewertung

| Audit | Urteil | Begruendung |
|---|---|---|
| Gemini | Nicht verwenden | Hat Alt-/Totlast wie `*.ps1` bewertet statt die echte .NET-App. |
| Codex | Stark | Hat den kritischen DryRun-/Vorschau-Bug gefunden. |
| Claude | Stark | Hat KI-Auto-Uebernahme und fachliche Plausibilitaetsluecken gefunden. |

## Verifizierte Top-Prioritaeten

| Rang | Schwere | Befund | Status | Beleg | Empfehlung |
|---:|---|---|---|---|---|
| 1 | Kritisch | Import-Vorschau/DryRun ist nicht read-only | verifiziert | `ImportPageViewModel.cs:172-180` reicht auch bei `dryRun` das echte `_shell.Project` weiter | DryRun immer auf Projekt-Kopie ausfuehren |
| 2 | Hoch | KI uebernimmt gruene Befunde automatisch als akzeptiert | verifiziert | `PlayerWindow.Coding.cs:3549-3558` setzt `Decision = Accepted`, wenn `gateResult.IsGreen` | KI darf nur vorschlagen, Mensch bestaetigt |
| 3 | Hoch | `CodingModeWindow` hat alten VLC-Close-Pfad | verifiziert | `CodingModeWindow.xaml.cs:240-253` stoppt/disposed direkt ohne `_closing`/VideoView-Detach | Sicheren Close-Pfad aus `PlayerWindow` uebernehmen |
| 4 | Hoch | Verzoegerter VLC-Zugriff nach Fenster-Schliessen moeglich | verifiziert | `CodingModeWindow.xaml.cs:396-403` nutzt nach `Task.Delay` weiter `_player` und `Dispatcher.Invoke` | CancellationToken und Closing-Guard nutzen |
| 5 | Hoch | Projekt oeffnen schuetzt Dirty-Daten nicht sichtbar | plausibel/verifiziert am Einstieg | `ShellViewModel.cs:274-282` laedt neues Projekt ohne erkennbare vorherige Dirty-Abfrage | Vor Oeffnen/Neu Speichern/Verwerfen/Abbrechen fragen |
| 6 | Hoch | Einzelloeschen ohne Rueckfrage | verifiziert | `DataPageViewModel.cs:431-437` entfernt direkt | Rueckfrage oder Undo/Papierkorb |
| 7 | Hoch | Spalte leeren ist zu gefaehrlich | verifiziert | `DataPage.xaml.cs:930-950` leert alle Werte und setzt `UserEdited` zurueck | Anzahl anzeigen, starke Bestaetigung, Undo |
| 8 | Mittel/Hoch | MultiModel kann still auf Ollama-only zurueckfallen | verifiziert | `VideoAnalysisPipelineService.cs:192-208` gibt bei Auto-Modus `false` zurueck | UI klar anzeigen: MultiModel aus, Qwen-only aktiv |
| 9 | Mittel/Hoch | KI-Timeouts widersprechen sich | verifiziert | `MultiModelAnalysisService.cs:35` = 300s, `EnhancedVisionAnalysisService.cs:148/190-191` = 60s | Einen zentralen Timeout verwenden |
| 10 | Mittel | IBAK/KINS Contains-Matching kann Haltungen verwechseln | verifiziert | `IbakExportImportService.cs:125-132`, `KinsImportService.cs:537-542` | Nur exakt/Gegenrichtung/Prefix-normalisiert matchen |
| 11 | Mittel | GIS-Pfade hart auf `D:\QGIS_V4` gesetzt | verifiziert | `KarteViewModel.cs:22-28` | Pfade in Projekt/Einstellungen speichern |
| 12 | Mittel | GIS-Farbskala fest invertiert | verifiziert | `KarteViewModel.cs:30-31`, `:179-187` | Skala sichtbar machen und mit echten Klassen testen |
| 13 | Mittel | Live-Control ohne Token und Body-Limit | verifiziert | `LiveControlServer.cs:40-65`, `:122-128`, `:169-192` | Token, Body-Limit, Retry separat freigeben |
| 14 | Mittel | Reimport kann Protokoll-/Befunddaten ueberschreiben | praezisiert | Stammdaten sind geschuetzt, siehe `HaltungRecord.cs:46-53`; Protokollpfade laufen separat | Protokoll-Reimport als Merge/Konflikt behandeln |

## Korrektur zum Codex-Befund User-Werte

Der pauschale Befund "WinCan/IBAK/KINS ueberschreiben User-Werte" ist fuer Stammdaten zu hart formuliert.

`HaltungRecord.SetFieldValue()` schuetzt user-editierte Felder:

- `HaltungRecord.cs:46-53`
- Wenn `FieldMeta.UserEdited == true` und der Import mit `userEdited: false` schreibt, wird der Wert nicht ueberschrieben.

Der Restpunkt bleibt aber wichtig:

- Protokoll-Eintraege
- VSA-Findings
- Foto-/Medienzuordnungen
- importierte Beobachtungen

Diese laufen nicht immer ueber denselben Feldschutz. Dort kann ein Reimport fachlich korrigierte Daten ersetzen.

## Wichtigste Code-Belege

### 1. DryRun mutiert Live-Projekt

`ImportPageViewModel.cs:172-180`:

```csharp
var ctx = new ImportRunContext(_importCts.Token, progress, runLog, dryRun);
return importFunc(source, _shell.Project, ctx);
```

Problem: `ctx.DryRun` existiert, aber das echte Projekt wird uebergeben. Importer, die `DryRun` nicht strikt beachten, schreiben direkt in die echten Daten.

### 2. KI akzeptiert automatisch

`PlayerWindow.Coding.cs:3549-3558`:

```csharp
var codingEvent = codingSessionService.AddEvent(entry);
codingEvent.AiContext = new CodingEventAiContext
{
    SuggestedCode = code,
    Confidence = gateResult.CompositeConfidence,
    Reason = finding.Label,
    Decision = gateResult.IsGreen
        ? CodingUserDecision.Accepted
        : CodingUserDecision.Ignored
};
```

Problem: Gruene KI-Befunde werden fachlich als akzeptiert gespeichert. Bei kritischen Codes oder hoher Severity muss der Mensch bestaetigen.

### 3. CodingModeWindow Close

`CodingModeWindow.xaml.cs:240-253`:

```csharp
_analysisCts?.Cancel();
_analysisCts?.Dispose();
StopAiStatusPulse();
_ollamaClient?.Dispose();
_player?.Stop();
_player?.Dispose();
_libVlc?.Dispose();
```

Problem: Kein `_closing`-Guard, kein idempotentes Cleanup, kein `VideoView.MediaPlayer = null`.

### 4. Contains-Matching

`IbakExportImportService.cs:125-132`:

```csharp
if (v.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(v, StringComparison.OrdinalIgnoreCase))
    return r;
```

Problem: `100-200` kann `100-2000` treffen.

## Nicht uebernehmen aus Gemini

Diese Punkte sind fuer die echte App nicht belastbar:

- "Mischbetrieb C# und PowerShell" als Hauptproblem
- Portierung alter PowerShell-Logik nach C#
- COM-Excel-Vorwurf
- falsche Modellannahmen wie `qwen3.5:27b`

Die alten `*.ps1`-Dateien koennen spaeter aufgeraeumt werden. Sie sind aber nicht der Kern der aktuellen App.

## Sofortplan 1-2 Tage

### 1. Import-Vorschau read-only machen

Ziel: Vorschau darf `Project.Data`, `ImportHistory`, `Conflicts`, `Dirty` nicht veraendern.

Umsetzung:

- In `ImportPageViewModel` bei `dryRun` mit `Project.DeepClone()` arbeiten.
- Nach Vorschau keine Live-Daten uebernehmen.
- Test: Vorschau starten, abbrechen, Projekt muss byte-/objektlogisch unveraendert bleiben.

### 2. KI-Auto-Accept stoppen

Ziel: KI macht Vorschlaege. Der Mensch entscheidet.

Umsetzung:

- `gateResult.IsGreen` darf nicht direkt `CodingUserDecision.Accepted` setzen.
- Stattdessen `Pending`/`Suggested` oder `Ignored` bis zur Nutzerbestaetigung.
- Besonders fuer Severity 4/5 niemals automatisch akzeptieren.

### 3. CodingModeWindow sicher schliessen

Ziel: Kein nativer VLC/D3D-Crash.

Umsetzung:

- `_closing`-Flag.
- Timer/CTS stoppen.
- `VideoView.MediaPlayer = null`.
- Stop/Dispose idempotent.
- Nach jedem `await`/Delay Closing pruefen.

### 4. Gefaehrliche UI-Aktionen absichern

Ziel: Kein versehentliches Loeschen.

Umsetzung:

- Einzelloeschen mit Rueckfrage.
- Spalte leeren mit Anzahl, Feldname und starker Bestaetigung.
- Optional Undo/Papierkorb.

### 5. Live-Control absichern

Ziel: Live-Control bleibt nuetzlich, aber nicht offen.

Umsetzung:

- Token beim Start erzeugen oder aus Datei/Env lesen.
- MCP sendet Token mit.
- Body-Limit setzen, z. B. 64 KB.
- `/pipeline/retry` separat freischalten.

## Kurzfristig 1-2 Wochen

1. Dirty-Guard vor Projekt oeffnen/neu.
2. IBAK/KINS Contains-Matching entfernen.
3. Reimport von Protokollen/Befunden als Konflikt/Merge behandeln.
4. KI-Timeouts vereinheitlichen.
5. Sidecar Deep-Health fuer YOLO/DINO/SAM/Qwen.
6. GIS-Pfade in Einstellungen/Projekt verschieben.
7. Karten-Farbskala mit echten Zustandsklassen absichern.

## Mittelfristig

1. KI-Vorschlaege als Review-Queue.
2. Import-Plausibilitaet: Meter, DN, Datum, Haltung, Video-Zuordnung.
3. VRAM-Scheduler statt nur Dokumentation.
4. Karte als Hauptnavigation.
5. Daten-Tabelle sortierbar.
6. Export-Assistent statt Ja/Nein-Kette.

## Abschlussurteil

SewerStudio hat eine starke technische und fachliche Basis. Die groessten Risiken sind jetzt nicht fehlende Funktionen, sondern zu maechtige Funktionen ohne genuegend Schutz.

Die naechsten Arbeiten sollten deshalb nicht "mehr Features" sein, sondern:

1. Vorschau wirklich sicher machen.
2. KI wieder als Assistent statt Entscheider behandeln.
3. Player/Coding-Fenster stabil schliessen.
4. Loeschen und Reimport fachlich absichern.

