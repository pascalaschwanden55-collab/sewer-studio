using System.Diagnostics;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Gemeinsame Hilfen fuer die Tests der einzeln ausgelagerten Modellschritte (AP05b):
/// ein Laufzustand mit einem Bild in Bearbeitung und der passende Bildkontext.
/// </summary>
internal static class MultiModelSchrittTestHilfe
{
    public static MultiModelLaufZustand Lauf(MultiModelSnapshotRecorder rec, int pipeDiameterMm = 300, int frameIndex = 3)
    {
        var run = new MultiModelLaufZustand("dummy/video.mp4", 10, 10, pipeDiameterMm,
            new TemporalFindingDeduplicator(new TemporalDedupOptions()), rec)
        {
            FrameIndex = frameIndex,
        };
        return run;
    }

    /// <summary>Bild mit Kennbyte, damit der Skript-Client je Bild antwortet.</summary>
    public static MultiModelBildKontext Bild(int marker = 3, double t = 2.0, double estimatedMeter = 4.0)
        => new(t, SnapshotFrames.Marked(marker), new PipelineFrameTrace { FrameIndex = marker, TimeSec = t },
            Stopwatch.StartNew(), 0, estimatedMeter);
}
