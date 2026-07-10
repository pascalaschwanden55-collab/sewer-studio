using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteDockingTests
{
    [Fact]
    public void KarteWindow_HostsExistingContent_InsteadOfCreatingNewKartePage()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "KarteWindow.xaml"));
        var code = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "KarteWindow.xaml.cs"));

        Assert.Contains("x:Name=\"ContentHost\"", xaml);
        Assert.DoesNotContain("<pages:KartePage", xaml, StringComparison.Ordinal);
        Assert.Contains("public void SetContent(UIElement content)", code);
        Assert.Contains("public UIElement? TakeContent()", code);
    }

    [Fact]
    public void MainWindow_DetachesCurrentKartePage_AndRedocksItOnClose()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "MainWindow.xaml.cs"));

        Assert.Contains("shell.CurrentPage is KartePage currentPage", source);
        Assert.Contains("shell.CurrentPage = placeholder", source);
        Assert.Contains("new KarteWindow(page)", source);
        Assert.Contains("window.TakeContent()", source);
        Assert.Contains("shell.CurrentPage = dockedPage", source);
    }

    [Fact]
    public void KartePage_DoesNotBuildMapAgain_WhenReloadedAfterDocking()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "KartePage.xaml.cs"));

        Assert.Contains("private bool _mapInitialized", source);
        Assert.Contains("if (!_mapInitialized && !_mapBuildInProgress)", source);
        Assert.Equal(1, CountOccurrences(source, "BuildMapAsync()"));
        Assert.Contains("await RefreshWhenSizedAsync(centerInitial: false)", source);
    }

    [Fact]
    public void ShellNavigation_CreatesReusableKartePage_ForDocking()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

        Assert.Contains("new AuswertungPro.Next.UI.Views.Pages.KartePage", source);
        Assert.Contains("DataContext = new Pages.KarteViewModel(this, _sp)", source);
    }

    private static int CountOccurrences(string source, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
