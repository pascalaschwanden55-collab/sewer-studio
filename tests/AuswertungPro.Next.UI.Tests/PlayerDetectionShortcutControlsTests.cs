using System.Threading;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerDetectionShortcutControlsTests
{
    [Fact]
    public void CreateActions_toggles_controls_and_invokes_click_handlers()
    {
        RunOnStaThread(() =>
        {
            var calls = new List<string>();
            var codingLiveAi = new ToggleButton { IsChecked = false };
            var liveDetection = new ToggleButton { IsChecked = true };

            var actions = PlayerDetectionShortcutControls.CreateActions(
                codingLiveAi,
                liveDetection,
                (sender, _) => calls.Add($"coding:{ReferenceEquals(sender, codingLiveAi)}"),
                (sender, _) => calls.Add($"live:{ReferenceEquals(sender, liveDetection)}"));

            var codingResult = PlayerDetectionShortcutWorkflow.Execute(
                new PlayerDetectionShortcutWorkflowRequest(
                    IsCodingMode: true,
                    IsCodingLiveAiChecked: codingLiveAi.IsChecked == true,
                    IsLiveDetectionChecked: liveDetection.IsChecked == true),
                actions);
            var liveResult = PlayerDetectionShortcutWorkflow.Execute(
                new PlayerDetectionShortcutWorkflowRequest(
                    IsCodingMode: false,
                    IsCodingLiveAiChecked: codingLiveAi.IsChecked == true,
                    IsLiveDetectionChecked: liveDetection.IsChecked == true),
                actions);

            Assert.Equal(PlayerDetectionShortcutWorkflowOutcome.CodingLiveAiToggled, codingResult.Outcome);
            Assert.Equal(PlayerDetectionShortcutWorkflowOutcome.LiveDetectionToggled, liveResult.Outcome);
            Assert.True(codingLiveAi.IsChecked);
            Assert.False(liveDetection.IsChecked);
            Assert.Equal(["coding:True", "live:True"], calls);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
