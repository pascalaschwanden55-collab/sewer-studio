using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.SelfImproving;

/// <summary>
/// Verteilung der Samples nach QualityGate-Stufe (Green/Yellow/Red/Unbekannt).
/// </summary>
public sealed record QualityDistribution(
    int Green,
    int Yellow,
    int Red,
    int Unknown)
{
    public int Total => Green + Yellow + Red + Unknown;

    public double GreenRatio => Total == 0 ? 0 : (double)Green / Total;
    public double YellowRatio => Total == 0 ? 0 : (double)Yellow / Total;
    public double RedRatio => Total == 0 ? 0 : (double)Red / Total;
    public double UnknownRatio => Total == 0 ? 0 : (double)Unknown / Total;
}

/// <summary>
/// Aggregierte Statistik je VSA-Code: wie viele Samples in der KB, wie oft validiert,
/// wie hoch die Trefferquote, und ein heuristischer Problem-Score zum Sortieren.
/// </summary>
/// <param name="VsaCode">Roher VSA-Code (z.B. "BAC", "BBA").</param>
/// <param name="SampleCount">Anzahl Samples in der KB.</param>
/// <param name="ValidationTotal">Anzahl Eintraege im ValidationLog.</param>
/// <param name="ValidationCorrect">Davon WasCorrect=1.</param>
/// <param name="Accuracy">Anteil korrekt — null wenn ValidationTotal=0.</param>
/// <param name="ProblemScore">Heuristik fuer Sortierung: niedrige Accuracy + ausreichend Stichproben = hoher Score.</param>
public sealed record CodeStat(
    string VsaCode,
    int SampleCount,
    int ValidationTotal,
    int ValidationCorrect,
    double? Accuracy,
    double ProblemScore);

/// <summary>
/// Ein einzelner Sample-Datensatz fuer den Review-Export.
/// </summary>
public sealed record ReviewSampleRow(
    string SampleId,
    string VsaCode,
    string SuggestedCode,
    double Priority,
    DateTime EnqueuedUtc,
    string FramePath,
    string MatchLevel);

/// <summary>
/// Ergebnis des Review-Export-Vorgangs.
/// </summary>
public sealed record ReviewBatchExport(
    string CsvPath,
    int Count,
    int UncertaintyPicks,
    int DiversityPicks);

/// <summary>
/// Haeufige Code-Verwechslung: KI hat <see cref="SuggestedCode"/> vorgeschlagen,
/// der Mensch hat sie auf <see cref="FinalCode"/> korrigiert. Anzahl
/// (<see cref="Count"/>) zeigt Schwerpunkte fuer gezieltes Training.
/// </summary>
public sealed record ConfusionPair(
    string SuggestedCode,
    string FinalCode,
    int Count);

/// <summary>
/// Gesamt-Snapshot fuer das KB-Dashboard. Wird im Diagnose-Tab gerendert.
/// </summary>
public sealed record KbDashboardSnapshot(
    int TotalSamples,
    int TotalValidations,
    double? OverallAccuracy,
    QualityDistribution Quality,
    IReadOnlyList<CodeStat> TopProblemCodes,
    IReadOnlyList<CodeStat> UnderRepresentedCodes,
    IReadOnlyList<ConfusionPair> TopConfusions,
    int ReviewQueueLength,
    DateTime GeneratedUtc);
