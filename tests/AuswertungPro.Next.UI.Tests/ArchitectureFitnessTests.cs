using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ArchitectureFitnessTests
{
    [Fact]
    public void Converted_xaml_files_use_icon_font_glyphs_instead_of_visible_symbol_characters()
    {
        var visibleSymbolRegex = new System.Text.RegularExpressions.Regex(
            @"[\u2713\u2715\u270e\u26a0\u2192]|\ud83c[\udc00-\udfff]|\ud83d[\udc00-\udfff]|\ud83e[\udd00-\udfff]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var files = new[]
        {
            RepoFile("src", "AuswertungPro.Next.UI", "Dialogs", "CostCatalogEditorDialog.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Dialogs", "PositionTemplateEditorDialog.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "MeasureTemplateEditorWindow.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "VideoAnalysisPipelineWindow.xaml")
        };

        var offenders = files
            .Select(path =>
            {
                var xaml = File.ReadAllText(path);
                var issues = new List<string>();
                if (visibleSymbolRegex.IsMatch(xaml))
                    issues.Add("visible symbol");
                if (xaml.Contains("Segoe UI Emoji", StringComparison.Ordinal))
                    issues.Add("Segoe UI Emoji");

                return new { Path = path, Issues = issues };
            })
            .Where(item => item.Issues.Count > 0)
            .Select(item => $"{Path.GetFileName(item.Path)}: {string.Join(", ", item.Issues)}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Konvertierte XAML-Dateien sollen MDL2/Icon-Font-Glyphs statt sichtbarer Symbol-/Emoji-Zeichen verwenden:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Shell_view_model_has_no_dead_guide_code_when_xaml_has_no_guide_bindings()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var xamlOffenders = Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var relative = Path.GetRelativePath(uiRoot, file);
                var issues = new List<string>();
                if (File.ReadAllText(file).Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide content");
                if (relative.Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide path");
                return issues.Select(issue => $"{relative}: {issue}");
            });

        var shellOffenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"),
            "Guide",
            "Ratten-Assistent");

        var offenders = xamlOffenders.Concat(shellOffenders).ToArray();

        Assert.True(
            offenders.Length == 0,
            "ShellViewModel soll keinen toten Guide-Code behalten, wenn XAML keine Guide-Bindings mehr hat:\n"
            + string.Join("\n", offenders));
    }

}
