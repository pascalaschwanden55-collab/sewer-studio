using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Live;

public sealed class DetectionConfirmationBuffer
{
    private IReadOnlyList<LiveFrameFinding> _findings = Array.Empty<LiveFrameFinding>();

    public IReadOnlyList<LiveFrameFinding> Findings => _findings;
    public byte[]? FrameBytes { get; private set; }
    public double? TimestampSeconds { get; private set; }
    public bool HasFindings => _findings.Count > 0;

    public void StoreFindings(
        IReadOnlyList<LiveFrameFinding> findings,
        byte[]? frameBytes,
        double timestampSeconds)
    {
        ArgumentNullException.ThrowIfNull(findings);

        _findings = findings.ToArray();
        FrameBytes = frameBytes;
        TimestampSeconds = timestampSeconds;
    }

    public void StoreAnalyzedFrame(byte[]? frameBytes, double timestampSeconds)
    {
        FrameBytes = frameBytes;
        TimestampSeconds = timestampSeconds;
    }

    public void Clear()
    {
        _findings = Array.Empty<LiveFrameFinding>();
        FrameBytes = null;
        TimestampSeconds = null;
    }
}
