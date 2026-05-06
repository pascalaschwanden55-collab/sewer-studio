// AuswertungPro – Video-Selbsttraining Phase 2
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Services;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>Anfrage fuer einen Video-Blinddurchlauf mit Differenzanalyse.</summary>
public sealed record VideoTrainingRequest
{
    /// <summary>Pfad zum Inspektionsvideo.</summary>
    public required string VideoPath { get; init; }

    /// <summary>Pfad zur Protokoll-Datenquelle (DB3, Daten.txt, etc.).</summary>
    public required string ProtocolSource { get; init; }

    /// <summary>Typ der Datenquelle: "WinCanDb3", "IbakDatenTxt".</summary>
    public required string ProtocolSourceType { get; init; }

    /// <summary>Rohrmaterial der Haltung (fuer KB-Kontext).</summary>
    public string? Rohrmaterial { get; init; }

    /// <summary>Nennweite in mm (fuer KB-Kontext).</summary>
    public int? NennweiteMm { get; init; }

    /// <summary>Haltungslaenge in Metern (fuer lineare Interpolation).</summary>
    public double? InspektionslaengeMeter { get; init; }

    /// <summary>Frame-Abstand in Sekunden (Default: 1.0s — feinere Abtastung fuer maximale Analyse-Qualitaet).</summary>
    public double FrameStepSeconds { get; init; } = 1.0;

    /// <summary>Meter-Toleranz fuer Zuordnung KI ↔ Protokoll (Default: ±0.5m).</summary>
    public double MeterTolerance { get; init; } = AuswertungPro.Next.Application.Ai.MeterTolerances.SingleTraining;

    /// <summary>
    /// Zentrierungs-Offset in Metern: Der Frame wird leicht nach vorn versetzt extrahiert,
    /// damit das Ereignis vertikal zentriert im Bild steht (nicht am oberen Rand).
    /// Default 0.3m — abhaengig von Kamerasichtwinkel und DN.
    /// </summary>
    public double CenteringOffsetMeter { get; init; } = 0.3;
}

/// <summary>Protokoll-Quellentypen.</summary>
public static class ProtocolSourceTypes
{
    public const string WinCanDb3 = "WinCanDb3";
    public const string IbakDatenTxt = "IbakDatenTxt";
    public const string InspektionsPdf = "InspektionsPdf";
}

// Phase 5.3: BlindDetection nach Domain/Ai/Training/BlindDetection.cs migriert.

/// <summary>Kategorie einer Differenz zwischen KI und Protokoll.</summary>
public enum DifferenceCategory
{
    /// <summary>KI hat den Schaden korrekt erkannt (Code + Meter stimmen).</summary>
    TruePositive,
    /// <summary>Schaden im Protokoll, aber von KI uebersehen.</summary>
    FalseNegative,
    /// <summary>KI meldet Schaden, der nicht im Protokoll steht.</summary>
    FalsePositive,
    /// <summary>Schaden erkannt, aber falscher Code.</summary>
    CodeMismatch
}

/// <summary>Ein einzelner Differenz-Eintrag (KI vs. Protokoll).</summary>
public sealed class DifferenceEntry
{
    /// <summary>Kategorie der Differenz.</summary>
    public required DifferenceCategory Category { get; init; }

    /// <summary>Protokolleintrag (null bei FalsePositive).</summary>
    public GroundTruthEntry? ProtocolEntry { get; init; }

    /// <summary>KI-Detektion (null bei FalseNegative).</summary>
    public BlindDetection? KiDetection { get; init; }

    /// <summary>Pfad zum Frame (aus KI oder Protokoll-Mapping). Wird nach der Analyse befuellt.</summary>
    public string? FramePath { get; set; }

    /// <summary>Zeitpunkt des zugeordneten Frames im Video (Sekunden, falls bekannt).</summary>
    public double? FrameTimeSeconds { get; set; }

    /// <summary>Erklaerungstext (z.B. "Code BAB erwartet, KI erkannte BCA").</summary>
    public string? Explanation { get; init; }

    /// <summary>
    /// V4.2 Phase 1.3: Match-Score aus <c>DifferenceAnalyzer.ScoreMatch</c> (0.0 - 1.0).
    /// Gewichtung: Code 0.40, Meter 0.30, Severity 0.15, Clock 0.15.
    /// Null bei FalseNegative ohne Kandidaten. Wird vom UncertaintySamplingService
    /// (Phase 1.4) fuer Review-Priorisierung genutzt: niedriger Score = unsicherer.
    /// </summary>
    public double? MatchConfidenceScore { get; init; }

    /// <summary>Review-Entscheidung (wird in der UI gesetzt).</summary>
    public ReviewDecision Decision { get; set; } = ReviewDecision.Pending;
}

/// <summary>Entscheidung des Reviewers pro Differenz-Eintrag.</summary>
public enum ReviewDecision
{
    /// <summary>Noch nicht bewertet.</summary>
    Pending,
    /// <summary>KI-Erkennung ist korrekt.</summary>
    KiCorrect,
    /// <summary>Protokoll ist korrekt (KI hat sich geirrt).</summary>
    ProtocolCorrect,
    /// <summary>Eintrag wird ignoriert (z.B. unklar, Qualitaetsproblem).</summary>
    Ignored
}

/// <summary>Gesamtergebnis eines Differenzvergleichs.</summary>
public sealed class DifferenceReport
{
    /// <summary>Alle Differenz-Eintraege.</summary>
    public List<DifferenceEntry> Entries { get; init; } = [];

    // --- Berechnete Metriken ---

    public int TruePositiveCount => Entries.Count(e => e.Category == DifferenceCategory.TruePositive);
    public int FalseNegativeCount => Entries.Count(e => e.Category == DifferenceCategory.FalseNegative);
    public int FalsePositiveCount => Entries.Count(e => e.Category == DifferenceCategory.FalsePositive);
    public int CodeMismatchCount => Entries.Count(e => e.Category == DifferenceCategory.CodeMismatch);

    /// <summary>
    /// Precision = TP / (TP + FP + MM).
    /// CodeMismatch zaehlt als Fehler auf Erkennungsseite (falscher Code gemeldet).
    /// </summary>
    public double Precision
    {
        get
        {
            var tp = TruePositiveCount;
            var fp = FalsePositiveCount;
            var mm = CodeMismatchCount;
            return (tp + fp + mm) > 0 ? (double)tp / (tp + fp + mm) : 0;
        }
    }

    /// <summary>
    /// Recall = TP / (TP + FN + MM).
    /// CodeMismatch zaehlt als Fehler auf Ground-Truth-Seite (richtiger Code verpasst).
    /// </summary>
    public double Recall
    {
        get
        {
            var tp = TruePositiveCount;
            var fn = FalseNegativeCount;
            var mm = CodeMismatchCount;
            return (tp + fn + mm) > 0 ? (double)tp / (tp + fn + mm) : 0;
        }
    }

    /// <summary>F1-Score = 2 * (P * R) / (P + R). 0 wenn P oder R null.</summary>
    public double F1
    {
        get
        {
            var p = Precision;
            var r = Recall;
            return (p + r) > 0 ? 2 * p * r / (p + r) : 0;
        }
    }
}

/// <summary>Fortschrittsmeldung waehrend des Video-Blinddurchlaufs.</summary>
public sealed record VideoTrainingProgress(
    string Phase,
    int Current,
    int Total,
    string Status,
    string? FramePreviewPath = null);

/// <summary>Gesamtergebnis des Video-Selbsttrainings.</summary>
public sealed record VideoTrainingResult(
    DifferenceReport Report,
    List<FrameMapping> FrameMappings,
    TimeSpan Duration);
