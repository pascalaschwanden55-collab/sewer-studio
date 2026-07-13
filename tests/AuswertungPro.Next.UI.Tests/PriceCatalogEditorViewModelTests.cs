using System.IO;
using System.Text.Json;
using System.Windows.Data;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PriceCatalogEditorViewModelTests
{
    [Fact]
    public void Add_filter_and_save_use_isolated_legacy_catalog_directory()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Programm");
        var dataDirectory = Path.Combine(projectRoot, "Data");
        var userDirectory = Path.Combine(temp.Path, "Benutzerdaten");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(
            Path.Combine(dataDirectory, "seed_price_catalog.json"),
            JsonSerializer.Serialize(CreateSeedCatalog(), JsonDefaults.Indented));

        StaTestRunner.Run(() =>
        {
            var service = new CostCalculationService(projectRoot, userDirectory);
            var dialogs = new DialogFake();
            var viewModel = new PriceCatalogEditorViewModel(service, dialogs);
            Assert.Contains(viewModel.Items, item => item.Id == "seed.position");

            viewModel.AddNewCommand.Execute(null);
            var added = viewModel.Items[^1];
            added.Id = "robot.test";
            added.Group = "Robotik";
            added.Label = "Roboter-Testposition";
            added.Unit = "St";
            added.UnitPrice = 345.60m;

            viewModel.SearchText = "robot";
            var visible = CollectionViewSource.GetDefaultView(viewModel.Items)
                .Cast<PriceItemRow>()
                .ToList();
            Assert.Equal(["robot.test"], visible.Select(item => item.Id));

            viewModel.SaveCommand.Execute(null);

            Assert.Contains(userDirectory, dialogs.LastInfo, StringComparison.OrdinalIgnoreCase);
        });

        var saved = new CostCalculationService(projectRoot, userDirectory).LoadCatalog();
        var savedItem = Assert.Single(saved.Items, item => item.Id == "robot.test");
        Assert.Equal("Roboter-Testposition", savedItem.Label);
        Assert.Equal(345.60m, savedItem.UnitPrice);
    }

    private static PriceCatalog CreateSeedCatalog()
        => new()
        {
            Items =
            [
                new PriceItem
                {
                    Id = "seed.position",
                    Group = "Bestand",
                    Label = "Seed-Position",
                    Unit = "m",
                    UnitPrice = 10m
                }
            ]
        };

    private sealed class DialogFake : IDialogService
    {
        public string? LastInfo { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") => LastInfo = message;
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => throw new Xunit.Sdk.XunitException(message);
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
                "SewerStudio_PriceCatalogEditor_" + Guid.NewGuid().ToString("N"));
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
