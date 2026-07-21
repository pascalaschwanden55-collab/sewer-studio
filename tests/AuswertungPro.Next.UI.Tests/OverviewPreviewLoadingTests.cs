using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewPreviewLoadingTests
{
    [Fact]
    public void Schneller_Auswahlwechsel_zeigt_nur_letzte_Vorschau()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var first = SaveProject(temp.Path, "A", "Erstes Projekt");
            var second = SaveProject(temp.Path, "B", "Letztes Projekt");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var settings = new AppSettings
            {
                EnableRestorePoints = false,
                ProjectsRootDirectory = temp.Path
            };
            var services = new ServiceProvider(
                settings,
                new DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);

            vm.SelectedProjectEntry = vm.ProjectEntries.Single(e => e.Path == first);
            vm.SelectedProjectEntry = vm.ProjectEntries.Single(e => e.Path == second);
            PumpDispatcherUntil(
                () => !vm.IsPreviewLoading && vm.SelectedPreview?.Path == second,
                TimeSpan.FromSeconds(3));

            Assert.False(vm.IsPreviewLoading);
            Assert.Equal(second, vm.SelectedPreview?.Path);
            Assert.Equal("Letztes Projekt", vm.SelectedPreview?.Name);
        });
    }

    [Fact]
    public void Rueckkehr_zur_bereits_angezeigten_auswahl_verwirft_fremde_wartende_vorschau()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var first = SaveProject(temp.Path, "A", "Erstes Projekt");
            var second = SaveProject(temp.Path, "B", "Zweites Projekt");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var settings = new AppSettings
            {
                EnableRestorePoints = false,
                ProjectsRootDirectory = temp.Path
            };
            var services = new ServiceProvider(
                settings,
                new DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);
            var firstEntry = vm.ProjectEntries.Single(e => e.Path == first);
            var secondEntry = vm.ProjectEntries.Single(e => e.Path == second);

            vm.SelectedProjectEntry = firstEntry;
            PumpDispatcherUntil(
                () => !vm.IsPreviewLoading && vm.SelectedPreview?.Path == first,
                TimeSpan.FromSeconds(3));

            vm.SelectedProjectEntry = secondEntry;
            vm.SelectedProjectEntry = firstEntry;
            PumpDispatcherFor(TimeSpan.FromMilliseconds(350));

            Assert.False(vm.IsPreviewLoading);
            Assert.Equal(first, vm.SelectedPreview?.Path);
            Assert.Equal("Erstes Projekt", vm.SelectedPreview?.Name);
        });
    }

    [Fact]
    public void BuildPreview_Steigt_bei_geoeffnetem_Projekt_frueh_aus()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var openProject = SaveProject(temp.Path, "Offen", "Offenes Projekt");
            var previewProject = SaveProject(temp.Path, "Vorschau", "Darf nicht laden");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var settings = new AppSettings
            {
                EnableRestorePoints = false,
                ProjectsRootDirectory = temp.Path
            };
            var services = new ServiceProvider(
                settings,
                new DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);

            Assert.True(shell.TryOpenProject(openProject));
            vm.SelectedProjectEntry = vm.ProjectEntries.Single(e => e.Path == previewProject);
            PumpDispatcherFor(TimeSpan.FromMilliseconds(350));

            Assert.True(vm.ShowFullDashboard);
            Assert.False(vm.IsPreviewLoading);
            Assert.Null(vm.SelectedPreview);
        });
    }

    private static string SaveProject(string root, string folderName, string projectName)
    {
        var projectFile = Path.Combine(root, folderName, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        var save = new JsonProjectRepository().Save(new Project { Name = projectName }, projectFile);
        Assert.True(save.Ok, save.ErrorMessage);
        return projectFile;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private static void PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            PumpDispatcherFor(TimeSpan.FromMilliseconds(50));
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "OverviewPreviewLoadingTests_" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
