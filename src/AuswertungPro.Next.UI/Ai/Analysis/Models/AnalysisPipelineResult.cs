// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Analysis.Models;

/// <summary>
/// Gesamtergebnis eines vollständigen Analyse-Pipeline-Durchlaufs für ein Video.
/// </summary>
public sealed class AnalysisPipelineResult
{
    /// <summary>True wenn die Pipeline ohne Fehler abgeschlossen hat.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Fehlermeldung (null bei Erfolg).</summary>
    public string? Error { get; init; }

    /// <summary>Alle erkannten Beobachtungen (nach Merge/Dedup).</summary>
    public IReadOnlyList<AnalysisObservation> Observations { get; init; } = [];

    /// <summary>Anzahl analysierter Frames.</summary>
    public int FramesAnalyzed { get; init; }

    /// <summary>Videodauer in Sekunden.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Zeitstempel des Analysestarts (UTC).</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Zeitstempel des Analyseendes (UTC).</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    public static AnalysisPipelineResult Failed(string error)
        => new() { IsSuccess = false, Error = error };

    public static AnalysisPipelineResult Succeeded(
        IReadOnlyList<AnalysisObservation> observations,
        int framesAnalyzed,
        double durationSeconds)
        => new()
        {
            IsSuccess       = true,
            Observations    = observations,
            FramesAnalyzed  = framesAnalyzed,
            DurationSeconds = durationSeconds,
            FinishedAt      = DateTimeOffset.UtcNow
        };
}
