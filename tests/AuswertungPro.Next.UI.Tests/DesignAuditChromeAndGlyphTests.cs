using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditChromeAndGlyphTests
{
    private static readonly Regex VisibleSymbolRegex = new(
        @"[✓✕✎⚠→]|\ud83c[\udc00-\udfff]|\ud83d[\udc00-\udfff]|\ud83e[\udd00-\udfff]",
        RegexOptions.Compiled);

    private static readonly string[] ConvertedGlyphFiles =
    [
        Path.Combine("Dialogs", "CostCatalogEditorDialog.xaml"),
        Path.Combine("Dialogs", "PositionTemplateEditorDialog.xaml"),
        Path.Combine("Theme", "Controls.xaml"),
        Path.Combine("Views", "Windows", "MeasureTemplateEditorWindow.xaml"),
        Path.Combine("Views", "Windows", "TrainingCenterWindow.xaml"),
        Path.Combine("Views", "Windows", "VideoAnalysisPipelineWindow.xaml")
    ];

    [Fact]
    public void Converted_xaml_files_use_mdl2_glyphs_instead_of_visible_symbol_characters()
    {
        foreach (var relativePath in ConvertedGlyphFiles)
        {
            var xaml = ReadUiFile(relativePath);

            Assert.DoesNotMatch(VisibleSymbolRegex, xaml);
            Assert.DoesNotContain("Segoe UI Emoji", xaml);
        }
    }

    [Fact]
    public void Shell_view_model_has_no_dead_guide_code_when_xaml_has_no_guide_bindings()
    {
        var uiRoot = Path.Combine(FindRepoRoot(), "src", "AuswertungPro.Next.UI");
        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(uiRoot, file);
            Assert.DoesNotContain("Guide", File.ReadAllText(file), StringComparison.Ordinal);
            Assert.DoesNotContain("Guide", relative, StringComparison.Ordinal);
        }

        var shell = ReadUiFile(Path.Combine("ViewModels", "ShellViewModel.cs"));
        Assert.DoesNotContain("Guide", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Ratten-Assistent", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_windows_use_sewerstudio_titles_and_responsive_minimum_sizes()
    {
        AssertWindowChrome(
            ReadUiFile(Path.Combine("Views", "Windows", "TrainingCenterWindow.xaml")),
            "SewerStudio — Training Center",
            "MinWidth=\"1200\"",
            "MinHeight=\"720\"");

        var protocolRoot = GetRootWindowTag(ReadUiFile(Path.Combine("Views", "ProtocolObservationsWindow.xaml")));
        Assert.Contains("Title=\"SewerStudio — Beobachtungen / Schäden\"", protocolRoot);
        Assert.Contains("WindowState=\"Maximized\"", protocolRoot);
        Assert.DoesNotContain("Width=\"980\"", protocolRoot);
        Assert.DoesNotContain("Height=\"620\"", protocolRoot);
        Assert.Contains("MinWidth=\"900\"", protocolRoot);
        Assert.Contains("MinHeight=\"600\"", protocolRoot);

        AssertWindowChrome(
            ReadUiFile(Path.Combine("Views", "Windows", "CorrectionDialog.xaml")),
            "SewerStudio — Korrektur",
            "MinWidth=\"420\"",
            "MinHeight=\"520\"");

        AssertWindowChrome(
            ReadUiFile(Path.Combine("Views", "Windows", "DossierPrintDialog.xaml")),
            "SewerStudio — Haltungsdossier drucken",
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
        var path = Path.Combine(FindRepoRoot(), "src", "AuswertungPro.Next.UI", relativePath);
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
