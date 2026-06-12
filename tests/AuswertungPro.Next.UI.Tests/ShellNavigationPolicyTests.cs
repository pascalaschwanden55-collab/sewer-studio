using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNavigationPolicyTests
{
    [Fact]
    public void LeaveGuard_AllowsClose_WhenCurrentPageHasNoGuard()
        => Assert.True(ShellLeaveGuard.CanLeave(new object()));

    [Fact]
    public void LeaveGuard_CallsCurrentPageGuard()
    {
        var page = new FakeConfirmLeave(allowLeave: false);

        Assert.False(ShellLeaveGuard.CanLeave(page));
        Assert.Equal(1, page.Calls);
    }

    [Fact]
    public void PageLifecycle_DisposesPreviousPage_WhenReplaced()
    {
        var previous = new DisposablePage();
        var next = new object();

        ShellPageLifecycle.DisposeIfReplaced(previous, next);

        Assert.True(previous.Disposed);
    }

    [Fact]
    public void PageLifecycle_DoesNotDispose_WhenSamePageIsAssignedAgain()
    {
        var page = new DisposablePage();

        ShellPageLifecycle.DisposeIfReplaced(page, page);

        Assert.False(page.Disposed);
    }

    [Fact]
    public void ShellViewModel_UsesLifecycleHelperForCurrentPageReplacements()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

        Assert.Contains("SetCurrentPage(SelectedNavItem.CreatePage())", source);
        Assert.Contains("SetCurrentPage(new Pages.SanierungsMatrixPageViewModel", source);
        Assert.DoesNotContain("CurrentPage = SelectedNavItem.CreatePage()", source);
    }

    [Theory]
    [InlineData("Uebersicht")]
    [InlineData("Projekt")]
    [InlineData("Export")]
    [InlineData("Einstellungen")]
    public void CorePagesStayAvailableWithoutProject(string title)
    {
        var item = new ShellViewModel.NavItem("", title, () => new object());

        Assert.True(ShellNavigationPolicy.CanOpenWithoutProject(title));
        Assert.False(ShellNavigationPolicy.RequiresProject(title));
        Assert.True(item.CanOpenWithoutProject);
        Assert.False(item.RequiresProject);

        item.UpdateAvailability(isProjectReady: false);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }

    [Theory]
    [InlineData("Haltungen")]
    [InlineData("Schaechte")]
    [InlineData("Import")]
    [InlineData("VSA")]
    public void DataPagesStillRequireProject(string title)
    {
        var item = new ShellViewModel.NavItem("", title, () => new object());

        Assert.False(ShellNavigationPolicy.CanOpenWithoutProject(title));
        Assert.True(ShellNavigationPolicy.RequiresProject(title));
        Assert.False(item.CanOpenWithoutProject);
        Assert.True(item.RequiresProject);

        item.UpdateAvailability(isProjectReady: false);
        Assert.False(item.IsAvailable);
        Assert.Equal(0.5, item.AvailabilityOpacity);

        item.UpdateAvailability(isProjectReady: true);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }

    [Fact]
    public void NavItemCanBeExplicitlyAvailableWithoutProject()
    {
        var item = new ShellViewModel.NavItem("", "Custom", () => new object(), canOpenWithoutProject: true);

        item.UpdateAvailability(isProjectReady: false);

        Assert.True(item.CanOpenWithoutProject);
        Assert.False(item.RequiresProject);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }

    private sealed class FakeConfirmLeave(bool allowLeave) : IConfirmLeave
    {
        public int Calls { get; private set; }

        public bool ConfirmLeave()
        {
            Calls++;
            return allowLeave;
        }
    }

    private sealed class DisposablePage : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

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
