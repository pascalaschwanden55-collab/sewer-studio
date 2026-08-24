using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewNavigationTests
{
    [Fact]
    public void OverviewViewModel_speichert_keinen_ServiceProvider_als_Feld()
    {
        var fields = typeof(OverviewPageViewModel).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
    }

    [Fact]
    public void NavigateConditionCommand_oeffnet_haltungen_auch_fuer_ohne_zustand()
    {
        using var scope = CreateOverview();

        scope.ViewModel.NavigateConditionCommand.Execute("ohne");

        var dataPage = Assert.IsType<DataPageViewModel>(scope.Shell.CurrentPage);
        Assert.Equal(new DataPageStartFilter("Zustandsklasse", "ohne"), dataPage.StartFilter);
    }

    [Fact]
    public void NavigateSchachtConditionCommand_oeffnet_schaechte_seite()
    {
        using var scope = CreateOverview();

        scope.ViewModel.NavigateSchachtConditionCommand.Execute("2");

        Assert.Equal(ShellMode.Workspace, scope.Shell.CurrentMode);
        Assert.Equal("Schaechte", scope.Shell.SelectedNavItem?.Title);
        Assert.IsType<SchaechtePageViewModel>(scope.Shell.CurrentPage);
    }

    [Fact]
    public void NavigateToHolding_oeffnet_Haltungen_und_waehlt_den_Datensatz()
    {
        using var scope = CreateOverview();
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "36051-36329", FieldSource.Manual, userEdited: true);
        scope.Shell.Project.Data.Add(record);

        scope.Shell.NavigateToHolding(record);

        Assert.Equal("Haltungen", scope.Shell.SelectedNavItem?.Title);
        var dataPage = Assert.IsType<DataPageViewModel>(scope.Shell.CurrentPage);
        Assert.Same(record, dataPage.Selected);
    }

    [Fact]
    public void NavigateToShaft_oeffnet_Schaechte_und_waehlt_den_Datensatz()
    {
        using var scope = CreateOverview();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "36051");
        scope.Shell.Project.SchaechteData.Add(record);

        scope.Shell.NavigateToShaft(record);

        Assert.Equal("Schaechte", scope.Shell.SelectedNavItem?.Title);
        var shaftPage = Assert.IsType<SchaechtePageViewModel>(scope.Shell.CurrentPage);
        Assert.Same(record, shaftPage.Selected);
    }

    private static OverviewScope CreateOverview()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);
        return new OverviewScope(loggerFactory, shell, vm);
    }

    private sealed class OverviewScope(
        ILoggerFactory loggerFactory,
        ShellViewModel shell,
        OverviewPageViewModel viewModel) : IDisposable
    {
        public ShellViewModel Shell { get; } = shell;
        public OverviewPageViewModel ViewModel { get; } = viewModel;

        public void Dispose()
        {
            Shell.Dispose();
            loggerFactory.Dispose();
        }
    }
}
