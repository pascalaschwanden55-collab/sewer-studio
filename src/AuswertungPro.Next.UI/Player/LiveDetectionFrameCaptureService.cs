using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Player;

public sealed class LiveDetectionFrameCaptureService
{
    private static readonly TimeSpan SnapshotWriteDelay = TimeSpan.FromMilliseconds(80);
    private const uint SnapshotWidth = 640;

    private readonly Func<string, uint, bool> _takeSnapshot;
    private readonly Func<string> _createTempPath;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, CancellationToken, Task<byte[]>> _readAllBytesAsync;
    private readonly Action<string> _deleteFile;

    public LiveDetectionFrameCaptureService(Func<string, uint, bool> takeSnapshot)
        : this(
            takeSnapshot,
            CreateTempPath,
            Task.Delay,
            File.Exists,
            File.ReadAllBytesAsync,
            File.Delete)
    {
    }

    public LiveDetectionFrameCaptureService(
        Func<string, uint, bool> takeSnapshot,
        Func<string> createTempPath,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<string, bool> fileExists,
        Func<string, CancellationToken, Task<byte[]>> readAllBytesAsync,
        Action<string> deleteFile)
    {
        _takeSnapshot = takeSnapshot ?? throw new ArgumentNullException(nameof(takeSnapshot));
        _createTempPath = createTempPath ?? throw new ArgumentNullException(nameof(createTempPath));
        _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readAllBytesAsync = readAllBytesAsync ?? throw new ArgumentNullException(nameof(readAllBytesAsync));
        _deleteFile = deleteFile ?? throw new ArgumentNullException(nameof(deleteFile));
    }

    public async Task<byte[]?> CaptureAsync(Func<bool> isUnavailable, CancellationToken cancellationToken)
    {
        if (isUnavailable())
            return null;

        var tempPath = _createTempPath();
        try
        {
            var success = _takeSnapshot(tempPath, SnapshotWidth);
            if (!success || isUnavailable())
                return null;

            await _delayAsync(SnapshotWriteDelay, cancellationToken);
            if (!_fileExists(tempPath))
                return null;

            return await _readAllBytesAsync(tempPath, cancellationToken);
        }
        catch
        {
            return null;
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (_fileExists(tempPath))
                        _deleteFile(tempPath);
                },
                "Snapshot: Temp loeschen");
        }
    }

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), $"sewer_live_{Guid.NewGuid():N}.png");
}
