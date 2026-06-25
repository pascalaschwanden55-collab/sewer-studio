using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        _codingLiveAiTimers ??= new CodingLiveAiTimerController(
            BtnCodingLiveAi,
            CodingLiveAiTimer_Tick,
            () => !_closing && _player is not null);

        CodingLiveAiToggleWorkflow.Execute(
            new CodingLiveAiToggleWorkflowRequest(
                BtnCodingLiveAi.IsChecked == true,
                _codingAiController.ModelName),
            new CodingLiveAiToggleWorkflowActions(
                StartTimers: _codingLiveAiTimers.Start,
                StopTimers: resetButton => _codingLiveAiTimers.Stop(resetButton),
                SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail)));
    }

    private void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
        => HandleCodingLiveAiTimerTickAsync().SafeFireAndForget("CodingLiveAiTimer");

    private async Task HandleCodingLiveAiTimerTickAsync()
    {
        await CodingLiveAiTimerTickWorkflow.ExecuteAsync(
            new CodingLiveAiTimerTickWorkflowRequest(
                IsClosing: _closing,
                HasPlayer: _player is not null,
                HasLiveDetection: _codingAiController.LiveDetection is not null,
                SessionState: _codingSessionRuntimeOwner.Service?.ActiveSession?.State,
                IsPlayerPlaying: _player?.IsPlaying == true),
            new CodingLiveAiTimerTickWorkflowActions(
                RunAnalysisAsync: () => RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere..."),
                TraceError: message => PlayerTrace.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {message}")));
    }
}
