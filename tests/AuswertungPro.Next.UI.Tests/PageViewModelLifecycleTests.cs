using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PageViewModelLifecycleTests
{
    [Theory]
    [InlineData("DataPageViewModel.cs", "DataPageViewModel")]
    [InlineData("OverviewPageViewModel.cs", "OverviewPageViewModel")]
    [InlineData("ProjectPageViewModel.cs", "ProjectPageViewModel")]
    public void Shell_subscriptions_are_disposed_by_page_viewmodels(string fileName, string typeName)
    {
        var source = ReadPageViewModel(fileName);

        Assert.Contains($"class {typeName} : ObservableObject, IDisposable", source);
        Assert.Contains("_shell.PropertyChanged += ShellPropertyChanged;", source);
        Assert.Contains("_shell.PropertyChanged -= ShellPropertyChanged;", source);
        Assert.Contains("public void Dispose()", source);
        Assert.DoesNotContain("_shell.PropertyChanged += (_, e) =>", source);
    }

    [Fact]
    public void DataPage_dispose_releases_self_subscriptions_timers_and_live_control_retry()
    {
        var source = ReadPageViewModel("DataPageViewModel.cs");

        Assert.Contains("PropertyChanged -= DataPageViewModel_PropertyChanged;", source);
        Assert.Contains("_timers.Stop();", source);
        Assert.DoesNotContain("_saveBannerTimer.Stop();", source);
        Assert.DoesNotContain("_autoSaveTimer.Stop();", source);
        Assert.Contains("LiveControlRetryBridge.Reset();", source);
    }

    private static string ReadPageViewModel(string fileName)
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", fileName));
}
