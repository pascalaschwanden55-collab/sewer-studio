using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FluentIconTests
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
    public void Glyph_uebernimmt_den_Wert_als_sichtbaren_Text()
    {
        RunOnSta(() =>
        {
            var icon = new FluentIcon { Glyph = "\uE74E" };

            Assert.Equal("\uE74E", icon.Text);
            Assert.Same(IconFonts.Default, icon.FontFamily);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
