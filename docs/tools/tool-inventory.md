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
| `tools/StageAExporter` | aktiv | Stage-A/YOLO-Export mit Eval-Schutz und echter BBox | Training samples | Export nur mit Guard |
| `tools/EvalSetBenchmark` | aktiv | Qwen/Sidecar-Kontexte gegen Eval-Set messen | eingefrorenes Eval-Set | Report, kein Training |
| `tools/EvalSetManifestHasher` | aktiv | Eval-Manifest mit Hashes/Counts aktualisieren | Eval-Set | `_manifest.json` |

## Analyse- und Diagnose-Tools

| Tool | Status | Zweck | Hinweis |
|---|---|---|---|
| `tools/EvalVisibilityReview` | analyse | Sichtbarkeitsreview fuer Eval-Frames | lokale Review-Hilfe |
| `tools/InspectionDateAudit` | analyse | Inspektionsdatum pruefen | CLI-Probe |
| `tools/SelfTrainingHarness` | analyse | Self-Training-Pfade isoliert starten | nicht Produktiv-UI |
| `tools/VsaShadowReport` | analyse | Shadow-Ergebnisse auswerten | in Solution |
| `tools/VsaClassificationRuleBuilder` | analyse | VSA-Regel-/Klassifizierungslogik bauen/pruefen | in Solution |
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
