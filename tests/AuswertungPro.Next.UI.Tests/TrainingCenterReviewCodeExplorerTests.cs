using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewCodeExplorerTests
{
    [Fact]
    public void ReviewKorrektur_nutzt_vsa_code_explorer_statt_freitext_dialog()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));

        var methodStart = source.IndexOf("private async void ReviewCorrect_Click", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ReviewCorrect_Click wurde nicht gefunden.");

        var methodEnd = source.IndexOf("//", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart, "Ende des ReviewCorrect_Click-Blocks wurde nicht gefunden.");

        var method = source[methodStart..methodEnd];
        Assert.Contains("new VsaCodeExplorerWindow", method);
        Assert.Contains("BuildReviewProtocolEntry", source);
        Assert.Contains("correctedDescription: dlg.SelectedEntry.Beschreibung", method);
        Assert.DoesNotContain("new CorrectionDialog", method);
    }

}
