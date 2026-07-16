using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DialogHostDependencyTests
{
    [Fact]
    public void DialogHost_is_an_immutable_fallback()
    {
        var source = ReadUiFile("Services", "DialogHost.cs");
        var app = ReadUiFile("App.xaml.cs");

        Assert.Contains("private static readonly Lazy<IDialogService> Fallback", source, StringComparison.Ordinal);
        Assert.Contains("=> Fallback.Value;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_current", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Configure(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DialogHost.Configure", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_player_and_training_paths_receive_the_registered_dialog_service()
    {
        var mainWindow = ReadUiFile("MainWindow.xaml.cs");
        var playerWindow = ReadUiFile("Views", "Windows", "PlayerWindow.xaml.cs");
        var quickScan = ReadUiFile("Player", "QuickScanController.cs");
        var trainingWindow = ReadUiFile("Views", "Windows", "TrainingCenterWindow.xaml.cs");
        var trainingViewModel = ReadUiFile("ViewModels", "Windows", "TrainingCenterViewModel.cs");

        Assert.Contains("_dialogs = services.Dialogs;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ConfirmCancel(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Dialogs: _protocolContext.Dialogs", playerWindow, StringComparison.Ordinal);
        Assert.Contains("IDialogService dialogs", quickScan, StringComparison.Ordinal);
        Assert.DoesNotContain("DialogHost.Current", quickScan, StringComparison.Ordinal);
        Assert.Contains("dialogs: _dialogs", trainingWindow, StringComparison.Ordinal);
        Assert.Contains("dialogs: _dialogs", trainingViewModel, StringComparison.Ordinal);
    }

    private static string ReadUiFile(params string[] segments)
        => File.ReadAllText(Path.Combine(
            new[] { TestRepoPaths.FindRepoRoot(), "src", "AuswertungPro.Next.UI" }
                .Concat(segments)
                .ToArray()));
}
