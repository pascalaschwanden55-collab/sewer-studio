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

    [Fact]
    public void Sanierungs_matrix_detail_ui_does_not_reintroduce_removed_grouped_measure_layout()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SanierungsMatrixPage.xaml"),
            "Header=\"Hauptarbeit\"",
            "DataContext.GroupedMeasureOptions",
            "<ComboBox.GroupStyle>");

        Assert.True(
            offenders.Length == 0,
            "SanierungsMatrixPage soll die Massnahmen-Spalte und das Lesedetail ohne altes GroupedMeasure-Layout behalten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void DataPage_measure_entry_uses_matrix_navigation_without_old_sanierung_window_path()
    {
        var shell = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        var singleModeBlock = ExtractBlockUntilReturn(shell, "if (singleHoldingMode)");
        var offenders = new List<string>();
        if (singleModeBlock.Contains("SelectedNavItem = target;", StringComparison.Ordinal))
            offenders.Add("ShellViewModel.cs singleHoldingMode: SelectedNavItem = target;");

        offenders.AddRange(FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"),
            "OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator)"));

        Assert.True(
            offenders.Count == 0,
            "DataPage-Sanierungseinstieg soll direkt in die Matrix navigieren und den alten Fensterpfad nicht reaktivieren:\n"
            + string.Join("\n", offenders));
    }

    private static string ExtractBlockUntilReturn(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Block-Marker wurde nicht gefunden: {marker}");

        var end = source.IndexOf("return;", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Block-Ende wurde nicht gefunden: {marker}");

        return source[start..end];
    }

}
