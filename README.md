# SewerStudio

Lokale Windows-Desktop-Anwendung fuer die automatisierte Auswertung von
Kanal-TV-Inspektionen. Aus Inspektionsvideos und Bestandsdaten werden
EN-13508-2- und VSA-KEK-konforme Schadensprotokolle. Die gesamte KI laeuft
auf der eigenen Workstation — kein Cloud-Upload, keine Internet-Abhaengigkeit.

> **Reifegrad (Stand 2026-05-07):** Externe Tests laufen.
> Als **Assistenzsystem** produktiv nutzbar (Mensch codiert, KI schlaegt vor).
> Fuer **autonome Codierung ohne Review noch nicht freigegeben** — die KI-Erkennung
> liegt aktuell bei rund 52 % ValidationLog-Accuracy. Der Weg dahin fuehrt
> ueber Datenarbeit (Active Learning), nicht ueber neue Features.

## Was die App kann

- **Inspektionsvideos abspielen** (VLC-basiert) und live mitcodieren
- **Schaeden automatisch erkennen** (YOLO + SAM + Grounding DINO + Qwen3-VL)
- **Bestandsdaten importieren** aus WinCan DB3, IBAK Daten.txt, KINS,
  XTF/SIA405 sowie PDFs (Fretz, KIT Bauinspekt, Abwasser Uri, IBAK direkt
  inkl. Caesar-Decode)
- **Stammdaten verwalten** (Schaechte, Haltungen, Projekte)
- **Schadensprotokolle** als druckfertige PDFs ausgeben (EN 13508-2)
- **Sanierungsempfehlungen** + Devis/Offerte generieren
- **Aus Korrekturen lernen** (Self-Training, KnowledgeBase mit ueber 21.000 Samples)
- **Sich selber warten** (Diagnose-Tab: Brain-Mirror-Health, Frame-Cleanup,
  Versions-Pruning)

## Stand der Codebasis

| | |
|---|---|
| Build | `dotnet build` → 0 Warnungen, 0 Fehler |
| Tests | 819 gruen, 1 uebersprungen |
| Codeumfang | ~620 C#-Dateien / ~140 k Zeilen (src + tests) |
| Architektur | Domain / Application / Infrastructure / UI mit Architekturtests |
| Plattform | .NET 10, WPF, Windows 11 |
| CI | GitHub Actions (Build + Test bei jedem Push, Python-Sidecar-Lint) |
| ADRs | 5 dokumentierte Architektur-Entscheidungen in `docs/adr/` |

## KI-Pipeline (lokal)

| Modell | Rolle | VRAM |
|---|---|---|
| YOLO26l-seg (TensorRT FP16) | Detektion + Segmentierung, 10 Klassen | ~1.5 GB |
| Qwen3-VL 8B Q8 (Ollama) | Schadensklassifikation, JSON-Output | ~11 GB |
| Qwen3-VL 32B hybrid | Eskalation bei unsicheren Faellen | ~2 GB GPU + 20 GB RAM |
| SAM 2.1 Hiera-L | pixelgenaue Segmentierung | ~2.5 GB |
| Grounding DINO 1.5 | Open-Vocabulary-Detection (lazy) | ~1.5 GB on-demand |
| nomic-embed-text | KB-Embeddings | ~0.6 GB |
| ByteTrack / OC-SORT | Tracking ueber Frames (CPU) | — |

**Empfohlene Hardware:** Workstation-Mode mit RTX 5090 (32 GB VRAM) +
64 GB RAM. Laptop-Mode existiert als Hardware-Profil, mit reduzierter
Eskalation und kleineren Batch-Groessen.

## Voraussetzungen

- Windows 11
- .NET 10 SDK (Preview oder neuer)
- NVIDIA-GPU mit CUDA fuer die KI-Pipeline (sonst nur Import / Export / Reports)
- Python-Sidecar fuer YOLO/SAM/DINO (Port 8100)
- [Ollama](https://ollama.com) mit Qwen3-VL 8B + 32B + nomic-embed (Port 11434)

Sidecar und Ollama werden separat eingerichtet — siehe
`docs/KI-PIPELINE-GESAMTAUDIT.md`.

## Quickstart (Entwicklung)

```powershell
git clone <dieses-repo>
dotnet restore AuswertungPro.sln
dotnet build  AuswertungPro.sln
dotnet test   AuswertungPro.sln
```

Start der UI:

```powershell
dotnet run --project src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj
```

## Externe Konfiguration

### pdftotext (PDF-Import)

Lege `pdftotext.exe` unter `src/AuswertungPro.Next.UI/tools/pdftotext.exe`
ab oder konfiguriere einen Pfad in den Einstellungen.

### Playwright/Chromium (PDF-Export)

Der PDF-Export rendert HTML-Templates ueber Playwright. Einmalig nach
Build/Restore Chromium installieren:

```powershell
pwsh "src/AuswertungPro.Next.UI/bin/Debug/net10.0-windows/playwright.ps1" install chromium
```

### Optional: Logo im PDF

Lege eine PNG-Datei unter
`src/AuswertungPro.Next.UI/Assets/Brand/abwasser-uri-logo.png` ab. Die
Datei wird nach `bin/...` kopiert und automatisch eingebettet.

### VSA-Klassifizierungstabellen (Anhang C/D)

Die App erwartet `classification_channels.json` und
`classification_manholes.json`. Pflege die Tabellen direkt oder
generiere sie aus der VSA-Richtlinie.

### Diagnose / Fehlercodes

In den **Einstellungen** kann `EnableDiagnostics` an-/abgeschaltet
werden. Bei aktiver Diagnostik wird bei Exceptions ein Fehlercode
angezeigt und in der Log-Datei gespeichert. Zusaetzlich gibt es einen
**Diagnose-Tab** mit Sidecar-Health, Brain-Mirror-Check, Frame-Cleanup
(orphane PNGs) und KB-Versions-Pruning.

## Datenpfade

- `C:\KI_BRAIN` — KnowledgeBase, training_frames, eval_set, Modelle (~113 GB)
- `E:\Brain Sync` — gespiegelte Kopie mit SHA256-Manifest (Brain-Mirror)
- LocalAppData — projektspezifische Konfiguration

## Architekturueberblick

```
Domain         — reine Datentypen (Haltung, Schacht, VsaFinding, ...)
Application    — Service-Vertraege (Interfaces), Pipeline-DTOs
Infrastructure — Importer, PDF-Parser, KI-Wrapper, SQLite-KB, Reports
UI             — WPF, ViewModels, Pages, Player, Diagnose-Tab
Sidecar (Py)   — YOLO / SAM / DINO / Florence-2 (Port 8100)
Ollama         — Qwen3-VL 8B / 32B / nomic-embed (Port 11434)
```

Architektur-Prinzipien siehe `CLAUDE.md`.

## Dokumentation

| Datei | Inhalt |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Projekt-Kontext, Pipeline-Stand, Coding-Regeln |
| [`docs/KI-PIPELINE-GESAMTAUDIT.md`](docs/KI-PIPELINE-GESAMTAUDIT.md) | Vollstaendige Pipeline-Dokumentation |
| [`docs/CODIER-MODUS-PIPELINE.md`](docs/CODIER-MODUS-PIPELINE.md) | Codiermodus im Detail |
| [`docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md`](docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md) | Aktuelle Standortbestimmung mit Notenskala |
| [`docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md`](docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md) | Schlussanalyse mit allen Befunden |
| [`docs/TEST_BRIEFING_2026-05-07.md`](docs/TEST_BRIEFING_2026-05-07.md) | Briefing fuer externe Tester |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Was erreicht ist + offene Punkte priorisiert |
| [`docs/adr/`](docs/adr/) | Architecture Decision Records (5 ADRs) |

## Status & Lizenz

Solo-Entwicklung, kein kommerzielles Ziel. Aktiver Branch:
`feature/pdf-import-beobachtungen`. Lizenzhinweis steht aus.
