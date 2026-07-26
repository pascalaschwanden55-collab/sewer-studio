using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockPanelPresenterTests
{
    [Fact]
    public void Build_versteckt_panel_fuer_none_modus()
    {
        var presentation = VsaCodeExplorerClockPanelPresenter.Build(
            clockMode: "none",
            clockHint: "Hinweis",
            clockVonText: "6",
            clockBisText: "9");

        Assert.False(presentation.ShowPanel);
        Assert.Null(presentation.TransferText);
    }

    [Fact]
    public void Build_baut_single_modus_mit_autobis_und_punkt_presets()
    {
        var presentation = VsaCodeExplorerClockPanelPresenter.Build(
            clockMode: "single",
            clockHint: "Nur Punkt",
            clockVonText: "6",
            clockBisText: "9");

        Assert.True(presentation.ShowPanel);
        Assert.Equal("LAGE AM UMFANG (PUNKT)", presentation.Title);
        Assert.Equal("Nur Punkt", presentation.Hint);
        Assert.True(presentation.ShowHint);
        Assert.True(presentation.ShowSinglePanel);
        Assert.False(presentation.ShowRangePanel);
        Assert.Equal("Klick = Punkt (Mitte der Feststellung)", presentation.UsageHint);
        Assert.False(presentation.ShowRightPreset);
        Assert.False(presentation.ShowGesamtPreset);
        Assert.Equal("00", presentation.ClockBisText);
        Assert.Equal("6", presentation.ClockSingleValue);
        Assert.Null(presentation.ClockRangeFrom);
        Assert.Null(presentation.ClockRangeTo);
        Assert.Equal("Transfer: 06 00", presentation.TransferText);
    }

    [Fact]
    public void Build_leert_single_autobis_und_picker_wenn_von_leer_oder_nulluhr_ist()
    {
        var blank = VsaCodeExplorerClockPanelPresenter.Build("single", null, " ", "9");
        var zero = VsaCodeExplorerClockPanelPresenter.Build("single", null, "00", "9");

        Assert.Equal("", blank.ClockBisText);
        Assert.Equal(" ", blank.ClockSingleValue);
        Assert.Equal("Transfer: -- --", blank.TransferText);

        Assert.Equal("00", zero.ClockBisText);
        Assert.Equal("", zero.ClockSingleValue);
        Assert.Equal("Transfer: 00 00", zero.TransferText);
    }

    [Fact]
    public void Build_baut_range_modus_mit_bereich_presets_und_pickerwerten()
    {
        var presentation = VsaCodeExplorerClockPanelPresenter.Build(
            clockMode: "range",
            clockHint: null,
            clockVonText: "00",
            clockBisText: "9");

        Assert.True(presentation.ShowPanel);
        Assert.Equal("LAGE AM UMFANG (VON-BIS)", presentation.Title);
        Assert.False(presentation.ShowHint);
        Assert.False(presentation.ShowSinglePanel);
        Assert.True(presentation.ShowRangePanel);
        Assert.Equal("1. Klick = Von, 2. Klick = Bis (im Uhrzeigersinn)", presentation.UsageHint);
        Assert.True(presentation.ShowRightPreset);
        Assert.True(presentation.ShowGesamtPreset);
        Assert.Null(presentation.ClockBisText);
        Assert.Null(presentation.ClockSingleValue);
        Assert.Equal("", presentation.ClockRangeFrom);
        Assert.Equal("9", presentation.ClockRangeTo);
        Assert.Equal("Transfer: 00 09", presentation.TransferText);
    }
}
