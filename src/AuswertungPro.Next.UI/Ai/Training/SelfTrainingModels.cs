// AuswertungPro – Selbststaendiges KI-Training Modelle
using System;

namespace AuswertungPro.Next.UI.Ai.Training;

// ── Match-Level fuer Vergleich KI vs. Protokoll ──

public enum MatchLevel
{
    /// <summary>Code + Meter + Uhr stimmen ueberein.</summary>
    ExactMatch,

    /// <summary>Code stimmt, aber Meter oder Uhr weicht ab.</summary>
    PartialMatch,

    /// <summary>KI hat etwas gefunden, aber Code passt nicht.</summary>
    Mismatch,

    /// <summary>KI hat gar nichts erkannt an dieser Position.</summary>
    NoFindings
}

// ── Ergebnis des Vergleichs KI-Erkennung vs. Protokoll-Eintrag ──

/// <param name="Level">Uebereinstimmungsgrad.</param>
/// <param name="ConfidenceScore">0.0–1.0 Gesamtbewertung.</param>
/// <param name="Explanation">Erklaerung auf Deutsch.</param>
/// <param name="CodeMatched">VSA-Code stimmt ueberein.</param>
/// <param name="MeterMatched">Meter-Position innerhalb Toleranz.</param>
/// <param name="SeverityPlausible">Schweregrad plausibel.</param>
/// <param name="ClockMatched">Uhrzeigerposition stimmt.</param>
/// <param name="BestMatchCode">VSA-Code der besten KI-Erkennung (null wenn NoFindings).</param>
/// <param name="BestMatchMeter">Meter-Position der besten KI-Erkennung.</param>
public sealed record ComparisonResult(
    MatchLevel Level,
    double ConfidenceScore,
    string Explanation,
    bool CodeMatched,
    bool MeterMatched,
    bool SeverityPlausible,
    bool ClockMatched,
    string? BestMatchCode,
    double? BestMatchMeter);

// ── Aufnahmetechnik-Bewertung ──

/// <param name="OsdReadable">OSD-Meter konnte gelesen werden.</param>
/// <param name="OsdDeltaMeters">Abweichung OSD vs. Protokoll in Metern.</param>
/// <param name="LightingQuality">Beleuchtung: Gut, Mittel, Schlecht.</param>
/// <param name="SharpnessQuality">Bildschaerfe: Gut, Mittel, Schlecht.</param>
/// <param name="CenteringQuality">Kamera-Zentrierung (Qwen, optional): Gut, Mittel, Schlecht.</param>
/// <param name="OverallGrade">Gesamtnote: A, B oder C.</param>
/// <param name="MeanLuminance">Durchschnittliche Bildhelligkeit (0–255).</param>
/// <param name="LaplacianVariance">Laplace-Varianz als Schaerfe-Mass.</param>
public sealed record TechniqueAssessment(
    bool OsdReadable,
    double? OsdDeltaMeters,
    string LightingQuality,
    string SharpnessQuality,
    string? CenteringQuality,
    string OverallGrade,
    double MeanLuminance,
    double LaplacianVariance);

// ── Fortschritt pro Protokolleintrag im Selbsttraining ──

/// <param name="EntryIndex">Index des aktuellen Eintrags (0-basiert).</param>
/// <param name="TotalEntries">Gesamtzahl der Protokolleintraege.</param>
/// <param name="VsaCode">VSA-Code aus dem Protokoll.</param>
/// <param name="MeterPosition">Meter-Position aus dem Protokoll.</param>
/// <param name="Stage">Aktueller Verarbeitungsschritt.</param>
/// <param name="Comparison">Vergleichsergebnis (null waehrend Verarbeitung).</param>
/// <param name="Technique">Aufnahmetechnik-Bewertung (null waehrend Verarbeitung).</param>
/// <param name="FramePath">Pfad zum extrahierten Frame.</param>
public sealed record SelfTrainingStep(
    int EntryIndex,
    int TotalEntries,
    string VsaCode,
    double MeterPosition,
    SelfTrainingStage Stage,
    ComparisonResult? Comparison,
    TechniqueAssessment? Technique,
    string? FramePath,
    string? ErrorMessage = null);

public enum SelfTrainingStage
{
    /// <summary>Phase 1: OSD-Scan laeuft, Timeline wird aufgebaut.</summary>
    BuildingTimeline,

    /// <summary>Frame wird extrahiert.</summary>
    ExtractingFrame,

    /// <summary>KI analysiert den Frame.</summary>
    Analyzing,

    /// <summary>Vergleich mit Protokoll laeuft.</summary>
    Comparing,

    /// <summary>Aufnahmetechnik wird bewertet.</summary>
    AssessingTechnique,

    /// <summary>Eintrag abgeschlossen.</summary>
    Completed
}

// ── Gesamtergebnis nach Selbsttraining einer Haltung ──

/// <param name="CaseId">TrainingCase ID.</param>
/// <param name="TotalEntries">Anzahl Protokolleintraege.</param>
/// <param name="ExactMatches">Anzahl ExactMatch.</param>
/// <param name="PartialMatches">Anzahl PartialMatch.</param>
/// <param name="Mismatches">Anzahl Mismatch.</param>
/// <param name="NoFindings">Anzahl NoFindings.</param>
/// <param name="OverallTechnique">Gesamtbewertung der Aufnahmetechnik (1x pro Haltung).</param>
/// <param name="Duration">Dauer des Trainingslaufs.</param>
/// <param name="SamplesGenerated">Anzahl erzeugter TrainingSamples.</param>
public sealed record SelfTrainingResult(
    string CaseId,
    int TotalEntries,
    int ExactMatches,
    int PartialMatches,
    int Mismatches,
    int NoFindings,
    TechniqueAssessment? OverallTechnique,
    TimeSpan Duration,
    int SamplesGenerated);
