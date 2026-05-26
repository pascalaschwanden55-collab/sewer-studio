# Externe Repos — Bewertung & Integrations-Empfehlung

Stand: 2026-05-26
Branch: `claude/import-integrate-dependencies-dOoKW`

Zusammenstellung von 7 GitHub-Projekten mit Pruefung, ob/wie sie sich in
**SewerStudio** (WPF/.NET 8, EN 13508-2, VSA-KEK, lokale Workstation-AI)
einbinden lassen. Keine Code-Aenderungen, keine NuGet-Installs.

Architektur-Leitplanken aus `CLAUDE.md`:

- Thin-AI: C# fuer Geschaeftslogik, LLM nur fuer Textgenerierung
- VRAM-Budget max. 29 GB, niemals alle Modelle gleichzeitig
- Keine grossen Refactorings ohne Diskussion
- Keine NuGet-Pakete ohne Rueckfrage
- Bestehendes Sidecar-Pattern (Ordner `sidecar/`, Python) ist nutzbar
  fuer externe Python-Tools

Empfehlungs-Schema:

- **GRUEN** — Klarer Mehrwert, geringer Eingriff, empfohlen
- **GELB** — Potenzial vorhanden, aber Entscheidung noetig
- **ROT** — Aktuell kein Fit, nur zur Kenntnis

---

## 1. Codex Plugin (OpenAI) — `openai/codex-plugin-cc`

- **Repo:** https://github.com/openai/codex-plugin-cc
- **Verwandt:** https://github.com/openai/codex (Codex CLI selbst)
- **Was:** Plugin um aus Claude Code heraus Codex (OpenAI CLI Coding Agent)
  aufzurufen — Code-Review, delegierte Tasks, `/codex:rescue` Befehle.
- **Sprache/Stack:** Rust (Codex CLI), Plugin-Manifest fuer Claude Code.
- **Bezug zu SewerStudio:** Reines Dev-Werkzeug, kein Laufzeit-Bestandteil
  der WPF-App.
- **Empfehlung:** **GELB**
  - Sinnvoll als zweite Meinung waehrend Entwicklung
    (Codex prueft C#-Diffs neben Claude Code).
  - Setup ausserhalb des Repos (`/plugin` in Claude Code), keine
    Aenderung am .NET-Solution-Code.
  - Kosten: OpenAI API Credits.
- **Naechster Schritt (falls gewollt):** Claude-Code-Konfig erweitern,
  Plugin via `/plugin marketplace add openai/codex-plugin-cc`. Sonst nichts.

## 2. AutoResearch (Karpathy) — `karpathy/autoresearch`

- **Repo:** https://github.com/karpathy/autoresearch
- **Was:** Autonome Research-Loop fuer LLM-Training auf einer Single-GPU
  (nanochat). AI-Agent veraendert Code, trainiert 5 min, vergleicht
  Metrik, iteriert.
- **Sprache/Stack:** Python + PyTorch.
- **Bezug zu SewerStudio:** SewerStudio nutzt **pretrained** Modelle
  (YOLO26m-seg, Qwen2.5-VL-32B Q5, Grounding DINO 1.5, SAM 3). Es wird
  aktuell **kein Training** im Projekt gemacht.
- **Empfehlung:** **ROT**
  - Kein direkter Anwendungsfall.
  - Theoretisch interessant fuer eine spaetere YOLO-Feintuning-Pipeline
    auf eigenen Sewer-Frames, aber das waere ein eigenes Projekt mit
    Annotations-Workflow und Datasets — nicht "mal kurz importieren".
- **Naechster Schritt:** Keiner. Bookmarken falls eigenes
  Sewer-YOLO-Training mal Thema wird.

## 3. OpenSpace (HKUDS) — `HKUDS/OpenSpace`

- **Repo:** https://github.com/HKUDS/OpenSpace
- **Community:** https://open-space.cloud
- **Was:** Self-Evolving Skill Engine fuer AI-Agents. Erfasst, abgeleitete
  und wiederverwendete Skills aus jeder Task-Ausfuehrung
  (FIX/DERIVED/CAPTURED). Funktioniert mit Claude Code, Codex, etc.
- **Sprache/Stack:** Python, integrierbar als Claude-Code-Plugin.
- **Bezug zu SewerStudio:** Dev-Tooling, keine Laufzeit-Komponente fuer
  die WPF-App.
- **Empfehlung:** **GELB**
  - Koennte beim wiederkehrenden Codieren von VSA-KEK-Schadenscodes
    helfen (Skills fuer "Riss klassifizieren", "Severity bewerten" …),
    aber das ist genau die Logik die laut `CLAUDE.md` **deterministisch
    in C#** bleiben soll, nicht in einem Agenten-Skill.
  - Risiko: Wer pflegt die evolvierenden Skills? Reproduzierbarkeit
    fuer normgerechte Reports (EN 13508-2) leidet.
- **Naechster Schritt (falls gewollt):** Nur als Entwickler-Hilfsmittel,
  nicht fuer Inferenz-Pipeline. Strikt vom Geschaeftscode trennen.

## 4. CLI-Anything (HKUDS) — `HKUDS/CLI-Anything`

- **Repo:** https://github.com/HKUDS/CLI-Anything
- **Hub:** https://clianything.cc
- **Was:** Agent-Friendly CLI Registry + Plugin, das fuer beliebige
  GUI-Apps (GIMP, Blender, Inkscape, OBS, LibreOffice …) automatisch
  CLI-Harnesses generiert, damit AI-Agents diese steuern koennen.
- **Sprache/Stack:** Python (`pip install cli-anything-hub`).
- **Bezug zu SewerStudio:** WinCan/Ikas IBSK sind GUI-Tools. Theoretisch
  koennte ein agent-native CLI fuer WinCan helfen, alte Inspektionen
  programmatisch aufzubereiten — aber WinCan ist proprietaer (Closed
  Source) und Lizenz-/Reverse-Engineering-Fragen schreckend.
- **Empfehlung:** **ROT**
  - Hoher Aufwand, unklare Lizenzlage, kein klarer Mehrwert gegenueber
    dem bestehenden Pfad (Video-Dateien + OSD direkt verarbeiten).
- **Naechster Schritt:** Keiner.

## 5. RAG-Anything (HKUDS) — `HKUDS/RAG-Anything`

- **Repo:** https://github.com/HKUDS/RAG-Anything
- **Paper:** https://arxiv.org/abs/2510.12323
- **Was:** Multimodales All-in-One RAG (Text, Diagramme, Tabellen, Formeln)
  auf Basis von LightRAG. Dual-Graph + Cross-Modal Hybrid Retrieval.
- **Sprache/Stack:** Python (`pip install raganything`), nutzt LightRAG.
- **Bezug zu SewerStudio:** **Bester Fit aus der Liste.**
  - Norm-Korpus VSA-KEK (Schweiz 2023, ca. 300+ Seiten PDFs mit
    Tabellen, Schadens-Skizzen) und EN 13508-2 koennten in einen
    RAG-Index ueberfuehrt werden.
  - Anwendung 1: Reviewer-Hilfe beim Codieren — "warum BAB-A statt
    BAB-B?" inkl. Norm-Zitat.
  - Anwendung 2: Im `ReportGenerator` zur Anreicherung des Texts
    (aber **streng als Quellenmaterial**, nicht zur Entscheidung —
    Klassifizierung bleibt `ClassificationService` / Qwen).
- **Empfehlung:** **GELB → GRUEN nach Entscheidung**
  - Passt in das bestehende `sidecar/` Pattern (Python-Service via HTTP).
  - VRAM-Impact gering, wenn ein **separates** kleines Embedding-Modell
    genutzt wird (nicht Qwen-VL-32B). Vorschlag: BGE-M3 oder
    `nomic-embed-text` (CPU/GPU ~500 MB).
  - Klaere vor Integration: welcher Embed-Provider? Auch LightRAG nutzt
    standardmaessig OpenAI — fuer lokal sollte Ollama angebunden werden.
- **Naechster Schritt (falls gewollt):** Separater Branch + Spike:
  1. `sidecar/rag/` mit `raganything` + Ollama-Anbindung.
  2. VSA-KEK PDFs (`vsa_rili_rules_kanaele.json` + Original-PDFs)
     einspeisen.
  3. C#-Wrapper-Service `VsaKekRagService` mit Interface, der
     parallel zum `ClassificationService` lebt.
  4. **Niemals** in `MeasurementService` oder `QualityGateService`
     einklinken (deterministisch laut `CLAUDE.md`).

## 6. Google Workspace CLI — `googleworkspace/cli`

- **Repo:** https://github.com/googleworkspace/cli
- **Was:** Offizielles (aber nicht "officially supported") CLI von Google
  fuer Gmail, Drive, Calendar, Docs, Sheets, Chat, Admin. Dynamisch aus
  Google Discovery Service gebaut, enthaelt AI-Agent-Skills und MCP-Server.
- **Sprache/Stack:** Node.js (`npm install -g @googleworkspace/cli`).
- **Bezug zu SewerStudio:** SewerStudio liefert PDF-Reports an
  Auftraggeber/Gemeinden. Aktuell vermutlich manueller Versand.
- **Empfehlung:** **GELB**
  - Sinnvoll, wenn Reports automatisiert via Gmail verschickt oder
    in Google Drive abgelegt werden sollen.
  - Solo-Entwickler ohne kommerzielles Ziel → vermutlich unnoetiger
    Overhead (Auth-Setup, OAuth-Flows, Token-Storage).
  - Achtung: Reports koennen sensible Infrastruktur-Daten enthalten
    (Schachtkoordinaten, Schaeden) — Cloud-Upload nur mit Auftraggeber-OK.
- **Naechster Schritt (falls gewollt):** Erst Use Case klaeren
  (Versand? Archivierung?). Dann ggf. als optionaler Export-Adapter
  in `Services/` (siehe `ExcelExportService.ps1`-Pattern).

## 7. Claude Peers MCP — `louislva/claude-peers-mcp`

- **Repo:** https://github.com/louislva/claude-peers-mcp
- **Was:** MCP-Server, der mehrere lokal laufende Claude-Code-Instanzen
  miteinander reden laesst. Broker-Daemon (SQLite + HTTP auf
  `localhost:7899`), pro Session ein MCP-Server.
- **Sprache/Stack:** TypeScript/Node.
- **Bezug zu SewerStudio:** Dev-Tooling, keine Laufzeit-Komponente.
- **Empfehlung:** **GELB**
  - Spannend wenn parallele Claude-Sessions getrennte Teile bearbeiten
    (z. B. eine Session am `InferenceOrchestratorService`, eine am
    `ReportGenerator`).
  - Bei Solo-Dev mit einer aktiven Session: Mehrwert begrenzt.
- **Naechster Schritt (falls gewollt):** Installation rein lokal,
  ausserhalb des Repos. Nichts am SewerStudio-Code aendern.

---

## Zusammenfassung & Vorschlag

| # | Repo | Fit | Aktion |
|---|------|-----|--------|
| 1 | Codex Plugin (CC) | GELB | Optionales Claude-Code-Plugin, kein Repo-Change |
| 2 | AutoResearch | ROT | Bookmarken, kein aktueller Use Case |
| 3 | OpenSpace | GELB | Nur Dev-Tooling, nicht in Inferenz-Pipeline |
| 4 | CLI-Anything | ROT | Kein Fit (WinCan ist proprietaer) |
| 5 | **RAG-Anything** | **GELB → GRUEN** | **Spike-Branch fuer VSA-KEK-RAG im Sidecar** |
| 6 | Google Workspace CLI | GELB | Erst Use Case klaeren |
| 7 | Claude Peers MCP | GELB | Lokales Setup, kein Repo-Change |

**Empfehlung in Reihenfolge:**

1. **Priorisieren: RAG-Anything** als Spike im `sidecar/`, eingebunden
   ueber neuen Service `VsaKekRagService` (Interface!). Quellen:
   VSA-KEK 2023 PDFs, EN 13508-2, eigene Schadenscodes-Doku.
2. **Optional fuer Workflow:** Codex Plugin + Claude Peers MCP als
   reine Claude-Code-Erweiterungen (kein Repo-Change).
3. **Spaeter:** Google Workspace CLI nur wenn Report-Versand konkret
   automatisiert werden soll.
4. **Ignorieren:** AutoResearch, CLI-Anything, OpenSpace (fuer dieses
   Projekt).

Naechster konkreter Schritt brauche ich Freigabe fuer:

- [ ] Spike-Branch `feature/rag-anything-vsakek` anlegen?
- [ ] Welches Embedding-Modell soll genutzt werden? (BGE-M3 lokal vs.
      Ollama vs. OpenAI)
- [ ] Welche Quellen-PDFs sollen rein? (Liste der Norm-Dokumente)

Ohne diese Entscheidungen kein Code-Change — gemaess CLAUDE.md.
