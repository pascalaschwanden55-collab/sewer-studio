using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingSnapshotFileCaptureService
{
    private const int MaxWaitAttempts = 20;
    private static readonly TimeSpan WaitDelay = TimeSpan.FromMilliseconds(50);
    private const long ReadyFileSizeBytes = 100;

    private readonly Action<string> _createDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, long> _getFileLength;
    private readonly Action<TimeSpan> _sleep;
    private readonly Action<string> _log;

    public CodingSnapshotFileCaptureService()
        : this(
            path => Directory.CreateDirectory(path),
            File.Exists,
            path => new FileInfo(path).Length,
            Thread.Sleep,
            message => PlayerTrace.WriteLine(message))
    {
    }

    public CodingSnapshotFileCaptureService(
        Action<string> createDirectory,
        Func<string, bool> fileExists,
        Func<string, long> getFileLength,
        Action<TimeSpan> sleep,
        Action<string>? log = null)
    {
        _createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _getFileLength = getFileLength ?? throw new ArgumentNullException(nameof(getFileLength));
        _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
        _log = log ?? (_ => { });
    }

    public string? CaptureSnapshot(CodingSnapshotTarget target, Action<string> takeSnapshot)
    {
        try
        {
            _createDirectory(target.PhotoDirectory);
            takeSnapshot(target.FilePath);

            for (var i = 0; i < MaxWaitAttempts; i++)
            {
                _sleep(WaitDelay);
                if (_fileExists(target.FilePath) && _getFileLength(target.FilePath) > ReadyFileSizeBytes)
                    return target.FilePath;
            }

            return _fileExists(target.FilePath) ? target.FilePath : null;
        }
        catch (Exception ex)
        {
            _log($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }
}
