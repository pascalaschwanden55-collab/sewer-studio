# Standortbestimmung KI-Pipeline SewerStudio — Stand 2026-08-08

Vollständige Spezifikation der KI-Verarbeitungskette, des Trainingswegs und
des Messwesens. Alle Zahlen sind aus den gebundenen Belegdateien gezogen, nicht
geschätzt. Wo eine Aussage unsicher ist, steht das ausdrücklich dabei.

---

## 0. Die Kurzfassung (für den eiligen Leser)

| Frage | Antwort heute |
|---|---|
| Läuft eine KI-Pipeline produktiv? | Ja, die **Kette** läuft (DINO → SAM → Qwen → Code-Mapping → QualityGate). |
| Ist ein eigenes Erkennungsmodell (YOLO) freigegeben? | **Nein.** Das Altmodell ist als *nicht qualifiziert* gesperrt, alle 17 Kandidaten sind `not_deployed`. |
| Was funktioniert nachweislich? | **BCC_bogen** (Bogen). Ein Einzelklassen-Modell findet 26–31 von 37 Sollboxen bei 1–7 % Fehlalarm. |
| Was funktioniert eingeschränkt? | **BCA_anschluss** (21–25 % Recall), **BAH_schadanschluss** (52–67 % Recall, aber kleine Stichprobe). |
| Was ist belegt gescheitert? | **BAB_riss** (Auflösungsgrenze) und **BAF_oberflaeche** (falsche Klassendefinition). Sammlung eingestellt. |
| Was ist der größte offene Posten? | Ein **frischer, unberührter Release-Holdout**. Der bisherige ist als Abnahmebestand verbraucht. |
| Wie viel Handarbeit steckt drin? | **1802 Goldsamples** in der Datei, davon **1728 verwendbar**, **1214** im aktiven Trainingsregister. |
| Nächster Entscheid bei Pascal | Zwei weitere Trainings-Seeds für das BCC-Artefakt (~8 h GPU) **oder** Einbau auf einem Lauf. |

**Der ehrliche Satz:** Wir haben eine sehr solide, mehrfach abgesicherte
Infrastruktur und ein einziges wirklich brauchbares Erkennungsergebnis (Bogen).
Der Weg vom „Assistenten für eine Klasse" zum „Assistenten für viele Klassen"
ist offen und teilweise durch die Bildqualität der Bestandsvideos begrenzt.

---

## 1. Hardware und Laufzeitumgebung

| Komponente | Spezifikation |
|---|---|
| Prozessor | Intel Core Ultra 9 285K |
| Grafikkarte | ASUS RTX 5090, 32 GB VRAM |
| Arbeitsspeicher | 64 GB DDR5 |
| Betriebssystem | Windows 11 Pro |
| App | WPF / .NET 10, MVVM |
| Sidecar | Python FastAPI, `127.0.0.1:8100` (nur Loopback erzwungen) |
| Wissenswurzel | `C:\KI_BRAIN` |
| Spiegel | Datenträger `Elements` → `Brain` (Echtzeit-Abgleich) |

**VRAM-Regel:** maximal 29 GB dürfen belegt werden, niemals alle Modelle
gleichzeitig. Das Training sperrt sich selbst, wenn weniger als 28 000 MB frei
sind oder der Sidecar läuft.

**Absicherung des Sidecars:** Der Host muss eine Loopback-Adresse sein
(erzwungen per Validator). Zugriff nur mit geteiltem Token
(`%LOCALAPPDATA%/SewerStudio/.sidecar_token`). Bildgrößen sind gedeckelt:
25 MB je Bild, 50 Mio. Pixel.

---

## 2. Die produktive Verarbeitungskette (Ist-Zustand)

### 2.1 Grundprinzip

**Thin-AI:** C# macht die gesamte Geschäftslogik, die Modelle liefern nur
Rohsignale. Das LLM (Qwen) erzeugt Text, keine Entscheidungen. Diese Trennung
ist das wichtigste Architekturprinzip und wurde nie gebrochen.

### 2.2 Ablauf einer Videoanalyse

```
UI / Service
  └─ VideoAnalysisPipelineService      (wählt Multi-Model- oder Fallback-Pfad)
       └─ MultiModelAnalysisService
            └─ VisionPipelineClient  ──HTTP──►  Sidecar :8100
                                                 ├─ YOLO   (gesperrt, s. 2.4)
                                                 ├─ Grounding DINO
                                                 ├─ SAM 2.1
                                                 └─ YOLO-cls
            └─ Ollama (eigener Prozess)  ──►  Qwen3-VL
       └─ VSA-Code-Mapping (C#)
       └─ TemporalFindingDeduplicator + TemporalCodeVotingService
       └─ QualityGateService  →  Grün / Gelb / Rot
```

### 2.3 Die Modelle im Einzelnen

| Modell | Aufgabe | Stand |
|---|---|---|
| **YOLO Detect** (`yolo26m.pt`) | Objekterkennung Schadensklassen | **gesperrt**, `qualified=false` |
| **YOLO-cls** (`vsa_cls_v5_nocrop`) | Bildklassifikation VSA-Code | aktiv, seit 2026-06-10 freigegeben |
| **Grounding DINO** | textgesteuerte Objektsuche | aktiv, Swin-B bevorzugt, Swin-T als Rückfall |
| **SAM 2.1** (`sam2.1_hiera_large.pt`) | Segmentierung (Maske aus Box) | aktiv |
| **Qwen3-VL** über Ollama | Bildbeschreibung, Textgenerierung | aktiv, 8b-q8 ab 24 GB VRAM, sonst 2b |

**Feste Parameter im Betrieb:**

- YOLO: `conf = 0,25`, `imgsz = 1280` (nicht 640 — kleine Schäden brauchen die
  höhere Auflösung)
- DINO: Box-Schwelle 0,25, Text-Schwelle 0,20, 38 englische Suchbegriffe
  (crack, root intrusion, deposit, pipe bend, …)
- SAM: Mindest-Score 0,5 — schlechtere Masken werden verworfen, nicht still
  übernommen
- YOLO-cls: `imgsz = 1024`, Letterbox-Vorverarbeitung

**Bewusst abgeschaltet:**

- `bend_geometry_enabled = false` (geometrische Bogenerkennung im SAM-Pfad)
- `sam3_enabled = false` (SAM 3 nur als deaktivierte Experiment-Option)
- SAM 1 (`vit_h`) ist vollständig entfernt

**Nicht vorhanden (nicht als Ist-Zustand behandeln):**
ByteTrack, OC-SORT, echtes Multi-Object-Tracking, `DetectionAggregator`,
meterbasierter Merge-Radius, `InferenceOrchestratorService`, produktiver
`KbDeduplicationService`, automatische 8B→32B-Eskalation.

### 2.4 Warum YOLO gesperrt ist

Am 2026-07-25 wurde das aktive Detect-Modell (Stand 2026-04-11) als **nicht
qualifiziert** markiert. Begründung in `sidecar/models/model_qualification.json`:

> „BBox-Kollaps bei verschiedenen Bildern (nahezu identische Box), alter
> Trainingsdatensatz fehlt — als Qualitätsnachweis nicht geeignet."

Die Sperre ist *fail-closed* gebaut:

- Nur ein ausdrückliches `qualified=true` gibt das Modell frei.
- Bei `false`, fehlendem Feld **oder Lesefehler** bleibt es gesperrt.
- Die Freigabedatei bindet PT, TensorRT-Engine und ONNX je an Dateiname **und**
  SHA-256. Jede Abweichung sperrt.
- `/health` meldet den Sidecar als `degraded` samt `detector_qualification`.
- Batch-Video und Player-Einzelframe verwenden YOLO weder als Frame-Filter noch
  als Konfidenz-Beweis. DINO und SAM laufen normal weiter, die Ampel bleibt
  orange und verlangt manuelle Prüfung.
- Das Training Studio sperrt den Fototest mit dem Standardmodell.

Prüfwerkzeug: `training/scripts/model_collapse_check.py` — schreibfrei, prüft
Box-Statistik, IoU gegen Gold-Boxen, Aktivierungen auf Negativen, optional mAP.
Ein echter Kollaps ergibt `FAIL`; zu wenig Daten ergibt `INCONCLUSIVE`, nie
einen falschen `PASS`.

### 2.5 GPU-Verwaltung im Sidecar (Paket 2, gehärtet)

- **Besitzbasierte Leases:** `acquire_busy` / `release_busy` mit UUID-Besitzer.
  Reihenfolge ist verbindlich: Predict-Lock **zuerst**, Lease **danach**. Nur
  der Besitzer entfernt seine eigene Lease. Wartende können weder Uhr noch
  Zustand verschieben.
- Einheitlich für YOLO (GPU und CPU als logische Lease `YOLO_CPU`), DINO, SAM,
  BCC-Test und YOLO-cls (`YOLO_CLS`).
- **Atomare VRAM-Freigabe:** Auswahl, letzte Lease-Prüfung und Reservierung
  laufen unter einem kurzen globalen Lock. Modellreferenzen, `empty_cache` und
  GC werden *danach* ohne Lock aufgeräumt — so bleibt Health/Watchdog auch bei
  hängender CUDA-Bereinigung ansprechbar.
- **Gleichzeitige Ladevorgänge** sind über In-flight-Reservierungen koordiniert:
  zwei Ladungen sehen nie denselben freien Speicher.
  Effektiv frei = frei − laufende Reservierungen.
- `unload` verweigert bei laufender Inferenz. Kein sicherer Kandidat →
  HTTP 503 mit `insufficient_vram` und den Zahlen free/required/reserved_gb.
- Der Testkandidat läuft in einem eigenen Slot `YOLO_TEST` und ersetzt den
  produktiven Zeiger nie.

### 2.6 Ausfallschutz auf C#-Seite

| Mechanismus | Verhalten |
|---|---|
| `SidecarOutageGuard` | 8 Folge-Frames mit Transportfehler → Lauf bricht *degraded* ab |
| `QwenOutageTracker` | Ollama ist ein eigener Prozess: ab 8 Folgefehlern nur eine Degraded-Notiz |
| Nutzerabbruch | wird sofort weitergeworfen, zählt **nie** als Ausfall |
| >10 % übersprungene Frames | setzt `Incomplete = true` am Ergebnis |
| `SidecarInsufficientVramException` | eigener Kapazitätsfehler: kein Retry, kein Neustart, kein Outage-Zählen |
| `model_unloaded` | bleibt gezielt wiederholbar |
| unbekanntes 503 | bleibt Transportfehler |
| `SidecarRequestTimeoutException` | interner Timeout, getrennt vom Benutzerabbruch |

**Sidecar-Neustart** (`SidecarRestartService`): höchstens **ein** Versuch je
Analyselauf, und nur für den **eigenen** Prozess. Prozess-Tracking über
PID + Startzeit + Prozessart + Programmpfad. Nur die ausdrückliche Art
`Sidecar` erlaubt einen Kill; `Unknown`, Ollama oder ein abweichender Pfad
sperren. Erfolg gilt erst nach zwei aufeinanderfolgenden Health-Abfragen.

### 2.7 Checkpoint und Fortsetzung

`AnalysisCheckpointJournal` — eine anfügende JSONL-Datei je Video, benannt nach
dem Kurz-Hash des Videopfads. Jeder Frame schreibt genau einen Zustand:

- `update` — mit Befunden
- `advance` — normal übersprungen
- `retry_required` — Transport-, Modell- oder Verarbeitungsfehler

Eine Fortsetzung übernimmt **nur den lückenlosen Anfang ab Frame 1** und spielt
ihn exakt über den Deduplikator nach. Dadurch liefert Abbruch + Fortsetzung
dieselben Befunde wie ein durchgehender Lauf. Fehlende, doppelte oder
rückwärtslaufende Frame-Nummern verwerfen die Fortsetzung vollständig.
Fehlende Pflichtfelder werden **nicht** durch Standardwerte erfunden.

### 2.8 Zusammenführung und Bewertung

- `TemporalFindingDeduplicator` — framebasiert, `MeterMergeGapMaxMeters = 1,0`,
  gespeist aus der OSD-Metrierung.
- `TemporalCodeVotingService` — zeitliche Abstimmung über den Code.
- `QualityGateService` — Grün/Gelb/Rot aus den verfügbaren Belegsignalen. Läuft
  in jedem Pfad durch, ohne Ausnahme.
- `RawVideoDetection.SeverityLevel` trägt zusätzlich die exakte Stufe 1–5.

---

## 3. Wie Goldwissen entsteht (die Datenquelle)

Das ist der eigentliche Kern der Arbeit der letzten Monate. Alle
Trainingsdaten stammen aus **persönlich bestätigten Handlabels** — es gibt
keinen automatischen Weg in den Goldbestand.

### 3.1 Das Gold-Gate (nicht verhandelbar)

Ein Sample wird nur dann `Approved` (Gold), wenn **alle** Bedingungen erfüllt sind:

1. Quelle ist `ManualCoding` oder streng belegtes `PdfPhoto`
2. Bild vorhanden und lesbar
3. Randgültige Hand-Box (rot gezogen, vom Menschen)
4. Gültige SAM-Segmentierung: RLE strikt dekodierbar, Laufsumme = Breite × Höhe,
   Maskenmaße = echte Bildmaße, mindestens ein gesetztes Pixel
5. **Mindestens 80 % der Maskenpixel liegen in der Hand-Box**
6. Beschreibung ist kein Platzhalter (`GoldBeschreibungGuard`)
7. `ConfirmedByUser` stimmt exakt mit `ApprovedBy` der Registry überein
8. Persönliches Akzeptieren im UI

Fehlt die gültige Maske, entsteht nur ein **Entwurf** (gelb, kein KB-Eintrag,
kein Teacher-Eintrag). Der Entwurf landet in der Warteschlange
„Unvollständige Goldframes" zur Reparatur.

Das Bild wird beim Bestätigen **inhaltsadressiert** kopiert nach
`gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>`. Das Kundenoriginal
bleibt immer unverändert. Scheitert die Kopie, wird gar nichts gespeichert.

### 3.2 Die fünf Wege, wie ein Goldsample entsteht

| Weg | Beschreibung |
|---|---|
| **Training Studio (Prüfplatz)** | Box ziehen → SAM → Codevorschlag → VSA-Code → Akzeptieren |
| **PDF-Protokoll-Import** | Operateurfotos aus Kunden-PDFs; Code kommt als Referenz mit, muss aber neu bestätigt werden |
| **Player-Codiermodus** | Rechteck im Video, SAM-Maske 3 s sichtbar, dann VSA-Codierfenster |
| **Foto-Assistent** | Handmarkierung an einem Foto einer bereits offenen VSA-Beobachtung |
| **Gold-Eingang** | Bilder in `training/gold_inbox` vorbereiten, dann durch den Prüfplatz schleusen |

Für den PDF-Weg gelten zusätzliche Regeln: Das PDF wird nur gelesen und vor und
nach der Extraktion per SHA-256 kontrolliert. Als sichere Zuordnung gelten in
dieser Reihenfolge: Code im selben Fotoblock → exakte Foto-ID/Dateiname →
vollständige Kombination aus Videozeit, Meter und normalisiertem Befundtext.
Unsichere Bilder werden übersprungen. Große Protokolle stoppen fail-closed bei
über 256 MiB extrahierten Fotobytes oder 250 Mio. Pixeln. CMYK-JPEGs werden
über einen eigenen Farbnormalisierer in sichtbares RGB umgewandelt, sonst
ausgelassen.

### 3.3 Sondermaßnahmen an den Golddaten (Chronologie)

| Datum | Maßnahme | Umfang |
|---|---|---|
| 2026-07-25 | Trennung Entwurf/Gold eingeführt, strenge Maskenprüfung | Grundsatzänderung |
| 2026-07-25 | Mehrfachobjekte je Bild unterstützt (Signatur mit Box) | Grundsatzänderung |
| — | Gold-Gehirn-Trennung (`tools/GoldBrainSeparation`) | Altbestand archiviert, neues Gehirn nur Handlabels |
| — | Archiv-Nachholung (`PersonalGoldArchiveRecoveryService`) | bestätigte Fälle aus der Alt-KB zurückgeholt |
| 2026-08-03/04 | PDF-Gold-Haltungs-IDs repariert | **169 repariert, 13 dekontaminiert** |
| 2026-08-05 | 9 ManualCoding-Samples mit Alt-CaseIds repariert | Byte-Beweis über Provenienz-PDF |
| 2026-08-06 | Inbox-Reparatur | **246 Haltungsnummern gesetzt, 15 dekontaminiert** |
| 2026-08-06 | 19 Samples ohne prüfbare Haltung auf `Draft` gesetzt | inkl. KB-Deindex und Teacher-Bereinigung |

Jede Reparatur läuft über einen **eindeutigen bytegleichen SHA-256-Treffer**.
Der Standardlauf ist schreibfrei; `--execute` verlangt eine ruhige Datenbank,
sichert JSON und SQLite gemeinsam und ändert Kundenbilder nie.

### 3.4 Warum das wichtig war

Die falschen Haltungsnummern waren kein Schönheitsfehler. Der Eval-Schutz
arbeitet über Haltungsnummern — mit falschen Nummern konnten Testbilder
unbemerkt ins Training rutschen. Genau das ist passiert (siehe 6.3).

---

## 4. Der Trainingsweg (plan-gesteuerter Export)

Seit AP 0.3 ist der Export **plan-gesteuert**. C# erzeugt genau einen
unveränderlichen Plan; Sidecar und lokaler Ausführer schreiben nur diesen Plan
und treffen keine eigene Entscheidung über Klassen, Split, Quarantäne oder
Dateinamen.

```
TrainingCenter (dünne UI-Hülle)
  → ITrainingYoloExportCoordinator
  → export_registry_v1.json lesen (freigegeben)
  → TrainingDataInventoryRuntimeSnapshot erzeugen
  → class_map v3 strikt lesen
  → ITrainingExportPlanService: genau ein Plan
  → ITrainingExportExecutionService: Sidecar ODER lokaler Ausführer
  → ITrainingExportCompletionService: nur bestätigte Samples markieren
```

**Eigenschaften des Plans:**

- pfadfrei; enthält feste Klassen, Haltungs-Splits, Ausschlüsse, Quell-Hashes
  und stabile Dateinamen `img_<sha256>.<endung>`
- gleiche Bild-SHAs werden einmal geschrieben, unterschiedliche Labels
  zusammengeführt
- beim Runden auf sechs YOLO-Nachkommastellen werden randbündige Boxen minimal
  nach innen begrenzt, damit keine gültige Box durch reine Rundung ungültig wird
- Schreibvorgang zuerst unter `.staging`, dann atomare Veröffentlichung nach
  `<KnowledgeRoot>\training\datasets\<plan_id>`
- unvollständige oder abweichende Ziele werden nie repariert oder ersetzt
- `plan_sha256` muss `plan_id` entsprechen
- HTTP-Wiederholungen sind idempotent; ein neuer Exportbefehl ist ein neuer
  Kandidat

**Die Fixture** unter `tests/Fixtures/TrainingExport/` führt Train, Dev-Val und
ein Multi-Label-Bild durch **beide** Ausführer. Relative Pfade, SHA-256 und
Bytes aller Ausgabedateien müssen identisch bleiben.

### 4.1 Die Klassenkarte v3

| | |
|---|---|
| Aktive Version | **v3**, 15 feste Klassen |
| Eingefroren | v2, 14 Klassen, 124 Migrationszeilen |
| Migrationszeilen v3 | **142** (92 Teacher-Codes, 35 Legacy-Schlüssel, 10 Modellnamen, 5 Annotation-Overrides) |
| Freigegeben | 73 Zeilen `approved` (60 `map` + 12 `discard` + 1 Legacy), 69 `pending` |
| Beobachtete Quellcodes in der Freigabe | 88 |
| SHA-256 Klassenkarte | `58f1160f2411d5a583bd7a69d3b739be9d29ef7dce33052e61d583fa773a7468` |
| VSA-Manifest-Hash | `6732ff859cc9ed919f08d045b81f6b8a82e15105cc9a29213e91cc5f64b0bd38` |

Die 15 Klassen: `BCA_anschluss`, `BAB_riss`, `BAC_bruch`, `BAA_verformung`,
`BAF_oberflaeche`, `BAH_schadanschluss`, `BAI_dichtung`, `BAJ_verbindung`,
`BBA_wurzeln`, `BBB_anhaftung`, `BBC_ablagerung`, `BBD_boden`,
`BBF_infiltration`, `SONST_schaden`, `BCC_bogen` (feste ID 14).

**Unbekannte oder offene Klassen stoppen den Export hart.** Es gibt keine stille
neue ID und keinen automatischen SONST-Rückfall.

### 4.2 Negativbilder (Bilder ohne Schaden)

Negative sind seit 2026-07-25 im gemeinsamen Detect-Plan angeschlossen. Neue
Läufe verwenden nur ausdrückliche `--negative-set`-Ordner unter
`training/negatives/sets`. Deren Manifest bindet Bild, echte Haltung, festen
Split, All-Class-Review, Queue, Kandidatenliste und class_map v3.

| Satz | Bilder | Herkunft |
|---|---:|---|
| `bcc_hn_54f6608b975a` | 10 | BCC-Fehlalarme, All-Class-reviewt (8 Train / 2 Val) |
| `bcc_hn_c25fd2f9d33f` | 9 | abgeleiteter Satz für den Gold-Audit (7/2) |
| **`proto_hn_fefb59779b86`** | **286** | protokollbasierte Sammlung, **aktuell im Register** (229/57) |

Der Review kennt nur drei Urteile: `all_classes_clear`, `mapped_object_visible`,
`exclude_uncertain`. Das alte Holdout-Urteil `negative` ist ausdrücklich **kein**
Trainingsnegativ — es hieß nur „kein Bogen", nicht „kein Schaden".

---

## 5. Der aktuelle Datenstand (Zahlen von heute)

### 5.1 Goldbestand (Audit `gold_stock_audit_20260806_154328_776.json`)

| Prüfstufe | Anzahl |
|---|---:|
| Samples in der Datei | 1802 |
| übersprungen (Entwurf) | 34 |
| eingelesen | **1768** |
| persönlich bestätigt | 1763 |
| Bild, Box, Maske je in Ordnung | 1763 |
| Code im Goldkatalog | 1732 |
| eval-sauber | 1728 |
| **final verwendbar** | **1728** |

Verworfen wurden Hauptcodes außerhalb des persönlichen Goldkatalogs (BCB, BAG,
AEC, BDB) und einzelne unvollständige PDF-Prüfspuren.

**Verteilung nach Hauptcode (verwendbare Samples):**

| Code | Anzahl | | Code | Anzahl |
|---|---:|---|---|---:|
| BAB (Riss) | 274 | | BAC (Bruch) | 54 |
| BCA (Anschluss) | 250 | | BDD | 47 |
| BCC (Bogen) | 229 | | BBC (Ablagerung) | 46 |
| BCE (Rohrende) | 120 | | BAI (Dichtung) | 39 |
| BAH (schadh. Anschluss) | 118 | | BBA (Wurzeln) | 32 |
| BCD (Rohranfang) | 109 | | BBB (Anhaftung) | 25 |
| BAF (Oberfläche) | 96 | | AED | 21 |
| BDA | 88 | | | |
| BAJ (Verbindung) | 63 | | | |
| BAA (Verformung) | 62 | | | |
| BBF (Infiltration) | 55 | | | |

Der Split folgt der Regel `sha256('split-v1|<Gruppe>')`, Ziel 70/15/15. Gruppe =
normalisierte Haltung; identische Bilder verbinden Haltungen. Der Test-Anteil ist
eingefroren und nur markiert. Status: **release-fähig**, 0 fehlende
Haltungsidentitäten.

### 5.2 Aktives Trainingsregister `DETECT_ALL`

| | |
|---|---|
| Freigegeben von | Besitzer, 2026-08-06 15:45 UTC |
| Gewählte Goldbilder | **1214** (996 Train / 218 Validation) |
| Verworfen | 336 |
| Testbilder ausgeschlossen | 178 |
| Negativbilder | 286 (`proto_hn_fefb59779b86`) |
| Quelle | `training_samples.json`, SHA-256 `502f8d84…` |
| Gebundener Audit | `gold_stock_audit_20260806_154328_776.json`, SHA-256 `04f405ac…` |

**Boxen je Klasse im Register:**

| Klasse | Boxen | | Klasse | Boxen |
|---|---:|---|---|---:|
| BAB_riss | 252 | | BAJ_verbindung | 53 |
| BCA_anschluss | 226 | | BBF_infiltration | 53 |
| BCC_bogen | 206 | | BAA_verformung | 51 |
| BAH_schadanschluss | 104 | | BAC_bruch | 48 |
| BAF_oberflaeche | 85 | | BBC_ablagerung | 45 |
| BAI_dichtung | 38 | | BBA_wurzeln | 29 |
| | | | BBB_anhaftung | 24 |

`BBD_boden` und `SONST_schaden` haben 0 Boxen — die Klassen existieren, sind
aber nicht belegt.

### 5.3 Datensätze und Kandidaten

10 exportierte Datensätze unter `training/datasets`, 17 Kandidaten unter
`training/models/candidates`. **Alle Kandidaten sind `not_deployed`.** Kein
einziges eigenes Detect-Modell ist jemals aktiviert worden.

---

## 6. Das Messwesen

Das ist der Teil, der am meisten Arbeit gekostet hat — und der am wenigsten
sichtbar ist.

### 6.1 Grundregeln

- Festes Protokoll für alle Standbild-Messungen: **`conf = 0,25`,
  `imgsz = 1280`, `IoU = 0,5`**
- Zuerst entsteht ein **labelblinder, SHA-gebundener Vorhersagebeleg**. Erst
  danach wird die Review geladen und ausgewertet.
- Technische Fehler zählen **nie** als Negativtreffer. Ein Teilfehler verhindert
  den Abschlussbericht.
- Das Messwerkzeug läuft bei ausgeschaltetem Sidecar, aus einer privaten
  hashgeprüften Momentaufnahme des Gewichts.
- Kein Messwerkzeug kann ein Modell trainieren, aktivieren oder freigeben.

### 6.2 Die Prüfbestände

| Bestand | Umfang | Rolle heute |
|---|---|---|
| `detect_release_holdout_45b66da2c778` | 400 Bilder (241 pos. / 74 neg. / 85 ausgeschl.) | **verbraucht** als Abnahmebestand |
| `detect_benchmark_extension_v1` | 17 Bilder (14 BAH + 3 BAJ) | Erweiterung, blind reviewt, 16/17 positiv |
| **`detect_benchmark_v1`** | **417 Bilder**, 417 Entscheidungen | **aktuelle Entwicklungs-Messlatte** |
| `bcc_release_holdout_64d06094c921` | 60 Bilder / 60 Haltungen (29 pos. / 31 neg.) | BCC-Binärbewertung, `ready_for_binary_evaluation` |
| Eval-Set V1 | 120 Frames | Basis für die ereignisbasierte Messung (AP 0.4) |

`detect_benchmark_v1` ist eine Byte-Union ohne Kollisionen (0 Überlappungen
geprüft), holdout_id `55cabe4fc444b47f…`, an Kandidat `detect_gold_3f45c1e945fe`
und Klassenkarte v3 gebunden. Status ehrlich: `coverage_incomplete` — BAC 15,
BBA 10, BBB 8, BBC 19, BBD 0, BBF 16, SONST 4 liegen unter der 20er-Regel. Die
Kandidatenklassen BAH 21, BAJ 20, BAI 26, BCA 53, BCC 37 sind gedeckt.

**Die Freigaberegel** (`ready_for_detect_evaluation`) verlangt: vollständige
Review, mindestens 20 Instanzen je Klasse, 75 echte Negativbilder und 30
negative physische Haltungen. Diese Grenzen dürfen nur erhöht werden.

### 6.3 Die aufgedeckte Kontamination

Am 2026-08-03 fand der schreibfreie Prüflauf `repair_pdf_gold_holding_ids.py`
**239 PDF-Goldsamples mit falscher Haltungs-ID**. Dreizehn davon zeigen
byte- bzw. ordnerbelegt auf zwei Haltungen des damaligen Holdouts
(`07.148371-10300` und `60604-60603`). **Acht davon standen im Trainingsregister**,
mit dem `detect_gold_9eb020e30322` trainiert wurde.

Konsequenz: Der Holdout war für diesen Kandidaten nicht unabhängig. Die
gemessenen Werte (Recall 10,3 %, F1 16,2 %) sind **nach oben verzerrt**. Am
Urteil `not_release_qualified` ändert das nichts — es verschärft es.

Das ist der Grund, warum ein **frischer Release-Holdout** heute Pflicht ist.

### 6.4 Menschliche Prüfplätze

Für jeden Messschritt gibt es einen eigenen lokalen Browser-Prüfplatz, der
**keine Modellvorhersagen zeigt**:

| Prüfplatz | Zweck | Urteile |
|---|---|---|
| `detect_release_holdout_review_server.py` | Boxen für den Release-Holdout | positive / negative / exclude |
| `bcc_hard_negative_review_server.py` | Harte Negative sammeln | all_classes_clear / mapped_object_visible / exclude_uncertain |
| `detect_gold_error_review_server.py` | Fehlfälle diagnostizieren | confirmed_model_error / gold_suspect / exclude_uncertain |
| `bcc_video_fehlalarm_review_server.py` | Video-Meldungen blind beurteilen | Bogen / kein Bogen / unsicher |
| `start_eval_metadata_review.ps1` | Stufe + Ereignis-ID nachpflegen | bestätigen / korrigieren / ausschließen |

Alle binden Bericht, Ledger, Kandidatenmanifest, Gewicht, Gold-Audit,
Trainingssamples und Klassenkarte per SHA-256 und prüfen vor jeder Entscheidung
erneut. Browser-Revision und Dateisperre verhindern stilles Überschreiben durch
zwei Tabs.

### 6.5 Die Fehlfall-Analyse

Aus dem korrigierten Bericht entstand die eingefrorene Queue
`detect_gold_failure_a46a82535c82`: **80 Fälle auf 67 Bildern** (56 verpasst,
8 falsche Klasse, 16 zusätzliche KI-Boxen). Die Review ist abgeschlossen:
80/80 Entscheidungen, davon **75 bestätigte Modellfehler, 0 Gold-Verdachtsfälle,
5 Ausschlüsse**.

Daraus wurde der Sammelplan `detect_gold_collection_874ec160e346` veröffentlicht:
60 positive Fehlerhinweise, 15 Fehlalarm-Hinweise, 6 Verwechslungen in
4 Klassenpaaren. Der Plan enthält **keine** Bildpfade, Hashes oder IDs — nur
aggregierte Klassenziele.

### 6.6 Ereignisbasierte Messung (AP 0.4a)

Die technische Grundlage steht: `EvalSetEventScorer` zählt ein Schadensereignis
über mehrere Frames nur einmal (Schlüssel: Haltung + EventId). Detect-Treffer
und nachgelagertes Gate werden getrennt ausgewiesen. Für Severity 4/5 gilt ein
Mindestumfang von 20 unabhängigen Ereignissen; Wilson- und exakte
95-%-Fehlergrenzen werden ausgegeben.

**AP 0.4 ist nicht abgeschlossen:** Das 120er-Set ist noch nicht vollständig
menschlich mit Stufe und Ereignis-ID nachgepflegt.

---

## 7. Die Messergebnisse (der harte Teil)

### 7.1 Der methodische Hauptbefund

> **Die Streuung zwischen zwei Trainingsläufen ist größer als die Effekte, die
> wir messen wollen.**

Belegt an `BCC_bogen`: Die Daten änderten sich praktisch nicht (99 → 105 → 104
Haltungen), das Ergebnis schwankte trotzdem zwischen **22 und 30 von 37**
Treffern — 59 % bis 81 % Recall, allein durch Zufall im Training.

| Lauf | BCC-Treffer von 37 |
|---|---:|
| `detect_gold_9eb020e30322` | 27 |
| `detect_gold_3f45c1e945fe` (Kurzlauf) | 29 |
| `..._lang` (Batch 8, 205 Epochen) | 30 |
| `detect_gold_61370615b1c1` (Geduld 20) | 24 |
| `..._geduld` (Geduld 80, 166 Epochen) | 22 |

**Konsequenz, seither verbindlich:** Jede Aussage „X hat geholfen" braucht
mehrere Seeds je Bedingung oder einen Effekt größer als diese Spanne.
Einzellauf-Vergleiche gelten nicht mehr als Beweis.

### 7.2 Referenzmessung Mehrklassenmodell (3 Seeds, 2026-08-07)

Datensatz `61370615b1c1`, 1359 Bilder (1101 Train / 258 Val), 1206 Instanzen.
Gemessen gegen `detect_benchmark_v1`, 257 gewertete Bilder, 379 Sollboxen.

| | Seed 42 | Seed 43 | Seed 44 |
|---|---:|---:|---:|
| TP | 58 | 74 | 50 |
| FP | 93 | 105 | 54 |
| FN | 321 | 305 | 329 |
| Precision | 38,4 % | 41,3 % | 48,1 % |
| **Recall** | **15,3 %** | **19,5 %** | **13,2 %** |
| F1 | 21,9 % | 26,5 % | 20,7 % |
| Fehlalarm-Bildrate (75 Negative) | 22,7 % | 18,7 % | **8,0 %** |

**Recall je Klasse (Sollboxen in Klammern):**

| Klasse | Soll | Seed 42 | Seed 43 | Seed 44 |
|---|---:|---:|---:|---:|
| BCC_bogen | 37 | 64,9 % | **75,7 %** | 62,2 % |
| BAH_schadanschluss | 21 | 61,9 % | **66,7 %** | 52,4 % |
| BAI_dichtung | 26 | 15,4 % | 23,1 % | 11,5 % |
| BCA_anschluss | 53 | 22,6 % | 24,5 % | 20,8 % |
| BAJ_verbindung | 20 | 10,0 % | 20,0 % | 5,0 % |
| BBC_ablagerung | 19 | 0 % | 10,5 % | 0 % |
| BBF_infiltration | 16 | 0 % | 6,3 % | 0 % |
| BAA_verformung | 21 | 0 % | 4,8 % | 0 % |
| BAB_riss | 40 | 5,0 % | 5,0 % | 2,5 % |
| BAF_oberflaeche | 89 | 1,1 % | 3,4 % | 0 % |
| BAC_bruch | 15 | 0 % | 0 % | 0 % |
| BBA_wurzeln | 10 | 0 % | 0 % | 0 % |
| BBB_anhaftung | 8 | 0 % | 0 % | 0 % |
| SONST_schaden | 4 | 0 % | 0 % | 0 % |

Alle drei tragen `status: not_release_qualified`, `release_qualified: false`,
`model_activated: false`.

**Lesart:** Das Mehrklassenmodell hat zwei funktionierende Klassen (BCC, BAH),
eine eingeschränkte (BCA) und elf, die praktisch nichts finden. Die Streuung
zwischen den Seeds ist bei BAH und der Fehlalarmquote erheblich (8 % bis 23 %).

### 7.3 Der Durchbruch: BCC als Einzelklasse

**Paket 1 (2026-08-07) — gefilterter Datensatz.** 233 Bilder (202 mit
BCC-Boxen, 31 echte Negative), Klasse 14 → 0, drei Seeds, 300 Epochen.

| Seed | BCC-Treffer von 37 | Fehlalarm sauberes Rohr | Feuer auf Fremdschaden |
|---|---:|---:|---:|
| 42 | **35** | 31/75 (41 %) | 120/220 (55 %) |
| 43 | **35** | 36/75 (48 %) | 113/220 (51 %) |
| 44 | **35** | 35/75 (47 %) | 117/220 (53 %) |

**94,6 % Recall** — der Einzelklassen-BCC schlägt das Mehrklassenmodell
(23–28/37) um sieben bis zwölf Boxen, deutlich über der Streuspanne.
**Aber:** Auf fast der Hälfte der sauberen Bilder eine Fehlbox. BCC als
Einzelklasse wird zum Sammelbecken für alles Runde und Dunkle.

**Paket 2 (2026-08-07) — Vollhintergrund.** Derselbe volle Export
(1359 Bilder: 202 mit BCC-Box, 1157 Hintergrund aus Fremdschäden und echten
Negativen), `cache=off`.

| conf | Seed 42 | Seed 43 | Seed 44 | Fehlalarm sauber | Feuer auf Fremdschaden |
|---:|---:|---:|---:|---|---|
| 0,05 | 30 | 32 | 32 | 9 / 4 / 4 | 12–15 % |
| **0,10** | 28 | 29 | **31** | **5 / 2 / 4** | **10–12 %** |
| 0,15 | 26 | 26 | 28 | 3 / 1 / 4 | 9–10 % |
| 0,20 | 26 | 25 | 28 | 1 / 1 / 3 | 6–9 % |
| 0,25 | 21 | 25 | 26 | 1 / 1 / 2 | 5–7 % |
| 0,35 | 16 | 24 | 26 | 1 / 1 / 2 | 5–7 % |

**Direktvergleich auf derselben Messlatte:**

| Aufbau | Treffer von 37 | Fehlalarm sauber | Fehlalarm fremd |
|---|---:|---:|---:|
| Mehrklassenmodell | 23–28 | 8–23 % | — |
| Einzelklasse, gefiltert (Paket 1) | 35/35/35 | 41–48 % | 51–55 % |
| **Einzelklasse, Vollhintergrund @0,10–0,15** | **26–31** | **1–7 %** | **7–12 %** |

Der Tausch ist ehrlich: rund 7 Boxen Recall gegen eine **um Faktor 10 bessere**
Fehlalarmquote. Für einen Vorschlags-Assistenten ist das der richtige Tausch.
Der Vollhintergrund wirkt als Ursachenbehandlung — das Modell lernt Fremdschaden
(BAJ, BCE) explizit als Nicht-Bogen.

**Das ist der erste produktiv brauchbare Baustein: 73–84 % Bogen-Recall bei
1–7 % Fehlalarm, ohne eine einzige neue Handlabel-Stunde.**

### 7.4 Der Videoweg (Paket 4 + Korrektur)

**Aufbau:** Modell `bcc_single_fullbg_20260807` Seed 44, 8 Haltungen (goldfrei
gewählt), 1 Frame/Sekunde via ffmpeg, zeitlicher Merge positiver Sekunden zu
Gruppen (Lücke > 3 s trennt). Ein Befund gilt als gefunden, wenn eine Gruppe das
Fenster ±15 s um den Protokoll-Zeitpunkt überlappt.

Ein Video war ein 3,4-Sekunden-Stumpf (Datei defekt). Effektiv: 7 Videos,
10 prüfbare protokollierte Bögen, 49,7 Video-Minuten.

**Laufzeit:** 50–56 fps bei 1-fps-Abtastung — 49,7 Video-Minuten in 55 s
gerechnet plus 21 s Extraktion. **Faktor ~50 über Echtzeit.** Das lässt feinere
Abtastung (2–4 fps) oder ganze Projektbestände zu.

**Die entscheidende Korrektur (2026-08-08):** Der erste Bericht stufte 39 der 64
protokollfremden Meldungen per **KI-Sichtprüfung** als echte, nur nicht codierte
Bögen ein. Die menschliche Blindprüfung aller 64 Meldungen widerlegte das:

- Bestätigt wurden **15** echte Bögen (43 „kein Bogen", 4 „unsicher")
- Von den 39 KI-Einstufungen waren **13** richtig → **Treffgenauigkeit 33 %**
- **KI-Sichtprüfungen sind als Beleg ungültig und kommen in dieser Pipeline
  nicht mehr als Wahrheitsersatz vor.**

**Die korrigierte Schwellenkurve** (drei Schachtanfänge ausgenommen, gelten als
durch Trimmung entfernbar):

| conf | Recall | richtig | falsch | unsicher | Precision |
|---:|:---:|---:|---:|---:|---:|
| 0,10 | 10/10 | 25 | 43 | 4 | 36,8 % |
| 0,15 | 9/10 | 24 | 36 | 2 | 40,0 % |
| 0,25 | 8/10 | 22 | 31 | 2 | 41,5 % |
| 0,35 | 7/10 | 19 | 22 | 2 | 46,3 % |
| **0,50** | **7/10** | **17** | **12** | **1** | **58,6 %** |
| 0,60 | 6/10 | 15 | 7 | 1 | 68,2 % |
| 0,70 | 3/10 | 9 | 0 | 1 | 100 % |

**Arbeitspunkt Videoweg: conf 0,50.** Jeder zweite Vorschlag ist echt, bei noch
7 von 10 protokollierten Bögen. Bei 0,10 kostet jeder Vorschlag mehr Prüfzeit
als Nutzen; 0,70 ist makellos, aber nutzlos.

Für die **Standbild-Messlatte bleibt das Protokoll 0,25.**

### 7.5 Das nc:15-Artefakt (Einbauweg)

**Problem:** Der Sidecar-Kandidatenpfad `/detect/yolo/bcc-test` erzwingt die
freigegebene 15er-Klassenkarte und filtert fest auf ID 14. Ein
Ein-Klassen-Kandidat (`{0: BCC_bogen}`) passt nicht durch.

**Lösung ohne Vertragsänderung:** Neutraining als `nc: 15` mit voller
Klassenkarte, aber nur BCC-Boxen (dieselben Bilder, Labels 0 → 14 gemappt). Der
Wächter bleibt unangetastet, der Kandidat erfüllt Manifest-, Hash- und
Klassenkartenprüfung von allein.

**Ergebnis** (`bcc_nc15_seed44_20260808`, Seed 44, 238 Epochen, interne Werte
P 0,826 / R 0,807 / mAP50 0,800 / mAP50-95 0,548):

| conf | TP (von 37) | FP | FA Negative (von 75) | FA Fremdschaden (von 220) |
|---:|---:|---:|---:|---:|
| 0,05 | 31 | 3 | 2 | 21 |
| 0,10 | 30 | 1 | 2 | 18 |
| 0,25 | 28 | 0 | 2 | 14 |
| 0,50 | 22 | 0 | 0 | 8 |

Der Videonachlauf mit `--class-id 14` fand **8 von 10** protokollierten Bögen.

**Einordnung:** Der alte Ein-Klassen-Bestand lag bei 0,25 in der Spanne 21–26.
Ein Lauf mit 28 liegt darüber, ist aber **ein Lauf gegen drei Seeds**. Die
belastbare Aussage lautet „nicht schlechter, keine Fehlalarm-Verschlechterung,
Vertrag erfüllt" — nicht „besser".

Auffällig bleibt: 14 von 220 Bildern mit anderem Schaden feuern bei 0,25. Die
Hauptverwechslung ist **Bogen ↔ verschobene Rohrverbindung mit Knick** —
dieselbe runde dunkle Form voraus, die auch in der Blindprüfung Fehlurteile
erzeugte.

### 7.6 BCC-Binärbewertung (Standbild, 2026-07-28)

240 Vorhersagen, null technische Fehler, gegen den eingefrorenen
`bcc_release_holdout_64d06094c921` (60 Bilder, 29 positiv / 31 negativ).

| Kandidat | TP | FN | TN | FP | Balanced Accuracy |
|---|---:|---:|---:|---:|---:|
| `bcc_bogen_af8020b688ac_v3_negatives` | 24 | 5 | 9 | 22 | 55,9 % |
| `bcc_bogen_b50b37ab8a4f` | 26 | 3 | 6 | 25 | 54,5 % |

Kein eindeutiger Spitzenreiter, beide zu viele Fehlalarme, beide
`not_deployed`. Bericht: `comparison_complete_not_release_qualified`. Weil
dieser Holdout vier Kandidaten verglichen hat, braucht ein späterer
Spitzenreiter vor Aktivierung einen **frischen** Bestätigungsholdout.

---

## 8. Was belegt nicht funktioniert

### 8.1 BAB_riss — Auflösungsgrenze

Reines `BAB_riss`-Modell, 212 Trainingsboxen aus 132 Haltungen, 143 Epochen:

| Schwelle | Treffer von 40 | Precision | Bilder mit Fehlalarm |
|---:|---:|---:|---:|
| 0,02 | 16 (40 %) | 0,7 % | 60 von 74 |
| 0,05 | 11 (27,5 %) | 1,7 % | 34 von 74 |
| 0,10 | 3 (7,5 %) | 2,2 % | 12 von 74 |
| 0,20 | 1 (2,5 %) | 100 % | 0 von 74 |

Höchste Konfidenz auf dem gesamten Holdout: **0,130.** Das Modell überschreitet
die Produktionsschwelle nirgends.

Vorher wurde BAB von 43 auf 132 Haltungen erweitert (+177 Handlabels, rund drei
Stunden Arbeit). Ergebnis: 1 → 2 Treffer von 40 — innerhalb der Streuung, also
kein Nachweis.

**Deutung:** Das Modell hat „rissähnliche Textur" gelernt, und rauer Beton ist
voll davon. Bei 788×576 Pixeln und einem zwei Pixel breiten Riss fehlt die
Information im Bild. **Das ist keine Frage der Datenmenge.**

### 8.2 BAF_oberflaeche — Klassendefinition

Der Trainingsbestand boxt kleine lokale Flecken, der Holdout-Standard markiert
rahmenfüllende Oberfläche. **62 % Musterdivergenz** laut
`artifacts/label-review-20260803/abweichungsliste.json`.

Ein rahmenfüllender Oberflächenschaden ist eine **Szeneneigenschaft, keine
Objektklasse**. Er gehört nicht in einen Box-Detektor.

### 8.3 Die Entscheidung

**Für BAB_riss und BAF_oberflaeche werden keine Boxen mehr gesammelt.** Belegt,
nicht vermutet. Beide Klassen wandern in eine mögliche **Screening-Spur** über
den Qwen-Weg — mit einer anderen Metrik: „Enthält dieses Bild mindestens einen
Riss: ja/nein", bildweise gemessen, **nicht** gegen Boxen bei IoU 0,5.

### 8.4 Was ebenfalls nichts gebracht hat

- **Trainingslänge:** 40 → 99 Epochen brachte Konvergenz, aber kein neues
  Niveau. 99 → 205 Epochen: F1 21,5 % → 21,2 %, also unverändert.
- **Stapelgröße:** `batch=8` verändert die Qualität nicht messbar (verkürzt aber
  zusammen mit `workers=8` und `cache=ram` die Epochen um rund 40 %).
- **Schwellenwert-Sweep** über die Vorhersagen: kein Arbeitspunkt mit brauchbarer
  Precision *und* Recall gleichzeitig für das Mehrklassenmodell.
- **Bildquelle:** Die rund 3000 Bestandsvideos bleiben PAL-SD. Strategisch für
  künftige Aufnahmen vormerken; am aktuellen Plan ändert es nichts.

---

## 9. Datenverfügbarkeit für den weiteren Ausbau

**BAH-Scan über den echten Importpfad** (`tools/PdfCodeScanner`, 1476 Ordner,
0 Importfehler):

| Gruppe | Haltungen |
|---|---:|
| Mit BAH-Befund (360 Befunde: BAHC 148 / BAHD 5 / BAHE 1) | **151** |
| … alle mit eingebetteten Fotos | 151 |
| Davon bereits in Gold | 65 |
| Davon nur im Benchmark (tabu) | 47 |
| **Frei verfügbar** | **39** |

Das Ziel „50–70 BAH-Haltungen im Training" ist damit belegt erreichbar
(65 + 39 = 104). Aus XTF/WinCan kommt nichts Neues — der Pool von 49 ist
restlos in Gold aufgegangen. **BAH-Sammlung läuft zwangsläufig über den
PDF-Kanal.**

---

## 10. Die Sicherungsmechanismen (warum das alles vertrauenswürdig ist)

Die Pipeline ist durchgehend **fail-closed** gebaut. Im Zweifel wird gesperrt,
nicht durchgelassen. Die wichtigsten Sperren:

| Was | Sperre |
|---|---|
| Modellfreigabe | nur ausdrückliches `qualified=true`; Datei + SHA je Backend |
| Trainingsexport | Registry `candidate` oder unbekannte Felder → Stopp |
| Klassen | unbekannte oder offene Klasse → harter Stopp, keine stille ID |
| Eval-Schutz | Bildbytes **und** beide Richtungen jeder Haltung gesperrt |
| Negativbilder | gleicher Bildhash immer gesperrt; Testhaltung + Gegenrichtung gesperrt |
| Gold | Box + Maske + 80-%-Regel + Text + persönliche Bestätigung |
| Zwei-Datei-Wechsel | Transaktionsmarker setzt Abbruch bytegenau zurück |
| Verknüpfungen | Register, Belege und Archive sind gegen Links/Junctions geschützt |
| Kundenoriginale | werden nie verändert — in keinem Werkzeug |
| Messwerkzeuge | können nicht trainieren, nicht aktivieren, nicht freigeben |
| Import | Staging + `.import-transaction.json` mit SHA-256, Recovery beim Projektladen |
| Sicherung | Echtzeit-Spiegel auf `Elements`, Zielmarker, Pfadgrenzen, SQLite als geprüfter Schnappschuss |

Zusätzlich: **SAM-Video-Regel.** SAM 2.1 kann Masken durch Videos propagieren,
darf aber nur Prüfwerkzeug für den Menschen sein. Propagierte Nachbarframes sind
stark voneinander abhängige Vorschläge. **Kein automatischer Gold-Export aus
Video-Propagation.**

Und: `yolo_wrapper._pil_rgb_to_ultralytics_bgr` wandelt dekodierte PIL-RGB-Bilder
vor jeder Inferenz explizit nach BGR. Ein früherer Lauf war wegen vertauschter
Rot-/Blaukanäle ungültig und musste aufgehoben werden.

---

## 11. Offene Punkte (Stand heute)

### 11.1 Entscheidung, die jetzt ansteht

**Zwei weitere Seeds für das nc:15-Artefakt (~8 h GPU, keine Handarbeit) oder
Einbau auf diesem einen Lauf?**

Empfehlung: die zwei Seeds. GPU-Zeit ist gratis, und der gepinnte Kandidat stünde
sonst auf n=1 gegen eine Dreier-Referenz — genau der Fehler, gegen den die
Dreier-Regel gebaut wurde. Bis zur Entscheidung wird nichts registriert; der
Kandidat bleibt unter `training/diagnostics/`.

### 11.2 Die lebende Liste

| # | Punkt | Umfang |
|---|---|---|
| 1 | **Quarantäne-Ordner `D:\Haltungen\06.691078-691070`** — Ordnername gegen PDF-Dateiname | 57 Goldsamples in 14 Gruppen, Vorlage liegt bereit |
| 2 | **43 Samples mit Platzhalter-Knoten** (`999006-10591` u. ä.) | einzeln auflösen vor Release; für Entwicklung tolerierbar, Bildschutz byteweise geprüft (0 Treffer) |
| 3 | **Frischer Release-Holdout** | Pflicht vor jeder Modellfreigabe |
| 4 | **OSD-Entscheidung** | Eingebrannter Text kann mitgelernt werden; Pilotprotokoll auswerten, dann entscheiden ob überdecken. **Muss vor der Massensammlung fallen.** |
| 5 | **Zwei defekte Test-Module** | `test_prepare_detect_gold.py`, `test_prepare_bcc_pilot.py` — ImportError durch schattierendes site-packages-Paket |
| 6 | **AP 0.4 abschließen** | 120er-Eval-Set mit Stufe und Ereignis-ID nachpflegen |
| 7 | **Schacht-Trimmung im Videoweg** | erste Sekunden bzw. Meter < 0,2 auslassen — gratis |
| 8 | **UI-Text für Vorschläge** | Vorschläge ohne Protokollbezug klar benennen, sonst wirkt der Assistent fehlerhaft |

### 11.3 Was ausdrücklich nicht getan werden soll

- Keine weiteren BAB- oder BAF-Boxen sammeln (belegt, nicht vermutet)
- Keine Einzellauf-Vergleiche mehr als Beweis führen
- Nicht am `gold_stock_audit.py` schrauben, um Samples ohne Haltung
  durchzulassen — der richtige Ort ist der Sample-Zustand, nicht der Wächter
- `conf = 0,25` als Standbild-Produktionsprotokoll nicht aufweichen
- Keinen zweiten YOLO-Datensatzschreiber neben dem plan-gesteuerten Coordinator
- KI-Sichtprüfungen nicht als Wahrheitsersatz verwenden

---

## 12. Einordnung: Wo stehen wir wirklich?

### Was stark ist

**Die Infrastruktur.** Der Weg von einem Kundenbild zu einem gemessenen Ergebnis
ist lückenlos hashgebunden, fail-closed und mehrfach abgesichert. Kein anderes
Ein-Personen-Projekt hätte diese Disziplin. Die Kontamination von 2026-08-03
wurde *durch die eigenen Prüfwerkzeuge* gefunden, nicht durch Zufall — das ist
der Beweis, dass die Absicherung funktioniert.

**Die Messkultur.** Labelblinde Vorhersagebelege, menschliche Blindprüfungen,
drei Seeds je Bedingung, technische Fehler nie als Negativtreffer. Die eigene
KI-Sichtprüfung wurde geprüft und als untauglich verworfen, statt sie zu
benutzen, weil sie das gewünschte Ergebnis lieferte.

**Ein funktionierendes Erkennungsergebnis.** BCC_bogen mit 73–84 % Recall bei
1–7 % Fehlalarm ist ein echter, reproduzierter Baustein — und der Videoweg
läuft mit Faktor 50 über Echtzeit.

### Was schwach ist

**Die Klassenbreite.** Von 15 Klassen funktionieren eine gut (BCC), eine
mittel mit kleiner Stichprobe (BAH), eine eingeschränkt (BCA). Elf liefern
praktisch nichts. Zwei davon (BAB, BAF) sind belegt nicht lösbar mit dem
aktuellen Bildmaterial bzw. dem aktuellen Klassenverständnis.

**Der Generalisierungsabstand.** Interne Validierung 32,2 % Recall gegen
15,7 % auf dem Holdout — Faktor 2. Das ist das eigentliche Restproblem.

**Der Abnahmebestand.** Ohne frischen Holdout ist keine echte Modellfreigabe
möglich, egal wie gut ein Kandidat misst.

**Die Bildquelle.** PAL-SD ist eine harte Grenze für feine Schäden. Kein
Trainingsaufwand ersetzt fehlende Pixel.

### Der realistische nächste Schritt

Nicht „bessere Erkennung für alles", sondern **ein Vorschlags-Assistent für
wenige Klassen mit menschlicher Bestätigung**:

- **Sicherer Kern:** BCC_bogen, BCA_anschluss
- **Kandidaten mitführen:** BAH, BAI, BAJ
- **Zielgröße ist die Fehlalarmquote, nicht der Recall.** Ein Assistent mit
  20 % Fehlalarm auf sauberem Rohr erzeugt bei 3000 Videos mehr Prüfarbeit, als
  er abnimmt.
- Für einen Assistenten reichen 60–80 % Recall bei tragbarer Fehlalarmquote.

Das ist mit dem BCC-Ergebnis heute erreichbar — für eine Klasse. Der Ausbau
läuft über BAH (39 freie Haltungen belegt verfügbar) und über einen frischen
Holdout, der die Erweiterung überhaupt beweisbar macht.

---

## Anhang: Belegdateien

| Bereich | Datei |
|---|---|
| Modellsperre | `sidecar/models/model_qualification.json` |
| Aktiver Klassifikator | `sidecar/models/active.json` |
| Klassenkarte | `training/class_maps/detect_class_map_v3.json` |
| Migration | `training/class_maps/detect_class_migration_v3.candidate.json` |
| Goldbestand | `C:\KI_BRAIN\training\reports\gold_stock_audit_20260806_154328_776.json` |
| Trainingsregister | `C:\KI_BRAIN\training\pilots\DETECT_ALL\registry_setup_v1.json` |
| Exportfreigabe | `C:\KI_BRAIN\training\export_registry_v1.json` |
| Referenzmessung 3 Seeds | `C:\KI_BRAIN\training\reports\detect_release_diagnostic_..._ref4{2,3,4}_...json` |
| BCC Paket 1 | `docs/quality/BCC-EINZELKLASSE-PAKET1-2026-08-07.md` |
| BCC Paket 2 | `docs/quality/BCC-EINZELKLASSE-PAKET2-2026-08-07.md` |
| BCC Videoweg | `docs/quality/BCC-VIDEO-MESSUNG-PAKET4-2026-08-07.md` (2 Aussagen widerlegt) |
| **BCC Arbeitspunkt** | `docs/quality/BCC-ARBEITSPUNKT-2026-08-08.md` |
| Benchmark v1 | `docs/quality/DETECT-BENCHMARK-V1-2026-08-06.md` |
| Strategie | `docs/briefings/detect-strategie-2026-08-06.md` |
| Kontaminations-Nachtrag | `docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md` |
| BAH-Verfügbarkeit | `docs/quality/BAH-VERFUEGBARKEIT-PDF-KANAL-2026-08-06.md` |
| Offene Punkte | `docs/quality/OFFENE-PUNKTE-TRAININGSPIPELINE.md` |
