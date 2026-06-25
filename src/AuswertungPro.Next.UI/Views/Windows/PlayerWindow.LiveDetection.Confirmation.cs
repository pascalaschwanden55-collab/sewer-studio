using System.Collections.Generic;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ShowDetectionConfirmation(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0) return;

        // Video pausieren und zur Fundstelle springen
        if (!_playbackDisposed)
            PlayerConfirmationPlayback.PauseLiveDetectionConfirmation(
                _playerPlaybackControlHost.IsPlaying,
                _playerPlaybackControlHost.SetPause);

        // Zur Fundstelle springen (Timestamp aus dem analysierten Frame)
        if (_detectionConfirmationBuffer.TimestampSeconds.HasValue)
        {
            long targetMs = (long)(_detectionConfirmationBuffer.TimestampSeconds.Value * 1000);
            _playerTimelineHost.SeekMilliseconds(targetMs);
        }

        LiveDetectionStatusControls.ShowDetectionConfirmation(
            DetectionConfirmationPanel,
            TxtDetectionFinding,
            TxtDetectionDetail,
            findings);
    }

    private void ResumeDetection()
    {
        _detectionConfirmationBuffer.Clear();
        LiveDetectionStatusControls.HideDetectionConfirmation(DetectionConfirmationPanel);

        // Video automatisch weiterlaufen lassen nach Entscheidung
        if (!_playerPlaybackControlHost.IsPlaying)
            _playerPlaybackControlHost.Play();
    }
}
