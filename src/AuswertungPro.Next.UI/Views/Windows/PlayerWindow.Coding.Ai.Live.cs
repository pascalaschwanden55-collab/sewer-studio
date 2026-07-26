using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        var timers = _codingLiveAiTimerOwner.Ensure(
            BtnCodingLiveAi,
            CodingLiveAiTimer_Tick,
            () => !_shutdownState.IsUnavailable);

        CodingLiveAiToggleWorkflow.Execute(
            new CodingLiveAiToggleWorkflowRequest(
                PlayerToggleButtonControls.IsChecked(BtnCodingLiveAi),
                _codingAiRuntimeOwner.Controller.ModelName),
            new CodingLiveAiToggleWorkflowActions(
                StartTimers: timers.Start,
                StopTimers: resetButton => timers.Stop(resetButton),
                SetCodingAiState: (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail)));
    }

    private void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
        => HandleCodingLiveAiTimerTickAsync().SafeFireAndForget("CodingLiveAiTimer");

    private async Task HandleCodingLiveAiTimerTickAsync()
    {
        await CodingLiveAiTimerTickWorkflow.ExecuteAsync(
            new CodingLiveAiTimerTickWorkflowRequest(
                IsClosing: _shutdownState.IsClosing,
                HasPlayer: !_shutdownState.IsPlaybackDisposed,
                HasLiveDetection: _codingAiRuntimeOwner.Controller.LiveDetection is not null,
                SessionState: _codingSessionRuntimeOwner.Service?.ActiveSession?.State,
                IsPlayerPlaying: !_shutdownState.IsPlaybackDisposed && _playerPlaybackControlHost.IsPlaying),
            new CodingLiveAiTimerTickWorkflowActions(
                RunAnalysisAsync: () => RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere..."),
                TraceError: message => PlayerTrace.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {message}")));
    }
}
