# SewerStudio Tool-Inventar

Datum: 2026-06-08  
Zweck: lokale Tools sichtbar machen, damit keine versteckte Werkzeuglandschaft entsteht.

## Status-Legende

| Status | Bedeutung |
|---|---|
| aktiv | gehoert zum aktuellen Arbeitsfluss |
| analyse | erzeugt Reports/Diagnosen, kein Produktivpfad |
| smoke | prueft einen realen Pfad manuell/opt-in |
| legacy | historisch nuetzlich, aktuell nicht priorisiert |
| output-only | generierte Ausgabe, nicht committen |

## Aktive / wichtige Tools

| Tool | Status | Zweck | Input | Output / Regel |
|---|---|---|---|---|
| `tools/SidecarE2eSmoke` | smoke | Echter Sidecar/GPU-Smoke: Health, YOLO-cls, YOLO-detect, optional DINO/SAM | Bild oder Video+Sekunde | JSON-Report optional; manuell ausfuehren |
| `tools/VideoLabelTool` | aktiv | Video-Scrub + Gold-Label-Erzeugung mit Frame/Box/Maske | Haltungs-Video, Befundzeit | Gold-Daten nach `C:\KI_BRAIN`, keine Rohvideos veraendern |
| `training/vsa_classifier` | aktiv | YOLO/VSA-Klassifikator trainieren, evaluieren, Schwellwerte pruefen | saubere Datensaetze unter `C:\KI_BRAIN` | Runs/Reports extern; keine Modelle ins Repo |
| `tools/ClassifierDatasetBuilder` | aktiv | eval-freien YOLO-cls-Datensatz bauen | `training_frames`, Eval-Set | Dataset extern; Report pruefen |
| `tools/StageAExporter` | aktiv | Kompatibilitaets-CLI fuer denselben AP-0.3-Plan wie WPF | kanonisches Inventar, freigegebenes Register und class_map v2 | Plan-only ohne Schreibzugriff oder lokaler Export nach `training\datasets\<plan-id>` |
| `tools/EvalSetBenchmark` | aktiv | Qwen/Sidecar-Kontexte gegen Eval-Set messen | eingefrorenes Eval-Set | Report, kein Training |
| `tools/EvalSetManifestHasher` | aktiv | Eval-Manifest mit Hashes/Counts aktualisieren | Eval-Set | `_manifest.json` |
| `tools/EvalSetV2Builder` | aktiv | Menschlich geprueftes V2 mit Streuungs- und Leakage-Schutz einfrieren | Kandidaten-JSON, V1 nur lesend | `C:\KI_BRAIN\eval_set\v2` |

## Analyse- und Diagnose-Tools

| Tool | Status | Zweck | Hinweis |
|---|---|---|---|
| `tools/EvalVisibilityReview` | analyse | Sichtbarkeitsreview fuer Eval-Frames | lokale Review-Hilfe |
| `tools/InspectionDateAudit` | analyse | Inspektionsdatum pruefen | CLI-Probe |
| `tools/SelfTrainingHarness` | analyse | Self-Training-Pfade isoliert starten | nicht Produktiv-UI |
| `tools/VsaShadowReport` | analyse | Shadow-Ergebnisse auswerten | in Solution |
| `tools/VsaClassificationRuleBuilder` | analyse | VSA-Regel-/Klassifizierungslogik bauen/pruefen | in Solution |
| `tools/AiQualityReport` | analyse | Deduplizierte Feldfehler und Schattenauswertung gemeinsam berichten | Markdown, JSON und CSV |
| `tools/IliCatalogReader` | analyse | ILI-Katalog lesen | in Solution |
| `tools/CadasterDbReader` | analyse | Kataster-/DB-Lesepfad pruefen | Lockfile getrackt |

## Lokale Outputs / nicht committen

Folgende Muster sind bewusst in `.gitignore`:

- `tools/**/output/`
- `tools/ProtocolPipelineDiagnostics/output-smoke/`
- `tools/SewerStudioTrainingBatch/*.json`
- `tools/SewerStudioTrainingBatch/*.jpg`
- `tools/VideoprojekteInventory/videoprojekte_inventory_*.csv`
- `tools/VideoprojekteInventory/videoprojekte_inventory_*.json`
- `tools/OffertenKalibrierung/`
- `.codex/`, `.codex-remote-attachments/`
- `xtf_korrektur/`

## Regeln

1. Neue Tools bekommen eine `README.md` oder Eintrag hier.
2. Tools mit `.csproj` sollen entweder in `AuswertungPro.sln` oder bewusst als Scratch markiert sein.
3. Tools duerfen keine Produktivdaten ins Repo schreiben.
4. Trainingsmodelle, externe Datensaetze und Video-Frames bleiben ausserhalb des Repos.
5. E2E-Smoke-Tools sind opt-in und duerfen normale Unit-Tests nicht instabil machen.

## Offene Ordnung

| Aufgabe | Grund |
|---|---|
| Orphan-Tools einzeln bewerten | Einige alte Tools sind nuetzlich, aber nicht in der Solution |
| README fuer `training/vsa_classifier` ergaenzen | CLI-Parameter und Abnahmekriterien sollen an einem Ort stehen |
| Reports vereinheitlichen | JSON-Reports statt reine Konsolenlogs |

## Cleanup-Review 2026-06-21

`tools/` hat real ~53 Unterordner; 9 sind in `AuswertungPro.sln`, 30 haben ein getracktes `.csproj`.
Rohlisten: `docs/cleanup/solution-projects.txt`, `docs/cleanup/tracked-tool-projects.txt`.

**Erledigt:** `tools/__pycache__` quarantaeniert. `bin`/`obj` bleiben (gitignored, regenerieren).

**Keine Loeschung ohne Einzelentscheidung** (Plan-Regel). Orphan-Tools (nicht in `.sln`,
einzeln per rg-Referenz + Doku-Nutzung pruefen, je 1 Commit):

- PDF/Import: AiDocPdf, DiagnosticPdfParser, IbakPdfAnalyzer, PdfCoverageAudit, PdfHeaderReader,
  PdfImageAnalyzer, PdfPhotoCoverageProbe, PdfPhotoLabelReview, PdfProtocolParser, QuickPdfAnalyzer,
  VsaPdfExtractor, ProtocolPipelineDiagnostics
- Kataster/DB/XTF: CadastreTableBuilder, Db3PilotReader, MdbSchemaReaderApp, HaltungTopologyExtractor,
  StammdatenExporter, XtfPilotReader
- KB/Training/Eval: BefundMatcher, CrossValidationReport, FachwissenIndexer, KbCodeCleanup, kb_audit,
  KnowledgeBaseInspector, sewer_classifier, SewerStudioTrainingBatch, TrainingSourceReport,
  SewerStudio.AiTestRunner, GroundTruthPipeScaleProbe
- Bild/Video/Sonstige: FrameMultiExtractor, GeminiPhotoCheck, ImageQualityAudit, video_ai,
  VideoprojekteInventory, AuswertungPro.MeasureCatalogCli, DichtheitDistributeTest,
  HoldingDistributionSmokeCheck

Nicht verwechseln: Tool `tools/EvalVisibilityReview` != Root-`EvalVisibilityReview_20260525` (Eval-Daten, bleibt).
