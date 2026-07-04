using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerInitialTimeWorkflowTests
{
    [Fact]
    public void Build_nutzt_vorhandene_viewmodel_zeit_und_aendert_viewmodel_nicht()
    {
        var presentation = VsaCodeExplorerInitialTimeWorkflow.Build(
            existingZeit: "01:02",
            currentVideoTime: TimeSpan.FromMinutes(3));

        Assert.Equal("01:02", presentation.TextBoxText);
        Assert.Null(presentation.ViewModelZeit);
        Assert.True(presentation.ShouldSetTextBox);
        Assert.False(presentation.ShouldUpdateViewModel);
    }

    [Fact]
    public void Build_nutzt_player_zeit_als_fallback_und_aktualisiert_viewmodel()
    {
        var presentation = VsaCodeExplorerInitialTimeWorkflow.Build(
            existingZeit: "",
            currentVideoTime: new TimeSpan(0, 1, 5, 30));

        Assert.Equal("01:05:30", presentation.TextBoxText);
        Assert.Equal("01:05:30", presentation.ViewModelZeit);
        Assert.True(presentation.ShouldSetTextBox);
        Assert.True(presentation.ShouldUpdateViewModel);
    }

    [Fact]
    public void Build_gibt_keine_aenderung_wenn_beide_quellen_leer_sind()
    {
        var blank = VsaCodeExplorerInitialTimeWorkflow.Build(" ", null);
        var zero = VsaCodeExplorerInitialTimeWorkflow.Build(null, TimeSpan.Zero);

        Assert.False(blank.ShouldSetTextBox);
        Assert.False(blank.ShouldUpdateViewModel);
        Assert.Null(blank.TextBoxText);
        Assert.False(zero.ShouldSetTextBox);
        Assert.False(zero.ShouldUpdateViewModel);
        Assert.Null(zero.TextBoxText);
    }
}
