using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Phase 3.5 (2026-05-08): Drift-Erkennung fuer Pipeline-Telemetry.
///
/// Reine Berechnung ohne I/O — der Detector vergleicht das P95 eines
/// aktuellen Beobachtungsfensters (Standard: letzte 7 UTC-Tage) mit
/// dem P95 eines vorgelagerten Baseline-Fensters (Standard: 7 Tage davor).
/// Steigt der aktuelle P95 systematisch um mehr als <see cref="RegressionFactor"/>
/// (Standard: 1.20 = 20 %), wird Drift gemeldet.
///
/// Eingabe ist eine Sequenz von <see cref="PipelineRunMetric"/>
/// (UTC-Zeitstempel + Messwert in ms). Aufrufer mappen typischerweise
/// <see cref="TelemetryRunSnapshot.QwenP95Ms"/> → <see cref="PipelineRunMetric.Value"/>,
/// koennen den Detector aber auch fuer YOLO-, SAM- oder Total-Latenzen
/// verwenden — die Logik ist Phasen-agnostisch.
///
/// Bewusst ohne Dependency-Injection-Overhead: sealed class, idempotente
/// Methode, Zustand nur in init-Properties. Dadurch trivial unit-testbar
/// (keine Mocks, keine Repositories, keine Async-Pfade).
/// </summary>
public sealed class PipelineDriftDetector
{
    /// <summary>
    /// Faktor, ab dem ein Anstieg des aktuellen P95 gegenueber der Baseline
    /// als Drift gilt. 1.20 bedeutet: current &gt; baseline * 1.20.
    /// </summary>
    public double RegressionFactor { get; init; } = 1.20;

    /// <summary>Anzahl Tage im aktuellen Fenster (Standard: 7).</summary>
    public int CurrentWindowDays { get; init; } = 7;

    /// <summary>Anzahl Tage im Baseline-Fenster (Standard: 7, direkt vor dem aktuellen).</summary>
    public int BaselineWindowDays { get; init; } = 7;

    /// <summary>
    /// Vergleicht P95 aktuelles Fenster vs. Baseline-Fenster.
    /// Bei zu wenig Daten in einem der Fenster wird kein Drift gemeldet
    /// (Reason erklaert, was fehlt). Beide Fenster respektieren UTC-Datums-
    /// grenzen (00:00 UTC), nicht relative Stunden.
    /// </summary>
    public DriftReport DetectDrift(IEnumerable<PipelineRunMetric> metrics, DateTime? nowUtc = null)
    {
        if (metrics is null) throw new ArgumentNullException(nameof(metrics));
        if (CurrentWindowDays <= 0) throw new InvalidOperationException("CurrentWindowDays must be > 0.");
        if (BaselineWindowDays <= 0) throw new InvalidOperationException("BaselineWindowDays must be > 0.");
        if (RegressionFactor <= 0) throw new InvalidOperationException("RegressionFactor must be > 0.");

        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        // UTC-Datumsgrenze: das aktuelle Fenster endet exklusiv an "morgen 00:00 UTC"
        // (d.h. der heutige Tag zaehlt komplett mit).
        var currentEndExclusive = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var currentStart = currentEndExclusive.AddDays(-CurrentWindowDays);
        var baselineEndExclusive = currentStart;
        var baselineStart = baselineEndExclusive.AddDays(-BaselineWindowDays);

        var range = new DriftRange(baselineStart, currentStart, currentEndExclusive);

        // Materialisieren — wir iterieren mehrfach (aktuelle + Baseline-Filter).
        var list = metrics as IList<PipelineRunMetric> ?? metrics.ToList();

        var currentValues = list
            .Where(m => m.RunUtc >= currentStart && m.RunUtc < currentEndExclusive)
            .Select(m => m.Value)
            .ToArray();

        var baselineValues = list
            .Where(m => m.RunUtc >= baselineStart && m.RunUtc < baselineEndExclusive)
            .Select(m => m.Value)
            .ToArray();

        // Mindest-Stichprobengroesse: wir verlangen mindestens einen Wert pro Fenster,
        // damit Percentile sinnvoll sind. Bei sehr wenigen Werten ist die Aussage
        // statistisch schwach — das wird im Reason transparent gemacht.
        if (currentValues.Length == 0 || baselineValues.Length == 0)
        {
            return new DriftReport(
                HasDrift: false,
                CurrentP95: 0,
                BaselineP95: 0,
                Reason: BuildInsufficientDataReason(currentValues.Length, baselineValues.Length),
                CheckedRangeUtc: range);
        }

        var currentP95 = Percentile(currentValues, 0.95);
        var baselineP95 = Percentile(baselineValues, 0.95);
        var threshold = baselineP95 * RegressionFactor;
        var hasDrift = currentP95 > threshold;

        var reason = hasDrift
            ? $"P95 stieg von {baselineP95:F1} ms auf {currentP95:F1} ms (Faktor {currentP95 / Math.Max(baselineP95, 1e-9):F2}, Schwelle {RegressionFactor:F2})."
            : $"P95 stabil: aktuell {currentP95:F1} ms vs. baseline {baselineP95:F1} ms (Schwelle {threshold:F1} ms).";

        return new DriftReport(
            HasDrift: hasDrift,
            CurrentP95: currentP95,
            BaselineP95: baselineP95,
            Reason: reason,
            CheckedRangeUtc: range);
    }

    private static string BuildInsufficientDataReason(int currentCount, int baselineCount)
    {
        if (currentCount == 0 && baselineCount == 0)
            return "Keine Telemetry-Daten in beiden Fenstern.";
        if (currentCount == 0)
            return "Kein Telemetry-Datenpunkt im aktuellen Fenster.";
        return "Kein Telemetry-Datenpunkt im Baseline-Fenster.";
    }

    /// <summary>
    /// Lineare Interpolation zwischen den zwei naechsten sortierten Werten.
    /// Identisch zur Methode in <see cref="PipelineTelemetry"/>, damit der
    /// Detector dieselbe P95-Definition nutzt wie der Aggregator.
    /// </summary>
    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0) return 0;
        if (values.Count == 1) return values[0];

        var sorted = values.ToArray();
        Array.Sort(sorted);

        var index = p * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var frac = index - lower;
        return sorted[lower] + frac * (sorted[upper] - sorted[lower]);
    }
}

/// <summary>
/// Eingabe-Record fuer <see cref="PipelineDriftDetector"/>.
/// Phasen-agnostisch: <see cref="Value"/> ist typisch ein P95-Latenz-Wert
/// in Millisekunden (z.B. <see cref="TelemetryRunSnapshot.QwenP95Ms"/>),
/// kann aber auch Throughput, Mean-Latency etc. sein.
/// </summary>
public readonly record struct PipelineRunMetric(DateTime RunUtc, double Value);

/// <summary>
/// Ergebnis der Drift-Pruefung. CheckedRangeUtc dokumentiert genau, welche
/// UTC-Fenster verglichen wurden — wichtig fuer Audit / Diagnostics-UI.
/// </summary>
public sealed record DriftReport(
    bool HasDrift,
    double CurrentP95,
    double BaselineP95,
    string Reason,
    DriftRange CheckedRangeUtc);

/// <summary>
/// Beide Fenster sind halb-offen: [Start, End). BaselineEnd == CurrentStart,
/// damit der Wechsel sich nahtlos ergibt. Alle Zeiten in UTC.
/// </summary>
public readonly record struct DriftRange(
    DateTime BaselineStart,
    DateTime CurrentStart,
    DateTime CurrentEndExclusive)
{
    public DateTime BaselineEndExclusive => CurrentStart;
}
