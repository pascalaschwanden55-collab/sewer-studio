using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerResultPanelPresenterTests
{
    [Fact]
    public void Build_blendet_result_panel_aus_und_code_hinweis_ein()
    {
        var presentation = VsaCodeExplorerResultPanelPresenter.Build(
            showResultPanel: false,
            finalCode: "BAA",
            finalLabel: "Riss",
            finalSublabel: "laengs",
            warnMessage: "Warnung");

        Assert.False(presentation.ShowResultPanel);
        Assert.True(presentation.ShowCodeHintPanel);
        Assert.False(presentation.ShouldUpdateDetailPanels);
        Assert.Equal("", presentation.FinalCodeText);
        Assert.Equal("", presentation.FinalLabelText);
        Assert.Equal("", presentation.WarnText);
        Assert.False(presentation.ShowWarn);
    }

    [Fact]
    public void Build_baut_result_panel_mit_finalcode_label_sublabel_und_warnung()
    {
        var presentation = VsaCodeExplorerResultPanelPresenter.Build(
            showResultPanel: true,
            finalCode: "BAB",
            finalLabel: "Riss",
            finalSublabel: "Laengsriss",
            warnMessage: "Pruefen");

        Assert.True(presentation.ShowResultPanel);
        Assert.False(presentation.ShowCodeHintPanel);
        Assert.True(presentation.ShouldUpdateDetailPanels);
        Assert.Equal("BAB", presentation.FinalCodeText);
        Assert.Equal("Riss \u2014 Laengsriss", presentation.FinalLabelText);
        Assert.Equal("Pruefen", presentation.WarnText);
        Assert.True(presentation.ShowWarn);
    }

    [Fact]
    public void Build_blendet_warnung_aus_wenn_keine_warnmeldung_vorhanden_ist()
    {
        var presentation = VsaCodeExplorerResultPanelPresenter.Build(
            showResultPanel: true,
            finalCode: "BAA",
            finalLabel: "Verformung",
            finalSublabel: null,
            warnMessage: null);

        Assert.Equal("Verformung", presentation.FinalLabelText);
        Assert.Equal("", presentation.WarnText);
        Assert.False(presentation.ShowWarn);
    }
}
