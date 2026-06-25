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
        LiveDetectionConfirmationDisplayWorkflow.Show(
            new LiveDetectionConfirmationShowRequest(
                findings,
                _playbackDisposed,
                _playerPlaybackControlHost.IsPlaying,
                _detectionConfirmationBuffer.TimestampSeconds),
            new LiveDetectionConfirmationShowActions(
                SetPause: _playerPlaybackControlHost.SetPause,
                SeekMilliseconds: _playerTimelineHost.SeekMilliseconds,
                ShowConfirmation: shownFindings => LiveDetectionStatusControls.ShowDetectionConfirmation(
                    DetectionConfirmationPanel,
                    TxtDetectionFinding,
                    TxtDetectionDetail,
                    shownFindings)));
    }

    private void ResumeDetection()
    {
        LiveDetectionConfirmationDisplayWorkflow.Resume(
            new LiveDetectionConfirmationResumeRequest(_playerPlaybackControlHost.IsPlaying),
            new LiveDetectionConfirmationResumeActions(
                ClearBuffer: _detectionConfirmationBuffer.Clear,
                HideConfirmation: () => LiveDetectionStatusControls.HideDetectionConfirmation(DetectionConfirmationPanel),
                Play: _playerPlaybackControlHost.Play));
    }
}
