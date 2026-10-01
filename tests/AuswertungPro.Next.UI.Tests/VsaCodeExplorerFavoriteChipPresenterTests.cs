using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerFavoriteChipPresenterTests
{
    [Fact]
    public void Build_zeigt_klartext_code_und_anzahl()
    {
        var presentation = VsaCodeExplorerFavoriteChipPresenter.Build(
            code: "babbb",
            anzahl: 4,
            klartext: " Riss radial ",
            gruppenLabel: "Struktur");

        Assert.Equal("Riss radial (BABBB) · 4×", presentation.Content);
        Assert.Equal(
            "Riss radial — 4× verwendet. Klick springt zum Hauptcode.",
            presentation.ToolTip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ZZZ")]
    public void Build_faellt_bei_fehlendem_klartext_auf_code_zurueck(string? klartext)
    {
        var presentation = VsaCodeExplorerFavoriteChipPresenter.Build(
            code: "zzz",
            anzahl: 2,
            klartext: klartext,
            gruppenLabel: "Sonstige");

        Assert.Equal("ZZZ · 2×", presentation.Content);
        Assert.Equal(
            "Sonstige — 2× verwendet. Klick springt zum Hauptcode.",
            presentation.ToolTip);
    }

    [Fact]
    public void BuildSelectable_blendet_nicht_auswaehlbaren_altcode_aus()
    {
        var presentation = VsaCodeExplorerFavoriteChipPresenter.BuildSelectable(
            code: "BCCYY",
            anzahl: 3,
            klartext: null,
            gruppenLabel: "Bestand");

        Assert.Null(presentation);
    }

    [Fact]
    public void BuildSelectable_zeigt_nur_code_mit_exaktem_klartext()
    {
        var presentation = VsaCodeExplorerFavoriteChipPresenter.BuildSelectable(
            code: "BCAAA",
            anzahl: 2,
            klartext: "Anschluss mit Formstück",
            gruppenLabel: "Bestand");

        Assert.NotNull(presentation);
        Assert.Equal(
            "Anschluss mit Formstück (BCAAA) · 2×",
            presentation!.Content);
    }
}
