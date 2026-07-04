using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerColumnTilePresenterTests
{
    [Fact]
    public void Build_baut_standard_tile_mit_gruppenrolle_badge_und_beschreibung()
    {
        var presentation = VsaCodeExplorerColumnTilePresenter.Build(new TileItem
        {
            Label = "BA",
            Description = "Rissbildung",
            BadgeText = "ICM",
            BadgeColor = null,
            GroupColor = "#123456"
        });

        Assert.Equal("BA", presentation.LabelText);
        Assert.True(presentation.ShowDescription);
        Assert.Equal("Rissbildung", presentation.DescriptionText);
        Assert.Equal("#123456", presentation.GroupColorHex);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Group, presentation.MarkerBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Group, presentation.CodeBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.TextSecondary, presentation.DescriptionBrushRole);
        Assert.Equal([new VsaCodeExplorerColumnTileBadge("ICM", "#2563EB")], presentation.Badges);
        Assert.False(presentation.ShowSelectedChrome);
        Assert.False(presentation.ShowInvalidChrome);
    }

    [Fact]
    public void Build_markiert_selected_tile_und_unterdrueckt_end_badge()
    {
        var presentation = VsaCodeExplorerColumnTilePresenter.Build(new TileItem
        {
            Label = "BB",
            IsFinal = true,
            IsSelected = true,
            GroupColor = "#654321"
        });

        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Accent, presentation.MarkerBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Accent, presentation.CodeBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Text, presentation.DescriptionBrushRole);
        Assert.Empty(presentation.Badges);
        Assert.True(presentation.ShowSelectedChrome);
    }

    [Fact]
    public void Build_markiert_invalid_tile_und_zeigt_tooltip()
    {
        var presentation = VsaCodeExplorerColumnTilePresenter.Build(new TileItem
        {
            Label = "BC",
            IsInvalid = true,
            IsFinal = true
        });

        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Invalid, presentation.MarkerBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Invalid, presentation.CodeBrushRole);
        Assert.Equal(VsaCodeExplorerColumnTileBrushRole.Invalid, presentation.DescriptionBrushRole);
        Assert.Equal([new VsaCodeExplorerColumnTileBadge("End", "#16A34A")], presentation.Badges);
        Assert.True(presentation.ShowInvalidChrome);
        Assert.Equal("Als ungueltig markiert - Auswahl ist trotzdem erlaubt.", presentation.InvalidTooltip);
    }
}
