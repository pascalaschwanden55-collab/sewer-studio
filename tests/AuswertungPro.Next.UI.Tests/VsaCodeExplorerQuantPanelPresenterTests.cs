using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerQuantPanelPresenterTests
{
    [Fact]
    public void Build_versteckt_q_panels_wenn_keine_quantifizierung_definiert_ist()
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(q1: null, q2: null);

        Assert.True(presentation.ShowNoQuant);
        Assert.False(presentation.Q1.ShowPanel);
        Assert.False(presentation.Q2.ShowPanel);
    }

    [Fact]
    public void Build_baut_q1_mit_label_einheit_bereich_hint_und_pflichtbadge()
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(
            q1: new QuantField
            {
                Label = "Rissbreite",
                Einheit = "mm",
                Pflicht = "P",
                Min = 1,
                Max = 5,
                Hint = "kritisch"
            },
            q2: null);

        Assert.False(presentation.ShowNoQuant);
        Assert.True(presentation.Q1.ShowPanel);
        Assert.Equal("Q1: Rissbreite", presentation.Q1.LabelText);
        Assert.Equal("mm", presentation.Q1.UnitText);
        Assert.Equal("[1\u20135] kritisch", presentation.Q1.RangeText);
        Assert.True(presentation.Q1.ShowRequiredBadge);
        Assert.NotNull(presentation.Q1.RequiredBadge);
        Assert.Equal("PFLICHT", presentation.Q1.RequiredBadge.Text);
        Assert.Equal(VsaCodeExplorerQuantBrushRole.Danger, presentation.Q1.RequiredBadge.BrushRole);
        Assert.Equal(0.12, presentation.Q1.RequiredBadge.BackgroundOpacity);
        Assert.False(presentation.Q2.ShowPanel);
    }

    [Fact]
    public void Build_baut_q2_mit_label_und_einheit_ohne_q1()
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(
            q1: null,
            q2: new QuantField
            {
                Label = "Breite",
                Einheit = "%",
                Min = 0,
                Max = 100
            });

        Assert.False(presentation.ShowNoQuant);
        Assert.False(presentation.Q1.ShowPanel);
        Assert.True(presentation.Q2.ShowPanel);
        Assert.Equal("Q2: Breite", presentation.Q2.LabelText);
        Assert.Equal("%", presentation.Q2.UnitText);
        Assert.Equal("[0\u2013100]", presentation.Q2.RangeText);
    }

    [Theory]
    [InlineData(2.0, null, null, ">= 2")]
    [InlineData(null, 9.0, null, "<= 9")]
    [InlineData(null, null, "optional", "optional")]
    [InlineData(null, null, null, "")]
    public void Build_formatiert_q1_bereich_und_hint(double? min, double? max, string? hint, string expected)
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(
            q1: new QuantField { Min = min, Max = max, Hint = hint },
            q2: null);

        Assert.Equal(expected, presentation.Q1.RangeText);
        Assert.False(presentation.Q1.ShowRequiredBadge);
        Assert.Null(presentation.Q1.RequiredBadge);
        Assert.Equal("Q1: Quantifizierung", presentation.Q1.LabelText);
        Assert.Equal("Einheit fehlt", presentation.Q1.UnitText);
    }

    [Theory]
    [InlineData("mm")]
    [InlineData("%")]
    [InlineData("\u00b0")]
    [InlineData("Stk.")]
    public void Build_zeigt_fachliche_einheit_direkt_am_eingabefeld(string unit)
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(
            q1: new QuantField { Label = "Messwert", Einheit = unit },
            q2: null);

        Assert.Equal(unit, presentation.Q1.UnitText);
    }
}
