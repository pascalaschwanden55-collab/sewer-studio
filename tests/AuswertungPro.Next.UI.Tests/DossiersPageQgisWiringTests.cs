using System.IO;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossiersPageQgisWiringTests
{
    [Fact]
    public void Dossier_Tabellen_melden_jeden_linken_Zeilenklick_an_Qgis()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

        Assert.Contains(
            "PreviewMouseLeftButtonDown=\"HoldingGrid_QgisReselectOnClick\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "PreviewMouseLeftButtonDown=\"ShaftGrid_QgisReselectOnClick\"",
            xaml,
            StringComparison.Ordinal);
    }
}
