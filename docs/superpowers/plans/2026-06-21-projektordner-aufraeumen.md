# Projektordner-Aufraeumen Implementation Plan

> **Hinweis (2026-06-21):** Diese Datei wurde rekonstruiert. Das untrackte Original
> ging durch ein `reset`/`clean` einer parallel laufenden zweiten Session verloren
> (siehe Memory-Notiz `rogue-cleanup-hazard`). Inhalt = Original + verifizierte
> Ergaenzungen + getroffene Entscheidungen. Ausfuehrungsstatus siehe Abschnitt am Ende.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den SewerStudio-Projektordner von lokalen Artefakten, alten PowerShell-Altlasten, veralteten Modellkopien, Review-Ausgaben und nicht mehr relevanten Tools befreien, ohne produktive App-, Sidecar-, Trainings- oder Eval-Daten zu verlieren.

**Architecture:** Aufraeumen erfolgt in drei Sicherheitsstufen: erst Inventar und Klassifikation, dann Quarantaene statt Sofort-Loeschung, danach gezielte `git rm`-/`Remove-Item`-Commits mit Build/Test-Nachweis. Produktive Pfade bleiben `src/`, `tests/`, `sidecar/sidecar/`, aktive `sidecar/models/*`, die in `AuswertungPro.sln` enthaltenen Tools, `docs/superpowers/` und aktuelle Betriebsdokumente.

**Tech Stack:** PowerShell, Git, .NET 10 Solution, Pytest fuer Sidecar, bestehende `.gitignore`-Regeln.

---

## Vorbedingungen und Sicherheitsregeln

- Keine Loeschung ohne vorheriges Inventar.
- Keine Loeschung von aktiven Modellgewichten, solange Sidecar-Health/Warmup nicht danach validiert wurde.
- Keine Loeschung von Eval-Daten, solange Trainings-/Benchmark-Skripte noch fest darauf zeigen.
- Getrackte Altlasten mit `git rm`, lokale/ignorierte Artefakte zuerst nach `C:\tmp\SewerStudioCleanupQuarantine\<Datum>\` verschieben.
- Jeder Cleanup-Block eigener Commit, pfad-begrenzt (keine fremden Aenderungen mitnehmen).
- Vor jedem `Remove-Item -Recurse`: absolute Zielpfade mit `Resolve-Path` pruefen; nur Pfade unter `C:\Sewer-Studio_KI_4.4` oder unter der Quarantaene anfassen.
- ACHTUNG Parallelbetrieb: Keine zweite Agent-Session gleichzeitig auf demselben Branch/Arbeitsverzeichnis (sie resettet/cleant und wirft Commits + untrackte Dateien weg). Siehe `rogue-cleanup-hazard`.

## Dateien und Bereiche

**Sicher behalten:**
- `AuswertungPro.sln`
- `Directory.Build.props`, `global.json`, `.gitignore`, `AGENTS.md`, `CLAUDE.md`
- `src/`, `tests/`
- `sidecar/sidecar/`, `sidecar/tests/`, `sidecar/start_sidecar.ps1`, `sidecar/build_engine.ps1`, `sidecar/pyproject.toml`, `sidecar/requirements*.txt`
- aktive Modellordner: `sidecar/models/yolo26m/`, `grounding_dino_swinb/`, `sam2.1/`, `active.json`
- aktiver Fallback behalten: `sidecar/models/grounding_dino_1.5/` (dokumentierter Swin-T-Fallback)
- Solution-Tools: ClassifierDatasetBuilder, ClassifierPilot, EvalSetBenchmark, EvalSetManifestHasher, IliCatalogReader, SelfTrainingHarness, SidecarE2eSmoke, VsaClassificationRuleBuilder, VsaShadowReport
- `Export_Vorlage/` (von der .NET-App aktiv genutzt: ExportPageViewModel/SchaechtePageViewModel + Tests) -> BEHALTEN
- `KOSTENBERECHNUNG.md` (aktuelle .Next-Kostenklassen) -> NICHT loeschen, nach `docs/kostenberechnung.md` verschoben
- `KI_CODIERMODUS_ANLEITUNG.md`, `KI_VIDEO_TRAINING_ANLEITUNG.md` (am 21.06 aktualisiert) -> behalten
- aktuelle Plaene/Specs: `docs/superpowers/`

**Loesch-/Quarantaene-Kandidaten:**
- lokale Caches/Artefakte: `.tmp/` (~28 GB), `tmp/`, `__pycache__/`, `.pytest_cache/`, `.ruff_cache/`, `.coverage`, `coverage.xml`, `package-lock.json`, `push_progress.log`, `training_export/`
- lokale Root-Modellkopien: `sam2.1_l.pt`, alle Root-`yolo*.pt`/`yolov8*.pt`, `models/`, `sam3.1/`
- lokale Review-Ausgaben: `ManualTrainingCombined_20260525/`, `ManualTrainingReview*_20260525/`, `Wasserstand*Review*/`, `WeakClassReview*_20260525/`
- Legacy PowerShell-App (getrackt): `HaltungsAuswertung*.ps1`, `HaltungenTool.ps1`, `AuswertungTool.ps1`, `Services/*.ps1`, Hilfsskripte, `vsa_rili_rules_kanaele.json`
- alte Hilfsskripte: `_check_nutzungsart.ps1`, `analyze_pdf.ps1`, `pdf_auswertung.ps1`, `ReadPdfHeader.ps1`, `TestPdfRead.ps1`, `quick_pdf_read.csx`, `VsaRiliZustand.ps1`
- alte Doku-Duplikate: `ARCHITECTURE.md`, `DATEIEN_MANIFEST.md`, `LIEFERUEBERSICHT.md`, `README_v2.md`, `RELEASE_NOTES_v2.1.0.md`, `CODE_AUDIT_REPORT.md`, `START.md`, alte `AUDIT_*.md` (ge-ignore-t)
- Sidecar-Altmodelle (nicht in `active.json`, nicht geladen): `sidecar/models/florence-2/`, `florence-2-ft/`, `RealESRGAN_x4plus.pth`, `sam2/`
- grosse lokale Daten: `Rohdaten/`, `Knowledge/`, `runs/`, `yolo_cls_runs/`, `xtf_korrektur/`
- Orphan-Tools (~22), die nicht in `AuswertungPro.sln` sind, nach separatem Review

---

### Task 1: Cleanup-Inventar erzeugen
- [x] `docs/cleanup/` anlegen, Top-Level-Groessen als CSV erfassen, getrackte Legacy-Kandidaten listen, `inventory.md` schreiben, committen.

### Task 2: Lokale Caches und Testartefakte quarantainen
- [x] Quarantaene `C:\tmp\SewerStudioCleanupQuarantine\<stamp>\` anlegen, Pfad in `docs/cleanup/last-quarantine-path.txt`.
- [x] `.tmp`, `tmp`, `__pycache__`, `push_progress.log`, `.pytest_cache`, `.ruff_cache`, `.coverage`, `coverage.xml`, `package-lock.json`, `training_export` per `Move-Item` quarantainen (alle ge-ignore-t/lokal, Workspace-Pfad-Check).
- [x] Build-Schnellvalidierung.

### Task 3: Root-Modellkopien bereinigen
- [x] Aktive Sidecar-Modelle vorher pruefen (`yolo26m`, `grounding_dino_swinb`, `sam2.1`, `active.json`).
- [x] Nur Root-Ebene: alle `*.pt` + `models/` + `sam3.1/` nach `<quarantaene>\root-model-files\`. Niemals `sidecar/`.
- [ ] Sidecar-Health/Warmup validieren (offen, braucht laufenden Sidecar):
  `sidecar\.venv\Scripts\python.exe -m pytest sidecar\tests\test_model_backend_selection.py sidecar\tests\test_warmup.py -q`

### Task 4: Legacy-PowerShell-App entfernen
- [x] Referenzen pruefen (kein aktiver .NET-Code haengt daran; Root-`Services/` != `src/.../UI/Services/`).
- [x] `git rm` (getrackt): HaltungsAuswertung.ps1, HaltungsAuswertungPro.ps1, HaltungsAuswertungPro_v2.ps1, **HaltungenTool.ps1**, AuswertungTool.ps1, export_haltungen.ps1, _check_nutzungsart.ps1, analyze_pdf.ps1, pdf_auswertung.ps1, ReadPdfHeader.ps1, TestPdfRead.ps1, quick_pdf_read.csx, VsaRiliZustand.ps1, **vsa_rili_rules_kanaele.json**; `git rm -r Services`.
- [x] README NICHT umbauen (beschrieb schon den .NET-Startweg). Build + Commit.

### Task 5: Alte Root-Doku-Duplikate entfernen
- [x] `git rm` README_v2.md, START.md, ARCHITECTURE.md, DATEIEN_MANIFEST.md, LIEFERUEBERSICHT.md, RELEASE_NOTES_v2.1.0.md, CODE_AUDIT_REPORT.md (AUDIT_*.md ist ge-ignore-t, nicht getrackt).
- [x] `git mv KOSTENBERECHNUNG.md docs/kostenberechnung.md` (Datei war bereits sauberes UTF-8 ohne BOM/Mojibake -> kein Inhaltsumbau).
- [x] `docs/README.md` als Doku-Index. Build + Commit.

### Task 6: Review-/Trainingsausgaben aus dem Root entfernen
- [ ] Eval-Abhaengigkeiten sichtbar machen (eval-set-warden vor jeder Aktion an Eval-Daten).
- [ ] Nicht-Eval-Review-Ausgaben quarantainen: `ManualTraining*_20260525/`, `Wasserstand*Review*/`, `WeakClassReview*_20260525/`.
- [ ] `EvalVisibilityReview_20260525/` (0,1 GB, von Trainings-Skripten referenziert): im Workspace behalten ODER nach `C:\KI_BRAIN\evalsets\...` + Defaults patchen. Entscheidung offen.

### Task 7: Tools ordnen und Orphan-Tools entscheiden
- [ ] `dotnet sln list` vs. getrackte Tools; `docs/tools/tool-inventory.md` kategorisieren.
- [ ] Tool-Output-Unterordner (bin/obj/output*) quarantainen; `tools/__pycache__` loeschen.
- [ ] Orphan-Tools (~22) einzeln entscheiden (rg-Referenz + Doku-Nutzung), je 1 Commit. Kein Tool blind loeschen.

### Task 8: Sidecar-Modell-Fallbacks bewusst entscheiden
- [ ] `sidecar/models/sam2/` (alt, nicht 2.1) quarantainen; `sam2.1/` bleibt.
- [ ] `florence-2/`, `florence-2-ft/`, `RealESRGAN_x4plus.pth` quarantainen (nicht in `active.json`, nicht geladen). RealESRGAN: KI-SR ist fuer Befunde/Training verboten (forensische Treue).
- [ ] `grounding_dino_1.5/` als Fallback BEHALTEN (operatives Risiko, nicht still entfernen).
- [ ] Test: `test_model_backend_selection.py`, `test_honesty.py`.

### Task 9: Lokale ignored Daten auslagern
- [ ] `Rohdaten/`, `Knowledge/`, `runs/`, `yolo_cls_runs/`, `xtf_korrektur/` (alle ~0 GB, faktisch leer -> Ordnung, kein Platzgewinn) nach `C:\KI_BRAIN\SewerStudio_LocalArchive_20260621\`.
- [ ] `docs/README.md` um lokale Datenablage ergaenzen.

### Task 10: Finalvalidierung und endgueltiges Loeschen der Quarantaene
- [ ] Build + .NET-Tests + Sidecar-Pytest.
- [ ] Root nach grossen Resten scannen.
- [ ] Quarantaene erst nach einem vollen, erfolgreichen App-Lauf + User-Bestaetigung loeschen (Pfad-Guard auf `C:\tmp\SewerStudioCleanupQuarantine`).

---

## Pruefungsergebnis und Ergaenzungen (verifiziert 2026-06-21)

Belegt durch `git ls-files`, `Get-ChildItem`, `dotnet sln list`, `.gitignore`-Inhalt, Ordnergroessen.

- **A. gitignore deckt fast alle Kandidaten schon ab** (`.tmp`, `*.pt`, `/models/`, `/sam3.1/`, Review-Ordner, `Rohdaten/`, `_legacy/`, `runs/`, `Knowledge/`, `skills.pdf`, `CODEX_SKILLS.md`, `yolo_cls_runs/`, `xtf_korrektur/`). -> Diese sind nur lokal; `git rm` greift nicht, nur Move/Delete. Echte `git rm`-Arbeit nur in Task 4/5.
- **B. Groessen-Realitaet:** `.tmp` 28,8 GB (~85 % des Gewinns) | `models/` 3,3 GB | `sam2.1_l.pt` 428 MB | Root-`yolo*.pt` ~1 GB | `ManualTraining*` ~1,8 GB | `EvalVisibilityReview` nur 0,1 GB | Task-9-Dirs ~0 GB (leer). sidecar 17,7 GB = behalten.
- **C. Task 4 Liste korrigiert:** 13 getrackte Root-Skripte (inkl. `HaltungenTool.ps1`, das im Original fehlte) + `Services/` + `vsa_rili_rules_kanaele.json`. Lokale Kopien liegen zusaetzlich in `_legacy/` (ge-ignore-t) = Backup.
- **D. Task 5:** `AUDIT_2026-05-25.md` ge-ignore-t (nicht getrackt). `KI_CODIERMODUS/VIDEO_TRAINING_ANLEITUNG.md` heute geaendert -> behalten. `KOSTENBERECHNUNG.md` beschreibt aktuelle .Next-Kostenklassen -> behalten/verschieben.
- **E. Neu gefunden:** `tmp/` (nicht ge-ignore-t, nicht getrackt), `vsa_rili_rules_kanaele.json` (getrackt, Legacy), `Export_Vorlage/` (getrackt, aktiv genutzt -> behalten), `push_progress.log` (ge-ignore-t), Root `_legacy/` (Backup der Legacy-Skripte).
- **F. sidecar/models extra:** `florence-2`, `florence-2-ft`, `RealESRGAN_x4plus.pth`, `candidates`, `yolo26l-seg` (zusaetzlich zum erwarteten Satz). Geprueft: Florence/RealESRGAN nicht in `active.json`, nicht geladen -> Quarantaene-Kandidaten.
- **G. Tools:** real ~54 Eintraege; ~22 Orphans im Original nicht klassifiziert (Task 7).
- **H. Eval:** eval-set-warden vor jeder Eval-Aktion (Hash/Freeze/Kontamination).

## Ausfuehrungsstatus (2026-06-21)

**Erledigt:**
- Disk-Cleanup ~33,5 GB in Quarantaene `C:\tmp\SewerStudioCleanupQuarantine\20260621-103905`
  (`.tmp` 28,8 GB, `tmp`, Caches, `models/`, Root-`*.pt`, `sam3.1/`). Reversibel.
- Commits auf `feature/gis-karte` (aufgesetzt auf Audit-Fix-Stand `caa19379`):
  - `a418bb2c` docs: add project cleanup inventory (Task 1)
  - `39d03a76` chore: remove legacy PowerShell prototype app (Task 4, 30 Dateien)
  - `05a86c9b` docs: remove obsolete root documentation (Task 5)
- Build nach jedem Block gruen (0 Fehler).

**Hinweis:** Die ersten Versuche (Commits `1dca0295`/`b50e00be`, dann Cherry-Picks `031ffbbf`/`4864d35b`)
wurden zweimal von einer parallelen Audit-Fix-Session per `reset` weggeworfen. Erst nachdem diese
fertig war (`caa19379`), hielten die finalen Cherry-Picks. Lehre: nur eine Agent-Session pro Branch.

**Offen:** Task 3 Warmup-Test, Tasks 6-10. Quarantaene-Final-Loeschung erst nach vollem App-Lauf.

## Self-Review

**Spec coverage:** lokale Artefakte, Modellkopien, Review-Ausgaben, Legacy-PowerShell, alte Doku, Tool-Leichen, Sidecar-Fallbacks, lokale Daten, Abschlussvalidierung.

**Open decisions:** `EvalVisibilityReview_20260525/` behalten vs. umziehen; Orphan-Tools (Task 7); Florence-2/RealESRGAN endgueltig (Task 8).
