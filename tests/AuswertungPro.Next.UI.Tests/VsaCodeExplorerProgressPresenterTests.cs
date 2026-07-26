using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerProgressPresenterTests
{
    [Fact]
    public void Build_markiert_vorherige_aktive_und_offene_segmente()
    {
        var presentation = VsaCodeExplorerProgressPresenter.Build(
            currentLevel: 2,
            showResultPanel: false,
            finalCode: "BAB");

        Assert.Equal("BAB", presentation.CodePreviewText);
        Assert.Equal(
            new[]
            {
                VsaCodeExplorerProgressBarRole.Group,
                VsaCodeExplorerProgressBarRole.Group,
                VsaCodeExplorerProgressBarRole.CurrentGroup,
                VsaCodeExplorerProgressBarRole.BorderLight
            },
            presentation.Segments.Select(segment => segment.BarRole));
        Assert.Equal([false, false, true, false], presentation.Segments.Select(segment => segment.LabelBold));
        Assert.Equal(
            new[]
            {
                VsaCodeExplorerProgressLabelRole.Secondary,
                VsaCodeExplorerProgressLabelRole.Secondary,
                VsaCodeExplorerProgressLabelRole.Secondary,
                VsaCodeExplorerProgressLabelRole.Muted
            },
            presentation.Segments.Select(segment => segment.LabelRole));
    }

    [Fact]
    public void Build_markiert_ab_aktueller_ebene_finale_segmente_als_erfolg()
    {
        var presentation = VsaCodeExplorerProgressPresenter.Build(
            currentLevel: 1,
            showResultPanel: true,
            finalCode: "BCA");

        Assert.Equal(
            new[]
            {
                VsaCodeExplorerProgressBarRole.Group,
                VsaCodeExplorerProgressBarRole.Success,
                VsaCodeExplorerProgressBarRole.Success,
                VsaCodeExplorerProgressBarRole.Success
            },
            presentation.Segments.Select(segment => segment.BarRole));
        Assert.All(presentation.Segments, segment => Assert.False(segment.LabelBold));
        Assert.All(presentation.Segments, segment => Assert.Equal(VsaCodeExplorerProgressLabelRole.Secondary, segment.LabelRole));
    }

    [Fact]
    public void Build_verwendet_leeren_vorschautext_wenn_code_null_ist()
    {
        var presentation = VsaCodeExplorerProgressPresenter.Build(
            currentLevel: 0,
            showResultPanel: false,
            finalCode: null);

        Assert.Equal("", presentation.CodePreviewText);
    }
}
