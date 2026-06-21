using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingSnapshotCaptureService
{
    private const int DefaultMaxAttempts = 20;
    private const int DefaultMinimumBytes = 100;

    private readonly Func<string, bool> _captureSnapshot;
    private readonly string _tempDirectory;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxAttempts;
    private readonly long _minimumBytes;

    public CodingSnapshotCaptureService(
        Func<string, bool> captureSnapshot,
        string? tempDirectory = null,
        TimeSpan? pollInterval = null,
        int maxAttempts = DefaultMaxAttempts,
        long minimumBytes = DefaultMinimumBytes)
    {
        _captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
        _tempDirectory = string.IsNullOrWhiteSpace(tempDirectory) ? Path.GetTempPath() : tempDirectory;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        _maxAttempts = Math.Max(1, maxAttempts);
        _minimumBytes = Math.Max(0, minimumBytes);
    }

    public async Task<byte[]?> CapturePngAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_tempDirectory);

        var snapFile = Path.Combine(_tempDirectory, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
        try
        {
            if (!_captureSnapshot(snapFile))
                return null;

            for (var i = 0; i < _maxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (IsReady(snapFile))
                    break;

                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            return File.Exists(snapFile)
                ? await File.ReadAllBytesAsync(snapFile, ct).ConfigureAwait(false)
                : null;
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(snapFile))
                        File.Delete(snapFile);
                },
                "Snapshot: Temp loeschen");
        }
    }

    private bool IsReady(string path)
        => File.Exists(path) && new FileInfo(path).Length > _minimumBytes;
}
