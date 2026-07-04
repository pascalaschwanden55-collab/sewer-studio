using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPropertyChangeWorkflowTests
{
    [Theory]
    [InlineData("CurrentLevel", true, true, false, false, false, false, false, false)]
    [InlineData("CurrentGroupColor", false, true, false, false, false, false, false, false)]
    [InlineData("ShowResultPanel", false, true, true, false, false, false, false, false)]
    [InlineData("FinalCode", false, true, true, false, false, false, false, false)]
    [InlineData("FinalLabel", false, false, true, false, false, false, false, false)]
    [InlineData("FinalSublabel", false, false, true, false, false, false, false, false)]
    [InlineData("WarnMessage", false, false, true, false, false, false, false, false)]
    [InlineData("Q1Rule", false, false, false, true, false, false, false, false)]
    [InlineData("Q2Rule", false, false, false, true, false, false, false, false)]
    [InlineData("Q1Error", false, false, false, false, false, false, true, false)]
    [InlineData("Q2Error", false, false, false, false, false, false, false, true)]
    [InlineData("ClockMode", false, false, false, false, true, false, false, false)]
    [InlineData("ClockHint", false, false, false, false, true, false, false, false)]
    [InlineData("CanConfirm", false, false, false, false, false, true, false, false)]
    [InlineData("ValidationMessage", false, false, false, false, false, true, false, false)]
    [InlineData("BreadcrumbItems", true, false, false, false, false, false, false, false)]
    public void Resolve_ordet_viewmodel_property_auf_ui_aktualisierungen(
        string propertyName,
        bool updateBreadcrumb,
        bool updateProgress,
        bool updateResultPanel,
        bool updateQuantPanel,
        bool updateClockPanel,
        bool syncValidation,
        bool updateQ1Error,
        bool updateQ2Error)
    {
        var actions = VsaCodeExplorerPropertyChangeWorkflow.Resolve(propertyName);

        Assert.Equal(updateBreadcrumb, actions.UpdateBreadcrumb);
        Assert.Equal(updateProgress, actions.UpdateProgress);
        Assert.Equal(updateResultPanel, actions.UpdateResultPanel);
        Assert.Equal(updateQuantPanel, actions.UpdateQuantPanel);
        Assert.Equal(updateClockPanel, actions.UpdateClockPanel);
        Assert.Equal(syncValidation, actions.SyncValidation);
        Assert.Equal(updateQ1Error, actions.UpdateQ1Error);
        Assert.Equal(updateQ2Error, actions.UpdateQ2Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unbekannt")]
    public void Resolve_ignoriert_unbekannte_properties(string? propertyName)
    {
        var actions = VsaCodeExplorerPropertyChangeWorkflow.Resolve(propertyName);

        Assert.False(actions.UpdateBreadcrumb);
        Assert.False(actions.UpdateProgress);
        Assert.False(actions.UpdateResultPanel);
        Assert.False(actions.UpdateQuantPanel);
        Assert.False(actions.UpdateClockPanel);
        Assert.False(actions.SyncValidation);
        Assert.False(actions.UpdateQ1Error);
        Assert.False(actions.UpdateQ2Error);
    }
}
