using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingLivePreviewClearControllerTests
{
    [Fact]
    public void Apply_leert_live_preview_ui_felder()
    {
        var values = new List<string>();

        TrainingLivePreviewClearController.Apply(
            values.Add,
            values.Add,
            values.Add,
            values.Add,
            values.Add,
            values.Add);

        Assert.Equal(["", "", "", "", "", ""], values);
    }

    [Fact]
    public void ApplyOnUi_leert_live_preview_ueber_ui_dispatch()
    {
        var calls = new List<string>();

        TrainingLivePreviewClearController.ApplyOnUi(
            value => calls.Add($"case:{value}"),
            value => calls.Add($"code:{value}"),
            value => calls.Add($"meter:{value}"),
            value => calls.Add($"comparison:{value}"),
            value => calls.Add($"entry:{value}"),
            value => calls.Add($"frame:{value}"),
            action =>
            {
                calls.Add("on-ui");
                action();
            });

        Assert.Equal(
            [
                "on-ui",
                "case:",
                "code:",
                "meter:",
                "comparison:",
                "entry:",
                "frame:"
            ],
            calls);
    }
}
