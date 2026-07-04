using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageOriginalPdfArchitectureTests
{
    [Fact]
    public void DataPage_context_menus_can_reveal_haltung_folder()
    {
        var pageXaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
        var pageCode = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var ansichtXaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml"));
        var ansichtCode = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml.cs"));

        Assert.Contains("Header=\"Gehe zu Ordner\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenContainingFolderMenu_Click\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Gehe zu Ordner\"", ansichtXaml, StringComparison.Ordinal);
        Assert.Contains("CtxOpenFolder_Click", ansichtXaml, StringComparison.Ordinal);
        Assert.Contains("RaiseAction(\"openfolder\")", ansichtCode, StringComparison.Ordinal);
        Assert.Contains("case \"openfolder\": OpenContainingFolderMenu_Click(this, e); break;", pageCode, StringComparison.Ordinal);
    }
}
