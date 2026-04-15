# Inspektions-Ablauf-Lerner — Design-Spec v2

**Datum:** 2026-04-15
**Version:** 2 (nach Review mit Ergaenzungen: Segmente, QualityFlags, CodeFull, Uebergangsmatrix, Kontextfenster)
**Ziel:** Aus 300-400 fertig codierten WinCan-Projekten den typischen Inspektionsablauf extrahieren und der KI als Kontextwissen beibringen.

## Problem

Die KI analysiert jeden Frame isoliert — sie weiss nicht:
- Ob die Kamera gerade im Schacht, im Rohr oder beim Schwenken ist
- Dass nach BCD normalerweise 2-5m ohne Schaden kommen
- Dass Nahaufnahmen nicht codiert werden
- Wie schnell die Kamera typisch faehrt
- Welche Codes in welcher Reihenfolge kommen

## Datenquellen

### WinCan DB3 (pro Projekt)
Tabelle `SECOBS` — eine Zeile pro codierter Beobachtung:

| Feld | Inhalt | Beispiel |
|------|--------|----------|
| `OBS_OpCode` | VSA-Hauptcode | "BAB", "BCA", "BCD" |
| `OBS_Char1` | Untertyp | "B" |
| `OBS_Char2` | Lage | "A" |
| `OBS_Distance` | Meterstand (m) | 12.30 |
| `OBS_TimeCtr` | Video-Zeitstempel | "00:02:54.70" |
| `OBS_ClockPos1/2` | Uhrlage | 3, 9 |
| `OBS_Q1/Q2/Q3` | Quantifizierung | "15" (mm/%) |
| `OBS_ContDefectLength` | Streckenschaden-Laenge | 2.5 |
| `OBS_Observation` | Langtext/Bemerkung | "Harte Ablagerungen..." |

Tabelle `SECOBSMM` — Fotos/Videos pro Beobachtung:

| Feld | Inhalt | Beispiel |
|------|--------|----------|
| `OMM_FileName` | Dateiname | "80638-80631_*.mpg" |
| `OMM_FileType` | Typ | "MPG", "JPG" |

Tabelle `SECTION` — Haltungs-Stammdaten:

| Feld | Inhalt |
|------|--------|
| `OBJ_Key` | Haltungsnummer (z.B. "80638-80631") |
| `OBJ_Length` | Haltungslaenge in m |

### Video-Dateien (MPG/AVI)
- Liegen im Export-Ordner unter `DISK1/Projects/.../`
- Zuordnung ueber Haltungsnummer im Dateinamen
- Typisch 4:3 oder 16:9, 576p bis 1080p

## Kern-Datenmodell

### Code-Kanonisierung

Aus `OBS_OpCode`, `OBS_Char1`, `OBS_Char2` werden immer zwei Formen gespeichert:

```
CodeMain = OBS_OpCode                    → "BAB", "BCA", "BCD"
CodeFull = OBS_OpCode + Char1 + Char2    → "BABBA", "BCAAA", "BCD"
```

Beide Formen werden in allen Datenstrukturen mitgefuehrt. `CodeFull` ist massgebend fuer Training und Few-Shot, `CodeMain` fuer Statistik und Uebergangsmatrix.

### Zeitnormalisierung

`OBS_TimeCtr` ("HH:MM:SS.ff") wird sofort in `float ZeitSek` umgerechnet.
`OBS_Distance` wird als `float Meter` gespeichert.
Alle Berechnungen arbeiten nur mit den normalisierten Werten.

## Datenstrukturen (v2)

### ProfileEvent — Einzelne Codierung

```json
{
  "zeit_sek": 174.70,
  "meter": 6.97,
  "code_main": "BAI",
  "code_full": "BAIZ",
  "char1": "Z",
  "char2": null,
  "uhr1": "12",
  "uhr2": null,
  "q1": "1",
  "streckenlaenge": null,
  "bemerkung": "Einragendes Dichtungsmaterial"
}
```

### ProfileSegment — Zeitabschnitt (NEU in v2)

Durchgehendes Segmentmodell — die Haltung wird lueckenlos in Abschnitte unterteilt:

```json
{
  "typ": "axial_fahrt",
  "von_zeit": 63.25,
  "bis_zeit": 122.61,
  "von_meter": 0.60,
  "bis_meter": 4.94,
  "dauer_sek": 59.36,
  "distanz_m": 4.34,
  "geschwindigkeit_m_s": 0.073,
  "quelle": "heuristik",
  "konfidenz": 0.85,
  "referenz_event_indices": []
}
```

**Segment-Typen:**

| Typ | Bedeutung | Heuristik |
|-----|-----------|-----------|
| `schacht` | Kamera im Schacht | vor erstem BCD |
| `uebergang` | Schacht↔Rohr Transition | 2s um BCD/BCE herum |
| `axial_fahrt` | Normaler Vortrieb | delta_meter > 0.2, delta_time > 0 |
| `stillstand` | Kamera steht, gleicher Meter | delta_meter < 0.05, delta_time > 2s |
| `detailbetrachtung` | Laengerer Stillstand | delta_meter < 0.05, delta_time > 4s |
| `mehrfachcodierung` | Mehrere Codes am selben Meter | delta_meter < 0.05, mehrere Events |
| `unbekannt` | Nicht klassifizierbar | Fallback |

**Segment-Heuristiken:**

```
if vor erstem BCD:
    typ = "schacht"
if nach letztem BCE:
    typ = "schacht"
if delta_meter < 0.05 and delta_time > 4s:
    typ = "detailbetrachtung"
if delta_meter < 0.05 and mehrere_events_gleicher_meter:
    typ = "mehrfachcodierung"
if delta_meter < 0.05 and delta_time > 2s:
    typ = "stillstand"
if abs(zeit - bcd_zeit) < 2s or abs(zeit - bce_zeit) < 2s:
    typ = "uebergang"
else:
    typ = "axial_fahrt"
```

**Wichtig:** `schwenk` und `nahaufnahme` werden aus DB3-Daten nur mit **niedriger Konfidenz** vergeben. Diese Typen koennen erst sicher durch Videoanalyse bestimmt werden.

### QualityFlags — Datenqualitaet pro Profil (NEU in v2)

```json
{
  "missing_video": false,
  "missing_section_length": false,
  "non_monotonic_distance": true,
  "non_monotonic_time": false,
  "duplicate_events_same_time": false,
  "missing_bcd": false,
  "missing_bce": true,
  "ambiguous_video_match": false,
  "few_events": false,
  "warnings": ["BCE fehlt — moeglicherweise Abbruch"]
}
```

### InspectionProfile (v2) — Komplett

```json
{
  "haltung_key": "80638-80631",
  "laenge_m": 13.06,
  "dauer_sekunden": 261,
  "video_pfad": "80638-80631_*.mpg",
  "video_match_confidence": 0.99,
  "ereignisse": [ ProfileEvent, ... ],
  "segmente": [ ProfileSegment, ... ],
  "luecken": [ ProfileGap, ... ],
  "statistik": { ... },
  "quality_flags": { ... }
}
```

### AggregatedPatterns (v2) — Statistische Muster

```json
{
  "anzahl_haltungen": 387,
  "anzahl_beobachtungen": 4231,
  "median_fahrgeschwindigkeit": 0.08,
  "median_codierungen_pro_meter": 0.35,
  "median_luecke_meter": 2.8,

  "code_verteilung": {
    "BCA": 28.1,
    "BAB": 15.3,
    "BBC": 12.0,
    "BAJ": 10.2
  },

  "uebergangs_matrix": {
    "BCD→BCA": 124,
    "BCD→BDB": 88,
    "BCA→BAB": 44,
    "BCA→BCA": 67,
    "BAB→BCA": 38
  },

  "distanz_bis_naechster_code": {
    "BCD": { "median": 1.2, "p90": 3.5 },
    "BCA": { "median": 4.8, "p90": 11.0 },
    "BAB": { "median": 3.2, "p90": 8.0 }
  },

  "sequenz_regeln": [
    { "regel": "BCD ist typischerweise erster Code", "support": 0.94, "ausnahmen": 21 },
    { "regel": "BCE ist typischerweise letzter Code", "support": 0.91, "ausnahmen": 35 },
    { "regel": "BDB folgt auf BCD innerhalb 2m", "support": 0.72, "ausnahmen": 108 }
  ],

  "aufnahmetechnik": {
    "schacht_phase_sek_median": 3.0,
    "schacht_phase_sek_p90": 8.0,
    "erste_codierung_meter_median": 0.5,
    "erste_codierung_meter_p90": 2.0
  },

  "geschwindigkeit_nach_kontext": {
    "axial_fahrt": { "median": 0.09, "p10": 0.04, "p90": 0.15 },
    "in_schadennaehe": { "median": 0.03, "p10": 0.01, "p90": 0.06 }
  }
}
```

### InspectionContextSnapshot — KI-Runtime-Format (NEU in v2)

Das ist was spaeter direkt in den Qwen-Prompt geht:

```json
{
  "haltung_key": "80638-80631",
  "haltung_laenge_m": 45.0,
  "current_meter": 5.30,
  "relative_position": 0.12,
  "estimated_view_type": "axial",
  "distance_since_last_code": 1.5,
  "time_since_last_code_sec": 18,
  "last_codes": ["BCD", "BCA", "BABBA"],
  "expected_next_codes": [
    { "code": "BC*", "probability": 0.34 },
    { "code": "BA*", "probability": 0.27 }
  ],
  "speed_estimate_mps": 0.09,
  "is_likely_detail_inspection": false
}
```

### ExtractedFrame — Frame mit Label-Hierarchie (v2)

```json
{
  "png_pfad": "80638-80631/174.70s_BAIZ_t+0.png",
  "haltung_key": "80638-80631",
  "zeit_sek": 174.70,
  "meter": 6.97,
  "offset_sek": 0,
  "is_reference_frame": true,

  "szene_klasse": "axial",
  "defekt_klasse": "BAIZ",
  "code_main": "BAI",
  "code_full": "BAIZ",
  "uhr": "12",

  "label_qualitaet": "direct_from_db",
  "frame_typ": "event_reference",
  "quelle": "codierung"
}
```

**Label-Hierarchie:**

| Ebene | Werte | Nutzen |
|-------|-------|--------|
| `szene_klasse` | schacht, axial, uebergang, stillstand, unbekannt | Aufnahmetechnik-Klassifikation |
| `defekt_klasse` | null, BAB, BBC, BCA, ... | Schadensklassifikation |
| `label_qualitaet` | direct_from_db, derived_from_gap, heuristic_viewtype, manual_verified | Trainingsfilter |

**Kontextfenster:** Pro Event werden 5 Frames extrahiert (t-2s, t-1s, t, t+1s, t+2s). Nur `t` bekommt `is_reference_frame=true`, der Rest ist `event_context`.

**Negativ-Frames:** Nur aus hochsicheren Leersegmenten (axial_fahrt, Luecke > 3m, kein Event in ±2m). Rest wird als `neutral_unlabeled` gespeichert statt falsches Negativ.

### VideoResolver — Video-Zuordnung (NEU in v2)

Dedizierter Resolver statt einfacher Dateiname-Match:

```
1. Exakter Match: OBJ_Key im Dateinamen → confidence 0.99
2. Regex-Match: Knotennummern extrahieren → confidence 0.90
3. SECOBSMM-Lookup: OMM_FileName mit MPG/AVI → confidence 0.95
4. Ordnerstruktur: Haltungsordner im Export → confidence 0.80
```

Bei mehreren Kandidaten: alle speichern, `ambiguous_video_match=true` setzen.

## Phasen (v2)

### Phase 1: Ablauf-Profile extrahieren
- DB3 lesen, Events normalisieren, Segmente ableiten
- QualityFlags setzen
- Video zuordnen (VideoResolver)
- JSON pro Haltung speichern

### Phase 2: Statistische Muster aggregieren
- Uebergangsmatrix berechnen
- Distanz-bis-naechster-Code pro Code
- Regeln mit Support statt Absolutaussagen
- Geschwindigkeit nach Kontext (Fahrt vs. Schadennaehe)
- Relative Positionsmuster (Anfang/Mitte/Ende der Haltung)

### Phase 3: Frames extrahieren
- Kontextfenster (5 Frames pro Event)
- Negativ-Frames nur aus sicheren Leersegmenten
- Standardisierte Dateinamen: `{haltung}_{timeSec}_{code}_{offset}.png`
- JSON-Metadaten pro Frame mit Label-Hierarchie

### Phase 4: KI-Anbindung
- Few-Shot-Beispiele aus besten Reference-Frames
- InspectionContextSnapshot im Qwen-Prompt
- Spaeter: YOLO-Training mit extrahierten Frames

## MVP-Reihenfolge

| MVP | Was | Ergebnis |
|-----|-----|----------|
| MVP 1 | Profile extrahieren | JSON + CSV pro Haltung, Datenqualitaet pruefen |
| MVP 2 | Muster aggregieren | Uebergangsmatrix, Regeln, Betriebsparameter |
| MVP 3 | Frames extrahieren (20-30 Haltungen) | Erste Few-Shot-Bibliothek + Negativ-Frames |
| MVP 4 | Prompt-Kontext live nutzen | InspectionContextSnapshot im Codier-Modus |

## Risiken und Absicherung

| Risiko | Absicherung |
|--------|-------------|
| TimeCtr passt nicht exakt zum visuellen Peak | Kontextfenster (5 Frames), spaeter Best-Frame-Auswahl |
| Unsaubere WinCan-Codierung | QualityFlags, Regeln mit Support, Ausnahmen zaehlen |
| View-Type nur indirekt aus DB3 ableitbar | Heuristisch mit Konfidenz, spaeter Videoanalyse |
| "kein_schaden"-Labels koennen falsch sein | Nur hochsichere Leersegmente, Rest als unlabeled |
| Video↔Haltung Zuordnung mehrdeutig | VideoResolver mit Confidence, ambiguous-Flag |

## Abhaengigkeiten

- `WinCanDbImportService` (existiert — DB3-Leselogik als Referenz)
- `ffmpeg` (existiert — Frame-Extraktion)
- `FewShotExampleStore` (existiert — Few-Shot-Speicher)
- `KnowledgeBaseManager` (existiert — KB-Integration)
- Zugang zu WinCan-Export-Ordnern auf Festplatte
