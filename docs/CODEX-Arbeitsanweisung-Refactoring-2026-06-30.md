# Arbeitsanweisung für Codex — Refactoring: projektmanager auf den Ein-Knopf-Import reconcilen

> Stand 2026-06-30. Adressat: Codex (UI-Lane). Du hast keinen Kontext aus der Backend-Session — diese
> Anweisung ist self-contained. Whole-File-Ownership gilt: **Codex = UI-Projekt** (`AuswertungPro.Next.UI`),
> Claude = Domain/Application/Infrastructure. Tests müssen grün bleiben, nichts pushen ohne OK.

## Ausgangslage (was frisch in `feature/gis-karte` ist)
Ein **Ein-Knopf-Import „Kanalfernseh-Projekt"** wurde gebaut + gemergt (HEAD ~`0b77b4cf`):

- **Backend (Infrastructure/Application, fertig, NICHT anfassen außer Nutzung):**
  - `ProjectImportOrchestrator.Import(sourceFolder, projectFolder, project, ctx)` — die Pipeline:
    Format erkennen (`KanalExportDetector`) → Rohdaten archivieren (`ImportSourceArchiver` →
    `Importdateien\{Datenbanken,XTF,PDF,TXT}`) → maßgebliche Quelle parsen (IKAS=VSA_KEK-XTF /
    WinCan=`.db3`, inkl. Pro-Beobachtung-Fotos) → SIA405-Whitelist-Anreicherung →
    `MediaDistributionService.DistributeImportedMedia` (Filme/PDFs + Fotos verteilen) → `OneClickImportResult`.
  - **Neue feste Projekt-Struktur** via `ProjectStructure` (Infrastructure.Import):
    `Importdateien\{Datenbanken,XTF,PDF,TXT}`, `Haltungen_Verteilt\<H>\`, `Schächte_Verteilt\<S>\`,
    `Fotos\Haltungen\<H>\` + `Fotos\Schächte\<S>\` (Fotos zentral GRUPPIERT), `Projektdateien\`,
    `__IMPORT_REPORTS\`, `__RESTORE_POINTS\`.
  - `MediaDistributionService` verteilt jetzt nach **`Haltungen_Verteilt\`** (nicht mehr `Haltungen\`),
    Fotos nach **`Fotos\Haltungen\<H>\`**, und Schächte nach `Schächte_Verteilt\<S>\`.
  - `ProjectFileLocator` (Application.Common): `projekt.json` liegt bei neuen Projekten unter
    `<Projekt>\Projektdateien\projekt.json` (+ Root-Pointer `projekt.pointer`); `ProjectRootFromFile`
    liefert den echten Root rückwärtskompatibel.
- **UI (bereits gemergt):**
  - `ImportPageViewModel.ImportKanalProjektCommand` / `ImportKanalProjektAsync` → ruft den Orchestrator;
    prominenter Knopf **„Import Kanalfernseh-Projekt"** auf `ImportPage.xaml`; die 5 alten Format-Knöpfe
    bleiben als „Manuell".
  - `ShellViewModel.CreateProjectFromDraft` legt jetzt die feste Struktur an (`ProjectStructure.EnsureCreated`)
    + schreibt `projekt.json` nach `Projektdateien\` + Root-Pointer.
  - `ShellViewModel.GetProjectFolder` ist rückwärtskompatibel (liefert den Projekt-Root, auch wenn die
    `projekt.json` in `Projektdateien\` liegt).

## Das Problem (warum Refactoring nötig ist)
Dein Branch **`feature/projektmanager` ist NICHT gemergt und divergiert** vom obigen Stand. Er wurde
gebaut, BEVOR der Ein-Knopf-Import in `gis-karte` lag, und kollidiert jetzt:

- **`MediaDistributionService.cs`** (Infrastructure): dein Branch hat die **alte Struktur** (`Haltungen\`)
  + einen `includeVideos`-Parameter; gis-karte hat `Haltungen_Verteilt\` + gruppierte Fotos + Schacht-
  Verteilung. → **harter Merge-Konflikt.**
- **`ShellViewModel.cs`**: dein Branch hat einen eigenen Projekterstellungs-Umbau (`NewProjectPathPolicy`,
  „Neues Projekt per Name + Ordner"); gis-karte hat jetzt `CreateProjectFromDraft` (Struktur +
  `Projektdateien\`) + `GetProjectFolder`. → **Konflikt + Doppellogik.**
- **`ImportPageViewModel.cs`**: dein Branch ändert `DistributeMediaToProjectFolder` (`includeVideos:false`);
  gis-karte hat den neuen Orchestrator-Knopf. → mergebar, aber zu integrieren.
- Dein Branch **nutzt den `ProjectImportOrchestrator` NICHT** (eigene/alte Import-Logik).

**User-Entscheid: Linie „A" — der Orchestrator-basierte Ein-Knopf-Import ist kanonisch.** `projektmanager`
NICHT blind in gis-karte mergen.

## Deine Aufgabe (Refactoring)
**Re-applizieren statt mergen:** Starte frisch von der aktuellen `feature/gis-karte` und übertrage NUR die
guten, nicht-duplizierten Verbesserungen aus `projektmanager` darauf, integriert mit den neuen APIs.

1. **Behalten + anpassen (aus `projektmanager`):**
   - `NewProjectPathPolicy` + „Neues Projekt per Name mit automatischer Ordner-Erstellung" (gute UX) —
     aber so, dass es auf `CreateProjectFromDraft` aufsetzt, das jetzt schon `ProjectStructure.EnsureCreated`
     + `projekt.json` nach `Projektdateien\` macht. Nicht die Struktur/Projekt-Datei-Ablage neu erfinden.
   - Politik **„Import kopiert keine Videos, erst beim Verteilen"**: re-applizieren als `includeVideos`-Flag
     auf der AKTUELLEN `MediaDistributionService`-Signatur (die jetzt `Haltungen_Verteilt\`/`Fotos\Haltungen\`
     + Schächte kennt). WICHTIG: das betrifft den **manuellen** Import-/Verteil-Pfad — der **Ein-Knopf-
     Import** (Orchestrator) verteilt Filme bewusst mit (User-Spec „verteilt die Filme"). Diese beiden
     Pfade nicht vermischen.
   - „Verteilen zielt standardmäßig auf den Projektordner", SettingsPage-/ExportPage-Verbesserungen,
     `chore: ungenutzten InputDialog/Prompt entfernen` — übernehmen, wenn konfliktfrei.
2. **Fallenlassen / vermeiden:**
   - Alles, was die **alte `Haltungen\`-Struktur** oder eine **parallele Import-Logik** wieder einführt.
   - Keine Konkurrenz zum `ProjectImportOrchestrator` — der ist die maßgebliche Import-Pipeline.
3. **`MediaDistributionService`-Konflikt:** Diese Datei gehört Claude (Infrastructure). Wenn du den
   `includeVideos`-Schalter brauchst, stimm den Patch mit Claude ab (kleine, additive Parameter-Erweiterung
   auf der NEUEN Signatur) — NICHT die alte Struktur zurückbringen.
4. **Verifikation:** volle Solution grün (`dotnet test AuswertungPro.sln`); manueller WPF-Smoke des
   Ein-Knopf-Imports + Projekterzeugung (Struktur + `Projektdateien\projekt.json` + Root-Pointer).

## Danach (optionale UI-Refactoring-Folge, falls Kapazität)
Siehe `docs/ARCHITEKTUR-FAHRPLAN-V3-QUALITAET.md` — Codex-UI-Lanes: Page-VMs `IDisposable`
(BuilderPageViewModel-Muster), UI-Threading-Fixes, Test-Hygiene (brittle String-Guards raus),
Dekompositions-Boden. Diese sind unabhängig vom Import und können nach der Reconciliation laufen.

## Leitplanken
- Eigener Worktree/Branch off `feature/gis-karte`, nicht direkt auf gis-karte arbeiten.
- Nur UI-Dateien ändern; Infrastructure/Application nur nutzen (Ausnahme `MediaDistributionService`-Flag:
  mit Claude abstimmen).
- Verhaltensneutral wo möglich; jede riskante Änderung mit Test. Nichts pushen ohne OK. Kommentare deutsch.
