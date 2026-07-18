# Skill-Konsolidierung — Implementierungsplan (2026-07-18, Rev. 2)

> **Umsetzung:** Schritt fuer Schritt mit Zwischenkontrolle. Die Schritte nutzen Checkbox-Syntax (`- [ ]`). *(Optional koennen Plan-Ausfuehrungs-Skills wie `superpowers:executing-plans` helfen, falls in der Sitzung verfuegbar — nicht erforderlich.)*

**Ziel:** Die Skill-Sammlung von **28 Ordnern auf genau 12 klare Skills** verkleinern, alle veralteten Fakten korrigieren und gegen kuenftiges Veralten absichern — durch **einen zentralen Faktenindex**, **eine ausfuehrliche Codebase-Karte** und **einen automatischen Skill-Linter**.

**Architektur:** Fakten stehen kuenftig genau einmal als code-gepruefter Index (`docs/SYSTEM-FAKTEN.md`); die ausfuehrliche Architektur in `docs/CODEBASE-KARTE.md`. Skills verweisen **nur** darauf, statt Werte zu kopieren. Ein Linter (`tools/skill-linter/`) prueft alle Skills gegen bekannte Fehlermuster und bricht bei kaputtem/unbekanntem Format mit "Pruefung nicht moeglich" ab — niemals faelschlich "sauber".

**Betroffene Ablagen:** `C:\Users\Besitzer\.claude\skills\` (Claude, 28 Ordner + `ehrliche-meinung.md`) und gespiegelt `C:\Users\Besitzer\.codex\skills\` (Codex, 31 Ordner; zusaetzlich `pdf`, `playwright`, `.system`). `docs/` und `tools/` liegen im Repo `c:\Sewer-Studio_KI_4.5`.

## Globale Randbedingungen

- Sprache Deutsch. Skill-Dateien **UTF-8**, echte Umlaute, einheitlich (keine `Ã¤`/`â€“`-Mojibake).
- Keine neuen Fach-Skills. Nur verkleinern, korrigieren, absichern. (Ausnahme: `sewer-codequalitaet` ersetzt zwei bestehende Skills, ist also additiv-neutral.)
- **Backup ungleich Commit.** `.claude`/`.codex` sind **kein** Git-Projekt — dort keine "Commits", sondern **datierte Kopie + SHA-256-Dateiliste**. Nur Repo-Dateien (`docs/`, `tools/`, `CLAUDE.md`, `AGENTS.md`) werden per Git committet.
- **Archiv liegt neben, nicht im Skill-Ordner.** Entfernte Skills wandern nach `C:\Users\Besitzer\.claude\skills-archiv\2026-07-18\` bzw. `C:\Users\Besitzer\.codex\skills-archiv\2026-07-18\` — nie in einen Unterordner von `skills\`, sonst finden Claude/Codex/Linter sie weiter.
- **Skills verweisen nur, kopieren nicht.** In allen Merge-/Fix-Aufgaben gilt: Werte aus `SYSTEM-FAKTEN.md`/`CODEBASE-KARTE.md` **verlinken**, nicht duplizieren.
- Die drei Waechter bleiben gesperrt (STOP-Kopf), bis ihre Verschlankung abgeschlossen, getestet und in einer **neuen Sitzung** freigegeben ist. Ein gruener Linter allein reicht dafuer nicht.
- **Code und Tests sind massgebend.** `SYSTEM-FAKTEN.md` ist ein code-gepruefter Index, keine eigenstaendige Wahrheit.

---

## Teil A — Befund (sauber getrennt)

### A1 — Echte Fehler (heute nachweislich falsch)

| Skill | Fehler | Richtig |
|---|---|---|
| quality-gate-keeper | liest `C:/Sewer-StudioKI_3.1/.../benchmark_metrics.json` + gleiche 3.1-`KnowledgeBase.db` | `benchmark_metrics.json` existiert nirgends; DB unter aufgeloestem Wissensordner |
| model-promotion-warden | flaches `active.json` (`active_model`, `yolo_v*.pt`, `.meta.json`) unter Root 4.0 / `models/yolo26m/`; Endpunkt `/model/reload` | verschachteltes `classifier{}`-`active.json` unter `sidecar\models\`; kein Reload-Endpunkt (Neustart noetig) |
| eval-set-warden | erwartet `hashes` als flache Hex-Strings → Kontaminations-Check laeuft ueber leere Menge → meldet immer "sauber" | reales `hashes` = Dict `{key:{sha256,size_bytes}}` |
| active-learning-curator | `DB="C:/Sewer-StudioKI_3.1/.../KnowledgeBase.db"` | unter aufgeloestem Wissensordner |
| sqlite-kb-inspector | `vsa_codes.json` unter Root 4.1 | `vsa_kek_2020_catalog_manifest.json` unter `src\...\UI\Data\` |
| fastapi-sidecar-tester | `/predict/yolo`, `/process/video`; SAM 3; DINO 1.5; Feld `boxes`; 3.1-Pfad | `/detect/yolo`, `/segment/sam`; SAM 2.1; DINO Swin-B; Feld `bounding_boxes` |
| sewer-pipeline-auditor | ByteTrack + 8B→32B-Eskalation + `UpdateActive` als Ist | kein Tracking; `TemporalFindingDeduplicator` + `TemporalCodeVotingService` |
| ollama-model-manager | 8B→32B-Eskalations-Workflow, Modell `qwen3-vl:32b` | nur `qwen3-vl:8b-q8` / `:2b`, keine Auto-Eskalation |
| vram-monitor | VRAM-Tabelle mit 32B-Eskalationszeile | kein 32B-Modell |
| ai-model-engineer | "Eskalation 8B→32B"; "YOLO26m-seg" | keine Eskalation; `yolo26m.pt` (Detect) |
| msbuild-error-parser | Build-/Clean-Befehle auf `C:/Sewer-StudioKI_3.1/...` | Befehle stehen bereits in `AGENTS.md` |
| sewer-testing | alle Test-Beispiele nutzen geloeschte Klassen | Klassen existieren nicht → neu aus heutigen Tests |
| sewer-architektur (Claude) | `CreateFewShotStore()` / `FewShotExampleStore` | entfernt (Test prueft, dass die Datei fehlt) |
| sewer-pdf-formate | `PdfProtocolTableParser` + `PdfToTextExePath` | `PdfParser`/`PdfProtocolExtractor`/`PrimaryDamageRowParser`; `DiagnosticsOptions.ExplicitPdfToTextPath` |
| ki-inspektionstechnik | `DetectionAggregator` | existiert nicht |
| ki-kanalinspektion | "Qwen2.5-VL-32B" als aktiv | `qwen3-vl:8b-q8` |
| sewer-fachwissen | `BBD` als Basiscode | kein Basiscode BBD; `BBD_boden`→`BBDZ` |

### A2 — Geplant, aber nie gebaut (keine Fehler — fehlende Infrastruktur)

- `benchmark_metrics.json` + `baselines/`: kein Erzeuger im Code.
- YOLO-**Detect**-Promotion-Kette (`yolo_v*.pt`): real gibt es nur die **cls**-Kette (`active.json`→`classifier`).
- Automatische 8B→32B-Laufzeit-Eskalation: laut CLAUDE.md nicht implementiert.

**Konsequenz:** Von den drei Waechtern bleiben als eigenstaendige Skills nur **eval-set-warden** und **model-promotion-warden** (verschlankt). Die reine **Betriebsbereitschafts-Pruefung** aus dem quality-gate-keeper wandert in `sewer-ai-runtime`; der Benchmark-/Regressionsteil entfaellt, bis die Infrastruktur existiert.

### A3 — Offene Architektur-Entscheidungen (nicht in diesem Plan geloest)

1. **Doppelpflege Claude/Codex.** Zielbild: Architektur einmal in `docs/CODEBASE-KARTE.md`; je ein duenner Claude-/Codex-Skill verweist nur. Dieser Plan baut die Karte und die duenne Claude-Seite; die Codex-Angleichung ist Aufgabe 10.
2. **Ob die Benchmark-/Promotion-Infrastruktur gebaut wird.** Solange nein, bleiben die Waechter bewusst klein und teils nur lesend.

### A4 — Ist-Stand in Zahlen

- **28 Skill-Ordner** unter `.claude\skills\` + lose `ehrliche-meinung.md` + 3 Steuer-Dateien.
- Audit-Ampel: **12 ROT, 7 GELB, 9 GRUEN**.
- Codex-Spiegel: 31 Ordner (zusaetzlich `pdf`, `playwright`, `ehrliche-meinung`-Ordner) + `.system`.

---

## Teil B — Ziel-Skill-Landkarte (28 → genau 12)

**Die 12 aktiven Skills am Ende:**

1. `einfach-erklaeren` — ← `sewer-explain` (Sprachregeln „immer Deutsch/einfach" nach `CLAUDE.md`)
2. `sewer-architektur` — duenn, verweist auf `CODEBASE-KARTE.md`; `FewShotExampleStore` raus
3. `sewer-fachwissen` — ← `ki-inspektionstechnik`; `ki-kanalinspektion` als Referenzabschnitt
4. `sewer-wpf` — ← `sewer-wpf-ui` + `xaml-binding-checker`
5. `sewer-ai-runtime` — ← `ollama-model-manager` + `vram-monitor` + `fastapi-sidecar-tester` + **Betriebspruefung aus quality-gate-keeper**
6. `sewer-pipeline-auditor` — **getrennt** gefixt (unabhaengige Pruefung der KI-Kette ≠ Betrieb)
7. `sewer-kb` — ← `sqlite-kb-inspector` + `active-learning-curator`, **neu aus dem echten DB-Schema** gebaut (nicht Alttext kopieren)
8. `sewer-pdf-formate` — echte Parser-Klassennamen
9. `ai-overlay-visualizer` — sauber, nur Faktenverweis
10. `sewer-codequalitaet` — **NEU**; ersetzt `sewer-testing` + `msbuild-error-parser`
11. `eval-set-warden` — verschlankt auf echten `_manifest.json`-Stand
12. `model-promotion-warden` — verschlankt, **zunaechst nur lesend**

**Ins Archiv (`skills-archiv\2026-07-18\`):** `project-architect`, `ai-model-engineer`, `ai-deployment-packager`, `msbuild-error-parser`, `sewer-testing` (alt), `sewer-quality-gate-keeper` (Betriebsteil geht in `sewer-ai-runtime`), sowie die in Merges aufgegangenen Ordner (`sewer-explain`, `ki-inspektionstechnik`, `ki-kanalinspektion`, `sqlite-kb-inspector`, `active-learning-curator`, `ollama-model-manager`, `vram-monitor`, `fastapi-sidecar-tester`, `sewer-wpf-ui`, `xaml-binding-checker`).

**Nach `CLAUDE.md` (Regeln):** `deutsch-only`, `lokalzeit`, `selbst-pruefung`.
**Nach `docs\` (Roadmap):** `ki-codier-vision`.
**Meta:** `ehrliche-meinung.md`-Prinzip nach `CLAUDE.md` heben oder als einzigen Meta-Skill behalten.

> **Der neue Skill `sewer-codequalitaet`** haelt SewerStudio dauerhaft sauber, verstaendlich, testbar. Aufgabenteilung: `sewer-architektur` sagt, **wo** etwas hingehoert; `sewer-codequalitaet` prueft, **ob** sauber umgesetzt wurde; `sewer-pipeline-auditor` prueft speziell die KI-Kette. Kontrolliert: zu grosse Klassen/Fenster, Fachlogik im WPF-Code, Doppel-Dienste, falsche Schicht-Abhaengigkeiten, Datei-/Prozess-/HTTP-Aufrufe in der UI, toter Code, unklare Namen/lange Methoden, Fehlerbehandlung/Abbruch, Erhalt von Schnittstellen/Dateiformaten, Verhaltenstests vor riskanten Umbauten, Build+Tests nach jeder Aenderung, Aktualisierung der Architekturkarte. Fester Ablauf: (1) echten Code untersuchen → (2) Probleme nach Risiko sortieren → (3) Riskantes zuerst mit Test schuetzen → (4) nur kleinen Bereich umbauen → (5) Build+Tests → (6) Architekturkarte aktualisieren.

---

## Teil C — Faktenindex (code-geprueft, verbuergter Ist-Stand)

Kern von `SYSTEM-FAKTEN.md`. **Code/Tests bleiben massgebend** — dies ist der gepruefte Index dazu:

- **Projekt-Root:** `c:\Sewer-Studio_KI_4.5` (WPF/.NET 10, UI-TFM `net10.0-windows10.0.19041`).
- **Wissensordner (KnowledgeRoot):** feste Aufloesungsreihenfolge (Beleg `KnowledgeBasePathService.cs:175 ff.`): (1) Umgebungsvariable `SEWERSTUDIO_KNOWLEDGE_ROOT` → (2) gespeicherte Einstellung (`ConfigureSettingsRoot`) → (3) Default `%LOCALAPPDATA%\SewerStudio\Knowledge`. `%APPDATA%\AuswertungPro\KiVideoanalyse` ist **nur** alter Migrationspfad. Auf dieser Maschine aktuell → `C:\KI_BRAIN`.
- **KnowledgeBase.db:** immer `<KnowledgeRoot>\KnowledgeBase.db` (aktuell `C:\KI_BRAIN\KnowledgeBase.db`).
- **Eval-Set:** `<KnowledgeRoot>\eval_set\` (`_manifest.json`, `_candidates.json`, `images\`, `labels\`; `frozen=true`, approved/exported=120). Hashes: `hash_algorithm` top-level; `hashes` = Dict `{key:{sha256,size_bytes}}`.
- **VSA-Katalog:** `src\AuswertungPro.Next.UI\Data\vsa_kek_2020_catalog_manifest.json` (kein `vsa_codes.json`).
- **YOLO Detect-Strecke:** `yolo26m.pt` (COCO-Fallback `yolo11m.pt`). Gehoert **nicht** zur Klassifikation.
- **Klassifikator (cls):** getrennte Gewichte, aufgeloest ueber `sidecar\models\active.json` → `classifier.weights_path` (Reihenfolge: active.json → `settings.yolo_cls_model_path` → Legacy; SHA-256 gegen Datei geprueft, Mismatch = cls bleibt AUS; Beleg `yolo_wrapper.py:482 ff.`). cls-Laeufe `C:\KI_BRAIN\yolo_cls_runs\`, Kandidaten `model_candidates\`.
- **active.json / Laden:** cls laedt beim **Warmup oder erster Anfrage**, bleibt danach gespeichert. Kein Hot-Reload-Endpunkt → `active.json`-Aenderung wirkt normalerweise erst nach **Sidecar-Neustart**.
- **Sidecar:** `sidecar\sidecar\`, Port **8100**. Routen: `/health`, `/warmup`, `/detect/yolo`, `/classify/yolo`, `/detect/dino`, `/segment/sam`, `/training/export-yolo`. **Nicht** vorhanden: `/predict/*`, `/model/reload`, `/enhance`, `/process/video`. SAM-Feld: `bounding_boxes`.
- **Weitere Modelle:** DINO Swin-B (`grounding_dino_swinb`), Fallback `grounding_dino_1.5`. SAM **2.1** (SAM 3 default aus; vit_h entfernt). Qwen `qwen3-vl:8b-q8` (>=24 GB) / `qwen3-vl:2b`; **nie** qwen2.5; **keine** 8B→32B-Auto-Eskalation. Embeddings `nomic-embed-text`. Ollama-Port 11434.
- **Pipeline:** Dedup C#-framebasiert (`TemporalFindingDeduplicator` + `TemporalCodeVotingService`). Kein ByteTrack, `DetectionAggregator`, `InferenceOrchestratorService`, `KbDeduplicationService`, `YoloDatasetExportService`, `FewShotExampleStore`.
- **PDF-Parser:** `PdfParser`, `PdfProtocolExtractor`, `PrimaryDamageRowParser`; pdftotext-Pfad `DiagnosticsOptions.ExplicitPdfToTextPath`.
- **Build/Test:** `dotnet build AuswertungPro.sln` / `dotnet test AuswertungPro.sln` (Befehle in `AGENTS.md`). Dev: `AuswertungPro.Dev.slnf`. Testprojekte: `.Infrastructure.Tests`, `.Pipeline.Tests`, `.UI.Tests`, `ProjectModernizer.Tests`.
- **VSA-Codes:** BCD=Rohranfang, BCE=Rohrende, BCA=seitl. Anschluss, BCC=Bogen; BAA=Verformung, BAB=Riss, BAC=Bruch, BAF=Oberflaechenschaden, BAJ=verschobene Rohrverbindung; BBA=Wurzeln, BBB=anhaftende Stoffe, BBC=Ablagerung, BBD*=eindringender Boden (`BBD_boden`→`BBDZ`).

---

## Teil D — Aufgaben

Reihenfolge: Sicherung → Faktenindex → Codebase-Karte → Linter → risikoarme Merges → weitere Merges/Fixes → codequalitaet → Waechter → Ausduennen → Codex.

### Aufgabe 0: Sicherung (kein Git in .claude/.codex)

- [ ] **Schritt 1:** Kopiere `C:\Users\Besitzer\.claude\skills\` nach `C:\Users\Besitzer\.claude\skills-backup-2026-07-18\`.
- [ ] **Schritt 2:** Erzeuge eine **SHA-256-Dateiliste** der Sicherung (z. B. `Get-ChildItem -Recurse | Get-FileHash -Algorithm SHA256`) und lege sie als `_sha256.txt` in die Sicherung.
- [ ] **Schritt 3:** Pruefe: 28 Ordner + `ehrliche-meinung.md` vorhanden. (Codex-Sicherung erst in Aufgabe 10, direkt vor den Codex-Aenderungen.)

### Aufgabe 1: Faktenindex `docs/SYSTEM-FAKTEN.md`

- [ ] **Schritt 1:** Teil C in `SYSTEM-FAKTEN.md` uebertragen. Kopf: „Code-gepruefter Faktenindex — Code und Tests bleiben massgebend. Aendert sich ein Wert, hier aendern; Skills verweisen nur."
- [ ] **Schritt 2:** Negativliste „Nicht mehr existent" (aus A1/A2) als Linter-Grundlage.
- [ ] **Schritt 3 (Verifikation):** Jeden Pfad/Namen per Grep/Dateisystem belegen — kein Wert ohne Beleg.
- [ ] **Schritt 4:** Repo-Commit.

### Aufgabe 2: Ausfuehrliche `docs/CODEBASE-KARTE.md`

Muss VOR dem Verkleinern der Architektur-Skills stehen, sonst gehen Regeln verloren.

- [ ] **Schritt 1:** Aus der ausfuehrlichen **Codex-**`sewer-architektur` (30 KB) + CLAUDE.md die vollstaendige Codebase-Karte nach `docs/CODEBASE-KARTE.md` ueberfuehren (Schichten, DI/ServiceProvider, Import-/Export-Vertraege, Merge-Semantik, Domaenenmodell, Test-Guards).
- [ ] **Schritt 2:** Gegen den echten Code gegenpruefen; entfernte Klassen (FewShotExampleStore, DetectionAggregator, …) raus.
- [ ] **Schritt 3:** Repo-Commit.

### Aufgabe 3: Skill-Linter (TDD) — `tools/skill-linter/`

**Schnittstelle:** `python skill_lint.py <skill-root>` → **Exit 0** sauberer Ordner, **Exit 1** Altbegriffe/Funde, **Exit 2** „Pruefung nicht moeglich" (kaputtes/unbekanntes Format). Exit 2 hat immer Vorrang.

- [ ] **Schritt 1: Failing Tests.** Fixtures: (a) sauber → Exit 0; (b) enthaelt `C:/Sewer-StudioKI_3.1` + `qwen3-vl:32b` affirmativ → Exit 1; (c) ohne/kaputtes Frontmatter → Exit 2; (d) Zeile „Qwen 2.5 niemals verwenden" → **Exit 0** (Negation/Meta, kein Fehler); (e) explizite Notiz „`benchmark_metrics.json` existiert derzeit nicht" → **Exit 0**. Zusaetzlich CLI-Test, der **alle drei** Rueckgabewerte real prueft.
- [ ] **Schritt 2:** Tests ausfuehren, muessen fehlschlagen.
- [ ] **Schritt 3: Implementierung.**
  - `forbidden.json`: Pfad-Muster (`Sewer-StudioKI_3.1`, `Sewer-Studio_KI_4.0`, `4.1`), tote Namen (`qwen3-vl:32b`, `qwen2.5`, `FewShotExampleStore`, `DetectionAggregator`, `YoloDatasetExportService`, `BenchmarkMetricsStore`, `PdfProtocolTableParser`, `benchmark_metrics.json`), falsche Routen (`/predict/`, `/model/reload`, `/enhance`, `/process/video`), veraltete Modelle (`SAM 3`, `vit_h`, `grounding-dino-1.5` als Standard), Mojibake (`Ã¤|Ã¶|Ã¼|â€“`).
  - **Kontext-/Negations-Regel:** Ein Treffer wird NICHT als Fehler gewertet, wenn die Zeile eine Negations-/Meta-Markierung enthaelt (`niemals`, `nicht (mehr)`, `kein`, `veraltet`, `entfernt`, `existiert nicht`, `DEAKTIVIERT`) oder ein `<!-- lint-ok: grund -->`-Marker gesetzt ist. So faellt `benchmark_metrics.json` als reine Verbots-Nennung NICHT auf — Widerspruch zu den Waechter-Notizen aufgeloest.
  - **Ignorieren:** Ordner `*-archiv*`, `_archiv`, `.system` nicht scannen.
  - Frontmatter validieren (`name`+`description`, YAML parsbar) — sonst Exit 2.
- [ ] **Schritt 4:** Tests gruen.
- [ ] **Schritt 5:** Zusaetzlich den vorhandenen **Standard-Skill-Validator** (Frontmatter/Struktur nach agentskills.io) ueber die Ablage laufen lassen.
- [ ] **Schritt 6:** Linter ueber die echte Ablage; Befund sichern. Repo-Commit.
- [ ] **Hinweis:** Ein Text-Linter findet nur bekannte Altfehler. Die zentralen Fakten (Routen, Klassen) brauchen als **Folgeschritt** zusaetzliche Pruefungen gegen echten Code — hier noch nicht enthalten.

### Aufgabe 4: Merges Sprache & WPF

- [ ] `sewer-explain` in `einfach-erklaeren` einarbeiten (nur Verweis auf Fakten, kein Kopieren).
- [ ] In `CLAUDE.md` Abschnitt „Arbeitsregeln": immer Deutsch, immer einfach, Zeitzone Europe/Zurich, Selbstpruefung vor finalen technischen Aussagen (ersetzt `deutsch-only`, `lokalzeit`, `selbst-pruefung`).
- [ ] `sewer-wpf` neu = verifizierter `sewer-wpf-ui`-Inhalt + Binding-Pruefregeln aus `xaml-binding-checker`.
- [ ] Alt-Ordner nach `skills-archiv\2026-07-18\`. Linter gruen. Skill-Sicherung aktualisieren (Kopie + SHA-256).

### Aufgabe 5: Merge `sewer-ai-runtime`

- [ ] Neuer Skill: Ollama-Verwaltung, VRAM-Monitoring, Sidecar-Health/Routen (nur Verweis auf Fakten) **plus** Betriebsbereitschafts-Pruefung aus quality-gate-keeper.
- [ ] Regel im Skill: Diese Pruefung meldet nur **„betriebsbereit"** / „nicht betriebsbereit" — **niemals „Modellqualitaet freigegeben"** (kein Benchmark vorhanden).
- [ ] Kein 32B/Eskalation, kein ByteTrack/`UpdateActive`, echte Routen, SAM 2.1, DINO Swin-B.
- [ ] `ollama-model-manager`, `vram-monitor`, `fastapi-sidecar-tester` → Archiv. Linter gruen.

### Aufgabe 6: `sewer-pipeline-auditor` (getrennt) fixen

- [ ] ByteTrack/Tracking, 8B→32B, `UpdateActive` entfernen; auf `TemporalFindingDeduplicator`/`TemporalCodeVotingService` und echte Sidecar-Routen umstellen (Verweis auf Fakten). Bleibt eigenstaendig. Linter gruen.

### Aufgabe 7: `sewer-fachwissen` (+ Inspektionstechnik + Kanalinspektion)

- [ ] `ki-inspektionstechnik` einarbeiten; `ki-kanalinspektion` als Referenzabschnitt.
- [ ] BBD-Basiscode raus (`BBD_boden`→`BBDZ`); BAB-Char2 an EN 13508-2/CLAUDE.md angleichen, Divergenz einmalig klarstellen. Alt-Ordner → Archiv. Linter gruen.

### Aufgabe 8: `sewer-kb` (neu aus echtem DB-Schema)

- [ ] **Schritt 1:** Reales Schema von `<KnowledgeRoot>\KnowledgeBase.db` erheben (Tabellen/Spalten via `sqlite3 .schema`).
- [ ] **Schritt 2:** `sewer-kb` **frisch** aus diesem Schema schreiben (Inspektion + Kuratierung), DB-Pfad ueber Wissensordner-Aufloesung, Katalog `vsa_kek_2020_catalog_manifest.json`. Keine Alttexte kopieren.
- [ ] `sqlite-kb-inspector`, `active-learning-curator` → Archiv. Linter gruen.

### Aufgabe 9: Reine Fixes + `sewer-codequalitaet` bauen

- [ ] `sewer-pdf-formate`: echte Parser-Klassennamen; „3 Formate"→4 (inkl. IBAK direkt); erfundene Regex-Namen als illustrativ kennzeichnen/entfernen.
- [ ] `sewer-architektur`: `FewShotExampleStore` raus; **duenn**, verweist nur auf `CODEBASE-KARTE.md`.
- [ ] `ai-overlay-visualizer`: Faktenverweis ergaenzen.
- [ ] **`sewer-codequalitaet` neu** anlegen:
  ```
  sewer-codequalitaet/
  ├── SKILL.md               (Rolle, Trigger, 6-Schritte-Ablauf, Kontroll-Liste)
  └── references/
      ├── review-checkliste.md   (die Kontrollpunkte)
      └── testmuster.md          (Testregeln aus dem neu erhobenen sewer-testing-Wissen)
  ```
  Trigger u. a.: „Weiter mit sauberem Code", „Codestruktur verbessern", „Klasse aufraeumen", „Pruefe die Architektur", „Ist das wartbar?", „Teile das Fenster sauber auf". Build-/Test-Befehle via Verweis auf `AGENTS.md`.
- [ ] `sewer-testing` (alt) + `msbuild-error-parser` → Archiv. Linter gruen.

### Aufgabe 10: Waechter verschlanken — nicht einfach reaktivieren

- [ ] **Schritt 1:** Die **drei Codex-Waechter** (eval-set-warden, quality-gate-keeper, model-promotion-warden) ebenfalls sperren (STOP-Kopf), da noch aktiv/veraltet. Vorher Codex-Sicherung (Kopie + SHA-256).
- [ ] **Schritt 2 (eval-set-warden):** nur dateibasierte Pruefungen gegen `<KnowledgeRoot>\eval_set\_manifest.json` im echten `hashes`-Format; echter Kontaminations-Check ueber die realen Hashes; tote Klassen/`pathPatterns` und der `benchmark_metrics.json`/`baselines`-Teil raus.
- [ ] **Schritt 3 (Betriebspruefung → ai-runtime):** bereits in Aufgabe 5; quality-gate-keeper-Ordner ins Archiv (Claude) bzw. aufloesen (Codex).
- [ ] **Schritt 4 (model-promotion-warden):** auf reale cls-`active.json` umstellen; Modell-Orte `yolo_cls_runs\`/`model_candidates\`; `/model/reload` streichen (Neustart noetig). **Zunaechst nur lesend.** Schreiben erst nach: festem Kandidatenformat, SHA-Pruefung, atomarer Sicherung, getestetem Rueckweg, ausdruecklicher menschlicher Freigabe.
- [ ] **Schritt 5:** Jeden Waechter mit **sauberen UND absichtlich kaputten** Testdaten pruefen (nicht nur Linter).
- [ ] **Schritt 6:** STOP-Koepfe erst entfernen (reaktivieren) in einer **neuen Claude-/Codex-Sitzung**, nachdem alles geprueft ist.

### Aufgabe 11: Ausduennen & Steuer-Dateien

- [ ] `project-architect`, `ai-model-engineer`, `ai-deployment-packager` → Archiv.
- [ ] `ki-codier-vision` → `docs\KI-CODIER-VISION.md`, Ordner → Archiv.
- [ ] `SKILL_INDEX.md`/`SKILL_GOVERNANCE.md` an den 12er-Bestand anpassen (Zahlen, Merges, entfernte Artefakte). `ehrliche-meinung.md`-Prinzip nach `CLAUDE.md` oder als einzigen Meta-Skill behalten.
- [ ] Linter ueber die gesamte finale Claude-Ablage — gruen. Skill-Sicherung + SHA-256 aktualisieren.

### Aufgabe 12: Codex-Ablage angleichen

- [ ] Codex-Sicherung (Kopie + SHA-256) — falls nicht schon in Aufgabe 10 geschehen.
- [ ] Denselben Endstand auf `.codex\skills\` anwenden (12 Skills + `pdf`, `playwright` bleiben; `.system` unberuehrt).
- [ ] Beide Architektur-Skills duenn auf `CODEBASE-KARTE.md` verweisen lassen. Linter ueber die Codex-Ablage — gruen.

---

## Selbstpruefung (nach Umsetzung)

1. **Abdeckung:** Jeder ROT-Fund aus A1 hat eine Aufgabe (inkl. der ins Archiv/aufgegangenen)?
2. **Zielzahl:** Endbestand **genau 12** aktive Claude-Skills (Liste Teil B)?
3. **Keine Blindgaenger:** Kein aktiver Skill nennt `benchmark_metrics.json` affirmativ, `qwen3-vl:32b`, `/predict/*`, `FewShotExampleStore`, `Sewer-StudioKI_3.1` — vom Linter bestaetigt (Exit 0)?
4. **Waechter:** eval-set-warden meldet nur mit echten Hashes; ai-runtime-Betriebspruefung nur „betriebsbereit"; model-promotion-warden nur lesend bis alle fuenf Schreibbedingungen erfuellt sind; reaktiviert nur in neuer Sitzung?
5. **Trennung Backup/Commit:** Repo-Dateien committet; externe Skills als datierte Kopie + SHA-256?
