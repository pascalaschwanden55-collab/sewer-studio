namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed record TileItem
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Description { get; init; }
    public string? BadgeText { get; init; }
    public string? BadgeColor { get; init; }
    public bool IsInvalid { get; init; }
    public bool IsFinal { get; init; }
    public bool IsSteuer { get; init; }
    public string? GroupColor { get; init; }
    public string? Icon { get; init; }
    public bool IsSelected { get; init; }
}
