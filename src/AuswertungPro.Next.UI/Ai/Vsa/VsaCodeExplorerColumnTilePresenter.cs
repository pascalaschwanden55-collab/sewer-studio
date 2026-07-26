using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public enum VsaCodeExplorerColumnTileBrushRole
{
    Group,
    Accent,
    Invalid,
    Text,
    TextSecondary
}

public sealed record VsaCodeExplorerColumnTileBadge(string Text, string ColorHex);

public sealed record VsaCodeExplorerColumnTilePresentation(
    string LabelText,
    string DescriptionText,
    bool ShowDescription,
    string? GroupColorHex,
    VsaCodeExplorerColumnTileBrushRole MarkerBrushRole,
    VsaCodeExplorerColumnTileBrushRole CodeBrushRole,
    VsaCodeExplorerColumnTileBrushRole DescriptionBrushRole,
    IReadOnlyList<VsaCodeExplorerColumnTileBadge> Badges,
    bool ShowSelectedChrome,
    bool ShowInvalidChrome,
    string? InvalidTooltip);

public static class VsaCodeExplorerColumnTilePresenter
{
    public static VsaCodeExplorerColumnTilePresentation Build(TileItem tile)
    {
        var markerRole = ResolveGroupRole(tile);
        var descriptionRole = tile.IsInvalid
            ? VsaCodeExplorerColumnTileBrushRole.Invalid
            : tile.IsSelected
                ? VsaCodeExplorerColumnTileBrushRole.Text
                : VsaCodeExplorerColumnTileBrushRole.TextSecondary;
        var badges = new List<VsaCodeExplorerColumnTileBadge>();

        if (tile.BadgeText is not null)
            badges.Add(new VsaCodeExplorerColumnTileBadge(tile.BadgeText, tile.BadgeColor ?? "#2563EB"));

        if (tile.IsFinal && !tile.IsSelected)
            badges.Add(new VsaCodeExplorerColumnTileBadge("End", "#16A34A"));

        return new VsaCodeExplorerColumnTilePresentation(
            LabelText: tile.Label,
            DescriptionText: tile.Description ?? string.Empty,
            ShowDescription: !string.IsNullOrEmpty(tile.Description),
            GroupColorHex: tile.GroupColor,
            MarkerBrushRole: markerRole,
            CodeBrushRole: markerRole,
            DescriptionBrushRole: descriptionRole,
            Badges: badges,
            ShowSelectedChrome: tile.IsSelected,
            ShowInvalidChrome: tile.IsInvalid,
            InvalidTooltip: tile.IsInvalid
                ? "Als ungueltig markiert - Auswahl ist trotzdem erlaubt."
                : null);
    }

    private static VsaCodeExplorerColumnTileBrushRole ResolveGroupRole(TileItem tile)
    {
        if (tile.IsInvalid)
            return VsaCodeExplorerColumnTileBrushRole.Invalid;

        return tile.IsSelected
            ? VsaCodeExplorerColumnTileBrushRole.Accent
            : VsaCodeExplorerColumnTileBrushRole.Group;
    }
}
