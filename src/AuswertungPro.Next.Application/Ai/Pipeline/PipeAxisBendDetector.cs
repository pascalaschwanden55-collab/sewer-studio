using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Pure-Logic Detektor fuer Rohrbogen (BCC) basierend auf Fluchtpunkt-Drift
/// ueber eine Sequenz von <see cref="PipeAxisResult"/>-Messungen.
///
/// Hintergrund (2026-05-11): Qwen-VL ist im Einzelframe-Modus schwach bei
/// Bogen-Erkennung — ein stehendes Bild eines geraden Rohrs sieht fast
/// genauso aus wie eines gekruemmten Rohrs. Die Information steckt in der
/// zeitlichen Veraenderung der Rohrachse:
///
/// - Geradeaus → Fluchtpunkt bleibt in der Bildmitte
/// - Bogen nach links (BCCAY) → Fluchtpunkt wandert systematisch nach links
/// - Bogen nach rechts (BCCBY) → Fluchtpunkt wandert systematisch nach rechts
///
/// Der Detektor wird mit einem Fenster der letzten N Messungen versorgt und
/// liefert einen Bend-Kandidaten zurueck. Auto-Event-Erzeugung ist Sache des
/// Aufrufers — diese Klasse trifft KEINE Persistenz-Entscheidungen.
/// </summary>
public sealed class PipeAxisBendDetector
{
    /// <summary>Mindest-Fensterlaenge fuer eine plausible Aussage.</summary>
    public int MinWindowSize { get; init; } = 5;

    /// <summary>Mindest-Confidence der Einzelframes (PipeAxisResult.Confidence).</summary>
    public double MinFrameConfidence { get; init; } = 0.30;

    /// <summary>
    /// Mindest-Drift in normierten X-Einheiten (0..1) ueber das Fenster,
    /// damit eine Krümmung als signifikant gilt. 0.05 = 5% Bildbreite.
    /// </summary>
    public double SignificantDriftThreshold { get; init; } = 0.03;

    /// <summary>
    /// Ergebnis pro Detektor-Lauf. <see cref="RecommendedCode"/> ist
    /// <c>null</c> wenn keine Krümmung erkannt wurde.
    /// </summary>
    public sealed record BendDetectionResult(
        int WindowSize,
        double CurvatureScore,
        BendDirection Direction,
        double Confidence,
        string? RecommendedCode,
        string DiagnosticText);

    public enum BendDirection
    {
        Unknown,
        Straight,
        Left,
        Right,
        Up,
        Down
    }

    /// <summary>
    /// Wertet eine Sequenz von Pipe-Axis-Messungen aus. Sequenz muss
    /// chronologisch nach Position sortiert sein. Die Position kann Meter oder
    /// Zeit sein; aktuell wertet der Detektor nur die Reihenfolge aus.
    /// arbeitet rein deterministisch — keine I/O, keine Zeitabhaengigkeit
    /// auf <c>DateTime.Now</c>.
    /// </summary>
    public BendDetectionResult Detect(IReadOnlyList<(PipeAxisResult Axis, double Position)> samples)
    {
        if (samples == null || samples.Count == 0)
            return new BendDetectionResult(0, 0, BendDirection.Unknown, 0, null,
                "PipeAxis: keine Samples");

        // Niedrig-konfidente Frames werden ignoriert — sie verfaelschen die Drift-Statistik.
        var valid = samples
            .Where(s => s.Axis.Confidence >= MinFrameConfidence)
            .ToList();

        if (valid.Count < MinWindowSize)
            return new BendDetectionResult(
                valid.Count, 0, BendDirection.Unknown, 0, null,
                $"PipeAxis: window={valid.Count}/{MinWindowSize} (zu wenig valide Frames, " +
                $"avg_conf={(samples.Count == 0 ? 0 : samples.Average(s => s.Axis.Confidence)):F2})");

        // Drift = Differenz zwischen den letzten und ersten Frames im Fenster.
        // Mittelwert ueber zwei Halbfenster glaettet Einzelausreisser.
        int half = valid.Count / 2;
        var firstHalf = valid.Take(half).ToList();
        var secondHalf = valid.Skip(valid.Count - half).ToList();

        double avgFirstX = firstHalf.Average(s => s.Axis.VanishingX);
        double avgFirstY = firstHalf.Average(s => s.Axis.VanishingY);
        double avgSecondX = secondHalf.Average(s => s.Axis.VanishingX);
        double avgSecondY = secondHalf.Average(s => s.Axis.VanishingY);

        double driftX = avgSecondX - avgFirstX; // >0 = nach rechts, <0 = nach links
        double driftY = avgSecondY - avgFirstY;

        double curvature = Math.Sqrt(driftX * driftX + driftY * driftY);
        double avgConf = valid.Average(s => s.Axis.Confidence);

        // Richtungs-Klassifikation: dominierende Komponente betrachten.
        // X-Drift hat Vorrang weil horizontale Bögen in Kanalisationen üblich sind.
        BendDirection direction;
        string? recommended;
        if (curvature < SignificantDriftThreshold)
        {
            direction = BendDirection.Straight;
            recommended = null;
        }
        else if (Math.Abs(driftX) >= Math.Abs(driftY))
        {
            if (driftX < 0)
            {
                direction = BendDirection.Left;
                recommended = "BCCAY"; // Bogen nach links
            }
            else
            {
                direction = BendDirection.Right;
                recommended = "BCCBY"; // Bogen nach rechts
            }
        }
        else
        {
            // Vertikale Komponente dominant — eher Steig-/Fallstueck.
            direction = driftY < 0 ? BendDirection.Up : BendDirection.Down;
            recommended = "BCC"; // generischer Bogen-Code
        }

        var diag = $"PipeAxis: window={valid.Count} " +
                   $"conf={avgConf:F2} " +
                   $"driftX={driftX:+0.00;-0.00;0.00} " +
                   $"driftY={driftY:+0.00;-0.00;0.00} " +
                   $"curv={curvature:F2} " +
                   $"→ {direction}{(recommended != null ? $" ({recommended})" : "")}";

        // Confidence-Score fuer das Ergebnis: kombiniert Frame-Konfidenz mit
        // Drift-Staerke (gedeckelt auf 1.0).
        double resultConfidence = Math.Min(1.0, avgConf * Math.Min(1.0, curvature / 0.2));

        return new BendDetectionResult(
            valid.Count, curvature, direction, resultConfidence, recommended, diag);
    }
}
