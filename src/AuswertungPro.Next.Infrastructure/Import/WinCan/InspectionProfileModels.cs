using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

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

public sealed record ProfileGap(
    [property: JsonPropertyName("von_zeit")] double VonZeit,
    [property: JsonPropertyName("bis_zeit")] double BisZeit,
    [property: JsonPropertyName("von_meter")] double? VonMeter,
    [property: JsonPropertyName("bis_meter")] double? BisMeter,
    [property: JsonPropertyName("dauer_sek")] double DauerSek,
    [property: JsonPropertyName("distanz_m")] double? DistanzM);

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

public sealed record ProfileStatistik(
    [property: JsonPropertyName("codierungen_pro_meter")] double? CodierungenProMeter,
    [property: JsonPropertyName("mittlere_luecke_sek")] double MittlereLueckeSek,
    [property: JsonPropertyName("mittlere_luecke_m")] double? MittlereLueckeM,
    [property: JsonPropertyName("fahrgeschwindigkeit_m_s")] double? FahrgeschwindigkeitMS);

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

public sealed record VideoMatch(
    [property: JsonPropertyName("haltung_key")] string HaltungKey,
    [property: JsonPropertyName("file_path")] string FilePath,
    [property: JsonPropertyName("match_type")] string MatchType,
    [property: JsonPropertyName("confidence")] double Confidence);

public sealed record SequenzRegel(
    [property: JsonPropertyName("regel")] string Regel,
    [property: JsonPropertyName("support")] double Support,
    [property: JsonPropertyName("ausnahmen")] int Ausnahmen);

public sealed record AufnahmetechnikMuster(
    [property: JsonPropertyName("schacht_phase_sek_median")] double SchachtPhaseSekMedian,
    [property: JsonPropertyName("schacht_phase_sek_p90")] double SchachtPhaseSekP90,
    [property: JsonPropertyName("erste_codierung_meter_median")] double ErsteCodierungMeterMedian,
    [property: JsonPropertyName("erste_codierung_meter_p90")] double ErsteCodierungMeterP90);

public sealed record GeschwindigkeitKontext(
    [property: JsonPropertyName("median")] double Median,
    [property: JsonPropertyName("p10")] double P10,
    [property: JsonPropertyName("p90")] double P90);

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

public sealed record CodePrediction(
    [property: JsonPropertyName("code_pattern")] string CodePattern,
    [property: JsonPropertyName("probability")] double Probability);
