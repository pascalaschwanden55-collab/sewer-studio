using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterUiThreadArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_nutzt_zentralen_ui_thread_dispatcher()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));

        Assert.Contains("UiThreadDispatcher.Run(action)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Application.Current?.Dispatcher", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }
}
