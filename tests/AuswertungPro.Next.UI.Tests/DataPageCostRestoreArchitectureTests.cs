using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCostRestoreArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_kosten_wiederherstellung_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private void RestoreCosts(HaltungRecord? record)");

        Assert.Contains("_costRestoreController.Restore(record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectCostStoreRepository", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCostsToRecord", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Info(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Warn(", method, StringComparison.Ordinal);
    }

}
