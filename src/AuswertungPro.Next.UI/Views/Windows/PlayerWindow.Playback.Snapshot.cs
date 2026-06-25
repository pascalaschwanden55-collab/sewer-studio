using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame als PNG.
    /// </summary>
    public static bool TryTakeSnapshot(out string snapshotPath)
    {
        snapshotPath = string.Empty;
        var playerWindow = _lastOpened;
        var result = PlayerSnapshotWorkflow.TryTakeSnapshot(
            new PlayerSnapshotRequest(
                HasPlayerWindow: playerWindow is not null,
                IsClosing: playerWindow?._closing == true,
                IsPlaybackDisposed: playerWindow?._playbackDisposed == true,
                IsPlaying: playerWindow?._playerPlaybackControlHost.IsPlaying == true,
                CurrentTime: playerWindow?._playerTimelineHost.CurrentTime),
            new PlayerSnapshotActions(
                Capture: () =>
                {
                    var target = PlayerSnapshotPathPolicy.Create();
                    var captured = PlayerSnapshotFileCaptureServiceFactory.Create()
                        .TryCapture(target, path => playerWindow!.TakeSnapshotSafe(path), out var capturedPath);
                    return new PlayerSnapshotCaptureResult(captured, capturedPath);
                }));

        snapshotPath = result.SnapshotPath;
        return result.Captured;
    }

    private bool TakeSnapshotSafe(string filePath, uint width = 0, uint height = 0)
        => PlayerSnapshotWorkflow.TakeSnapshotSafe(
            new PlayerSnapshotSafeRequest(_closing, _playbackDisposed),
            new PlayerSnapshotSafeActions(
                PauseIfPlaying: () => PlayerSnapshotPauseStarter.PauseIfPlaying(
                    _playerPlaybackControlHost.IsPlaying,
                    _playerPlaybackControlHost.SetPause),
                IsPlaybackUnavailable: () => _closing || _playbackDisposed,
                DisableMarqueeOverlay: _playerMarqueeOverlayHost.Disable,
                TakeSnapshot: () => _playerSnapshotCaptureHost.TakeSnapshot(filePath, width, height),
                ResumeIfNeeded: wasPlaying => PlayerSnapshotPauseRestorer.ResumeIfNeeded(
                    wasPlaying,
                    _closing,
                    _playbackDisposed,
                    _playerPlaybackControlHost.SetPause))).Captured;
}
