using System.IO;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PositionTemplateEditorViewModelTests
{
    [Fact]
    public void Commands_reorder_store_restore_and_persist_positions_in_isolated_file()
    {
        using var temp = new TempDirectory();
        var positionPath = Path.Combine(temp.Path, "position_templates.user.json");
        var costPath = Path.Combine(temp.Path, "cost_catalog.user.json");
        var positionStore = new PositionTemplateStore(positionPath);
        var costStore = new CostCatalogStore(costPath);
        Assert.True(positionStore.SaveUserOverride(CreatePositionCatalog(), out var positionError), positionError);
        Assert.True(costStore.SaveUserOverrides(CreateCostCatalog(), out var costError), costError);

        StaTestRunner.Run(() =>
        {
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new PositionTemplateEditorViewModel(
                null,
                window,
                positionStore,
                costStore,
                dialogs);
            viewModel.SelectedGroup = viewModel.Groups.Single(group => group.Name == "Testgruppe");
            var second = viewModel.SelectedGroup.Positions[1];
            viewModel.SelectedPosition = second;

            Assert.True(viewModel.MoveUpCommand.CanExecute(null));
            viewModel.MoveUpCommand.Execute(null);
            Assert.Same(second, viewModel.SelectedGroup.Positions[0]);

            viewModel.MoveToStorageCommand.Execute(null);
            var stored = Assert.Single(viewModel.StorageBox);
            Assert.NotSame(second, stored);
            Assert.Single(viewModel.SelectedGroup.Positions);

            viewModel.SelectedStoragePosition = stored;
            viewModel.RestoreFromStorageCommand.Execute(null);
            Assert.Empty(viewModel.StorageBox);
            Assert.Equal(2, viewModel.SelectedGroup.Positions.Count);
            Assert.NotSame(stored, viewModel.SelectedGroup.Positions[1]);

            var catalogItem = Assert.Single(
                viewModel.AvailableItems,
                item => item.Key == "TEST_POSITION");
            Assert.IsType<CatalogItemViewModel>(catalogItem);
            Assert.Contains("Testposition", catalogItem.DisplayName, StringComparison.Ordinal);

            window.Loaded += (_, _) => viewModel.SaveCommand.Execute(null);
            Assert.True(window.ShowDialog());
            Assert.Null(dialogs.LastError);
        });

        var saved = new PositionTemplateStore(positionPath).LoadMerged(null);
        var savedGroup = saved.Groups.Single(group => group.Name == "Testgruppe");
        Assert.Equal(2, savedGroup.Positions.Count);
        Assert.Equal("POSITION_1", savedGroup.Positions[0].ItemKey);
        Assert.Equal("POSITION_2", savedGroup.Positions[1].ItemKey);
    }

    [Fact]
    public void SaveCommand_preserves_corrupt_position_override()
    {
        using var temp = new TempDirectory();
        var positionPath = Path.Combine(temp.Path, "position_templates.user.json");
        const string corruptContent = "{ kaputte Positionsvorlagen";
        File.WriteAllText(positionPath, corruptContent);

        StaTestRunner.Run(() =>
        {
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new PositionTemplateEditorViewModel(
                null,
                window,
                new PositionTemplateStore(positionPath),
                new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
                dialogs);
            var remainedOpen = false;
            window.Loaded += (_, _) =>
            {
                viewModel.SaveCommand.Execute(null);
                remainedOpen = window.IsVisible;
                window.Close();
            };

            Assert.False(window.ShowDialog());
            Assert.True(remainedOpen);
            Assert.Contains("Fehler beim Speichern", dialogs.LastError, StringComparison.Ordinal);
        });

        Assert.Equal(corruptContent, File.ReadAllText(positionPath));
    }

    private static PositionTemplateCatalog CreatePositionCatalog()
        => new()
        {
            Groups =
            [
                new PositionGroup
                {
                    Name = "Testgruppe",
                    Positions =
                    [
                        new PositionTemplate { ItemKey = "POSITION_1", DefaultQty = 1m },
                        new PositionTemplate { ItemKey = "POSITION_2", DefaultQty = 2m }
                    ]
                }
            ]
        };

    private static CostCatalog CreateCostCatalog()
        => new()
        {
            Items =
            [
                new CostCatalogItem
                {
                    Key = "TEST_POSITION",
                    Name = "Testposition",
                    Unit = "St",
                    Type = "Fixed",
                    Active = true
                }
            ]
        };

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
                "SewerStudio_PositionTemplateEditor_" + Guid.NewGuid().ToString("N"));
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
