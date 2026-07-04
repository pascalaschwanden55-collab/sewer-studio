using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageViewModelThreadingTests
{
    [Fact]
    public void RecordPropertyChanged_FromBackgroundThread_RefreshesRowsOnUiDispatcher()
    {
        RunOnStaThread(() =>
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(
                new AppSettings(),
                new DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            using var viewModel = new BuilderPageViewModel(shell, services);

            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "06-001", FieldSource.Manual, userEdited: true);
            record.SetFieldValue("Eigentuemer", "Privat", FieldSource.Manual, userEdited: true);
            shell.Project.Data.Add(record);
            PumpDispatcherFor(TimeSpan.FromMilliseconds(350));
            Assert.Equal("Privat", Assert.Single(viewModel.Rows).Owner);

            Task.Run(() => record.SetFieldValue("Eigentuemer", "Kanton", FieldSource.Manual, userEdited: true))
                .GetAwaiter()
                .GetResult();
            PumpDispatcherFor(TimeSpan.FromMilliseconds(350));

            Assert.Equal("Kanton", Assert.Single(viewModel.Rows).Owner);
        });
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
}
