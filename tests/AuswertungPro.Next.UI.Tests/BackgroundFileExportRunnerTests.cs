using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackgroundFileExportRunnerTests
{
    [Fact]
    public void Async_npk_exports_keep_existing_xaml_command_names()
    {
        var viewModelType = typeof(BuilderPageViewModel);

        Assert.NotNull(viewModelType.GetProperty("ExportNpkLeistungsverzeichnisCommand"));
        Assert.NotNull(viewModelType.GetProperty("ExportNpkLeistungsverzeichnisExcelCommand"));
    }

    [Fact]
    public async Task RunAsync_returns_before_blocked_export_finishes_and_uses_another_thread()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var callerThread = Environment.CurrentManagedThreadId;
        var exportThread = callerThread;

        var task = BackgroundFileExportRunner.RunAsync(() =>
        {
            exportThread = Environment.CurrentManagedThreadId;
            started.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        });

        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(task.IsCompleted);
        Assert.NotEqual(callerThread, exportThread);

        release.Set();
        await task;
    }
}
