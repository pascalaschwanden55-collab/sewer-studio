# Ein-Knopf-Import „Kanalfernseh-Projekt" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein Import-Knopf, der einen WinCan- oder IKAS-Quellordner erkennt, die maßgebliche Quelle importiert (inkl. Pro-Beobachtung-Fotos), Rohdaten archiviert, Filme/PDFs verteilt, Fotos zentral gruppiert ablegt, alles relativ verlinkt und idempotent bleibt.

**Architecture:** Eine deterministische Pipeline (`ProjectImportOrchestrator`) ruft kleine, einzeln testbare Bausteine in Reihenfolge auf: Restore-Point → `KanalExportDetector` → `ImportSourceArchiver` → vorhandener Parser (`IXtfImportService` für IKAS / `IWinCanDbImportService` für WinCan) → `Sia405WhitelistEnricher` (nur IKAS) → `HoldingFolderDistributor`/`MediaDistributionService` (neue Zielstruktur) → Report. Alle neuen Backend-Klassen liegen in `AuswertungPro.Next.Infrastructure.Import` bzw. `…Application`. Der UI-Knopf ist Codex-Lane.

**Tech Stack:** .NET 10, C#, xUnit, `Microsoft.Data.Sqlite` (vorhanden), `System.Xml.Linq`. Keine neuen NuGet-Pakete.

## Global Constraints

- Datenhoheit: IKAS = VSA_KEK-XTF, WinCan = `.db3`. FDB/Daten.txt/PDF nur archivieren, NICHT als Datenquelle parsen.
- Feldpriorität: `UserEdit > Hauptquelle > SIA405-Whitelist > leer`. UserEdit-Felder NIE überschreiben.
- SIA405-Whitelist (nur füllen wenn leer): `Rohrmaterial`, `DN_mm`, `Nutzungsart`, `Strasse`, Geometrie/Lage. NIE durch SIA405: `Datum_Jahr`, `Bemerkungen`, `Haltungslaenge_m`, Befunde.
- Jeder Konflikt → Report-Zeile, **nicht-blockierend** (einzelne Fehler isolieren, Lauf läuft weiter).
- Echte Bilddateien NUR zentral gruppiert: `Fotos\Haltungen\<Haltung>\`, `Fotos\Schächte\<Schacht>\`. Verteil-Ordner: Filme (Haltungen) + PDFs (Haltungen+Schächte). Videos NICHT in `Importdateien\` doppeln.
- Records speichern ausschließlich RELATIVE Pfade (über `ProjectPathResolver.MakeRelative`).
- Whole-File-Ownership: Claude = Domain/Application/Infrastructure (+Tests). Codex = UI-Projekt. Kommentare deutsch.
- Idempotent: erneuter Import doppelt nichts (Records nach Schlüssel matchen; Dateien nach Name+Größe wiederverwenden).
- Verbindliche Ordnernamen: `Importdateien\{Datenbanken,XTF,PDF,TXT}`, `Haltungen_Verteilt\<Haltung>\`, `Schächte_Verteilt\<Schacht>\`, `Fotos\Haltungen\` / `Fotos\Schächte\`, `Projektdateien\projekt.json`, `__IMPORT_REPORTS\`, `__RESTORE_POINTS\`.

---

## File Structure

**Neu (Infrastructure/Import):**
- `ProjectStructure.cs` — zentrale, verbindliche Ordnernamen + `EnsureCreated(projectFolder)` + Pfad-Helfer. Eine Quelle der Wahrheit für die Struktur.
- `KanalExportDetector.cs` — Quellordner → `ExportFormat { WinCan, Ikas, Unknown, Ambiguous }` + Fundorte (db3/xtf/film…).
- `ImportSourceArchiver.cs` — Roh-Quellen → `Importdateien\{Datenbanken,XTF,PDF,TXT}` (idempotent, ohne Videos).
- `ProjectImportOrchestrator.cs` — die Pipeline (orchestriert die obigen + vorhandene Parser/Distributor).

**Neu (Application):**
- `Common/Sia405WhitelistEnricher.cs` — füllt Whitelist-Felder aus dem SIA405-XTF, nur wenn leer, mit Konflikt-Logging.
- `Common/ProjectFileLocator.cs` — findet `projekt.json` in Root ODER `Projektdateien\` (rückwärtskompatibel).

**Ändern:**
- `Infrastructure/Import/MediaDistributionService.cs` — Zielstruktur `Haltungen_Verteilt\`, `Schächte_Verteilt\`, gruppierte `Fotos\Haltungen\`/`Fotos\Schächte\`.
- `Infrastructure/Import/ProjectPhotoAssignmentService.cs` — gruppierte Fotos (`Fotos\Haltungen\<Haltung>\`).
- `Application/Common/NewProjectFolderPlanner.cs` — projekt.json-Pfad nach `Projektdateien\` + Root-Pointer (kompatibel).
- (Codex) `UI/.../ImportPageViewModel.cs` + `ImportPage.xaml` — neuer Hauptknopf, alte Knöpfe nach „Manuell".

**Tests (Infrastructure.Tests / *.Tests):**
- `ProjectStructureTests.cs`, `KanalExportDetectorTests.cs`, `ImportSourceArchiverTests.cs`, `Sia405WhitelistEnricherTests.cs`, `ProjectFileLocatorTests.cs`, `ProjectImportOrchestratorTests.cs`, Erweiterungen in `MediaDistributionServiceTests.cs`/`ProjectPhotoAssignmentServiceTests.cs`.

---

## Task 1: ProjectStructure — verbindliche Ordnerstruktur

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/ProjectStructure.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ProjectStructureTests.cs`

**Interfaces — Produces:**
```csharp
public static class ProjectStructure
{
    public const string Importdateien = "Importdateien";
    public const string Datenbanken = "Datenbanken"; // unter Importdateien
    public const string XtfDir = "XTF";
    public const string PdfDir = "PDF";
    public const string TxtDir = "TXT";
    public const string HaltungenVerteilt = "Haltungen_Verteilt";
    public const string SchaechteVerteilt = "Schächte_Verteilt";
    public const string Fotos = "Fotos";
    public const string FotosHaltungen = "Haltungen"; // unter Fotos
    public const string FotosSchaechte = "Schächte"; // unter Fotos
    public const string Projektdateien = "Projektdateien";
    public const string ImportReports = "__IMPORT_REPORTS";
    public const string RestorePoints = "__RESTORE_POINTS";

    public static void EnsureCreated(string projectFolder);            // legt alle (leeren) Ordner an, idempotent
    public static string HaltungVerteiltDir(string proj, string san);  // proj\Haltungen_Verteilt\<san>
    public static string SchachtVerteiltDir(string proj, string san);
    public static string FotosHaltungDir(string proj, string san);     // proj\Fotos\Haltungen\<san>
    public static string FotosSchachtDir(string proj, string san);
    public static string ImportdateienDir(string proj, string subKind);// subKind = Datenbanken|XTF|PDF|TXT
}
```
`san` = sanitisierter Segmentname via `AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment`.

- [ ] **Step 1: Failing test** — `ProjectStructureTests.EnsureCreated_CreatesAllFolders`:
```csharp
var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
try {
    ProjectStructure.EnsureCreated(root);
    Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "Datenbanken")));
    Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "XTF")));
    Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "PDF")));
    Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "TXT")));
    Assert.True(Directory.Exists(Path.Combine(root, "Haltungen_Verteilt")));
    Assert.True(Directory.Exists(Path.Combine(root, "Schächte_Verteilt")));
    Assert.True(Directory.Exists(Path.Combine(root, "Fotos", "Haltungen")));
    Assert.True(Directory.Exists(Path.Combine(root, "Fotos", "Schächte")));
    Assert.True(Directory.Exists(Path.Combine(root, "Projektdateien")));
    Assert.True(Directory.Exists(Path.Combine(root, "__IMPORT_REPORTS")));
    Assert.True(Directory.Exists(Path.Combine(root, "__RESTORE_POINTS")));
    ProjectStructure.EnsureCreated(root); // 2. Aufruf darf nicht werfen (idempotent)
} finally { try { Directory.Delete(root, true); } catch {} }
```
Plus `FotosHaltungDir_ReturnsGroupedPath`: `Assert.Equal(Path.Combine(root,"Fotos","Haltungen","06-001"), ProjectStructure.FotosHaltungDir(root,"06-001"));`

- [ ] **Step 2: Run → FAIL** (`dotnet test … --filter FullyQualifiedName~ProjectStructureTests`), Klasse fehlt.
- [ ] **Step 3: Implement** `ProjectStructure` mit den Konstanten + `EnsureCreated` (`Directory.CreateDirectory` je Ordner, ist idempotent) + Pfad-Helfern (`Path.Combine` + `ProjectPathResolver.SanitizePathSegment`).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(import): ProjectStructure - verbindliche Projekt-Ordnerstruktur`.

---

## Task 2: ProjectFileLocator + NewProjectFolderPlanner — projekt.json-Auffindbarkeit

**Files:**
- Create: `src/AuswertungPro.Next.Application/Common/ProjectFileLocator.cs`
- Modify: `src/AuswertungPro.Next.Application/Common/NewProjectFolderPlanner.cs`
- Test: `tests/AuswertungPro.Next.Application.Tests/ProjectFileLocatorTests.cs` (oder vorhandenes Application-Testprojekt)

**Hintergrund:** Heute liegt `projekt.json` im Root (`NewProjectFolderPlanner.ProjectFileName`). Neu: `Projektdateien\projekt.json`. Der Locator muss BEIDE finden (rückwärtskompatibel).

**Interfaces — Produces:**
```csharp
public static class ProjectFileLocator
{
    // Sucht die projekt.json eines gewählten Projektordners: zuerst Projektdateien\projekt.json,
    // dann <Root>\projekt.json (Altprojekte). Gibt null, wenn keine existiert.
    public static string? Locate(string projectFolder);
    // Zielpfad fuer NEUE Projekte: <Root>\Projektdateien\projekt.json.
    public static string TargetPath(string projectFolder);
    public const string PointerFileName = "projekt.pointer"; // Root-Pointer (enthaelt relativen Pfad)
}
```

- [ ] **Step 1: Failing test** `ProjectFileLocatorTests`:
```csharp
// (a) findet in Projektdateien
var root = NewTemp();
Directory.CreateDirectory(Path.Combine(root, "Projektdateien"));
File.WriteAllText(Path.Combine(root, "Projektdateien", "projekt.json"), "{}");
Assert.Equal(Path.Combine(root,"Projektdateien","projekt.json"), ProjectFileLocator.Locate(root));
// (b) findet Altprojekt im Root
var r2 = NewTemp(); File.WriteAllText(Path.Combine(r2,"projekt.json"),"{}");
Assert.Equal(Path.Combine(r2,"projekt.json"), ProjectFileLocator.Locate(r2));
// (c) keins -> null
Assert.Null(ProjectFileLocator.Locate(NewTemp()));
// (d) TargetPath ist immer Projektdateien
Assert.Equal(Path.Combine(root,"Projektdateien","projekt.json"), ProjectFileLocator.TargetPath(root));
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** `ProjectFileLocator` (Reihenfolge Projektdateien → Root, `File.Exists`). In `NewProjectFolderPlanner`: `ProjectFilePath` auf `ProjectFileLocator.TargetPath(candidate)` umstellen; vorhandene Konstante `ProjectFileName` beibehalten (Kompatibilität).
- [ ] **Step 4: Run → PASS** (+ vorhandene `NewProjectFolderPlanner`-Tests grün).
- [ ] **Step 5:** Call-Sites des Projekt-Öffnens (Shell/Project-Store) auf `ProjectFileLocator.Locate` umstellen — per Grep `"projekt.json"` / `ProjectFileName` finden; jede Lade-Stelle nutzt den Locator. Beim Speichern neuer Projekte zusätzlich Root-Pointer `projekt.pointer` mit relativem Pfad schreiben.
- [ ] **Step 6: Run volle Solution → grün. Commit** `feat(project): projekt.json in Projektdateien\ + rueckwaertskompatibler Locator`.

---

## Task 3: KanalExportDetector — Format-Erkennung

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/KanalExportDetector.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/KanalExportDetectorTests.cs`

**Interfaces — Consumes:** `AuswertungPro.Next.Infrastructure.Import.Ibak.KiasExportPattern.Detect(string)`.
**Produces:**
```csharp
public enum KanalExportFormat { Unknown, Ikas, WinCan, Ambiguous }
public sealed record KanalExportDetection(
    KanalExportFormat Format,
    string? Db3Path,        // WinCan: .db3 unter \DB\
    string? VsaKekXtfPath,  // IKAS: VSA_KEK-XTF
    string? Sia405XtfPath,  // IKAS: SIA405-XTF (optional)
    string? Reason);
public static class KanalExportDetector
{
    public static KanalExportDetection Detect(string sourceFolder);
}
```
**Regeln:** WinCan = es existiert `*.db3` in einem Ordner namens `DB` (rekursiv; `*_Meta.db3` ausschließen, größte nehmen). IKAS = `KiasExportPattern.Detect(...).IsKias` ODER eine `VSA_KEK`-XTF gefunden. VSA_KEK-XTF erkennen: `*.xtf`, dessen Inhalt `VSA_KEK_2020_LV95` enthält (nicht `_SIA405`); SIA405-XTF = enthält `SIA405`. Beides → `Ambiguous`; keins → `Unknown`.

- [ ] **Step 1: Failing tests** (temp-Ordner mit Mini-Fixtures):
  - WinCan: lege `src\DB\proj.db3` + `src\DB\proj_Meta.db3` an → `Format==WinCan`, `Db3Path` endet auf `proj.db3` (nicht Meta).
  - IKAS: lege `src\Dokumente\x.xtf` mit Inhalt `<…VSA_KEK_2020_LV95…>` + `src\Dokumente\x_SIA405.xtf` mit `SIA405` + `src\Data\Arizona.fdb` (leer) + `src\Film\` → `Format==Ikas`, `VsaKekXtfPath`/`Sia405XtfPath` gesetzt.
  - Leer → `Unknown`.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** mit `Directory.EnumerateFiles` (Recurse, `IgnoreInaccessible`) + kleinem Header-Read (erste ~64 KB) zur Modell-Erkennung. KiasExportPattern für die IKAS-Heuristik nutzen.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(import): KanalExportDetector - WinCan/IKAS-Erkennung`.

---

## Task 4: ImportSourceArchiver — Rohdaten nach Importdateien\

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/ImportSourceArchiver.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ImportSourceArchiverTests.cs`

**Interfaces — Consumes:** `ProjectStructure`, `Common.MediaFileIndex`/`SafeFileEnumeration` (vorhanden).
**Produces:**
```csharp
public sealed record ArchiveResult(int Copied, int Reused, IReadOnlyList<string> Messages);
public static class ImportSourceArchiver
{
    // Kopiert .fdb/.db3 -> Datenbanken, .xtf -> XTF, .pdf -> PDF, .txt -> TXT.
    // Videos werden NICHT kopiert. Idempotent: gleicher Name+Groesse -> Reuse.
    public static ArchiveResult Archive(string sourceFolder, string projectFolder);
}
```
Endungs-Mapping: `.fdb`,`.db3` → `Datenbanken`; `.xtf` → `XTF`; `.pdf` → `PDF`; `.txt` → `TXT`. Andere (inkl. `.mpg`,`.mp4`,…) ignorieren.

- [ ] **Step 1: Failing test:** source mit `a.db3`,`b.xtf`,`c.pdf`,`Daten.txt`,`film.mpg` → nach Archive: Dateien in den jeweiligen Importdateien-Unterordnern, `film.mpg` NICHT vorhanden; 2. Archive-Aufruf → `Copied==0, Reused==4` (idempotent, keine `_1`-Duplikate).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** (rekursiv enumerieren, je Endung Zielordner via `ProjectStructure.ImportdateienDir`, `File.Exists`+Größe vergleichen für Reuse, sonst kopieren).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(import): ImportSourceArchiver - Rohdaten nach Importdateien (ohne Videos, idempotent)`.

---

## Task 5: MediaDistributionService + ProjectPhotoAssignmentService → neue Struktur

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Import/MediaDistributionService.cs`
- Modify: `src/AuswertungPro.Next.Infrastructure/Import/ProjectPhotoAssignmentService.cs`
- Modify: `tests/AuswertungPro.Next.Infrastructure.Tests/MediaDistributionServiceTests.cs`, `ProjectPhotoAssignmentServiceTests.cs`

**Änderung:** Foto-Ziel von flach `Fotos\` (heute, Commit f9947528) auf gruppiert `Fotos\Haltungen\<Haltung>\` (über `ProjectStructure.FotosHaltungDir`). Verteil-Ziel von `Haltungen\<H>\` auf `Haltungen_Verteilt\<H>\` (Video/PDF). Schacht-PDFs → `Schächte_Verteilt\<Schacht>\`; Schacht-Fotos → `Fotos\Schächte\<Schacht>\`.

- [ ] **Step 1: Test anpassen/ergänzen** — `CopyProtocolFotos_PutsPhotosUnderFotosHaltungen`: Record „06-001" mit `entry.FotoPaths=[<extern>\bild.jpg]` → nach Distribution liegt die Datei unter `Fotos\Haltungen\06-001\bild.jpg` und `entry.FotoPaths[0]` ist der relative Pfad dorthin. (Bestehende Video-Tests müssen auf `Haltungen_Verteilt\` umgestellt werden.)
- [ ] **Step 2: Run → FAIL** (alte Pfade).
- [ ] **Step 3: Implement** — in `CopyRevisionFotos` `destDir = ProjectStructure.FotosHaltungDir(projectFolder, sanDerHaltung)`. Dazu muss die Haltung bis in die Foto-Kopie durchgereicht werden (heute kennt `CopyRevisionFotos` nur `holdingRoot`); Signatur um `string haltungSan` erweitern oder `holdingRoot` durch die neue Struktur ersetzen. Video/PDF-Ziele auf `Haltungen_Verteilt\` umstellen. In `ProjectPhotoAssignmentService` `fotoDir = ProjectStructure.FotosHaltungDir(projectFolder, san)`.
- [ ] **Step 4: Run → PASS** (volle Infrastructure-Tests).
- [ ] **Step 5: Commit** `refactor(import): Medien in neue Struktur (Haltungen_Verteilt, gruppierte Fotos)`.

---

## Task 6: Sia405WhitelistEnricher — kontrollierte Anreicherung

**Files:**
- Create: `src/AuswertungPro.Next.Application/Common/Sia405WhitelistEnricher.cs`
- Test: `tests/AuswertungPro.Next.Application.Tests/Sia405WhitelistEnricherTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record EnrichmentResult(int Filled, IReadOnlyList<string> Conflicts);
public static class Sia405WhitelistEnricher
{
    // Whitelist-Felder, NUR wenn am Record leer + NICHT userEdited.
    public static readonly string[] Whitelist =
        { "Rohrmaterial", "DN_mm", "Nutzungsart", "Strasse" }; // + Geometrie-Feld(er) sobald im Record-Schema benannt
    public static readonly string[] Protected =
        { "Datum_Jahr", "Bemerkungen", "Haltungslaenge_m" }; // Befunde ohnehin nie aus SIA405

    // sia405ByHaltung: Haltungsname -> (Feld -> Wert) aus dem SIA405-XTF (durch den Orchestrator gefuellt).
    public static EnrichmentResult Apply(Project project, IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>> sia405ByHaltung);
}
```
**Regel:** für jede Haltung: nur `Whitelist`-Felder; setzen NUR wenn `record.GetFieldValue(feld)` leer UND Feld nicht userEdited (`FieldSource`). Wenn der SIA405-Wert von einem bereits gefüllten Wert abweicht → Konflikt-Zeile, NICHT setzen. `Protected`/Befunde nie anfassen.

- [ ] **Step 1: Failing tests:** (a) leeres `Rohrmaterial` wird aus SIA405 gefüllt; (b) gefülltes `Rohrmaterial` (abweichend) bleibt + erzeugt Konflikt-Zeile; (c) `Datum_Jahr` wird NIE gesetzt, auch wenn leer; (d) userEdited-Feld bleibt unangetastet.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** (über `record.GetFieldValue` + `SetFieldValue(..., FieldSource.Xtf, userEdited:false)`; userEdited-Erkennung über das vorhandene FieldSource/Meta-Modell — Muster aus `ApplyImportedField`/`SetFieldValue` der bestehenden Importer übernehmen).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** `feat(import): Sia405WhitelistEnricher - kontrollierte Anreicherung leerer Felder`.

---

## Task 7: ProjectImportOrchestrator — die Pipeline

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Import/ProjectImportOrchestrator.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ProjectImportOrchestratorTests.cs`

**Interfaces — Consumes:** `KanalExportDetector`, `ImportSourceArchiver`, `ProjectStructure`, `IXtfImportService`, `IWinCanDbImportService`, `Sia405WhitelistEnricher`, `HoldingFolderDistributor.Distribute`, `MediaDistributionService`. **Produces:**
```csharp
public sealed record OneClickImportResult(
    KanalExportFormat Format, int Found, int Created, int Updated,
    int Errors, int Conflicts, IReadOnlyList<string> Messages);
public sealed class ProjectImportOrchestrator
{
    public ProjectImportOrchestrator(IXtfImportService xtf, IWinCanDbImportService winCan);
    // projectFolder = offenes Projekt (Struktur existiert). sourceFolder = Kanalfernseh-Quellordner.
    public OneClickImportResult Import(string sourceFolder, string projectFolder, Project project, ImportRunContext? ctx = null);
}
```
**Ablauf (nicht-blockierend, jeder Schritt in try/catch → Messages):**
1. Restore-Point (vorhandenes Restore-Muster nutzen; sonst `__RESTORE_POINTS\projekt\…` kopieren).
2. `ProjectStructure.EnsureCreated(projectFolder)`.
3. `KanalExportDetector.Detect(sourceFolder)`. `Ambiguous`/`Unknown` → Result mit Hinweis, kein Parse (UI fragt nach).
4. `ImportSourceArchiver.Archive(sourceFolder, projectFolder)`.
5. Parsen: IKAS → `xtf.ImportXtfFiles([VsaKekXtfPath], project, ctx)`; WinCan → `winCan.ImportWinCanExport(sourceFolder, project, ctx)`.
6. IKAS + `Sia405XtfPath` vorhanden → SIA405-Whitelist-Felder aus dem XTF lesen (Stammdaten-Parsepfad von `LegacyXtfImportService.ParseSia405` wiederverwenden bzw. minimal extrahieren) → `Sia405WhitelistEnricher.Apply`.
7. Verteilen: `HoldingFolderDistributor.Distribute(...)` Ziel `Haltungen_Verteilt\` + `MediaDistributionService` (Fotos gruppiert, Schächte). Quelle für Videos = `sourceFolder` (Filme bleiben dort, werden in `Haltungen_Verteilt\` kopiert).
8. Relativ verlinken (vorhandene Relink-Logik / `ProjectPortabilityService`-Muster).
9. Report nach `__IMPORT_REPORTS\` (vorhandener `ImportRunReportExporter`).

- [ ] **Step 1: Failing integration test** `ProjectImportOrchestratorTests.Import_Ikas_LinksPhotosPerObservation_AndArchives`: Mini-IKAS-Fixture (temp): `Dokumente\test.xtf` (VSA_KEK mit 1 Untersuchung „06-001", 2 Kanalschäden BCD/BAA, 1 `KEK.Datei` Objekt=BAA-TID), `Foto\H_06-001_002.jpg`, `Film\` leer. Projekt-Temp mit Struktur. Erwartung: `Format==Ikas`; Record „06-001"; BAA-Finding hat FotoPath, BCD nicht; `Importdateien\XTF\test.xtf` existiert; Foto liegt unter `Fotos\Haltungen\06-001\`. (XTF-Inhalt analog `XtfImportTests.VsaKekImport_LinksPhotoToCorrectObservation_ViaKanalschadenTid`.)
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** die Pipeline. Echte Importer injizieren (`XtfImportServiceAdapter`, `WinCanDbImportService`).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Idempotenz-Test** `Import_TwiceSameSource_NoDuplicates`: zweimal importieren → Record-Anzahl unverändert, keine doppelten FotoPaths, Datei-Reuse. Implementieren bis grün.
- [ ] **Step 6: Commit** `feat(import): ProjectImportOrchestrator - Ein-Knopf-Pipeline (detect/archive/parse/enrich/distribute/link/report)`.

---

## Task 8: UI-Knopf „Import Kanalfernseh-Projekt" (CODEX-Lane)

> **Eigentümer Codex** (UI-Projekt). Hier nur die Schnittstelle, kein Claude-Code.

**Files (Codex):** `UI/.../ViewModels/Pages/ImportPageViewModel.cs`, `UI/.../Views/Pages/ImportPage.xaml`.

- Neuer prominenter Knopf **„Import Kanalfernseh-Projekt"** → `_sp.Dialogs.SelectFolder(...)` → `new ProjectImportOrchestrator(xtf, winCan).Import(source, projectFolder, project, ctx)` (auf Hintergrund-Thread, `await Task.Run`), danach `TrySaveProject()` + Zusammenfassung/Report wie bei den vorhandenen Importen.
- Bei `Ambiguous`/`Unknown` → kurze Rückfrage/Hinweis.
- Bestehende 5 Format-Knöpfe in einen Bereich „Manuell / Spezialfall" verschieben (nicht löschen).

---

## Self-Review

**Spec-Abdeckung:** Struktur (Task 1) ✔; projekt.json-Kompat (Task 2) ✔; Detector (3) ✔; Archiv ohne Videos (4) ✔; gruppierte Fotos + Verteil-Struktur (5) ✔; SIA405-Whitelist + Priorität (6) ✔; Pipeline + Restore + Report + Idempotenz (7) ✔; Knopf + alte Knöpfe behalten (8, Codex) ✔; Datenhoheit IKAS=XTF/WinCan=db3 (7) ✔; FDB nur Archiv (4, nie geparst) ✔.

**Offene Präzisierung beim Bau:** (a) das konkrete Geometrie-/Lage-Feld im Record-Schema für die SIA405-Whitelist benennen (Task 6, per Grep im Record-Feldschema); (b) die genaue Restore-Point-API (vorhandenes Muster) in Task 7 verdrahten; (c) Call-Sites des Projekt-Öffnens (Task 2 Step 5) per Grep finden.

**Reihenfolge/Abhängigkeit:** 1 → (2,3,4 unabhängig) → 5 → 6 → 7 → 8. Task 7 hängt von 1,3,4,5,6.
