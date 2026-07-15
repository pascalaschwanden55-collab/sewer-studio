using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Sichere VisualTree-Navigation nach oben. HINTERGRUND: <see cref="UIElement.InputHitTest(Point)"/>
/// und <c>e.OriginalSource</c> von Maus-/Drag-Events koennen ein <see cref="System.Windows.ContentElement"/>
/// liefern (z.B. einen Text-<c>Run</c> in einer Tabellenzelle/Listen-Kachel). <see cref="VisualTreeHelper.GetParent"/>
/// kennt aber nur Visual/Visual3D und wuerfe sonst „... ist kein Visual oder Visual3D" (App-Absturz beim
/// Klick/Rechtsklick/Ziehen auf den Text). Diese Helfer springen darum ueber ContentElemente per LogicalTree
/// hoch und laufen erst ab einem Visual wieder im VisualTree weiter.
/// </summary>
public static class VisualTreeSafe
{
    /// <summary>Ein Schritt nach oben: fuer ContentElemente ueber den LogicalTree, sonst ueber den VisualTree.</summary>
    public static DependencyObject? GetParentSafe(DependencyObject? node)
    {
        if (node is null)
            return null;
        return node is Visual or Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
    }

    /// <summary>Laeuft nach oben bis zum ersten Vorfahren vom Typ <typeparamref name="T"/> (oder null).</summary>
    public static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = GetParentSafe(node);
        }
        return null;
    }

    /// <summary>Findet das erste benannte Kind im visuellen Baum.</summary>
    public static T? FindNamedDescendant<T>(DependencyObject? node, string childName)
        where T : FrameworkElement
    {
        if (node is not (Visual or Visual3D))
            return null;

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);
            if (child is T match && string.Equals(match.Name, childName, StringComparison.Ordinal))
                return match;

            var nested = FindNamedDescendant<T>(child, childName);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
