using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerValidationPresenterTests
{
    [Fact]
    public void Build_uebernimmt_confirm_status_und_blendet_leere_meldung_aus()
    {
        var presentation = VsaCodeExplorerValidationPresenter.Build(
            canConfirm: true,
            validationMessage: "");

        Assert.True(presentation.CanApply);
        Assert.Equal("", presentation.ValidationText);
        Assert.False(presentation.ShowValidation);
    }

    [Fact]
    public void Build_verwendet_leeren_text_wenn_meldung_null_ist()
    {
        var presentation = VsaCodeExplorerValidationPresenter.Build(
            canConfirm: false,
            validationMessage: null);

        Assert.False(presentation.CanApply);
        Assert.Equal("", presentation.ValidationText);
        Assert.False(presentation.ShowValidation);
    }

    [Fact]
    public void Build_zeigt_validierung_wenn_meldung_inhalt_hat()
    {
        var presentation = VsaCodeExplorerValidationPresenter.Build(
            canConfirm: false,
            validationMessage: "Bitte einen Code auswaehlen.");

        Assert.False(presentation.CanApply);
        Assert.Equal("Bitte einen Code auswaehlen.", presentation.ValidationText);
        Assert.True(presentation.ShowValidation);
    }
}
