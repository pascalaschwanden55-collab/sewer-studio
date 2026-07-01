using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingCatalogArchitectureTests
{
    [Fact]
    public void PlayerWindow_code_catalog_helpers_live_in_coding_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeCatalog.cs");

        Assert.True(File.Exists(catalogPath), "CodeCatalog-/VsaCodeExplorer-Helfer sollen nicht im LiveDetection-Partial liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.DoesNotContain("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", liveDetection);
        Assert.DoesNotContain("private AppProtocol.ICodeCatalogProvider? CodeCatalog", liveDetection);
        Assert.DoesNotContain("private ViewModels.Windows.VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", liveDetection);
        Assert.Contains("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", catalog);
        Assert.Contains("private AppProtocol.ICodeCatalogProvider? CodeCatalog", catalog);
        Assert.Contains("private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", catalog);
    }
}
