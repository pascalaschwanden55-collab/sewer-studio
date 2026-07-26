using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModePreparePlaybackWorkflowTests
{
    [Fact]
    public void Execute_pauses_playback_and_hides_live_detection_entry_when_detection_is_not_running()
    {
        var calls = new List<string>();

        CodingModePreparePlaybackWorkflow.Execute(
            new CodingModePreparePlaybackWorkflowRequest(IsLiveDetectionRunning: false),
            Actions(
                setPause: pause => calls.Add($"pause:{pause}"),
                stopLiveDetection: () => calls.Add("stop"),
                uncheckLiveDetectionToggle: () => calls.Add("uncheck"),
                hideLiveDetectionEntry: () => calls.Add("hide")));

        Assert.Equal(["pause:True", "hide"], calls);
    }

    [Fact]
    public void Execute_stops_running_live_detection_before_hiding_entry()
    {
        var calls = new List<string>();

        CodingModePreparePlaybackWorkflow.Execute(
            new CodingModePreparePlaybackWorkflowRequest(IsLiveDetectionRunning: true),
            Actions(
                setPause: pause => calls.Add($"pause:{pause}"),
                stopLiveDetection: () => calls.Add("stop"),
                uncheckLiveDetectionToggle: () => calls.Add("uncheck"),
                hideLiveDetectionEntry: () => calls.Add("hide")));

        Assert.Equal(["pause:True", "stop", "uncheck", "hide"], calls);
    }

    private static CodingModePreparePlaybackWorkflowActions Actions(
        Action<bool>? setPause = null,
        Action? stopLiveDetection = null,
        Action? uncheckLiveDetectionToggle = null,
        Action? hideLiveDetectionEntry = null)
        => new(
            SetPause: setPause ?? (_ => { }),
            StopLiveDetection: stopLiveDetection ?? (() => { }),
            UncheckLiveDetectionToggle: uncheckLiveDetectionToggle ?? (() => { }),
            HideLiveDetectionEntry: hideLiveDetectionEntry ?? (() => { }));
}
