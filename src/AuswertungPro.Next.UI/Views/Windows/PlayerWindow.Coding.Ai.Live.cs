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

        if (BtnCodingLiveAi.IsChecked == true)
        {
            _codingLiveAiTimers.Start();

            var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
                isActive: true,
                LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName));
            SetCodingAiState(status.StatusText, PlayerStatusColors.Success, status.DetailText);
        }
        else
        {
            _codingLiveAiTimers.Stop(resetButton: true);

            var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
                isActive: false,
                LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName));
            SetCodingAiState(status.StatusText, PlayerStatusColors.Success, status.DetailText);
        }
    }

    private void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
        => HandleCodingLiveAiTimerTickAsync().SafeFireAndForget("CodingLiveAiTimer");

    private async Task HandleCodingLiveAiTimerTickAsync()
    {
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            if (!CodingLiveAiTickPolicy.ShouldAnalyze(
                    _closing,
                    hasPlayer: _player is not null,
                    hasLiveDetection: _codingAiController.LiveDetection is not null,
                    _codingSessionService?.ActiveSession?.State,
                    isPlayerPlaying: _player?.IsPlaying == true))
                return;

            await RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere...");
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {ex.Message}");
        }
    }
}
