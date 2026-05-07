# SewerStudio — Ist-Zustand vs. geplante Zukunft

**Stand:** 2026-04-20
**Zweck:** Umfassende Gegenueberstellung des heutigen Produktivsystems mit allen offenen Plaenen und Implementierungen
**Lesereihenfolge:** §1 Ueberblick → §2 Ist → §3 Soll → §4 Delta → §5 Reihenfolge → §6 Risiken

---

## 1. Ueberblick

SewerStudio ist eine WPF-Desktop-Anwendung (.NET 10, Clean Architecture) fuer automatisierte Kanal­inspektion nach **EN 13508-2** und **VSA-KEK 2023**. Das System ist **produktiv einsetzbar** — die heutigen offenen Arbeiten sind Optimierungen und Erweiterungen, keine Grundlagenarbeit.

### Eckdaten heute (2026-04-20)

| Metrik | Wert |
|---|---|
| KB-Samples (mit Embedding) | 13'154 |
| Verschiedene VSA-Codes in KB | 181 |
| Training-Samples gesamt (JSON) | 21'908 |
| Approved Samples | 19'509 |
| Rejected Samples | 2'399 |
| Bereits verarbeitete Haltungen | 4'755 |
| KB-Versionen (Snapshots) | 1'143 |
| QualityGate-Verteilung | Green 20.5% · Yellow 9.7% · Red 69.7% |
| WPF-Fenster | 32 |
| AI-Services | ca. 25 |
| Unterstuetzte PDF-Formate | 4 (Fretz, KIT, Uri, IBAK) + Bildbericht/Haltungsbilder/ColumnStacked |
| KI-Modelle im Orchester | 6 (YOLO/DINO/SAM 2/Qwen 8B/Qwen 32B/nomic-embed) |

### Was heute gemacht wurde (2026-04-19 → 20)

- **PDF-Parser:** Andermatt-Formate (Bildbericht/Haltungsbilder/ColumnStacked) eingebaut
- **KB-Dedup-Bug:** `KbIndexState.Deduplicated` Enum-Wert hinzugefuegt, 9'070 luegende Indexed-Samples repariert
- **Review-UI:** Filter (Code-Praefix, Status), Bulk-Approve/Reject/Delete, Multi-Selection
- **Profile-Extraktion** auf 175 Profile + 8'944 Frames erweitert
- **Protokoll-Training:** Bulk-Add-Fix (von O(n²) auf O(n) + Task.Run, Hang behoben)
- **Batch-Scanner:** ueberspringt `System Volume Information` / System-Ordner
- **Review-Marathon:** 1'985 Pending → 0 abgearbeitet
- **SDF-Konverter:** Externe PowerShell+Python-Scripts plus C#-Klasse `SdfToSqliteConverter` (Code fertig, noch nicht gebaut)

---

## 2. Ist-Zustand (funktionierend heute)

### 2.1 Architektur

**Schichten (Clean Architecture):**

```
UI (WPF, ViewModels, KI-Orchestrierung)
  → Infrastructure (SQLite, PDF/XTF/WinCan/IBAK Import)
    → Application (Use Cases, Service-Interfaces)
      → Domain (Entities, Value Objects — kennt niemanden)

Python-Sidecar (FastAPI Port 8100)
  → YOLO · DINO · SAM 2 · Florence-2 Shadow
```

**Projekt-Projekte:** 4 Haupt-Projekte + 2 Test-Suites.

### 2.2 KI-Pipeline (lokal, Workstation-Mode)

**GPU-Auslastung (RTX 5090, 32 GB):**

| Modell | VRAM | Lade-Strategie |
|---|---|---|
| Qwen 8B (Q8_0, 6 Slots, 8192 ctx) | 11.7 GB | permanent |
| YOLO 26m (TensorRT FP16 `.engine`) | 2.5 GB | permanent |
| Grounding DINO 1.5 | 2.0 GB | permanent |
| nomic-embed-text | 0.6 GB | permanent |
| SAM 2 | 3.0 GB | on-demand |
| Florence-2 Shadow | 3.5 GB | lazy |
| Qwen 32B (Q4_K_M hybrid, num_gpu=10) | 8.0 GB + 14.8 GB RAM | Eskalation |

**Pipeline-Fluss pro Video:**

```
VideoFrameStream (persistenter ffmpeg)
  → FrameQualityFilter (Laplacian + Luminanz + dHash)
  → YOLO Pre-Screening (TensorRT FP16)
  → Grounding DINO (autocast + float16, torch.compile)
  → SAM 2 (hiera_l, Ring-Scan)
  → MaskQuantificationService (px → mm)
  → DetectionAggregator (Meter-State-Machine, kein ByteTrack!)
  → Qwen 8B (JSON-Schema erzwungen, Ollama format:)
  → QualityGateService (8 Signale → Green/Yellow/Red)
  → Eskalation auf Qwen 32B bei Yellow/Red
  → RawVideoDetection → Protokoll/PDF/Excel
```

### 2.3 Datenmodell (Kern-Entitaeten)

**`VsaFinding`** (Domain) — 16 Properties:
- `KanalSchadencode`, `Quantifizierung1`, `Quantifizierung2` (als Text!)
- `SchadenlageAnfang`, `SchadenlageEnde`, `LL`
- `MeterStart`, `MeterEnd`, `MPEG`, `Timestamp`, `FotoPath`
- `EZD`, `EZS`, `EZB` (VSA-KEK-Bewertung)
- `Raw`
- **KEIN** `Einheit1/2`, **KEIN** `MeasurementTool`, **KEIN** `MeasurementSubject`

**`TrainingSample`** (UI-Schicht) — ausfuehrliches Schema mit:
- Basis: `SampleId`, `CaseId`, `Code`, `Beschreibung`, `MeterStart/End`
- Status: `Status` (New/Approved/Rejected), `KbIndexState` (None/Pending/Indexed/Error/Deduplicated)
- Quality: `QualityGateLevel`, `HasOsdMismatch`, `TruthMeterCenter`, `OdsDeltaMeters`
- Training: `SourceType`, `MatchLevel`, `KiCode`, `TechniqueGrade`, `IsKorrigiert`
- BBox-Felder (aktuell unbenutzt: 0 von 10'109 approved haben echte BBox)

### 2.4 Storage-Layout

```
C:\KI_BRAIN\                         (zentraler Knowledge-Root)
├── KnowledgeBase.db                 (SQLite: Samples, Embeddings, Versions)
├── training_samples.json            (Review-Queue + Approved-Store)
├── training_settings.json
├── benchmark_metrics.json           (FIFO 50 Eintraege — aktuell leer!)
├── benchmark_set.json
├── eval_set\                        (120 Frames mit YOLO-Labels)
├── frames\                          (Batch-produzierte Frames)
├── training_frames\                 (Profile-Extraktor-Frames, 13'047 PNG, 14 GB)
├── inspection_profiles\             (175 JSON-Profile pro Haltung)
├── inspection_patterns.json         (Median-Fahrgeschw., Codierungen/m etc.)
├── fewshot_images\                  (Qwen Few-Shot-Beispiele)
├── sdf_converted\                   (3 konvertierte WinCan-VX DB3)
├── teacher_images\, teacher_labels\ (Few-Shot-Trainingsmaterial)
└── logs\, batch_processed.txt       (Run-Tracking)

Mirror: E:\Brain  (automatisch via KnowledgeMirrorService)
```

### 2.5 Import-Formate (funktionierend)

| Format | Import-Service | Status |
|---|---|---|
| WinCan DB3 (SQLite) | `InspectionProfileExtractor` + `WinCanDbImportService` | ✅ produktiv |
| WinCan SDF (SSCE) | `SdfToSqliteConverter` (C#, Code fertig) | ⚠️ noch nicht gebaut |
| IBAK XTF (SIA 405 + VSA_KEK_2020) | `LegacyXtfImportService` | ⚠️ unvollstaendig (Videozaehlerstand-Mapping?) |
| PDF-Protokoll (Fretz/KIT/Uri/IBAK) | `PdfProtocolExtractor` + `PdfProtocolTableParser` | ✅ 4 Format-Pfade, heute +3 |
| XTF SIA 405 (Stammdaten) | `LegacyXtfImportService` | ✅ produktiv |

### 2.6 Review-Workflow (neu heute)

| UI-Element | Funktion |
|---|---|
| Status-Filter (Pending/Approved/Rejected/Alle) | Sample-Liste filtern |
| Code-Praefix-Filter (z.B. "BAF") | Alle BAF* Codes |
| Sichtbar-Counter | Aktuelle Filter-Treffermenge |
| Alle Sichtbaren approven/rejecten | Bulk-Aktion auf Filter-Treffer |
| Markierte approven/loeschen | Bulk auf Strg+Klick-Selektion |
| Einzel-Approve/Reject | Buttons pro Sample |

### 2.7 Was heute **nicht** geht oder fehlt

| Baustelle | Auswirkung |
|---|---|
| **BatchPipeline-Deadlock** | Parallele Qwen-Requests hingen bei 6 Slots → **deaktiviert** |
| **QualityGate Signal-Minimum** | 1 Signal kann Green erzeugen (pro Category-Ebene) |
| **QuickScanService** | ohne FrameQualityFilter (frisst Garbage-Frames) |
| **NormalizeClock** | nur deutsch (top/bottom/left/right werden nicht erkannt) |
| **PipeImageWidthRatio hardcoded 0.70** | Messungen ±15% ungenau |
| **96% Red-Samples** im April (heute 69.7%, besser aber noch unbalanciert) | KI lernt verzerrt |
| **OSD-Erkennung** kameraabhaengig | bei fremden Kameras ungenau |
| **YOLO-BBoxes** | 0 von 10'109 approved — kein YOLO-Retrain moeglich |
| **Benchmark-Metrics** `benchmark_metrics.json` leer | keine F1-Zeitreihe |
| **Schachtcodes (DA/DB/DC/DD)** nicht im VsaCodeTree | UI-Explorer zeigt keine Schachtbefunde (vernachlaessigbar, User fokussiert Leitungen) |
| **Einheiten in VsaFinding** | Quantifizierung nur als Text, keine Einheit daneben |
| **MeasurementTool-Herkunft** | keine Nachvollziehbarkeit welches Werkzeug einen Wert erzeugt hat |

---

## 3. Soll-Zustand (geplant, noch nicht umgesetzt)

### 3.1 Phase 1 — VSA-KEK 2023 Versions-Konsistenz

**Ziel:** UI und Code-Baum auf 2023 ausrichten, damit nichts stilles gegen 2018-Tree arbeitet.

| Aktion | Datei | Aufwand |
|---|---|---|
| UI-Label korrigieren | `VsaCodeExplorerWindow.xaml:36` | 2 Min |
| Header-Kommentar korrigieren | `VsaCodeTree.cs:7` | 2 Min |
| **AEC/AED/AEF Null-Bewertungsregeln** (17+ Faelle in Bürglen ohne Regel) | `classification_channels.json` | 15 Min |
| Live-Test `LookupLabel` gegen 53 Bürglen-Codes | Test-Skript nach Build | 30 Min |

**Schacht-Codes (DA/DB/DC/DD):** Auf User-Wunsch **vernachlaessigt** — SewerStudio fokussiert Leitungsinspektion.

**Migrations-Tabelle 2020→2023:** NICHT noetig laut Diff-Analyse — Haupt-Leitungscodes stimmen 1:1.

### 3.2 Phase 2 — IBAK-XTF-Import vervollstaendigen

**Ziel:** IBAK IKAS Evolution Exporte (VSA_KEK_2020_LV95, INTERLIS 2.3) als Ground-Truth nutzbar machen.

| Aktion | Datei | Aufwand |
|---|---|---|
| Ist-Analyse: welche XTF-Felder gehen verloren? | `LegacyXtfImportService.cs` | 1 Std |
| `Videozaehlerstand` (HH:MM:SS:FF) → `VsaFinding.Timestamp` | `LegacyXtfImportService.cs` | 1 Std |
| MPEG-Datei via `Film/Daten.txt` zuordnen | `LegacyXtfImportService.cs` | 1 Std |
| INTERLIS-2.3-Namespace sauber verarbeiten | `LegacyXtfImportService.cs` | 30 Min |
| Round-Trip-Test: Bürglen importieren → findings pruefen → wieder nach XTF | Integration-Test | 1 Std |

**Test-Datei:** `D:\TESTSAMPLES\Bürgle_Seitenanschlüsse\...\_SIA405.xtf`
- 608 Kanalschaden-Eintraege
- 53 verschiedene volle Codes
- 26 Hauptcodes (alle in VsaCodeTree bekannt)

**VSA-KEK 2020 → 2023 Layer:** Laut Diff-Analyse nicht noetig, optional nachruesten.

### 3.3 Phase 3 — PhotoMeasurement Einheiten + Werkzeug-Herkunft

**Ziel:** `VsaFinding` WinCan-kompatibel erweitern, alle 11 Werkzeuge schreiben Einheit + Herkunft.

**Datenmodell-Erweiterung:**

```csharp
// In VsaFinding.cs hinzufuegen:
public string? Einheit1 { get; set; }              // "mm", "%", "°", "cm"
public string? Einheit2 { get; set; }
public string? MeasurementTool { get; set; }       // "Lineal", "Wasserstand", ...
public string? MeasurementSubject { get; set; }    // "Wurzel", "Abplatzung", "Fehlstelle"
```

**Alle Felder nullable → backward-kompatibel, keine Migration.**

**Werkzeug-Mapping (alle 11 schon vorhanden!):**

| Werkzeug | `MeasurementTool` | Erwartete Einheit |
|---|---|---|
| Lineal | "Lineal" | "mm" |
| Wasserstand | "Wasserstand" | "%" |
| Ablagerung | "Ablagerung" | "%" |
| Hindernis | "Hindernis" | "%" |
| Querschnitt | "Querschnitt" | "%" (Wurzel/Abplatzung/Fehlstelle via Subject-Dropdown) |
| Deformation | "Deformation" | "%" (Ovalitaet) |
| Anschluss | "Anschluss" | "mm" (H×B) |
| Abzweig | "Abzweig" | "°" |
| Bogen | "Bogen" | "°" / "mm" |
| Kalibrieren | — | — (nur Skala) |
| Markieren | — | — (KI-Training-BBox) |

**Kritische technische Korrektur gegen urspruenglichen Plan:**

XTF-Dateien haben **keine Einheiten als Attribut**. Bürglen-XTF zeigt `<Quantifizierung1>15</Quantifizierung1>` — ohne Einheit. Loesung:

```csharp
// Neue Methode in VsaCodeTree
public static string? GetQuantificationUnit(string code, int index)
{
    // Liest aus Katalog: "BAB" → Q1.Einheit = "mm"
    //                    "BAA" → Q1.Einheit = "%"
}
```

Das loest XTF + PDF + Qwen in einem Zug. Aufwand: **+1 Std** gegenueber urspruenglichem Plan.

**Querschnitt-Subject-Dropdown** (Popup nach Polygon-Abschluss):
- Wurzel
- Abplatzung
- Fehlstelle
- Sonstige Querschnittsreduktion (Default)

**Bewusst NICHT dabei:**
- Riss-Messwerkzeuge (ohne 2-Punkt-Laser-Referenz zu unpraezise)
- Neue Werkzeuge (alle Anwendungsfaelle mit 11 bestehenden abgedeckt)
- Datenmigration alter Findings

**Aufwand gesamt:** ~1.5 Arbeitstage (urspruenglich 1, aber Blast-Radius unterschaetzt — 69 Referenzen auf `Quantifizierung1/2` in 14 Dateien).

### 3.4 Phase 4 — SDF-Support produktiv machen

**Code bereits fertig in:**
- `SdfToSqliteConverter.cs` (Infrastructure) — laedt SSCE-DLL via Reflection, konvertiert zu SQLite
- `TrainingCenterWindow.xaml.cs` — Dialog-Filter erweitert, auto-konvertiert bei .sdf

**Fehlt nur:** `dotnet build` nach Batch-Ende.

**Bereits produziert (via Script):**
- `Andermatt_Zone_2.11.db3` — 209 Beobachtungen
- `Andermatt_Zone_2.12.db3` — **1'051 Beobachtungen** (groesster Gewinn)
- `Erstfeld_Zone_6.19.db3` — 348 Beobachtungen
- **Total: 1'608 Ground-Truth-Codierungen**

Nach Batch-Ende: Diese 3 .db3 via "Profile extrahieren" im UI einspielen → weitere 5'000-8'000 Frames fuers Training.

### 3.5 Phase 5 — KI-Modell-Evaluationen (Backlog)

Aus Meta-Review-Analyse (2026-04-20):

| Prio | Modell | Begruendung |
|---|---|---|
| **P1** | `qwen3-vl:8b-thinking` | Reasoning-Mode fuer 5-10 Red-Frames testen. **Noch nicht lokal, `ollama pull` noetig (~5 GB)** |
| **P2** | RT-DETR v2 | Sewer-validiert (Istanbul F1 79%), auf 120-Frame Eval-Set messen |
| **P2** | RT-DETR v4 | experimental danach (VFM-Distillation via DSI+GAM, noch ohne Sewer-Beleg) |
| **P3** | Qwen3-VL-Embedding | Multimodale KB-Suche (Bild+Text), verfuegbar seit Juni 2025 |

**Entscheidungs-Kriterium:** Nicht nach Paper-Benchmark, sondern nach Messung auf eigenem Eval-Set.

---

## 4. Delta — was sich konkret aendert

### 4.1 Datenmodell-Delta

| Entitaet | Heute | Soll (Phase 3) |
|---|---|---|
| `VsaFinding` | 16 Properties | 20 Properties (+Einheit1/2, +MeasurementTool, +MeasurementSubject) |
| `TrainingSample.KbIndexState` | 5 Werte (None/Pending/Indexed/Error/Deduplicated) | unveraendert |
| `VsaCodeTree.LookupLabel(code)` | existiert | unveraendert |
| **`VsaCodeTree.GetQuantificationUnit(code, idx)`** | **fehlt** | **neu** |

### 4.2 UI-Delta

| UI-Bereich | Heute | Soll |
|---|---|---|
| `VsaCodeExplorerWindow` Titel | "VSA-KEK 2018" | "VSA-KEK 2023" |
| Samples-Tab Filter | Status + Code-Praefix | unveraendert |
| Samples-Tab Bulk | Approve/Reject/Delete | unveraendert |
| PhotoMeasurement Werkzeuge | 11 (keine Einheiten-Logik) | 11 (+ Einheit-Metadaten) |
| Querschnitt-Werkzeug | Ein-Subject | + Subject-Dropdown (Wurzel/Abplatzung/Fehlstelle) |
| Beobachtungen-Sidebar Anzeige | "15" | "15 % (Ablagerung)" |
| "Profile extrahieren" Button | Nur .db3 | .db3 + .sdf + .sqlite |

### 4.3 Import-Delta

| Importer | Heute | Soll |
|---|---|---|
| `LegacyXtfImportService` | Quantifizierung als Text | + Einheit aus `VsaCodeTree.GetQuantificationUnit()` |
| `LegacyXtfImportService.Videozaehlerstand` | ungenutzt? | → `VsaFinding.Timestamp` + MPEG-Mapping |
| `PdfProtocolTableParser` | Einheit als Text in Beschreibung | Einheit abspalten + in `Einheit1/2` |
| `EnhancedVisionAnalysisService` (Qwen) | JSON liefert Einheit | in `VsaFinding.Einheit1/2` mappen |
| `InspectionProfileExtractor` | nur DB3 | via SDF-Converter auch .sdf-Input |

### 4.4 KB-Delta-Erwartung

| Metrik | Heute | Nach Phase 4 (SDF-Einspielung) | Nach Phase 5 (Modell-Evals) |
|---|---|---|---|
| KB-Samples | 13'154 | ~19'000 (1'608 DB3-Samples + ~5'000 Frame-Pfade) | unveraendert (Eval ist read-only) |
| QG Green-Quote | 20.5% | ~25% (mehr Ground-Truth) | ~30-35% (bessere Modelle) |
| BA-Anteil | 20.3% | ~24% (Andermatt Zone 2.12 hat viele BA) | unveraendert |
| BB-Anteil | 7.7% | ~10% | unveraendert |

---

## 5. Implementierungs-Reihenfolge

### Reihenfolge-Logik

1. **Erst korrigieren, dann erweitern.** Phase 1 (VSA-KEK-Labels) ist Voraussetzung fuer XTF-Import-Validierung.
2. **Erst Import, dann UI.** Phase 2 (XTF-Import) liefert Daten mit Einheiten — Phase 3 (UI-Einheiten) profitiert davon.
3. **Erst Datenmodell, dann Werkzeuge.** Phase 3.1 (`VsaFinding` erweitern) Voraussetzung fuer alle Werkzeug-Handler.
4. **Erst Tools, dann Evaluierung.** Phase 4 (SDF) liefert Ground-Truth-Bias-Korrektur — Phase 5 (Modell-Evals) misst erst sinnvoll.

### Konkrete Morgen-Reihenfolge (nach Batch-Ende)

**Block A — Trivial, schnell (30 Min)**
1. `dotnet build` nach Batch-Ende (aktiviert SDF-Support)
2. VSA-KEK-Label `VsaCodeExplorerWindow.xaml:36` + `VsaCodeTree.cs:7`
3. 3 konvertierte .db3 via "Profile extrahieren" einspielen
4. AEC/AED/AEF Null-Bewertungsregeln in `classification_channels.json`

**Block B — XTF-Importer-Polish (3-4 Std)**
5. Live-Lookup-Test 53 Bürglen-Codes → ggf. fehlende Sub-Codes ergaenzen
6. `LegacyXtfImportService` Ist-Analyse
7. `Videozaehlerstand`-Parser + MPEG-Mapping
8. Round-Trip-Test mit Bürglen

**Block C — PhotoMeasurement-Plan (1.5 Tage)**
9. `VsaFinding` um 4 Felder erweitern
10. `VsaCodeTree.GetQuantificationUnit()` Helper
11. 11 Werkzeug-Handler auf neues Schema umstellen
12. Querschnitt-Subject-Dropdown
13. Import-Pfade (XTF/PDF/Qwen) → `GetQuantificationUnit()` nutzen
14. UI-Anzeige in 14 Dateien auf "Wert Einheit (Tool)" umstellen
15. Round-Trip-Test Bürglen (XTF → findings → XTF)

**Block D — Modell-Evals (mehrere Tage, asynchron)**
16. `ollama pull qwen3-vl:8b-thinking` → Test auf 5-10 Red-Frames
17. RT-DETR v2 Setup + Eval auf 120-Frame-Set
18. (optional) RT-DETR v4 experimentell
19. (optional) Qwen3-VL-Embedding multimodale KB

---

## 6. Risiken

### 6.1 Blast-Radius-Risiken

| Aenderung | Consumer-Count | Risiko |
|---|---|---|
| `VsaFinding.Einheit1/2` hinzu | 14 Files, 69 Stellen | niedrig (nullable → backward-kompat) |
| `VsaCodeTree.GetQuantificationUnit()` neu | 0 (neue API) | niedrig |
| UI-Anzeige "Wert + Einheit" | 14 Files | mittel — Tests noetig |
| `LegacyXtfImportService.Videozaehlerstand` | 1 File | niedrig |
| SDF-Dialog-Filter erweitern | 2 Files | niedrig |

### 6.2 Datenrisiken

- **JSON-Store-Schreiboperationen** waehrend gleichzeitigem Batch: schon heute Race-Condition-Risiken (via `_fileLock` geschuetzt, aber Atomic-Save-Logik muss getestet sein)
- **KB-Konsistenz** nach Enum-Erweiterung (`KbIndexState.Deduplicated` war heute Bug-Quelle — das Muster sollte fuer PhotoMeasurement-Felder NICHT wiederholt werden)

### 6.3 Modell-Risiken

- **qwen3-vl:8b-thinking** — neuer Thinking-Mode, unbekanntes VRAM-Verhalten in Kombination mit 6 parallelen Slots
- **RT-DETR v2** — benoetigt eigene Trainingspipeline (ISWDS-Dataset), Integration in Sidecar noetig
- **RT-DETR v4** — nur Paper, kein Sewer-Benchmark → Bauchgefuehl-Entscheidung

### 6.4 Prozess-Risiken

- **Review-Queue-Qualitaet** nach Bulk-Approve: bei falschem Stichprobenansatz vergiftet KB
- **Nachtbatch-Unterbrechung** — jede Code-Aenderung bedeutet Build → Batch-Neustart. Pendenzen bewusst gestapelt auf "nach Batch"

---

## 7. Was sicher nicht Teil des Plans ist

| Nicht gemacht | Begruendung |
|---|---|
| Rewrite/Refactor grosser Services | Thin-AI-Prinzip, funktionierender Code bleibt |
| Schacht-Codes (DA/DB/DC/DD) in VsaCodeTree | User-Entscheidung, Fokus auf Leitungsinspektion |
| Migration 2020→2023 Code-Tabelle | Diff-Analyse zeigt: Hauptcodes 1:1, nicht noetig |
| Riss-Messwerkzeuge im PhotoMeasurement | Ohne 2-Punkt-Laser zu unpraezise |
| Cloud-Integration | Offline-First-Prinzip |
| Multi-Tenancy / SaaS | Solo-Entwicklung, kein kommerzielles Ziel |
| ByteTrack/OC-SORT | eigene Meter-State-Machine (`DetectionAggregator`) reicht |
| BatchPipeline-Deadlock-Fix | offen, aber nicht blockierend (Fallback-Modus funktioniert) |

---

## 8. Erfolgs-Indikatoren

Wann ist der ganze Plan "fertig"?

- [ ] `dotnet build AuswertungPro.sln` gruen, 0 Warnungen, 0 Fehler
- [ ] VSA-KEK-Label im UI zeigt "2023"
- [ ] `LookupLabel` loest alle 53 Bürglen-Codes auf
- [ ] Bürglen-XTF-Import liefert ≥ 608 `VsaFinding`-Eintraege mit Timestamp+MPEG
- [ ] `VsaFinding.Einheit1` nicht leer fuer alle BA/BB-Codes aus XTF
- [ ] PhotoMeasurement-Werkzeuge schreiben `MeasurementTool` in Findings
- [ ] UI-Anzeige zeigt "15 % (Wurzel)" statt "15"
- [ ] 3 SDF-Datenbanken eingespielt, KB +1'608 Codierungen
- [ ] `qwen3-vl:8b-thinking` getestet auf 5-10 Red-Frames, Ergebnis dokumentiert
- [ ] Benchmark-Lauf mit aktualisierter KB → `benchmark_metrics.json` nicht mehr leer

---

## 9. Meta-Anmerkungen

- **Dokument ist read-only erstellt waehrend laufendem Nachtbatch** (keine Code-Aenderung)
- **Basiert auf:** Code-Grep, Schema-Inspektion, JSON-Datenbank-Abfragen, vorhandenen Plaenen
- **Nicht gepflegt:** Automatisch, alte Teile koennten veralten — Stand 2026-04-20
- **Ergaenzende Dokumente:**
  - `AUDIT_SEWERSTUDIO_KOMPLETT_2026-04-19.md` — tieferer Architektur-Audit (gestern)
  - `MEMORY.md` — Kurzform-Lektionen fuer KI-Assistent
  - `CLAUDE.md` — dauerhafter Projekt-Kontext

---

*Erstellt 2026-04-20 waehrend Nachtbatch-Lauf (4'755 von ~5'150 Haltungen verarbeitet)*
