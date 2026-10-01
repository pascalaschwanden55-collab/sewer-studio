using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditChromeAndGlyphTests
{
    [Fact]
    public void Key_windows_use_sewerstudio_titles_and_responsive_minimum_sizes()
    {
        AssertWindowChrome(
            ReadUiFile(Path.Combine("Views", "Windows", "TrainingCenterWindow.xaml")),
            "SewerStudio \u2014 Training Center",
            "MinWidth=\"1200\"",
            "MinHeight=\"720\"");

        var protocolRoot = GetRootWindowTag(ReadUiFile(Path.Combine("Views", "ProtocolObservationsWindow.xaml")));
        Assert.Contains("Title=\"SewerStudio \u2014 Beobachtungen / Sch\u00e4den\"", protocolRoot);
        Assert.Contains("WindowState=\"Maximized\"", protocolRoot);
        Assert.Contains("MinWidth=\"900\"", protocolRoot);
        Assert.Contains("MinHeight=\"600\"", protocolRoot);
        Assert.DoesNotContain("Width=\"980\"", protocolRoot);
        Assert.DoesNotContain("Height=\"620\"", protocolRoot);

        AssertWindowChrome(
            ReadUiFile(Path.Combine("Views", "Windows", "DossierPrintDialog.xaml")),
            "SewerStudio \u2014 Haltungsdossier drucken",
            "MinWidth=\"480\"",
            "MinHeight=\"620\"");
    }

    private static void AssertWindowChrome(string xaml, string expectedTitle, string expectedMinWidth, string expectedMinHeight)
    {
        var root = GetRootWindowTag(xaml);
        Assert.Contains($"Title=\"{expectedTitle}\"", root);
        Assert.Contains(expectedMinWidth, root);
        Assert.Contains(expectedMinHeight, root);
    }

    private static string GetRootWindowTag(string xaml)
    {
        var end = xaml.IndexOf('>');
        Assert.True(end > 0, "Window root tag was not found.");
        return xaml[..end];
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = RepoFile("src", "AuswertungPro.Next.UI", relativePath);
        return File.ReadAllText(path);
    }
}
