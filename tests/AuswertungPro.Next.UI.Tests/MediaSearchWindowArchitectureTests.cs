using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MediaSearchWindowArchitectureTests
{
    [Fact]
    public void Fenster_delegiert_Datensatzmutation_und_behaelt_Einstellungen_und_Abschluss()
    {
        var windowPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "MediaSearchWindow.xaml.cs");
        var controllerPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "MediaSearchApplyController.cs");
        var photoImportPath = RepoFile(
            "src",
            "AuswertungPro.Next.Application",
            "Media",
            "PhotoImportService.cs");

        Assert.True(File.Exists(controllerPath), controllerPath);
        var window = File.ReadAllText(windowPath);
        var controller = File.ReadAllText(controllerPath);
        var photoImport = File.ReadAllText(photoImportPath);

        Assert.Contains("MediaSearchApplyController.Apply(", window);
        Assert.Contains("_settings.LastVideoSourceFolder = FolderBox.Text.Trim();", window);
        Assert.Contains("_settings.Save();", window);
        Assert.Contains("DialogResult = true;", window);
        Assert.Contains("Close();", window);

        var applyStart = window.IndexOf("private void Apply_Click", StringComparison.Ordinal);
        var applyEnd = window.IndexOf("private void Close_Click", applyStart, StringComparison.Ordinal);
        Assert.True(applyStart >= 0 && applyEnd > applyStart);
        var applyMethod = window[applyStart..applyEnd];
        AssertInOrder(
            applyMethod,
            "MediaSearchApplyController.Apply(",
            "_settings.LastVideoSourceFolder = FolderBox.Text.Trim();",
            "_settings.Save();",
            "DialogResult = true;",
            "Close();");

        Assert.DoesNotContain("SetFieldValue(", window);
        Assert.DoesNotContain("new AuswertungPro.Next.Domain.Protocol.ProtocolDocument", window);
        Assert.DoesNotContain("TryParseMeterFromFileName", window);
        Assert.Contains("PhotoFileMeterParser.TryParseFromPath", controller);
        Assert.Contains("PhotoProtocolEntryMatcher.FindNearestActiveEntry", controller);
        Assert.Contains("PhotoFileMeterParser.TryParseFromPath(file)", photoImport);
        Assert.Contains("PhotoProtocolEntryMatcher.FindNearestActiveEntry", photoImport);
        Assert.DoesNotContain("Regex.Match", photoImport);
        Assert.DoesNotContain("OrderBy(e => Math.Abs", photoImport);
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
