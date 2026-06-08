using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewCodeExplorerTests
{
    [Fact]
    public void ReviewKorrektur_nutzt_vsa_code_explorer_statt_freitext_dialog()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));

        var methodStart = source.IndexOf("private async void ReviewCorrect_Click", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ReviewCorrect_Click wurde nicht gefunden.");

        var methodEnd = source.IndexOf("//", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart, "Ende des ReviewCorrect_Click-Blocks wurde nicht gefunden.");

        var method = source[methodStart..methodEnd];
        Assert.Contains("new VsaCodeExplorerWindow", method);
        Assert.Contains("BuildReviewProtocolEntry", source);
        Assert.DoesNotContain("new CorrectionDialog", method);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
