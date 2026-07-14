using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DialogServiceFolderSelectionTests
{
    [Fact]
    public void SelectFolder_VerwendetEchtenOrdnerdialog()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "DialogService.cs"));

        Assert.Contains("new OpenFolderDialog", source, StringComparison.Ordinal);
        Assert.Contains("dlg.FolderName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileName = \"Ordner auswaehlen\"", source, StringComparison.Ordinal);
    }
}
