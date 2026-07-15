using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VisualTreeConsolidationTests
{
    [Fact]
    public void UI_parent_searches_use_the_shared_safe_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var helperPath = Path.GetFullPath(Path.Combine(uiRoot, "Behaviors", "VisualTreeSafe.cs"));

        var violations = Directory
            .EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetFullPath(path).Equals(helperPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .Where(file =>
                file.Source.Contains("VisualTreeHelper.GetParent(", StringComparison.Ordinal)
                || file.Source.Contains("LogicalTreeHelper.GetParent(", StringComparison.Ordinal)
                || file.Source.Contains("private static T? FindAncestor<", StringComparison.Ordinal)
                || file.Source.Contains("FindCodingChild<", StringComparison.Ordinal)
                || (Path.GetFileName(file.Path).StartsWith("PlayerWindow", StringComparison.Ordinal)
                    && file.Source.Contains("VisualTreeHelper.GetChildrenCount(", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(root, file.Path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "VisualTree-Suche ausserhalb von VisualTreeSafe gefunden: " + string.Join(", ", violations));
    }
}
