# SewerStudio — Vollstaendiger Programm-Audit

**Datum:** 4. April 2026
**Umfang:** Gesamtes Programm (UI, Domain, Infrastructure, KI-Pipeline, Sidecar, Training, Knowledge Base)
**Dateien geprueft:** 80+ kritische Dateien ueber alle Layer

---

## Bewertungsuebersicht

| Bereich | Score | Status |
|---------|-------|--------|
| **A. Shell & Navigation** | 7.5/10 | Solide Basis, fehlende Statusleiste |
| **B. UI-Seiten & Fenster** | 7.5/10 | Professionell, Datenvisualisierung fehlt |
| **C. Domain-Modell** | 7/10 | Vollstaendig, Validierung schwach |
| **D. Import/Export** | 7/10 | Robust, aber FormelEvaluator-Bugs |
| **E. KI-Pipeline** | 8/10 | Sehr gut architekturiert, Retry fehlt |
| **F. Python Sidecar** | 8/10 | Effizient, VRAM-Eviction fehlt |
| **G. Quality Gate** | 8/10 | Gute Evidence-Fusion |
| **H. Training Center** | 8/10 | Sophisticated, atomare Writes fehlen |
| **I. Knowledge Base** | 8/10 | RAG funktional, Thresholds haerten |
| **J. Kostenrechnung** | 7/10 | Korrekt, Concurrency-Lock fehlt |
| **Gesamt** | **7.6/10** | Professionell — mit gezielten Fixes auf 9/10 hebbar |

---

## A. SHELL & NAVIGATION

### Staerken
- Saubere 3-Teil-Architektur: Menu > Sidebar > Content
- 12 Nav-Items mit Custom-Icons (Segoe MDL2 + Canvas-Shapes)
- Focus-Mode (F11) mit Sidebar-Ausblendung
- AnimatedContentControl fuer Seitenwechsel
- Guide-System mit 7 sequenziellen Schritten

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| A1 | **Statusleiste unten** — Aktuelle Seite, letzter Speicherstatus, KI-Status, Projektname | HOCH | Mittel |
| A2 | **Fenster-Titelleiste** mit App-Icon und Branding (aktuell WindowStyle=None ohne Chrome) | HOCH | Gering |
| A3 | **"Ungespeicherte Aenderungen"-Warnung** vor Seitenwechsel | HOCH | Gering |
| A4 | **Startup-Splash mit Fortschritt** — Aktuell "Initialisiere..." ohne Statusupdate (5s Minimum) | MITTEL | Gering |
| A5 | **Sidebar-Animation** — Aktuell blendet sofort aus, kein Smooth-Collapse | MITTEL | Gering |
| A6 | **Navigations-Historie** — Zurueck-Button fuer Seitenverlauf | MITTEL | Mittel |
| A7 | **Fensterposition wiederherstellen** — WindowStates in AppSettings definiert aber nicht angewendet | MITTEL | Gering |
| A8 | **Fehlende Control-Styles** — TextBox, DataGrid-Header, DatePicker, ValidationError fehlen in Controls.xaml | MITTEL | Mittel |

---

## B. UI-SEITEN & FENSTER

### B1. Druckcenter (BuilderPage)
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B1.1 | **Druckvorschau** — PDF-Thumbnail vor dem Export anzeigen | HOCH |
| B1.2 | **Filter-Badges** — Aktive Filter als Chips mit Anzahl (z.B. "Eigentuemer: 3 gewaehlt") | HOCH |
| B1.3 | **Sticky DataGrid-Header** — Spaltenkoepfe fixieren beim Scrollen | MITTEL |
| B1.4 | **Export-Fortschritt** — "Verarbeite 150/500 Zeilen..." statt unbestimmter Ladeanzeige | MITTEL |

### B2. Sanierungsmassnahmen / Offerten
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B2.1 | **Konfidenz-Anzeige** — Radiales Gauge statt nur Fortschrittsbalken (Rot/Gelb/Gruen-Zonen) | HOCH |
| B2.2 | **Kosten-Visualisierung** — Min/Erwartet/Max als Balkendiagramm, nicht nur Text | HOCH |
| B2.3 | **Drag-Drop-Feedback** — Sichtbare Drop-Zonen und Ghost-Drag-Image | MITTEL |
| B2.4 | **Vergleichsansicht** — Regelbasierte vs. KI-Empfehlung nebeneinander | MITTEL |
| B2.5 | **Offert-Header** — "3 Haltungen, 5 Massnahmen, Total CHF 45'000" als Zusammenfassung | HOCH |
| B2.6 | **Totals-Design** — Groessere, fette Typografie, farbcodiert (Gruen fuer Endsumme) | HOCH |

### B3. Hydraulik (HydraulikPanel)
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B3.1 | **Rohrquerschnitt-Visualisierung** — Wasser-Fuellstand animiert im Canvas darstellen | HOCH |
| B3.2 | **DWA-Konformitaet** — Ergebnisse farbig hinterlegen (Gruen=OK, Gelb=Warnung, Rot=Fail) | HOCH |
| B3.3 | **VSA-Indikatoren vergroessern** — Aktuell 12x12px, empfohlen 16x16px mit Labels | MITTEL |
| B3.4 | **PDF-Export-Button** im Header des Hydraulik-Panels | MITTEL |

### B4. VSA-Zustandsbewertung (VsaPage)
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B4.1 | **Zustandsklassen-Verteilung** — Balkendiagramm Klasse 0-4 nach VSA-Berechnung | HOCH |
| B4.2 | **Fortschrittsanzeige** — "234/1000 Haltungen bewertet" waehrend Batch-Berechnung | MITTEL |
| B4.3 | **Details-Button** — Ergebnis-Zusammenfassung mit vollstaendiger Aufschluesselung | MITTEL |

### B5. Protokoll-System
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B5.1 | **Foto-Vorschau im Editor** — Inline-Panel mit bis zu 2 Fotos im ProtocolEntryEditor | HOCH |
| B5.2 | **Clock-Picker modernisieren** — Gelb (#FFF6A8) durch Theme-Farbe ersetzen | HOCH |
| B5.3 | **Code-Hierarchie-Breadcrumb** — "Gruppe > Code > Char1 > Char2" nach Auswahl anzeigen | MITTEL |
| B5.4 | **Inline-Validierung** — Fehlermeldungen unter jedem Feld statt nur rotem Rahmen | MITTEL |
| B5.5 | **Foto-Icon im DataGrid** — Kamera-Symbol bei Eintraegen mit Fotos | MITTEL |

### B6. VSA-Code-Explorer
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B6.1 | **Suchfeld pro Spalte** — Live-Filter in Group/Code/Char1/Char2-Spalten | HOCH |
| B6.2 | **Klickbarer Breadcrumb** — Zurueck-Navigation durch Klick auf Hierarchie-Stufe | HOCH |
| B6.3 | **Zuletzt verwendete Codes** — Schnellzugriff-Sektion oben in der Gruppen-Liste | MITTEL |

### B7. Theme-Konsistenz
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| B7.1 | **Druck-Dialoge** — Von Dark-Theme (#FF0D1117) auf Light-Theme umstellen | HOCH |
| B7.2 | **Einheitlicher Spacing-Scale** — 8/12/16/24px statt aktuell 8/10/12/14/16 gemischt | MITTEL |
| B7.3 | **Icon-Groessen standardisieren** — 16/20/24px mit klarem Einsatzschema | MITTEL |
| B7.4 | **DataGrid-AlternatingRows** — Ueberall einheitlich (manche mit, manche ohne) | NIEDRIG |

---

## C. DOMAIN-MODELL

### Staerken
- Comprehensive: 34 Felder pro HaltungRecord mit FieldMetadata-Tracking
- Protokoll-System mit Revisionen und vollstaendiger Aenderungshistorie
- VSA-Regelwerk korrekt implementiert (ZN = EZ_min + 0.4 - A)

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| C1 | **Feld-Validierung** — Typ-Pruefung bei SetFieldValue (Int/Decimal/Combo gegen FieldCatalog) | HOCH | Mittel |
| C2 | **Haltungsname-Eindeutigkeit** — Duplikat-Check bei AddRecord() und Merge | HOCH | Gering |
| C3 | **SetFieldValue-Logging** — Abgelehnte Aenderungen (UserEdited) loggen statt still ignorieren | HOCH | Gering |
| C4 | **VsaRuleProvider leerer Catch** — Exception nicht verschlucken, sondern loggen | HOCH | Gering |
| C5 | **ProtocolEntry-Validierung** — MeterEnd > MeterStart, nicht-leerer Code | MITTEL | Gering |
| C6 | **FotoPaths-Validierung** — Pfadformat pruefen, existierende Dateien verifizieren | MITTEL | Gering |

---

## D. IMPORT/EXPORT

### Staerken
- Multi-Source Merge-Engine mit Prioritaetsregeln (XTF:80, PDF:60, Legacy:50)
- UserEdited-Felder werden nie ueberschrieben
- Import-Vorschau und Import-Bericht

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| D1 | **FormelEvaluator KRITISCH** — Division-durch-0 gibt 0 zurueck, Substring-Ersetzung fehlerhaft ("qty" in "quantity") | HOCH | Mittel |
| D2 | **MergeEngine DryRun** — Aendert Records trotz DryRun=true im Speicher (Clone noetig) | HOCH | Mittel |
| D3 | **XTF-Import Pfad** — Schreibt in AppContext.BaseDirectory statt User-Datenordner | HOCH | Gering |
| D4 | **PdfParser Regex-Cache** — _allRegexes wird pro Aufruf neu erstellt statt gecacht | MITTEL | Gering |
| D5 | **CSV-Export** — Heisst "Excel" aber erzeugt nur CSV; echten Excel-Export mit ClosedXML | MITTEL | Hoch |
| D6 | **PDF-Renderer** — Browser pro Aufruf gestartet; Browser-Instanz wiederverwenden | MITTEL | Mittel |
| D7 | **Unbegrenzter PrimaryDamage-Accumulator** — Kann bei fehlerhaften PDFs OOM verursachen | MITTEL | Gering |

### PDF-Berichte (QuestPDF)
| # | Empfehlung | Prioritaet |
|---|-----------|------------|
| D8 | **Inhaltsverzeichnis** — Automatisches TOC im Haltungsdossier-PDF | HOCH |
| D9 | **Seitennummern** — "Seite 3 von 12" im Footer | HOCH |
| D10 | **Diagramme** — Balken/Tortendiagramme fuer Hydraulik-Ergebnisse und Kosten statt nur Tabellen | MITTEL |
| D11 | **QR-Code** — Digitaler Link zum Projekt/Haltung im PDF-Header | NIEDRIG |

---

## E. KI-PIPELINE (C# Orchestrierung)

### Staerken
- Multi-Model Architektur: YOLO→DINO→SAM→Qwen mit Eskalation 8B→32B
- YOLO-cls Prefilter (ueberspringt 90% Normalframes)
- Intelligentes Frame-Sampling (BCD/BCE-Zonen immer, 50% periodisch)
- Deduplication-Window (3-Frame Sliding Window)
- Few-Shot Learning mit gecachten Bildern

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| E1 | **Retry-Logik (Polly)** — OllamaClient hat keinen Retry; 3x mit exponential Backoff | HOCH | Mittel |
| E2 | **Circuit Breaker** — Nach 5 aufeinanderfolgenden Timeouts Fast-Fail statt 5min haengen | HOCH | Mittel |
| E3 | **Health-Monitoring zur Laufzeit** — Sidecar nur beim Start geprueft, nicht waehrend Pipeline | HOCH | Mittel |
| E4 | **Off-by-One Fix** — MultiModelAnalysisService Zeile 147: `frameIndex % 2 == 0` sollte `== 1` sein | HOCH | Gering |
| E5 | **Per-Frame Timeout** — Aktuell 45s pro Pipeline, sollte pro Frame sein | HOCH | Gering |
| E6 | **Eskalations-Rate-Limit** — Max 5 Eskalationen/Minute um 32B-Ueberlastung zu verhindern | MITTEL | Gering |
| E7 | **Signal-Widerspruchserkennung** — YOLO conf=0.9 aber SAM-Maske <1% → Warnung statt Durchwinken | MITTEL | Mittel |
| E8 | **JSON-Schema-Validierung** — Vor Deserialisierung der Qwen-Antwort Struktur pruefen | MITTEL | Gering |

---

## F. PYTHON SIDECAR (FastAPI)

### Staerken
- Persistent GPU-Slots mit Double-Check Locking
- TensorRT Auto-Export fuer YOLO (2-5 Min, gecacht)
- AMP FP16 fuer DINO (30% Speedup, 50% Memory-Ersparnis)
- Batch SAM Prediction (N Boxen in einem Forward-Pass)
- NDJSON Streaming (kein Buffering)

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| F1 | **VRAM-Watermark-Monitoring** — Warnung bei 75%, Error bei 90%, Auto-Eviction bei 80% | HOCH | Mittel |
| F2 | **DINO Inference-Timeout** — Kein Timeout-Wrapper; kann Sidecar unbegrenzt blockieren | HOCH | Gering |
| F3 | **SAM Batch-Limit** — 500 Boxen in einem Pass = VRAM-Explosion; Max 100 pro Batch | HOCH | Gering |
| F4 | **TensorRT Export Race Condition** — File-Lock fuer parallele Prozesse fehlt | MITTEL | Gering |
| F5 | **RLE-Kompression** — SAM-Masken unkomprimiert (8MB); mit GZIP auf 100KB reduzierbar | MITTEL | Gering |
| F6 | **Model-Pre-Validation** — Beim Start: .pt/.pth Dateien existieren pruefen vor Lazy-Load | MITTEL | Gering |
| F7 | **Error-Differenzierung** — CUDA OOM vs. ungueltige Eingabe unterscheiden (aktuell: generisches Exception) | MITTEL | Gering |

---

## G. QUALITY GATE

### Staerken
- 9-dimensionaler Evidence-Vektor (YOLO, DINO, SAM, Qwen, LLM-Code, KB, Plausibilitaet)
- Gewichtete Durchschnitts-Fusion mit automatischer Renormalisierung
- Drei-Stufen Ampel: Gruen ≥0.75, Gelb ≥0.45, Rot <0.45
- MC-Dropout mit 3 Temperatur-Passes (0.1, 0.5, 0.9)
- Adaptive Gewichtung pro Schadenskategorie

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| G1 | **Signal-Floor** — YOLO conf <0.15 unterdruecken statt gleichbehandeln | MITTEL | Gering |
| G2 | **WeightLearning MinSamples** — Von 20 auf 50 erhoehen (Varianz bei wenigen Samples) | MITTEL | Gering |
| G3 | **WeightLearning Iterations** — Von 5 auf 10-15 erhoehen fuer Konvergenz-Stabilitaet | MITTEL | Gering |
| G4 | **Hold-Out Test-Set** — 20% der Daten zurueckhalten zur Ueberanpassungs-Pruefung | MITTEL | Mittel |
| G5 | **MC-Dropout fuer alle Zonen** — Aktuell nur fuer Gelb (0.45-0.75); auch Gruen/Rot pruefen | NIEDRIG | Gering |

---

## H. TRAINING CENTER

### Staerken
- PDF-Protocol-First Ansatz (Ground-Truth aus eingebetteten Fotos)
- 4-stufiger Code-Matching-Fallback (direkt → Hint → Inference → Reverse)
- Few-Shot Library ab 0.85 Quality-Score automatisch befuellt
- Pause/Resume fuer langlaeuige Trainings
- Benchmark mit Per-Code-Prefix Aggregation (BAB, BAC, BCA getrennt)

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| H1 | **Atomare Writes** — TrainingSamplesStore: tmp-Datei → atomarer Rename (Crash-Schutz) | HOCH | Gering |
| H2 | **GPU-Memory-Monitoring** — Concurrency reduzieren wenn VRAM-Limit nahe | HOCH | Mittel |
| H3 | **Frame-Validierung** — Vor Parallel-Verarbeitung pruefen ob extrahierte Frames existieren | HOCH | Gering |
| H4 | **OSD-Schwelle** — 20m ist zu permissiv fuer urbane Haltungen; adaptiv auf 10m senken | HOCH | Gering |
| H5 | **Signature-Normalisierung** — Grossschreibung fuer Dedup (aktuell case-sensitiv) | HOCH | Gering |
| H6 | **Benchmark Timeout** — Pro-Haltung Timeout (5min) fehlt; haengender Qwen blockiert alles | HOCH | Gering |
| H7 | **Protokoll-Validierung** — Min 3-Zeichen Code, nicht-leere Beschreibung, Meter-Reihenfolge | MITTEL | Gering |
| H8 | **Stratifiziertes Sampling** — YOLO-Export: Train/Val-Split nach Klassen-Verteilung | MITTEL | Mittel |

---

## I. KNOWLEDGE BASE

### Staerken
- SQLite mit nomic-embed-text Vektor-Embeddings
- RAG-Retrieval mit MinSimilarity-Threshold (0.35)
- Modell-Mismatch-Erkennung (warnt bei verschiedenen Embedding-Modellen)
- Dual-Threshold Dedup (Normal: 0.92, Korrigiert: 0.85)
- Enrichment mit IsKorrigiert-Flag fuer Korrektur-Tracking

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| I1 | **Auto-Approval Policy** — Default "alles genehmigen" ist zu aggressiv; Safe-Preset erstellen | HOCH | Gering |
| I2 | **Traceability** — CaseId + Timestamp bei auto-genehmigten Samples fehlt | MITTEL | Gering |
| I3 | **MinSimilarity konfigurierbar** — 0.35 hardcoded; 0.40-0.45 getestet empfohlen | MITTEL | Gering |
| I4 | **Feedback-Dedup** — Gleiche Korrektur 3x = 3x Trainings-Signal (Bias-Risiko) | MITTEL | Gering |
| I5 | **Feedback-Decay** — Alte Korrekturen (>30 Tage) mit 0.5x gewichten | NIEDRIG | Gering |
| I6 | **Embedding-Retry** — Kein Retry bei HTTP-Fehler; 2x mit 500ms Backoff empfohlen | NIEDRIG | Gering |

---

## J. KOSTENRECHNUNG

### Staerken
- 3-stufiger Katalog-Fallback (User → Legacy → Seed)
- Self-Healing bei korruptem Katalog
- Korrekte Berechnung: SubTotal → -Rabatt → -Skonto → +MWST
- DN-abhaengige Preisauswahl (naechster Bereich)

### Empfehlungen

| # | Empfehlung | Prioritaet | Aufwand |
|---|-----------|------------|---------|
| J1 | **FormelEvaluator Substring-Bug** — Variable "qty" ersetzt Substring in "quantity" | HOCH | Mittel |
| J2 | **Division durch 0** — FormelEvaluator gibt 0 zurueck statt Fehler | HOCH | Gering |
| J3 | **File-Lock** — user_catalog.json nicht gegen parallele Zugriffe geschuetzt | HOCH | Gering |
| J4 | **Input-Validierung** — DN >0, Laenge >=0, Rabatt/Skonto/MWST 0-100% pruefen | MITTEL | Gering |
| J5 | **Rundungsfehler** — Bei Multi-Zeilen-Offerten kumulativ; Bankrundung (MidpointRounding.ToEven) empfohlen | NIEDRIG | Gering |

---

## ZUSAMMENFASSUNG: TOP 20 PRIORITAETEN

### Sofort umsetzen (Kritisch)
1. **E1** — Retry-Logik mit Polly fuer OllamaClient
2. **E2** — Circuit Breaker (5 Timeouts → Fast-Fail)
3. **D1** — FormelEvaluator: Substring-Bug + Division/0 fixen
4. **E4** — Off-by-One in MultiModelAnalysisService Frame-Sampling
5. **H1** — Atomare Writes fuer TrainingSamplesStore

### Kurzfristig (Naechster Sprint)
6. **A1** — Statusleiste mit Projekt/Seite/KI-Status
7. **B2.1** — Konfidenz-Gauge fuer Sanierungsempfehlung
8. **B4.1** — VSA Zustandsklassen-Verteilung als Diagramm
9. **F1** — VRAM-Watermark-Monitoring im Sidecar
10. **H4** — OSD-Schwelle von 20m auf 10m senken

### Mittelfristig (Naechste 4-6 Wochen)
11. **B1.1** — Druckvorschau fuer alle PDF-Exporte
12. **B3.1** — Rohrquerschnitt-Visualisierung mit Wasser-Fuellstand
13. **B5.1** — Foto-Vorschau im Protokoll-Editor
14. **B6.1** — Suchfelder im VSA-Code-Explorer
15. **D8** — Inhaltsverzeichnis im Haltungsdossier-PDF

### Laengerfristig (Nice-to-have)
16. **E7** — Signal-Widerspruchserkennung im Quality Gate
17. **D5** — Echter Excel-Export mit ClosedXML
18. **A6** — Navigations-Historie mit Zurueck-Button
19. **B7.1** — Druck-Dialoge von Dark auf Light Theme
20. **G4** — Hold-Out Test-Set fuer Weight-Learning

---

## ARCHITEKTUR-STAERKEN (Beibehalten!)

Diese Patterns sind vorbildlich und sollten nicht geaendert werden:

- **Thin-AI Prinzip** — C# fuer Geschaeftslogik, LLM nur fuer Textgenerierung
- **Eskalations-Mechanismus** — 8B → 32B bei Unsicherheit/Severity>=4
- **Evidence-Vektor** — 9-dimensionale Konfidenz-Fusion
- **PDF-Protocol-First Training** — Ground-Truth aus menschlichen Protokollen
- **Merge-Engine mit UserEdited-Schutz** — Nutzereingaben werden nie ueberschrieben
- **Debounced Settings-Save** — 750ms Debounce verhindert exzessives I/O
- **Atomare Projektdateien** — Temp-File → Rename Pattern
- **Few-Shot Library** — Automatisch aus hochqualitativen Matches befuellt
- **Dual-Threshold Dedup** — Normal (0.92) vs. Korrigiert (0.85) in KB

---

*Audit abgeschlossen. 80+ Dateien geprueft ueber alle 10 Bereiche.*
*Naechster Schritt: Prioritaeten 1-5 (Sofort) als Issues erstellen und umsetzen.*
