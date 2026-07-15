namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Art eines Knotens in der grafischen Ordnerbaum-Vorschau.</summary>
public enum DistributionTreeNodeKind
{
    Ordner,
    Pdf,
    Video
}

/// <summary>
/// Ein Knoten der grafischen Ordnerbaum-Vorschau (eine eingerueckte Zeile mit Icon).
/// <see cref="Depth"/> = Einrueck-Tiefe (0 = Ziel-Wurzel).
/// </summary>
public sealed record DistributionTreeNode(string Label, DistributionTreeNodeKind Kind, int Depth);
