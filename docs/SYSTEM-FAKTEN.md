# SYSTEM-FAKTEN — code-geprüfter Faktenindex

> **Zweck:** Genau **eine** Quelle für veränderliche Systemfakten (Pfade, Routen, Modellnamen, Dateiformate). Alle Skills **verweisen** hierauf, statt Werte zu kopieren.
>
> **Massgebend bleiben Code und Tests.** Dies ist ein *geprüfter Index* dazu, keine eigenständige Wahrheit. Ändert sich ein Wert, wird er **hier** geändert.
>
> **Arbeitsteilung der drei Dokumente (kein Kopieren untereinander):**
> - `SYSTEM-FAKTEN.md` (dieses) → konkrete **Werte** (Pfade, Routen, Modelle, Formate).
> - `CODEBASE-KARTE.md` → **Architektur** (Schichten, DI, Verträge, Klassennamen, Merge-Semantik).
> - `CLAUDE.md` → **Regeln/Prinzipien** (Thin-AI, Sprache, Arbeitsweise).
>
> **Stand:** 2026-07-18. Belege in Klammern (`Datei:Zeile`).

## 1. Projekt & Build

- Projekt-Root: `c:\Sewer-Studio_KI_4.5` (WPF / .NET 10; UI-TFM `net10.0-windows10.0.19041`).
- Build: `dotnet build AuswertungPro.sln` · Test: `dotnet test AuswertungPro.sln` (Befehle in `AGENTS.md`).
- Entwicklungs-Solution: `AuswertungPro.Dev.slnf` (ohne Hilfsprogramme).
- Testprojekte: `AuswertungPro.Next.Infrastructure.Tests`, `.Pipeline.Tests`, `.UI.Tests`, `ProjectModernizer.Tests`.

## 2. Wissensordner (KnowledgeRoot)

Feste Auflösungsreihenfolge (`KnowledgeBasePathService.Resolve`, Beleg `src\...\Infrastructure\Ai\KnowledgeBase\KnowledgeBasePathService.cs:175 ff.`):

1. Umgebungsvariable `SEWERSTUDIO_KNOWLEDGE_ROOT`
2. gespeicherte Einstellung (`ConfigureSettingsRoot`)
3. Default `%LOCALAPPDATA%\SewerStudio\Knowledge`

- `%APPDATA%\AuswertungPro\KiVideoanalyse` ist **nur** alter Migrationspfad, kein aktiver Default.
- Auf dieser Maschine löst der Wissensordner aktuell auf `C:\KI_BRAIN` auf.
- **KnowledgeBase.db** liegt immer unter `<KnowledgeRoot>\KnowledgeBase.db` (aktuell `C:\KI_BRAIN\KnowledgeBase.db`).
- **Eval-Set:** `<KnowledgeRoot>\eval_set\` (`_manifest.json`, `_candidates.json`, `images\`, `labels\`; `frozen=true`, approved/exported = 120). Hashes: `hash_algorithm` top-level; `hashes` = Dict `{schlüssel: {sha256, size_bytes}}`.

## 3. Sidecar

- Ordner `sidecar\sidecar\`, Port **8100**.
- Routen: `/health`, `/warmup`, `/detect/yolo`, `/classify/yolo`, `/detect/dino`, `/segment/sam`, `/training/export-yolo`.
- SAM-Request-Feld: **`bounding_boxes`** (`sidecar\sidecar\schemas\segmentation.py:29`).
- **Nicht** vorhanden: `/predict/*`, `/model/reload`, `/enhance`, `/process/video`.

## 4. Modelle

- **YOLO Detect-Strecke:** `yolo26m.pt` (COCO-Fallback `yolo11m.pt`; `sidecar\sidecar\config.py:69`, `yolo_wrapper.py:59/76`). Gehört **nicht** zur Klassifikation.
- **Klassifikator (cls):** eigene Gewichte, aufgelöst über `sidecar\models\active.json` → `classifier.weights_path`. Reihenfolge: `active.json` → `settings.yolo_cls_model_path` → Legacy. SHA-256 wird gegen die Datei geprüft; Mismatch = cls bleibt AUS (`yolo_wrapper.py:482 ff.`). cls-Läufe `C:\KI_BRAIN\yolo_cls_runs\`, Kandidaten `C:\KI_BRAIN\model_candidates\`.
- **active.json / Laden:** cls lädt beim **Warmup oder bei der ersten Anfrage**, bleibt danach gespeichert. Kein Hot-Reload → eine Änderung an `active.json` wirkt normalerweise erst nach **Sidecar-Neustart**.
- **Grounding DINO:** Swin-B bevorzugt (`grounding_dino_swinb`), Fallback Swin-T OGC (`grounding_dino_1.5`) (`config.py:92-94`).
- **SAM:** **2.1** (`sam_backend = auto|sam2.1`, `config.py:113-115`). SAM 3 default aus (`sam3_enabled=False`, `config.py:136`). SAM-1 `vit_h` entfernt.
- **Qwen3-VL** (Ollama, Port 11434): `qwen3-vl:8b-q8` bei ≥24 GB VRAM, sonst `qwen3-vl:2b` (`GpuModelSelector.cs:150/153`). **Nie** qwen2.5. **Keine** automatische 8B→32B-Laufzeit-Eskalation.
- **Embeddings:** `nomic-embed-text`.
- **VRAM-Budget:** max 29 GB stabil, nie alle Modelle gleichzeitig. Hardware: Intel Core Ultra 9 285K · RTX 5090 32 GB · 64 GB DDR5.
- `bend_geometry`: per Default deaktiviert.

## 5. Pipeline

- Dedup/Merge **C#-framebasiert**: `TemporalFindingDeduplicator` + `TemporalCodeVotingService`.
- Kein ByteTrack/OC-SORT, kein echtes Multi-Object-Tracking.
- Nicht existent: `DetectionAggregator`, `InferenceOrchestratorService`, `KbDeduplicationService`, `YoloDatasetExportService`, `FewShotExampleStore`.

## 6. PDF-Import

- Parser: `PdfParser` (`Import\Pdf\`), `PdfProtocolExtractor` (`Ai\Training\Services\`), `PrimaryDamageRowParser`.
- pdftotext-Pfad: `DiagnosticsOptions.ExplicitPdfToTextPath` (als Parameter durchgereicht, keine statische Parser-Property).

## 7. VSA-KEK

- Aktive Quelle: `src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json` (2020). Es gibt **kein** `vsa_codes.json`.
- Codes: BCD=Rohranfang, BCE=Rohrende, BCA=seitl. Anschluss, BCC=Bogen · BAA=Verformung, BAB=Riss, BAC=Bruch, BAF=Oberflächenschaden, BAH=schadhafter Anschluss, BAI=einragendes Dichtungsmaterial, BAJ=verschobene Rohrverbindung · BBA=Wurzeln, BBB=anhaftende Stoffe, BBC=Ablagerung, BBD*=eindringender Boden. Detect-Klasse `BBD_boden` → C# mappt auf `BBDZ`, nie nacktes `BBD`.
- Eval-Messung: ereignisbasiert (`EvalSetEventScorer`, Schlüssel Haltung+EventId; `EvalSetReleaseDatasetValidator`, `EvalSetV2Builder`, `EvalSetManifestHasher`). `EvalSetBenchmark.cs` ist aufgeteilt/entfernt.

## 8. Negativliste — nicht mehr existent (Grundlage für den Skill-Linter)

Diese Begriffe dürfen in aktiven Skills **nicht mehr affirmativ** vorkommen (Negation/Meta wie „existiert nicht" ist erlaubt):

- Pfade: `Sewer-StudioKI_3.1`, `Sewer-Studio_KI_4.0`, `Sewer-Studio_KI_4.1`
- Modelle: `qwen3-vl:32b`, `qwen2.5`, `YOLO26m-seg`, `SAM 3`, `vit_h`, `grounding-dino-1.5` (als Standard)
- Dateien/Klassen: `benchmark_metrics.json`, `vsa_codes.json`, `FewShotExampleStore`, `DetectionAggregator`, `YoloDatasetExportService`, `BenchmarkMetricsStore`, `BenchmarkRunner`, `EvalSetGenerator`, `PdfProtocolTableParser`, `PdfToTextExePath`
- Routen: `/predict/`, `/model/reload`, `/enhance`, `/process/video`
- Konzepte: automatische „8B→32B-Eskalation", ByteTrack/Tracking, `UpdateActive` als Dedup
