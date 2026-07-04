using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.LiveControl;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PageViewModelLifecycleTests
{
    [Theory]
    [InlineData("DataPage")]
    [InlineData("OverviewPage")]
    [InlineData("ProjectPage")]
    public void Shell_subscriptions_are_disposed_by_page_viewmodels(string page)
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = CreatePageViewModel(page, shell, services);
        using var disposable = Assert.IsAssignableFrom<IDisposable>(viewModel);

        var projectNotifications = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Project))
                projectNotifications++;
        };

        shell.ReplaceProject(new Project { Name = "Vor Dispose" });
        Assert.True(projectNotifications > 0, $"{page} reagiert nicht auf Shell-Projektwechsel.");

        disposable.Dispose();
        var notificationsBeforeDispose = projectNotifications;
        shell.ReplaceProject(new Project { Name = "Nach Dispose" });

        Assert.Equal(notificationsBeforeDispose, projectNotifications);
    }

    [Fact]
    public void DataPage_dispose_removes_live_control_retry_handler()
    {
        LiveControlRetryBridge.Reset();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        using var viewModel = new DataPageViewModel(shell, services);

        var beforeDispose = LiveControlRetryBridge.Invoke("06-001");
        viewModel.Dispose();
        var afterDispose = LiveControlRetryBridge.Invoke("06-001");

        Assert.Contains("nicht im geladenen Projekt gefunden", beforeDispose.Message);
        Assert.Contains("Datenseite nicht geoeffnet", afterDispose.Message);
    }

    private static ObservableObject CreatePageViewModel(string page, ShellViewModel shell, ServiceProvider services)
        => page switch
        {
            "DataPage" => new DataPageViewModel(shell, services),
            "OverviewPage" => new OverviewPageViewModel(shell, services),
            "ProjectPage" => new ProjectPageViewModel(shell),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };
}
