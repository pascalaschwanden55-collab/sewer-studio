using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingCatalogArchitectureTests
{
    [Fact]
    public void PlayerWindow_code_catalog_helpers_live_in_coding_catalog_partial()
    {
        var catalogPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.CodeCatalog.cs");

        Assert.True(File.Exists(catalogPath), "CodeCatalog-/VsaCodeExplorer-Helfer sollen nicht im LiveDetection-Partial liegen.");

        var catalog = File.ReadAllText(catalogPath);

        Assert.Contains("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", catalog);
        Assert.Contains("private AppProtocol.ICodeCatalogProvider? CodeCatalog", catalog);
        Assert.Contains("private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", catalog);
    }
}
