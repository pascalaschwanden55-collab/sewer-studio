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

    [Fact]
    public void Player_keeps_coding_overlay_visible_when_window_loses_focus()
    {
        var coding = ReadUiFile("Views", "Windows", "PlayerWindow.Coding.cs");
        var suspendBody = ExtractMethodBody(coding, "private void SuspendCodingOverlayInput()");

        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = false", suspendBody);
        Assert.Contains("CodingOverlayCanvas.IsHitTestVisible = false", suspendBody);
    }

    [Fact]
    public void Player_uses_same_overlay_policy_for_rendering_and_events()
    {
        var coding = ReadUiFile("Views", "Windows", "PlayerWindow.Coding.cs");

        Assert.Contains("BuildVisibleCodingFindings", coding);
        Assert.Contains("SamMaskRenderer.RenderCandidates", coding);
        Assert.Contains("visibleCodierbar", coding);
        Assert.DoesNotContain("AddMultiModelFindingsAsEvents(\r\n                    segmented.Where(s => s.Proximity.IsCodierbar).ToList()", coding);
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method signature not found: {signature}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"Method body not found: {signature}");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceStart..(i + 1)];
            }
        }

        throw new InvalidDataException($"Method body not closed: {signature}");
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
