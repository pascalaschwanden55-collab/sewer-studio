using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePartialHandlerTests
{
    [Fact]
    public void Every_DataPage_Xaml_event_handler_exists_in_a_partial_class_file()
    {
        var pagesRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pagesRoot, "DataPage.xaml"));
        var code = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(pagesRoot, "DataPage*.cs")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));

        var handlers = Regex.Matches(
                xaml,
                "(?<![A-Za-z0-9_])(?:Checked|Unchecked|Click|KeyDown|PreviewKeyDown|PreviewMouseLeftButtonDown|PreviewMouseRightButtonDown|PreviewMouseMove|PreviewMouseWheel|PreviewMouseDoubleClick|Drop|LoadingRow|TextChanged|CellEditEnding|PreparingCellForEdit|CurrentCellChanged)\\s*=\\s*\"(?<handler>[A-Za-z_][A-Za-z0-9_]*)\"")
            .Select(match => match.Groups["handler"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            Assert.Matches(
                $@"\bvoid\s+{Regex.Escape(handler)}\s*\(",
                code);
        }
    }
}
