# Cleanup Inventory 2026-06-21

Verifiziert gegen den echten Ist-Zustand (`git ls-files`, `Get-ChildItem`, `dotnet sln list`).
Begleitdateien: `2026-06-21-delete-candidates.csv` (alle Top-Level-Groessen),
`2026-06-21-tracked-legacy-candidates.txt` (getrackte Altlasten).

## Keep (produktiv, nicht anfassen)
- `src/`, `tests/`, `sidecar/sidecar/`, `sidecar/tests/`
- aktive Modelle: `sidecar/models/yolo26m/`, `grounding_dino_swinb/`, `sam2.1/`, `active.json`
- aktiver Fallback: `sidecar/models/grounding_dino_1.5/` (dokumentierter Swin-T-Fallback)
- Solution-Tools (9, in `AuswertungPro.sln`)
- `Export_Vorlage/` (von der .NET-App aktiv genutzt: ExportPageViewModel/SchaechtePageViewModel + Tests)
- `KOSTENBERECHNUNG.md` (aktuelle .Next-Kostenklassen) -> nach `docs/kostenberechnung.md` verschieben + Encoding bereinigen, NICHT loeschen
- aktuelle Doku: `docs/superpowers/`, `docs/VSA-Regelwerk-KI-Pipeline.md`, `CLAUDE.md`, `AGENTS.md`,
  `KI_CODIERMODUS_ANLEITUNG.md`, `KI_VIDEO_TRAINING_ANLEITUNG.md` (beide am 21.06 aktualisiert)

## Quarantine First (nur lokal / ge-ignore-t, reversibel)
Groesse (verifiziert): `.tmp` 28,8 GB | `models/` 3,3 GB | `sam2.1_l.pt` 428 MB |
Root-`yolo*.pt` ~1,0 GB | `ManualTraining*` ~1,8 GB | `sam3.1/` (Source-Checkout).
- Risikoarmer Start: `.tmp/`, `tmp/`, `__pycache__/` (falls vorhanden), `push_progress.log`
- Caches: `.pytest_cache/`, `.ruff_cache/`, `.coverage`, `coverage.xml`, `package-lock.json`, `training_export/`
- Root-Modellkopien: `sam2.1_l.pt`, alle Root-`yolo*.pt`/`yolov8*.pt`, `models/`, `sam3.1/`
- Review-Ausgaben: `ManualTraining*_20260525/`, `Wasserstand*Review*/`, `WeakClass*Review*/`
- Sidecar-Altmodelle (nicht in `active.json`, nicht geladen): `sidecar/models/florence-2/`, `florence-2-ft/`, `RealESRGAN_x4plus.pth`, `sam2/`

## Git Remove Candidates (getrackt -> `git rm`)
- Legacy-PowerShell: 13 Root-`.ps1`/`.csx` inkl. `HaltungenTool.ps1` + `Services/` (15 `.ps1` + 1 JSON)
- Legacy-Regeln: `vsa_rili_rules_kanaele.json` (alte PowerShell-Welt)
- Alte Root-Doku: `README_v2.md`, `START.md`, `ARCHITECTURE.md`, `DATEIEN_MANIFEST.md`,
  `LIEFERUEBERSICHT.md`, `RELEASE_NOTES_v2.1.0.md`, `CODE_AUDIT_REPORT.md`
  (`AUDIT_*.md` ist bereits ge-ignore-t, nicht getrackt)

## Needs Manual Decision
- `EvalVisibilityReview_20260525/` (105 MB, von Trainings-Skripten referenziert -> eval-set-warden vor Aktion)
- ~22 nicht klassifizierte Orphan-Tools in `tools/` (Einzelentscheidung, je 1 Commit)
- `tools/__pycache__` (sofort loeschbar)

## Hinweis Git-Status
Bereits gestaged (fremde Arbeit, NICHT mit Cleanup buendeln):
`ShellViewModel.cs`, `TrainingCenterViewModel.cs`.
Cleanup-Commits daher pfad-begrenzt: `git commit -m "..." -- <pfade>`.
