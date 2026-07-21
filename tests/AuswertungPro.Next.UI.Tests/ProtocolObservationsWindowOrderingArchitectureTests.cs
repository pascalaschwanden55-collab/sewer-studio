using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolObservationsWindowOrderingArchitectureTests
{
    [Fact]
    public void Fenster_delegiert_Sortierregeln_und_behaelt_Auswahl_und_Anzeigeaktualisierung()
    {
        var windowPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "ProtocolObservationsWindow.xaml.cs");
        var window = File.ReadAllText(windowPath);

        var methodStart = window.IndexOf("private void ResortActiveEntries", StringComparison.Ordinal);
        var methodEnd = window.IndexOf(
            "private IReadOnlyList<ProtocolEntry> BuildImportedEntries",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = window[methodStart..methodEnd];

        AssertInOrder(
            method,
            "ProtocolEntryOrdering.Order(_doc.Current.Entries)",
            "var active = ordered.Where(entry => !entry.IsDeleted).ToList();",
            "_doc.Current.Entries.Clear();",
            "foreach (var entry in ordered)",
            "_doc.Current.Entries.Add(entry);",
            "_isRefreshingEntries = true;",
            "_entries.Clear();",
            "foreach (var entry in active)",
            "_entries.Add(entry);",
            "EntriesGrid.SelectedItem = target;",
            "_isRefreshingEntries = false;",
            "EntriesGrid.Items.Refresh();");

        Assert.DoesNotContain("OrderBy(", method);
        Assert.DoesNotContain("ThenBy(", method);
        Assert.DoesNotContain("vsa.distanz", method);
        Assert.DoesNotContain("Distance", method);
        Assert.DoesNotContain("TryGetPrimaryOrderingMeter", window);
        Assert.DoesNotContain("TryGetSecondaryOrderingMeter", window);
    }

    private static void AssertInOrder(string text, params string[] expectedParts)
    {
        var previousIndex = -1;
        foreach (var part in expectedParts)
        {
            var currentIndex = text.IndexOf(part, StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex, $"'{part}' steht nicht an der erwarteten Stelle.");
            previousIndex = currentIndex;
        }
    }
}
