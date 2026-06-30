using System;
using System.IO;
using System.Linq;

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
        Assert.Contains("_saveBannerTimer.Stop();", source);
        Assert.Contains("_autoSaveTimer.Stop();", source);
        Assert.Contains("LiveControlRetryBridge.Reset();", source);
    }

    private static string ReadPageViewModel(string fileName)
        => File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", fileName));

    private static string FindRepoFile(params string[] relativeParts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(SourceFilePath())! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
