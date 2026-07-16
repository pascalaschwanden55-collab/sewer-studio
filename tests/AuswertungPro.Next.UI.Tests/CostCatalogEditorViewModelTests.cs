using System.IO;
using System.Windows;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCatalogEditorViewModelTests
{
    [Fact]
    public void SaveCommand_writes_new_position_to_isolated_override_file()
    {
        using var temp = new TempDirectory();
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");

        StaTestRunner.Run(() =>
        {
            var store = new CostCatalogStore(overridePath);
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new CostCatalogEditorViewModel(null, window, store, dialogs);
            viewModel.AddCommand.Execute(null);
            var expectedKey = viewModel.SelectedItem!.Key;
            viewModel.SelectedItem.Name = "Gepruefte Testposition";
            window.Loaded += (_, _) => viewModel.SaveCommand.Execute(null);

            var result = window.ShowDialog();

            Assert.True(result);
            Assert.True(File.Exists(overridePath));
            var saved = new CostCatalogStore(overridePath).LoadUserOverrides();
            Assert.Contains(saved.Items, item =>
                item.Key == expectedKey && item.Name == "Gepruefte Testposition");
            Assert.Null(dialogs.LastError);
        });
    }

    [Fact]
    public void SaveCommand_does_not_overwrite_corrupt_override_file()
    {
        using var temp = new TempDirectory();
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");
        const string corruptContent = "{ kein gueltiger Katalog";
        File.WriteAllText(overridePath, corruptContent);

        StaTestRunner.Run(() =>
        {
            var store = new CostCatalogStore(overridePath);
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new CostCatalogEditorViewModel(null, window, store, dialogs);
            var remainedOpen = false;
            window.Loaded += (_, _) =>
            {
                viewModel.SaveCommand.Execute(null);
                remainedOpen = window.IsVisible;
                window.Close();
            };

            var result = window.ShowDialog();

            Assert.False(result);
            Assert.True(remainedOpen);
            Assert.Equal(corruptContent, File.ReadAllText(overridePath));
            Assert.Contains("Speichern fehlgeschlagen", dialogs.LastError, StringComparison.Ordinal);
        });
    }

    private static Window CreateTestWindow()
        => new()
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10_000,
            Top = -10_000,
            Width = 200,
            Height = 150
        };

    private sealed class DialogFake : IDialogService
    {
        public string? LastError { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => LastError = message;
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio_CostCatalogEditor_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
