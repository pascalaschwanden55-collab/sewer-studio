using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Haelt verlorene Verarbeitungsschritte bis zum Abschluss eines einzelnen Laufs fest.</summary>
internal sealed class MultiModelRunCompleteness
{
    public VideoFrameStreamCompletion? Extraction { get; set; }
    public int SamFailureFrames { get; private set; }
    public int QwenFailureFrames { get; private set; }

    public bool RecordSam(SamResponse response, int requestedBoxes)
    {
        // Score-Verwerfungen sind eine bewusste Modellentscheidung. Fehlende oder
        // fehlerhaft verarbeitete Masken duerfen dadurch nicht als gesund gelten.
        var missing = Math.Max(response.SkippedBoxes, requestedBoxes - response.Masks.Count);
        var lowScore = Math.Clamp(response.LowScoreBoxes, 0, Math.Max(0, response.SkippedBoxes));
        var failed = missing > lowScore || !string.IsNullOrWhiteSpace(response.Error)
            || (response.Degraded && missing <= 0);
        if (failed) SamFailureFrames++;
        return failed;
    }

    public void RecordQwenFailure() => QwenFailureFrames++;

    public void FinishExtraction(int framesRead, int expectedFrames, bool sidecarOutage)
    {
        // Auch bestehende injizierte Frame-Quellen behalten ihren Vertrag. Ihnen
        // fehlt nur der Prozess-Exit; die gelieferte Bildzahl bleibt pruefbar.
        if (Extraction is null && !sidecarOutage)
            Extraction = VideoFrameStream.EvaluateCompletion(framesRead, expectedFrames, null, null);
    }

    public string? ExtractionWarning => Extraction is { IsComplete: false } partial
        ? $"Video nur teilweise analysiert (Frames {partial.FramesRead}/{partial.ExpectedFrames}, {partial.Reason})."
        : null;

    public bool CanCompleteJournal(bool sidecarOutage, int failedFrames)
        => !sidecarOutage && failedFrames == 0 && Extraction is { IsComplete: true };
}
