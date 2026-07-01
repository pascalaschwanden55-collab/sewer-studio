# Handoff an Codex — Claudes Arbeit am 2026-07-01

> Zweck: Wissenstransfer. Was Claude (Backend-Lane) heute in `feature/gis-karte` gebaut/gemergt hat,
> welche APIs/Strukturen neu sind, wo Claude UI-Dateien angefasst hat (Konfliktrisiko mit deiner
> Refactoring-Lane) und was offen ist. Alles **lokal gemergt, NICHT gepusht**.

## Arbeitsteilung (Erinnerung)
- **Claude:** Domain / Application / Infrastructure (+ deren Tests). Ausnahme: die Ein-Knopf-Import-
  Feature-UI (ImportPage/DataPage/ExportPage-Teile), die zum Feature gehört — siehe „UI von Claude".
- **Codex:** UI-Projekt (`AuswertungPro.Next.UI`), God-Klassen-Dekomposition (DataPage-/Player-Controller etc.).
- Konvention: whole-file-ownership, `--no-ff`-Merges in `feature/gis-karte`, nichts pushen ohne OK,
  ein Worktree pro Agent, Kommentare deutsch, volle Suite grün vor Merge.

## Claudes Merges heute (chronologisch, oben = neu)
| Merge | Inhalt |
|---|---|
| `94fc05ee` | Manuelle Verteilung (Export) → `Haltungen_Verteilt\` / `Schächte_Verteilt\` statt Projekt-Root |
| `45e8f53d` | Revert: KEINE Auto-Schacht-Verteilung (Schächte werden manuell verteilt); Haltung-„ein-PDF" bleibt |
| `aa2a5c79` | Ein Protokoll/Haltung (SelectPrimaryProtocolPdf) + (revidiert) seiten-gruppierte Schacht-Protokolle |
| `b2f80014` | Rename-Sync benennt auch `Fotos\Haltungen\<H>\` mit um |
| `6dc33fce` | Haltungsnummer-Rename auch aus dem Formular-Detail-Editor (nicht nur Datagrid) |
| `b917afdc` | Rename-Sync verteilte Dateien (neue Struktur) + `ProjectPathResolver.ResolveFilePath` gegen Root |
| `be11a240` | Play/Medien/Protokoll: relativen Link gegen Projekt-ROOT auflösen (6 UI-Stellen) |
| `bb524167` | WinCan `Datum_Jahr` = Inspektionsdatum (INS_StartDate) statt Baujahr |
| `2c0362f2` | Feldabdeckung: MergeEngine-Wurzelfix + Schacht/Eigentümer/FunktionHierarchisch aus XTF/db3/SIA405 |
| `b901b0e2` | Import verteilt Original-Protokoll; eigenes `_E`-Protokoll per Knopf „Protokoll neu generieren" |
| `f0855fb0`, `6a63661c` | (früher am Tag) Verteilung flach+datumsbenannt; XTF Foto/Film-Auflösung Eltern-Ebenen |

## Neue/wichtige Backend-APIs & Regeln (relevant für dich)

### 1. MergeEngine-Wurzelfix (`Infrastructure/Import/Common/MergeEngine.cs`)
`MergeRecord` mergte NUR `FieldCatalog.ColumnOrder` (34 Felder) und verwarf still alle DYNAMISCHEN
Quell-Felder. Jetzt: `ColumnOrder + source.FieldMeta.Keys`. **Folge für UI:** dynamische Felder wie
`Schacht_oben`, `Schacht_unten`, `PDF_Eigen` überleben jetzt den XTF-/PDF-Import und erscheinen im
Datagrid-Detail („Weitere Angaben"). Kein UI-Change nötig, aber gut zu wissen.

### 2. Feldabdeckung — neue gefüllte Felder
`Schacht_oben`/`Schacht_unten` (VSA_KEK von-/bisPunkt, SIA405-Whitelist, WinCan From/ToNode mit
Befahrungsrichtung), `Eigentuemer`, `FunktionHierarchisch` (SIA405, speist VSA-Note B4), Bemerkungen
mit Inspektionskontext. SIA405-Whitelist erweitert. `PDF_Eigen` = neues Feld für das generierte
`_E`-Protokoll (Original bleibt in `PDF_Path`).

### 3. Verteil-Protokolle
- **Import** verteilt das **Original-Protokoll** (`PDF_Path`), Video flach+datumsbenannt (`Link`), Fotos.
  Nur EIN maßgebliches Protokoll pro Haltung (`KanalImportDistributor.SelectPrimaryProtocolPdf`).
- **„Protokoll neu generieren"** (ImportPage, `ProtokollNeuGenerierenCommand` → `ProtocolRegenerationService`)
  erzeugt am Ende das eigene `_E`-Protokoll → `PDF_Eigen`.
- **Schächte** verteilt der Import NICHT (`includeSchacht:false`); der Anwender nutzt „Schacht Verteilen".

### 4. **WICHTIGE REGEL: relative Projektpfade IMMER gegen den Root auflösen**
Seit die `projekt.json` unter `Projektdateien\` liegt, ist `Path.GetDirectoryName(projekt.json)` NICHT
mehr der Projekt-Root. Verteilte Links sind relativ zum **Root** (`Haltungen_Verteilt\…`) gespeichert.
→ **Immer** `AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(projektJsonPfad)`
verwenden, NIE `GetDirectoryName`. Betroffene/gefixte Stellen: `DataPageProtocolPathResolver`,
`ProjectPathResolver.ResolveFilePath`, `HoldingRenameService`. **Falls du weitere Stellen mit
`GetDirectoryName(LastProjectPath)` findest (z.B. in ViewModels/Views), dort denselben Fix anwenden.**

## UI-Dateien, die Claude angefasst hat (KONFLIKTRISIKO mit deiner Lane)
Bitte beim Refactoring dieser Dateien Claudes Änderungen berücksichtigen:
- `ViewModels/Pages/DataPageViewModel.cs` — `_shell.GetProjectFolder()` als Video-Initialordner (Root-Fix).
- `Views/Pages/DataPage.xaml.cs` — neue Methode `ApplyHoldingNameChange` (Haltungsnummer-Rename), aus
  Datagrid-CellEditEnding **und** `CommitHaltungDetailField` (Formular-Editor) aufgerufen.
- `ViewModels/Pages/ExportPageViewModel.cs` — `ResolveDistributionSubfolder` (Verteilung in `*_Verteilt\`).
- `ViewModels/Pages/ImportPageViewModel.cs` — `ImportKanalProjektCommand`, `ProtokollNeuGenerierenCommand`.
- `Views/Pages/ImportPage.xaml` — Knöpfe „Import Kanalfernseh-Projekt", „Protokoll neu generieren".
- `DataPage/DataPageProtocolPathResolver.cs`, `DataPage/DataPageProtocolWindowController.cs`,
  `DataPage/DataPageVideoRelinkController.cs`, `Views/ProtocolObservationsWindow.xaml.cs`,
  `Views/ProtocolEntryEditorDialog.xaml.cs` — Root-Auflösung von Medienpfaden.

## Offene Punkte / nächste Schritte
1. **Gegeninspektion abspielen/ansehen** (User-Wunsch, noch offen): das `_G`-Video wird verteilt
   (`<stamp>_<H>_G.mpg`), aber an kein Record-Feld gehängt → Play kommt nicht dran. Vorschlag:
   Backend setzt `Link_G` (+ ggf. `PDF_G`) beim Verteilen; UI zeigt einen zweiten „Play (G)"/
   „Protokoll (G)"-Knopf, wenn das Feld gesetzt ist. (UI-Teil = deine Lane.)
2. **Schacht-Parser an SchachtPro-Format anpassen**, falls „Schacht Verteilen" den 203-MB-Gesamtauszug
   nicht sauber pro Schacht trennt (`SplitPdfIntoShafts`/`ParseSchachtPdf`).
3. **Manueller WPF-Smoke** des Ein-Knopf-Imports + der neuen Abläufe (Play/Rename/Protokoll neu generieren).

## Wissenstransfer-Mechanismus (Vorschlag)
- **Git-Log ist die Wahrheit:** Claude-Commits tragen `Co-Authored-By: Claude …`, Codex-Commits nicht.
  `git log --first-parent` zeigt die Merge-Reihenfolge; Commit-Messages erklären Was/Warum.
- **Dieses `docs/CODEX-Handoff-…md`** pro größerem Claude-Arbeitsblock — du liest es vor deinem nächsten
  Refactoring-Zug. (Claudes private Memory ist für dich NICHT sichtbar; der Repo-Doc ist der Kanal.)
- **Whole-File-Ownership beibehalten** minimiert Konflikte; wo Claude UI angefasst hat (Liste oben),
  vor dem Refactoring kurz `git log -p <datei>` prüfen.
