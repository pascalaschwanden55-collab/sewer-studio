using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageViewModelDependencyTests
{
    [Fact]
    public void DataPage_haelt_Dialoge_und_Einstellungen_gezielt()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("private readonly IDialogService _dialogs;", source);
        Assert.Contains("private readonly AppSettings _settings;", source);
        Assert.DoesNotContain("_sp.Dialogs", source);
        Assert.DoesNotContain("_sp.Settings", source);
    }
}
