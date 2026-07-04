# Datensicherung „PC-Ausfall-Schutz" — Übergabe an Codex

**Stand: 2026-07-03. Schritt 1 FERTIG (Tests grün), Schritt 2 halb fertig (Code da + kompiliert, Tests fehlen). Schritte 3–7 offen.**

## Kontext

Neuer Button in den Einstellungen: Klick → Explorer-Ordnerauswahl → alle **nicht wiederherstellbaren** Daten werden als **inkrementeller Spiegel** (kein ZIP) in `<Ziel>\SewerStudio_Datensicherung\` gesichert. Größen werden vor dem Lauf pro Komponente angezeigt, Fortschritt in Prozent (nach Bytes), Abbruch möglich. **Keine Projekte/Videos** (sichert der User separat). Das bestehende „KI-Wissen Backup" (ZIP, `KnowledgeBackupService`) bleibt unangetastet.

**Mit dem User geklärt (2026-07-03):**
- KI-Brain nur Unersetzliches (~55 GB): OHNE `yolo_*dataset*`-Ordner (~130 GB), `training_frames` (~21 GB), `kb_backups` (~22 GB) — regenerierbar.
- Altbestände weglassen: `%LOCALAPPDATA%\SewerStudio\Knowledge` (13,4 GB, tot seit 05.06.) und `%APPDATA%\AuswertungPro\frames` (13,4 GB, tot seit März).
- Spiegel-Ordner statt ZIP; Wiederholungsläufe müssen schnell sein.

## Sicherungs-Umfang (fix, nicht ändern)

| Ziel | Quelle | Ausschlüsse |
|---|---|---|
| `Programm\` | Repo-Root (zur Laufzeit via `RepoRootLocator`) inkl. `.git` und `sidecar\models\` | `bin`, `obj`, `.vs`, `node_modules`, `.venv`/`venv`, `__pycache__` |
| `KI_BRAIN\` | `KnowledgeBasePaths.GetRoot()` (Env `SEWERSTUDIO_KNOWLEDGE_ROOT` → aktuell `C:\KI_BRAIN`); vorher WAL-Checkpoint | `yolo_*dataset*`-Muster, `training_frames`, `kb_backups` |
| `Einstellungen\Local_SewerStudio\` | `%LOCALAPPDATA%\SewerStudio` | Top-Level: `Knowledge`, `logs`, `Telemetry` |
| `Einstellungen\Roaming_SewerStudio\` | `%APPDATA%\SewerStudio` | — |
| `Einstellungen\Roaming_AuswertungPro\` | `%APPDATA%\AuswertungPro` | Top-Level: `frames`, `yolo_dataset` |
| `Logs\logs\` + `Logs\Telemetry\` | `%LOCALAPPDATA%\SewerStudio\logs` + `\Telemetry` | — |
| `Extras\` | Desktop-Skripte (`SewerStudio.bat`, `Start_SewerStudio.bat`, `Backup_KI_BRAIN.bat`, falls vorhanden); generiert: `umgebung.txt` (Env-Vars `SEWERSTUDIO_*`/`SEWER_*` + `ollama list` best effort), `RESTORE-ANLEITUNG.txt` | — |
| `manifest.json` (Root) | generiert: Datum, App-Version, Git-Commit, Quellpfade, Größen/Dateizahlen, übersprungene Dateien (gekappt ~200); atomar via `AtomicTextFileWriter` | — |

## Was FERTIG ist (nicht neu bauen)

**Schritt 1 — Application-Basistypen, kompiliert, 52 Tests grün:**
- `src\AuswertungPro.Next.Application\Backup\IFullBackupService.cs` — Interface `AnalyzeAsync`/`RunAsync` + Records `ComponentSize`, `FullBackupSizeReport`, `FullBackupProgress`, `FullBackupResult`
- `src\AuswertungPro.Next.Application\Backup\FullBackupSources.cs` — Record aller Quellpfade, komplett injizierbar (Tests fassen NIE echte Pfade an)
- `src\AuswertungPro.Next.Application\Backup\BackupExclusionRules.cs` — pure Ausschluss-Prädikate
- `src\AuswertungPro.Next.Application\Backup\BackupPlanBuilder.cs` — 5 Komponenten; Konstanten `TargetFolderName` = `SewerStudio_Datensicherung`, `MarkerFileName` = `.sewerstudio-datensicherung`, `DesktopScriptNames`
- `src\AuswertungPro.Next.Application\Backup\RestoreAnleitungText.cs` — generiert deutsche Wiederherstellungs-Anleitung
- `src\AuswertungPro.Next.Application\Common\ByteSizeFormatter.cs` — Bytes → „1,2 GB"
- Tests: `tests\AuswertungPro.Next.Infrastructure.Tests\Backup\` → `BackupExclusionRulesTests.cs`, `BackupPlanBuilderTests.cs`, `ByteSizeFormatterTests.cs` (52 grün)

**Schritt 2 — Spiegel-Kern, Code fertig + kompiliert, TESTS FEHLEN NOCH:**
- `src\AuswertungPro.Next.Infrastructure\Backup\BackupTargetGuard.cs` — `ValidateAndCreateMarker` (leerer Ordner → Marker anlegen; Fremdinhalt ohne Marker → Fehlertext; Marker da → ok), `IsInsideBackupRoot`, `CheckSourceTargetConflict` (Ziel-in-Quelle / Quelle-in-Ziel)
- `src\AuswertungPro.Next.Infrastructure\Backup\DirectoryMirror.cs` — `MirrorSourceAsync` (inkrementell: kopiert wenn Ziel fehlt oder Größe/LastWriteTimeUtc-Differenz > 2 s; Temp-Datei `*.tmp_sewerbackup` → Timestamp → `File.Move(overwrite)`; `FileShare.ReadWrite`; Fehler pro Datei in `MirrorStats.Errors` sammeln), `MirrorFileAsync` (Einzeldateien), `DeleteOrphans` (nur unterhalb Backup-Root, leere Ordner mit), eigene stack-basierte Enumeration, die ausgeschlossene Ordner gar nicht betritt

## Was NOCH ZU TUN ist

### Schritt 2b: Tests für den Spiegel-Kern
`tests\...\Backup\DirectoryMirrorTests.cs` + `BackupTargetGuardTests.cs` — nur Temp-Ordner (`Path.GetTempPath()` + Guid), nach jedem Test aufräumen:
- Erstkopie vollständig inkl. Timestamp-Übernahme; 2. Lauf: 0 kopiert, alles unverändert
- Größen-/Zeitänderung (> 2 s) → Kopie; Differenz < 2 s → keine
- `DeleteOrphans` entfernt Verwaistes + leere Ordner; erwartete Pfade bleiben
- Pfad außerhalb backupRoot wird nie angefasst
- Gesperrte Quelldatei (im Test mit `FileShare.None` offen halten) → Fehlereintrag, Lauf läuft weiter
- Abbruch via Token → keine `.tmp_sewerbackup`-Leiche wird Zieldatei; Folgelauf vervollständigt
- Guard: leer→Marker ok; Fremdinhalt ohne Marker→Fehler; mit Marker→ok; Konflikt-Fälle

### Schritt 3: Orchestrierung (`src\AuswertungPro.Next.Infrastructure\Backup\`)
- `GitCommitResolver.cs` — Commit-Hash aus `<repo>\.git\HEAD` (bei `ref: refs/heads/x` die Ref-Datei lesen, sonst `packed-refs`), ohne git.exe, jede Exception → null. Test mit Temp-`.git`-Attrappe.
- `RepoRootLocator.cs` — von `AppContext.BaseDirectory` aufwärts bis Ordner mit `AuswertungPro.sln`; null wenn nicht gefunden. Test mit Temp-Baum (Parameter fuer Startpfad).
- `src\AuswertungPro.Next.Infrastructure\Ai\KnowledgeBase\KnowledgeWalCheckpoint.cs` — `public static void TryCheckpoint()`: Muster aus `KnowledgeBackupService.FlushSqliteWal` (UI\Services\KnowledgeBackupService.cs:228–246) extrahieren — Existenz-Check `KnowledgeBasePaths.GetKnowledgeDbPath()`, `PRAGMA wal_checkpoint(TRUNCATE)` über `new KnowledgeBaseContext()`, Fehler nur `Debug.WriteLine`. Bestehenden `KnowledgeBackupService` NICHT anfassen.
- `FullBackupService.cs` — implementiert `IFullBackupService`. Konstruktor: `Func<FullBackupSources> quellenFactory` (bei jedem Aufruf frisch!), `Action? walCheckpoint = null`, `Func<CancellationToken, Task<string?>>? ollamaListe = null`.
  - `AnalyzeAsync`: Plan via `BackupPlanBuilder.Build`, pro Komponente Dateien enumerieren (gleiche Ausschlüsse wie Mirror!) und Bytes/Anzahl summieren; `SourceFound` = mind. eine Quelle existiert.
  - `RunAsync`-Ablauf: (1) `backupRoot = Path.Combine(zielOrdner, BackupPlanBuilder.TargetFolderName)`; `BackupTargetGuard.CheckSourceTargetConflict` (alle SourceRoots) + `ValidateAndCreateMarker` — Fehlertext → `FullBackupResult(Success=false, Error=…)`. (2) `walCheckpoint?.Invoke()`. (3) Analyze für BytesGesamt, dann pro Komponente `MirrorSourceAsync`/`MirrorFileAsync`; Fortschritt via `onFileDone`-Callback, **gedrosselt auf ~4 Meldungen/s** (Stopwatch), Prozent nach Bytes. (4) Extras generieren: `umgebung.txt` (Env-Snapshot aus Sources + Ollama-Liste), `RESTORE-ANLEITUNG.txt` (`RestoreAnleitungText.Build`) — direkt ins Ziel schreiben und deren Relativpfade + `manifest.json`/`manifest.json.bak` + Marker ins expected-Set aufnehmen. (5) `DeleteOrphans` — NUR wenn nicht abgebrochen. (6) `manifest.json` atomar (`AtomicTextFileWriter.WriteAllTextAsync`). `OperationCanceledException` durchreichen; andere Exceptions → Result mit Error.
  - Test `FullBackupServiceTests.cs`: End-to-End auf Temp-Quellbaum (alle Quellen = Temp-Ordner, walCheckpoint=null, ollamaListe=null): Manifest-Inhalt; Marker/Extras/manifest überleben Orphan-Löschung; 2. Lauf inkrementell (0 kopiert); Analyze-Summe == kopierte Bytes.

### Schritt 4: Verdrahtung
- `src\AuswertungPro.Next.UI\Services\FullBackupSourcesFactory.cs` — baut `FullBackupSources` aus: `RepoRootLocator.Locate()`, `KnowledgeBasePaths.GetRoot()`, `AppSettings.AppDataDir`, `%APPDATA%\SewerStudio` + `%APPDATA%\AuswertungPro` (`Environment.GetFolderPath(ApplicationData)`), `Environment.GetFolderPath(DesktopDirectory)`, `AppIdentity.Version`, Env-Snapshot (alle Variablen mit Präfix `SEWERSTUDIO_`/`SEWER_` aus `Environment.GetEnvironmentVariables()`).
- `src\AuswertungPro.Next.UI\ServiceProvider.cs` — Property `public IFullBackupService FullBackup { get; }` (bei `Dialogs`/`Toasts`, ~Z. 52–55); Instanzierung im Konstruktor: `new FullBackupService(FullBackupSourcesFactory.ErmittleAktuelleQuellen, KnowledgeWalCheckpoint.TryCheckpoint, ct => OllamaListAsync(ct))` — Ollama-Delegate via `ExternalProcessRunner.RunAsync("ollama", new[]{"list"}, TimeSpan.FromSeconds(10), cancellationToken: ct)`, bei Fehler/Timeout null.
- `src\AuswertungPro.Next.UI\AppSettings.cs` — neue Properties `LastFullBackupUtc` (DateTime?), `LastFullBackupPath` (string?), `LastFullBackupSizeBytes` (long?).

### Schritt 5: ViewModel (`src\AuswertungPro.Next.UI\ViewModels\Pages\SettingsPageViewModel.cs`)
- ObservableProperties: `FullBackupStatusText`, `FullBackupPercent` (double), `IsFullBackupRunning` (bool), `FullBackupCurrentFile`, `LastFullBackupInfo` (im Konstruktor aus den 3 AppSettings-Feldern + `ByteSizeFormatter`).
- `CreateFullBackupCommand = new AsyncRelayCommand(CreateFullBackupAsync)` mit der `Func<CancellationToken,Task>`-Überladung → `CancelFullBackupCommand` ruft `CreateFullBackupCommand.Cancel()`.
- Ablauf (Vorbild `ExportBackupAsync` Z. 332–370 und `ExportPageViewModel` Z. 199–212):
  1. `_sp.Dialogs.SelectFolder("Zielordner für die Datensicherung wählen", _sp.Settings.LastFullBackupPath)` → null = raus.
  2. `IsFullBackupRunning = true`, Status „Berechne Größen…", `AnalyzeAsync` via `Task.Run`.
  3. `_sp.Dialogs.Confirm(...)`: Komponenten-Auflistung mit `ByteSizeFormatter.Format`, Gesamtgröße, Zielpfad, Hinweise „inkrementeller Spiegel — im Sicherungsordner Verwaistes wird entfernt" + „NTFS/exFAT-Ziel empfohlen (FAT32: 4-GB-Grenze)".
  4. `AppSettings.FlushPendingSave()`, dann `RunAsync` mit `Progress<FullBackupProgress>` → `FullBackupPercent = 100.0 * BytesDone / BytesTotal`, Statustext + `Path.GetFileName(CurrentFile)`.
  5. Erfolg: `_sp.Toasts.Success`, 3 AppSettings-Felder setzen + speichern, `LastFullBackupInfo` aktualisieren; `SkippedFiles.Count > 0` → `_sp.Dialogs.Warn` (Anzahl + erste ~10). Fehler: `_sp.Toasts.Error` + `_sp.Dialogs.Error`. `OperationCanceledException`: Status „Abgebrochen — bereits Kopiertes bleibt erhalten". `finally`: `IsFullBackupRunning = false`.

### Schritt 6: XAML (`src\AuswertungPro.Next.UI\Views\Pages\SettingsPage.xaml`)
Neuer Abschnitt „Datensicherung (kompletter PC-Ausfall-Schutz)" nach dem Abschnitt „KI-Wissen Backup" (nach ~Z. 180, vor dem Speichern-Button). Stil wie Nachbar-Abschnitte (Überschrift `MutedBrush`, Beschreibung `TextSecondaryBrush` FontSize 11 — Text erklärt Abgrenzung zum KI-Wissen-ZIP und „Projekte/Videos nicht enthalten"). Elemente: Button „Datensicherung erstellen…" (`CreateFullBackupCommand`, disabled während Lauf via CanExecute des AsyncRelayCommand), Fortschrittsbereich sichtbar bei `IsFullBackupRunning` (`BoolToVis` existiert auf der Seite): determinate `ProgressBar` 0–100 `{Binding FullBackupPercent}` + Prozenttext + `{Binding FullBackupCurrentFile}` + Button „Abbrechen" (`CancelFullBackupCommand`); darunter `{Binding FullBackupStatusText}` und `{Binding LastFullBackupInfo}`. **Danach jeden `{Binding}`-Pfad gegen die ViewModel-Properties prüfen** (stille Binding-Fehler!).

### Schritt 7: Verifikation
1. `dotnet build AuswertungPro.sln` — 0 Fehler.
2. `dotnet test tests\AuswertungPro.Next.Infrastructure.Tests` — neue + bestehende grün (insb. `SafePathGuardTests`, `AtomicPersistenceArchitectureTests`).
3. Manueller WPF-Smoke (macht der User): Sicherung auf leeren Testordner → 5 Komponenten + Gesamt (~55–60 GB) im Dialog, Prozentbalken, Toast; Stichproben: `Programm\.git` da, `Programm\sidecar\models\active.json` da, KEIN `bin`, KEIN `KI_BRAIN\training_frames`, KEIN `Einstellungen\Local_SewerStudio\Knowledge`; `manifest.json` + `Extras\RESTORE-ANLEITUNG.txt` + `umgebung.txt` da. 2. Lauf schnell/inkrementell. Abbruch sauber. Fremdordner ohne Marker → Fehlermeldung, nichts gelöscht. `C:\KI_BRAIN` als Ziel → blockiert.

## Regeln (Repo-Konventionen)
- Kommentare auf Deutsch, keine neuen NuGet-Pakete, bestehenden Code nur anfassen wo oben genannt.
- Tests: NIE echte Datenpfade/settings.json anfassen (TestAppDataIsolation) — alles über injizierte Temp-Pfade.
- Sicherheit ist der Kern des Features: Es wird NIEMALS außerhalb von `<Ziel>\SewerStudio_Datensicherung` gelöscht, und dort nur mit Marker.

## Bekannte Randnotizen
- `%LOCALAPPDATA%\SewerStudio\Knowledge` wird bewusst NICHT gesichert (Altbestand); wenn `SEWERSTUDIO_KNOWLEDGE_ROOT` fehlt, zeigt `KnowledgeBasePaths.GetRoot()` genau dorthin — dann sichert die KI_BRAIN-Komponente diesen Ordner ohnehin, keine Doppelung.
- QGIS-Plugins des Users sind NICHT Teil der Sicherung (steht in der RESTORE-ANLEITUNG).
- Voller Plan mit Begründungen: `C:\Users\Besitzer\.claude\plans\datensicherungs-feature-button-in-einste-quirky-melody.md` (falls lesbar); dieses Dokument ist aber self-contained.
