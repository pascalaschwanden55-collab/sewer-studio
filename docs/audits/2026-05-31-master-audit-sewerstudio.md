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
| 1 | Kritisch | Import-Vorschau/DryRun ist nicht read-only | verifiziert | `ImportPageViewModel.cs:172-180` reicht auch bei `dryRun` das echte `_shell.Project` weiter; IBAK/KINS/WinCan/LegacyPDF/XTF beachten `dryRun` nicht | DryRun immer auf Projekt-Kopie ausfuehren |
| 2 | Hoch | KI uebernimmt gruene Befunde automatisch als akzeptiert | verifiziert | `PlayerWindow.Coding.cs:3549-3558` setzt `Decision = Accepted`, wenn `gateResult.IsGreen` | KI darf nur vorschlagen, Mensch bestaetigt |
| 3 | Hoch | `CodingModeWindow` hat alten VLC-Close-Pfad | verifiziert | `CodingModeWindow.xaml.cs:240-253` stoppt/disposed direkt ohne `_closing`/VideoView-Detach | Sicheren Close-Pfad aus `PlayerWindow` uebernehmen |
| 4 | Hoch | Verzoegerter VLC-Zugriff nach Fenster-Schliessen moeglich | verifiziert | `CodingModeWindow.xaml.cs:396-403` nutzt nach `Task.Delay` weiter `_player` und `Dispatcher.Invoke` | CancellationToken und Closing-Guard nutzen |
| 5 | Hoch | Projekt oeffnen schuetzt Dirty-Daten nicht sichtbar | verifiziert | `ShellViewModel.cs:274-293` laedt neues Projekt + `ReplaceProject` ohne vorherige Dirty-Abfrage | Vor Oeffnen/Neu Speichern/Verwerfen/Abbrechen fragen |
| 6 | Hoch | Einzelloeschen ohne Rueckfrage | verifiziert | `DataPageViewModel.cs:431-437` entfernt direkt | Rueckfrage oder Undo/Papierkorb |
| 7 | Hoch | Spalte leeren ist zu gefaehrlich | verifiziert | `DataPage.xaml.cs:930-950` leert alle Werte und setzt `UserEdited` zurueck (nur schwache Ja/Nein-Box, keine Anzahl, kein Undo) | Anzahl anzeigen, starke Bestaetigung, Undo |
| 8 | Mittel/Hoch | MultiModel kann still auf Ollama-only zurueckfallen | verifiziert | `VideoAnalysisPipelineService.cs:192-208` gibt bei Auto-Modus `false` zurueck | UI klar anzeigen: MultiModel aus, Qwen-only aktiv |
| 9 | Mittel/Hoch | KI-Timeouts widersprechen sich | verifiziert | `MultiModelAnalysisService.cs:35` = 300s, `EnhancedVisionAnalysisService.cs:148` = 60s | Einen zentralen Timeout verwenden |
| 10 | Mittel | IBAK/KINS Contains-Matching kann Haltungen verwechseln | verifiziert | `IbakExportImportService.cs:125-132`, `KinsImportService.cs:537-542` | Nur exakt/Gegenrichtung/Prefix-normalisiert matchen |
| 11 | Mittel | GIS-Pfade hart auf `D:\QGIS_V4` gesetzt | verifiziert | `KarteViewModel.cs:22-28` | Pfade in Projekt/Einstellungen speichern |
| 12 | Mittel | GIS-Farbskala falsch geeicht (0-4 vs 0-5) | verifiziert | `ZustandColorMapper.cs:15-21` rechnet `5 - wert`; Feld ist 0-4 → Klasse 2 wird rot statt orange; `KarteViewModel.cs:30-31,179-187` | Schwellen auf 0-4 abstimmen, sichtbar testen |
| 13 | Mittel | Live-Control ohne Token und Body-Limit | verifiziert | `LiveControlServer.cs:40-65,122-128,169-192` (Loopback-only + Opt-in vorhanden) | Token, Body-Limit, Retry separat freigeben |
| 14 | Mittel | Reimport kann Protokoll-/Befunddaten ueberschreiben | praezisiert | Stammdaten sind geschuetzt (`HaltungRecord.cs:46-53`); Protokoll/Findings/Medien laufen separat | Protokoll-Reimport als Merge/Konflikt behandeln |
| 15 | Hoch | Offline-KI-Eintraege als `Manual` getarnt | verifiziert | `FullProtocolGenerationService.cs:392` setzt `Source = Manual` statt `Ai` | `Source = Ai` setzen (AiMeta wird schon befuellt) |
| 16 | Hoch | Keine fachliche Import-Plausibilitaet | verifiziert | nur `< 0`-Check (`PdfFieldMapping.cs:155`); keine Pruefung Meter > Laenge, DN-Bereich, Datum | Validierungsschicht mit Warnungen in den ImportLog |
| 17 | Mittel | Path-Containment: `..` in Haltungsname nicht entfernt | verifiziert | `ProjectPathResolver.cs:101-117` strippt `.` nicht | `.`/`..`-Segmente mappen + Containment-Check |

## Korrektur zum Codex-Befund "User-Werte"

Der pauschale Befund "WinCan/IBAK/KINS ueberschreiben User-Werte" ist fuer Stammdaten zu hart formuliert.

`HaltungRecord.SetFieldValue()` schuetzt user-editierte Felder:

- `HaltungRecord.cs:46-53`
- Wenn `FieldMeta.UserEdited == true` und der Import mit `userEdited: false` schreibt, wird der Wert nicht ueberschrieben.

Der Restpunkt bleibt aber wichtig — diese Pfade laufen NICHT ueber den Feldschutz und koennen fachlich korrigierte Daten ersetzen:

- Protokoll-Eintraege (`ApplyProtocol`)
- VSA-Findings (`UpdateFindings`)
- Foto-/Medienzuordnungen
- importierte Beobachtungen

## Wichtigste Code-Belege

### 1. DryRun mutiert Live-Projekt

`ImportPageViewModel.cs:172-180`:

```csharp
var ctx = new ImportRunContext(_importCts.Token, progress, runLog, dryRun);
return importFunc(source, _shell.Project, ctx);
```

Problem: `ctx.DryRun` existiert, aber das echte Projekt wird uebergeben. Eine Suche nach `dryRun` ueber den Import-Ordner trifft nur `MergeEngine.cs` und `MediaDistributionService.cs` — IBAK/KINS/WinCan/LegacyPDF/XTF schreiben direkt via `SetFieldValue(...)` und setzen `project.Dirty = true`.

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

Problem: Gruene KI-Befunde werden fachlich als akzeptiert gespeichert. Bei kritischen Codes oder hoher Severity muss der Mensch bestaetigen. Hinweis: rechnerisch wird im Live-Pfad nur Severity 5 gruen (Evidence nur aus Severity/5 + fester Plausibility 0.6).

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

Problem: Kein `_closing`-Guard, kein idempotentes Cleanup, kein `VideoView.MediaPlayer = null`. Im Gegensatz dazu ist `PlayerWindow` (Playback.cs:346,380,392) sauber abgesichert.

### 4. Contains-Matching

`IbakExportImportService.cs:125-132`:

```csharp
if (v.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(v, StringComparison.OrdinalIgnoreCase))
    return r;
```

Problem: `100-200` kann `100-2000` treffen.

### 5. Spalte leeren entfernt Schutz

`DataPage.xaml.cs:943-950`:

```csharp
foreach (var record in vm.Records)
{
    record.SetFieldValue(fieldName, string.Empty, FieldSource.Manual, userEdited: true);
    if (record.FieldMeta.TryGetValue(fieldName, out var meta))
        meta.UserEdited = false;
}
```

Problem: Leert alle Werte einer Spalte und setzt das Schutzflag zurueck. Bestaetigung ist nur eine einfache Ja/Nein-Box ohne Anzahl/Undo.

## Nicht uebernehmen aus Gemini

Diese Punkte sind fuer die echte App nicht belastbar:

- "Mischbetrieb C# und PowerShell" als Hauptproblem
- Portierung alter PowerShell-Logik nach C#
- COM-Excel-Vorwurf
- falsche Modellannahmen wie `qwen3.5:27b` (real: qwen3-vl:8b / 32b)

Die alten `*.ps1`-Dateien koennen spaeter aufgeraeumt werden. Sie sind aber nicht der Kern der aktuellen App.

## Sofortplan 1-2 Tage

### 1. Import-Vorschau read-only machen
- In `ImportPageViewModel` bei `dryRun` mit `Project.DeepClone()` arbeiten.
- Nach Vorschau keine Live-Daten uebernehmen.
- Test: Vorschau starten, abbrechen, Projekt muss unveraendert bleiben (Dirty bleibt false).

### 2. KI-Auto-Accept stoppen
- `gateResult.IsGreen` darf nicht direkt `CodingUserDecision.Accepted` setzen.
- Stattdessen `Pending`/`Suggested` bis zur Nutzerbestaetigung.
- Besonders fuer Severity 4/5 niemals automatisch akzeptieren.
- `FullProtocolGenerationService.cs:392`: `Source = Ai` statt `Manual`.

### 3. CodingModeWindow sicher schliessen
- `_closing`-Flag, Timer/CTS stoppen, `VideoView.MediaPlayer = null`, Stop/Dispose idempotent.
- Nach jedem `await`/Delay Closing pruefen.

### 4. Gefaehrliche UI-Aktionen absichern
- Einzelloeschen mit Rueckfrage.
- Spalte leeren mit Anzahl, Feldname und starker Bestaetigung, optional Undo.

### 5. Live-Control absichern
- Token beim Start erzeugen oder aus Datei/Env lesen; MCP sendet Token mit.
- Body-Limit setzen (z. B. 64 KB).
- `/pipeline/retry` separat freischalten.

## Kurzfristig 1-2 Wochen

1. Dirty-Guard vor Projekt oeffnen/neu.
2. IBAK/KINS Contains-Matching entfernen.
3. Reimport von Protokollen/Befunden als Konflikt/Merge behandeln.
4. KI-Timeouts vereinheitlichen.
5. Sidecar Deep-Health fuer YOLO/DINO/SAM/Qwen + sichtbarer Modus-Hinweis bei Ollama-only-Fallback.
6. GIS-Pfade in Einstellungen/Projekt verschieben.
7. Karten-Farbskala auf 0-4-Skala korrigieren + Test `Map(0..4, invertiert)`.
8. Import-Plausibilitaet (Meter > Laenge, DN-Bereich, Datum) als Warnungen.

## Mittelfristig

1. KI-Vorschlaege als Review-Queue.
2. VRAM-Scheduler statt nur Dokumentation.
3. `TrainingCenterViewModel` entflechten (Service-Factories), doppelte Modelle (`VsaFinding`, Cost) konsolidieren, `VsaIliEvaluator` entfernen.
4. Karte als Hauptnavigation; Verlauf-Fallback + Mapsui-Threading absichern.
5. Daten-Tabelle sortierbar; Export-Assistent statt Ja/Nein-Kette.

## Build- und Test-Stand (selbst ausgefuehrt)

- `dotnet build AuswertungPro.sln -v minimal`: 0 Fehler, 0 Warnungen (net10.0).
- `dotnet test AuswertungPro.sln`: Exitcode 0 — Infrastructure 180/1 uebersprungen, UI 275, Pipeline 281 (gesamt 737 bestanden, 1 uebersprungen).

## Abschlussurteil

SewerStudio hat eine starke technische und fachliche Basis. Die groessten Risiken sind jetzt nicht fehlende Funktionen, sondern zu maechtige Funktionen ohne genuegend Schutz.

Die naechsten Arbeiten sollten deshalb nicht "mehr Features" sein, sondern:

1. Vorschau wirklich sicher machen.
2. KI wieder als Assistent statt Entscheider behandeln.
3. Player/Coding-Fenster stabil schliessen.
4. Loeschen und Reimport fachlich absichern.

---

## Anhang: Verifikations-Protokoll (Claude, 2026-05-31)

Folgende Stellen wurden fuer dieses Master-Dokument direkt im Code gegengeprueft:

- Rang 1 — `dryRun`-Suche im Import-Ordner trifft nur `MergeEngine.cs` + `MediaDistributionService.cs`; IBAK setzt `project.Dirty = true` ohne DryRun-Zweig (`IbakExportImportService.cs:111-112`). BESTAETIGT.
- Rang 2 — `PlayerWindow.Coding.cs:3555` `Decision = Accepted` bei `IsGreen`. BESTAETIGT.
- Rang 5 — `ShellViewModel.cs:274-293` oeffnet ohne Dirty-Abfrage. BESTAETIGT.
- Rang 7 — `DataPage.xaml.cs:943-950` leert + setzt `UserEdited=false`; schwache Ja/Nein-Box vorhanden. BESTAETIGT.
- Rang 9 — `EnhancedVisionAnalysisService.cs:148` = 60s. BESTAETIGT (300s-Seite aus Teil-Audit uebernommen).
- Rang 14 — `HaltungRecord.cs:46-53` schuetzt `UserEdited`-Felder. BESTAETIGT → Codex-Befund #7 entsprechend praezisiert.

Nicht erneut einzeln verifiziert (aus den Teil-Audits uebernommen, mit Belegstelle): Rang 3,4,6,8,10,11,12,13,15,16,17.
