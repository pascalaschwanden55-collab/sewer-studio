using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditPlayerCodingSidePanelTests
{
    [Fact]
    public void Player_coding_side_panel_has_only_the_active_inline_detail_panel()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var coding = ReadUiFile("Views", "Windows", "PlayerWindow.Coding.cs");

        Assert.Contains("x:Name=\"CodingDefectDetailInline\"", sidePanel);
        Assert.DoesNotContain("x:Name=\"CodingDefectDetailPanel\"", sidePanel);
        Assert.DoesNotContain("CodingDefectDetailPanel", accessors);
        Assert.DoesNotContain("CodingDefectDetailPanel", coding);
        Assert.DoesNotContain("UpdateCodingDefectDetailPanel", coding);
    }

    [Fact]
    public void Player_coding_side_panel_uses_readable_font_sizes_and_section_labels()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");

        Assert.DoesNotContain("FontSize=\"8\"", sidePanel);
        Assert.DoesNotContain("FontSize=\"9\"", sidePanel);
        Assert.Contains("Style=\"{DynamicResource SectionLabel}\"", sidePanel);
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}
