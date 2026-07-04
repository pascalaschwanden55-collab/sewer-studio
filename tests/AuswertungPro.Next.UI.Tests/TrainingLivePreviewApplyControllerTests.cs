using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingLivePreviewApplyControllerTests
{
    [Fact]
    public void Apply_setzt_live_preview_felder_und_frame()
    {
        var calls = new List<string>();

        TrainingLivePreviewApplyController.Apply(
            new TrainingLivePreview(
                "case",
                "code",
                "meter",
                "comparison",
                "entry-code",
                "frame.png"),
            new TrainingLivePreviewApplyUi(
                value => calls.Add($"case:{value}"),
                value => calls.Add($"code:{value}"),
                value => calls.Add($"meter:{value}"),
                value => calls.Add($"comparison:{value}"),
                value => calls.Add($"entry:{value}"),
                value => calls.Add($"throttled:{value}"),
                () => "",
                value => calls.Add($"frame:{value}")));

        Assert.Equal(
            [
                "case:case",
                "code:code",
                "meter:meter",
                "comparison:comparison",
                "entry:entry-code",
                "throttled:frame.png"
            ],
            calls);
    }

    [Fact]
    public void Apply_erzwingt_leeren_frame_wenn_preview_kein_frame_und_bisher_leer_ist()
    {
        var calls = new List<string>();

        TrainingLivePreviewApplyController.Apply(
            new TrainingLivePreview("case", "code", "meter", "comparison", "entry-code", null),
            new TrainingLivePreviewApplyUi(
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                value => calls.Add($"throttled:{value}"),
                () => "",
                value => calls.Add($"frame:{value}")));

        Assert.Equal(["frame:"], calls);
    }

    [Fact]
    public void Apply_behaelt_bestehenden_frame_wenn_preview_kein_frame_liefert()
    {
        var calls = new List<string>();

        TrainingLivePreviewApplyController.Apply(
            new TrainingLivePreview("case", "code", "meter", "comparison", "entry-code", null),
            new TrainingLivePreviewApplyUi(
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                value => calls.Add($"throttled:{value}"),
                () => "old-frame.png",
                value => calls.Add($"frame:{value}")));

        Assert.Empty(calls);
    }

    [Fact]
    public void ApplyOnUi_baut_preview_und_fuehrt_update_auf_ui_aus()
    {
        var calls = new List<string>();

        TrainingLivePreviewApplyController.ApplyOnUi(
            "case",
            "code",
            "meter",
            "frame.png",
            new TrainingLivePreviewApplyUi(
                value => calls.Add($"case:{value}"),
                value => calls.Add($"code:{value}"),
                value => calls.Add($"meter:{value}"),
                value => calls.Add($"comparison:{value}"),
                value => calls.Add($"entry:{value}"),
                value => calls.Add($"throttled:{value}"),
                () => "",
                value => calls.Add($"frame:{value}")),
            action =>
            {
                calls.Add("on-ui");
                action();
            });

        Assert.Equal(
            [
                "on-ui",
                "case:case",
                "code:code",
                "meter:meter",
                "comparison:code @ meter",
                "entry:code",
                "throttled:frame.png"
            ],
            calls);
    }
}
