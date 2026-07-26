namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingSnapshotCaptureFactory
{
    public static Task<byte[]?> CapturePngAsync(
        Func<string, bool> captureSnapshot,
        CancellationToken ct = default,
        string? tempDirectory = null)
        => new CodingSnapshotCaptureService(captureSnapshot, tempDirectory)
            .CapturePngAsync(ct);
}
