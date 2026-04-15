# Inspektions-Ablauf-Lerner Implementation Plan v2

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aus 300-400 WinCan-Projekten den Inspektionsablauf extrahieren, Segmente + statistische Muster ableiten, Frames mit Label-Hierarchie extrahieren und als Kontextwissen in die KI-Pipeline einspeisen.

**Architecture:** 4 unabhaengige Services (ProfileExtractor → PatternAggregator → FrameExtractor → ContextSnapshot), jeweils als statische C#-Klasse. Ergebnisse als JSON. Frame-Extraktion via ffmpeg CLI. Segmentmodell als durchgehende Zeitachse. Codes kanonisch als CodeMain+CodeFull.

**Tech Stack:** C# .NET 8+, Microsoft.Data.Sqlite, System.Text.Json, ffmpeg CLI

**Spec:** `docs/superpowers/specs/2026-04-15-inspektions-ablauf-lerner-design.md` (v2)

---

## Datei-Struktur

| Aktion | Pfad | Verantwortung |
|--------|------|---------------|
| Erstellen | `src/.../Import/WinCan/InspectionProfileModels.cs` | Alle Datenmodelle (Records) |
| Erstellen | `src/.../Import/WinCan/InspectionProfileExtractor.cs` | DB3 → Profile mit Events + Segmente + QualityFlags |
| Erstellen | `src/.../Import/WinCan/VideoResolver.cs` | Video↔Haltung Zuordnung mit Confidence |
| Erstellen | `src/.../Import/WinCan/InspectionPatternAggregator.cs` | N Profile → Uebergangsmatrix + Regeln mit Support |
| Erstellen | `src/.../Import/WinCan/InspectionFrameExtractor.cs` | Profile + Video → PNG-Frames mit Kontextfenster |
| Erstellen | `src/.../Import/WinCan/InspectionContextSnapshot.cs` | Runtime-Kontext fuer Qwen-Prompt |
| Erstellen | `tests/.../InspectionProfileExtractorTests.cs` | Unit-Tests |
| Erstellen | `tests/.../InspectionPatternAggregatorTests.cs` | Unit-Tests |

(Alle `src/...` = `src/AuswertungPro.Next.Infrastructure`, alle `tests/...` = `tests/AuswertungPro.Next.Infrastructure.Tests`)

---

### Task 1: Datenmodelle (InspectionProfileModels.cs)

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionProfileModels.cs`

- [ ] **Step 1: Alle Records erstellen**

```csharp
// src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionProfileModels.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>Einzelne codierte Beobachtung im Inspektionsablauf.</summary>
public sealed record ProfileEvent(
    [property: JsonPropertyName("zeit_sek")] double ZeitSek,
    [property: JsonPropertyName("meter")] double? Meter,
    [property: JsonPropertyName("code_main")] string CodeMain,
    [property: JsonPropertyName("code_full")] string CodeFull,
    [property: JsonPropertyName("char1")] string? Char1,
    [property: JsonPropertyName("char2")] string? Char2,
    [property: JsonPropertyName("uhr1")] string? Uhr1,
    [property: JsonPropertyName("uhr2")] string? Uhr2,
    [property: JsonPropertyName("q1")] string? Q1,
    [property: JsonPropertyName("streckenlaenge")] double? Streckenlaenge,
    [property: JsonPropertyName("bemerkung")] string? Bemerkung);

/// <summary>Zeitabschnitt im Inspektionsablauf (durchgehend, lueckenlos).</summary>
public sealed record ProfileSegment(
    [property: JsonPropertyName("typ")] string Typ,
    [property: JsonPropertyName("von_zeit")] double VonZeit,
    [property: JsonPropertyName("bis_zeit")] double BisZeit,
    [property: JsonPropertyName("von_meter")] double? VonMeter,
    [property: JsonPropertyName("bis_meter")] double? BisMeter,
    [property: JsonPropertyName("dauer_sek")] double DauerSek,
    [property: JsonPropertyName("distanz_m")] double? DistanzM,
    [property: JsonPropertyName("geschwindigkeit_m_s")] double? GeschwindigkeitMS,
    [property: JsonPropertyName("quelle")] string Quelle,
    [property: JsonPropertyName("konfidenz")] double Konfidenz,
    [property: JsonPropertyName("referenz_event_indices")] int[] ReferenzEventIndices);

/// <summary>Luecke zwischen zwei Codierungen.</summary>
public sealed record ProfileGap(
    [property: JsonPropertyName("von_zeit")] double VonZeit,
    [property: JsonPropertyName("bis_zeit")] double BisZeit,
    [property: JsonPropertyName("von_meter")] double? VonMeter,
    [property: JsonPropertyName("bis_meter")] double? BisMeter,
    [property: JsonPropertyName("dauer_sek")] double DauerSek,
    [property: JsonPropertyName("distanz_m")] double? DistanzM);

/// <summary>Datenqualitaet eines Profils.</summary>
public sealed record QualityFlags(
    [property: JsonPropertyName("missing_video")] bool MissingVideo,
    [property: JsonPropertyName("missing_section_length")] bool MissingSectionLength,
    [property: JsonPropertyName("non_monotonic_distance")] bool NonMonotonicDistance,
    [property: JsonPropertyName("non_monotonic_time")] bool NonMonotonicTime,
    [property: JsonPropertyName("duplicate_events_same_time")] bool DuplicateEventsSameTime,
    [property: JsonPropertyName("missing_bcd")] bool MissingBcd,
    [property: JsonPropertyName("missing_bce")] bool MissingBce,
    [property: JsonPropertyName("ambiguous_video_match")] bool AmbiguousVideoMatch,
    [property: JsonPropertyName("few_events")] bool FewEvents,
    [property: JsonPropertyName("warnings")] List<string> Warnings);

/// <summary>Statistik einer Haltung.</summary>
public sealed record ProfileStatistik(
    [property: JsonPropertyName("codierungen_pro_meter")] double? CodierungenProMeter,
    [property: JsonPropertyName("mittlere_luecke_sek")] double MittlereLueckeSek,
    [property: JsonPropertyName("mittlere_luecke_m")] double? MittlereLueckeM,
    [property: JsonPropertyName("fahrgeschwindigkeit_m_s")] double? FahrgeschwindigkeitMS);

/// <summary>Komplettes Inspektions-Profil einer Haltung (v2).</summary>
public sealed record InspectionProfile(
    [property: JsonPropertyName("haltung_key")] string HaltungKey,
    [property: JsonPropertyName("laenge_m")] double? LaengeM,
    [property: JsonPropertyName("dauer_sekunden")] double DauerSekunden,
    [property: JsonPropertyName("video_pfad")] string? VideoPfad,
    [property: JsonPropertyName("video_match_confidence")] double VideoMatchConfidence,
    [property: JsonPropertyName("ereignisse")] IReadOnlyList<ProfileEvent> Ereignisse,
    [property: JsonPropertyName("segmente")] IReadOnlyList<ProfileSegment> Segmente,
    [property: JsonPropertyName("luecken")] IReadOnlyList<ProfileGap> Luecken,
    [property: JsonPropertyName("statistik")] ProfileStatistik Statistik,
    [property: JsonPropertyName("quality_flags")] QualityFlags QualityFlags);

/// <summary>Video-Zuordnung mit Konfidenz.</summary>
public sealed record VideoMatch(
    [property: JsonPropertyName("haltung_key")] string HaltungKey,
    [property: JsonPropertyName("file_path")] string FilePath,
    [property: JsonPropertyName("match_type")] string MatchType,
    [property: JsonPropertyName("confidence")] double Confidence);

/// <summary>Sequenz-Regel mit statistischem Support.</summary>
public sealed record SequenzRegel(
    [property: JsonPropertyName("regel")] string Regel,
    [property: JsonPropertyName("support")] double Support,
    [property: JsonPropertyName("ausnahmen")] int Ausnahmen);

/// <summary>Aufnahmetechnik-Muster.</summary>
public sealed record AufnahmetechnikMuster(
    [property: JsonPropertyName("schacht_phase_sek_median")] double SchachtPhaseSekMedian,
    [property: JsonPropertyName("schacht_phase_sek_p90")] double SchachtPhaseSekP90,
    [property: JsonPropertyName("erste_codierung_meter_median")] double ErsteCodierungMeterMedian,
    [property: JsonPropertyName("erste_codierung_meter_p90")] double ErsteCodierungMeterP90);

/// <summary>Geschwindigkeit nach Kontext.</summary>
public sealed record GeschwindigkeitKontext(
    [property: JsonPropertyName("median")] double Median,
    [property: JsonPropertyName("p10")] double P10,
    [property: JsonPropertyName("p90")] double P90);

/// <summary>Aggregierte Muster ueber alle Haltungen (v2).</summary>
public sealed record AggregatedPatterns(
    [property: JsonPropertyName("anzahl_haltungen")] int AnzahlHaltungen,
    [property: JsonPropertyName("anzahl_beobachtungen")] int AnzahlBeobachtungen,
    [property: JsonPropertyName("median_fahrgeschwindigkeit")] double MedianFahrgeschwindigkeit,
    [property: JsonPropertyName("median_codierungen_pro_meter")] double MedianCodierungenProMeter,
    [property: JsonPropertyName("median_luecke_meter")] double MedianLueckeMeter,
    [property: JsonPropertyName("code_verteilung")] Dictionary<string, double> CodeVerteilung,
    [property: JsonPropertyName("uebergangs_matrix")] Dictionary<string, int> UebergangsMatrix,
    [property: JsonPropertyName("distanz_bis_naechster_code")] Dictionary<string, GeschwindigkeitKontext> DistanzBisNaechsterCode,
    [property: JsonPropertyName("sequenz_regeln")] List<SequenzRegel> SequenzRegeln,
    [property: JsonPropertyName("aufnahmetechnik")] AufnahmetechnikMuster Aufnahmetechnik,
    [property: JsonPropertyName("geschwindigkeit_nach_kontext")] Dictionary<string, GeschwindigkeitKontext> GeschwindigkeitNachKontext);

/// <summary>Extrahierter Frame mit Label-Hierarchie (v2).</summary>
public sealed record ExtractedFrame(
    [property: JsonPropertyName("png_pfad")] string PngPfad,
    [property: JsonPropertyName("haltung_key")] string HaltungKey,
    [property: JsonPropertyName("zeit_sek")] double ZeitSek,
    [property: JsonPropertyName("meter")] double? Meter,
    [property: JsonPropertyName("offset_sek")] double OffsetSek,
    [property: JsonPropertyName("is_reference_frame")] bool IsReferenceFrame,
    [property: JsonPropertyName("szene_klasse")] string SzeneKlasse,
    [property: JsonPropertyName("defekt_klasse")] string? DefektKlasse,
    [property: JsonPropertyName("code_main")] string? CodeMain,
    [property: JsonPropertyName("code_full")] string? CodeFull,
    [property: JsonPropertyName("uhr")] string? Uhr,
    [property: JsonPropertyName("label_qualitaet")] string LabelQualitaet,
    [property: JsonPropertyName("frame_typ")] string FrameTyp,
    [property: JsonPropertyName("quelle")] string Quelle);

/// <summary>KI-Runtime-Kontext fuer Prompt-Injection (v2).</summary>
public sealed record InspectionContextSnapshot(
    [property: JsonPropertyName("haltung_key")] string HaltungKey,
    [property: JsonPropertyName("haltung_laenge_m")] double? HaltungLaengeM,
    [property: JsonPropertyName("current_meter")] double CurrentMeter,
    [property: JsonPropertyName("relative_position")] double RelativePosition,
    [property: JsonPropertyName("estimated_view_type")] string EstimatedViewType,
    [property: JsonPropertyName("distance_since_last_code")] double DistanceSinceLastCode,
    [property: JsonPropertyName("time_since_last_code_sec")] double TimeSinceLastCodeSec,
    [property: JsonPropertyName("last_codes")] List<string> LastCodes,
    [property: JsonPropertyName("expected_next_codes")] List<CodePrediction> ExpectedNextCodes,
    [property: JsonPropertyName("speed_estimate_mps")] double SpeedEstimateMps,
    [property: JsonPropertyName("is_likely_detail_inspection")] bool IsLikelyDetailInspection);

/// <summary>Vorhersage fuer naechsten Code.</summary>
public sealed record CodePrediction(
    [property: JsonPropertyName("code_pattern")] string CodePattern,
    [property: JsonPropertyName("probability")] double Probability);
```

- [ ] **Step 2: Build pruefen**

Run: `dotnet build src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionProfileModels.cs
git commit -m "Inspektions-Ablauf-Lerner v2: Datenmodelle mit Segmenten, QualityFlags, Label-Hierarchie"
```

---

### Task 2: InspectionProfileExtractor — DB3 → Profile mit Segmenten

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionProfileExtractor.cs`
- Erstellen: `tests/AuswertungPro.Next.Infrastructure.Tests/InspectionProfileExtractorTests.cs`

Implementierung wie Plan v1 Task 2, aber mit folgenden Ergaenzungen:
- `BuildCanonicalCode(opCode, char1, char2)` → CodeMain + CodeFull
- `BuildSegments(events)` → Segment-Heuristiken anwenden
- `BuildQualityFlags(events, profile)` → Warnungen setzen
- Monotonie-Pruefung fuer Distance und Time

Tests pruefen: ParseTimeCtr, BuildProfile, Segmente, QualityFlags, kanonische Codes.

---

### Task 3: VideoResolver — Video↔Haltung Zuordnung

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/VideoResolver.cs`

4-stufige Suche: Exakt → Regex → SECOBSMM → Ordnerstruktur.
Gibt `VideoMatch` mit `Confidence` zurueck.

---

### Task 4: InspectionPatternAggregator — Uebergangsmatrix + Regeln

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionPatternAggregator.cs`
- Erstellen: `tests/AuswertungPro.Next.Infrastructure.Tests/InspectionPatternAggregatorTests.cs`

Wie Plan v1 Task 3, plus:
- Uebergangsmatrix (CodeMain → CodeMain Paare zaehlen)
- Distanz-bis-naechster-Code pro CodeMain
- SequenzRegeln mit Support (BCD-erst, BCE-letzt als float, nicht bool)
- Geschwindigkeit nach Segment-Typ

---

### Task 5: InspectionFrameExtractor — Kontextfenster + Label-Hierarchie

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionFrameExtractor.cs`

Wie Plan v1 Task 4, plus:
- 5 Frames pro Event (t-2s, t-1s, t, t+1s, t+2s)
- `is_reference_frame=true` nur fuer t
- Negativ-Frames nur aus sicheren Segmenten (axial_fahrt, >3m Luecke, kein Event in ±2m)
- Unsichere Luecken als `neutral_unlabeled`
- Standardisierte Dateinamen: `{haltung}_{timeSec}_{code}_{offset}.png`

---

### Task 6: InspectionContextSnapshot — Runtime-Kontext fuer Prompt

**Files:**
- Erstellen: `src/AuswertungPro.Next.Infrastructure/Import/WinCan/InspectionContextSnapshot.cs`

Statische Methode die aus AggregatedPatterns + aktuellem Zustand (Meter, letzte Codes) einen Snapshot baut. Wird spaeter im Codier-Modus als Prompt-Kontext injiziert.

---

### Task 7: Integrationstest auf echtem WinCan-Export

Manueller Test mit `G:\GEP_Altdorf_2025_Zone_1.15_29261_925_Export`:
1. Profile extrahieren → JSON pruefen
2. QualityFlags pruefen (welche Warnungen?)
3. Segmente visuell pruefen (stimmt axial_fahrt, stillstand, uebergang?)
4. Muster aggregieren → Uebergangsmatrix, Regeln anschauen
5. Frames extrahieren (20-30 Haltungen) → visuell pruefen

---

### Task 8: Prompt-Integration (MVP 4, spaeter)

InspectionContextSnapshot in den Qwen-Prompt im Codier-Modus injizieren. Nicht Teil dieses Plans — eigener Plan wenn MVP 1-3 verifiziert sind.
