using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MeasureTemplateEditorViewModelTests
{
    [Fact]
    public void SaveTemplateCommand_writes_template_and_decimal_quantity_to_isolated_store()
    {
        using var temp = new TempDirectory();
        var templatePath = Path.Combine(temp.Path, "measure_templates.user.json");
        var dialogs = new DialogFake();
        var store = new MeasureTemplateStore(templatePath);
        var viewModel = CreateViewModel(temp.Path, store, dialogs);

        viewModel.NewTemplateCommand.Execute(null);
        viewModel.TemplateId = "test_robotersanierung";
        viewModel.TemplateName = "Roboter-Sanierung Test";
        viewModel.AddLineCommand.Execute(new CatalogItemRow(new CostCatalogItem
        {
            Key = "ROBOTER_ST",
            Name = "Roboterposition",
            Unit = "St",
            Active = true
        }));
        viewModel.CurrentLines[0].Qty = "2,5";

        viewModel.SaveTemplateCommand.Execute(null);

        var saved = store.LoadUserOverrides();
        var template = Assert.Single(saved.Measures, item => item.Id == "TEST_ROBOTERSANIERUNG");
        Assert.Equal("Roboter-Sanierung Test", template.Name);
        var line = Assert.Single(template.Lines);
        Assert.Equal("ROBOTER_ST", line.ItemKey);
        Assert.Equal(2.5m, line.DefaultQty);
        Assert.Contains("Template gespeichert", dialogs.LastInfo, StringComparison.Ordinal);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void SaveTemplateCommand_preserves_corrupt_user_file_and_reports_error()
    {
        using var temp = new TempDirectory();
        var templatePath = Path.Combine(temp.Path, "measure_templates.user.json");
        const string corruptContent = "{ kaputte Vorlagendatei";
        File.WriteAllText(templatePath, corruptContent);
        var dialogs = new DialogFake();
        var store = new MeasureTemplateStore(templatePath);
        var viewModel = CreateViewModel(temp.Path, store, dialogs);
        viewModel.NewTemplateCommand.Execute(null);
        viewModel.TemplateId = "darf_nicht_speichern";
        viewModel.TemplateName = "Nicht speichern";

        viewModel.SaveTemplateCommand.Execute(null);

        Assert.Equal(corruptContent, File.ReadAllText(templatePath));
        Assert.Contains("konnte nicht gespeichert", dialogs.LastError, StringComparison.Ordinal);
        Assert.Null(dialogs.LastInfo);
    }

    [Fact]
    public void Constructor_migrates_newer_legacy_templates_replaces_existing_id_and_saves()
    {
        using var temp = new TempDirectory();
        var legacyPath = Path.Combine(temp.Path, "legacy", "measure_templates.json");
        var activePath = Path.Combine(temp.Path, "measure_templates.user.json");
        var store = new MeasureTemplateStore(activePath);
        var existing = new MeasureTemplateCatalog
        {
            Measures =
            [
                new MeasureTemplate { Id = "KEEP", Name = "Behalten" },
                new MeasureTemplate { Id = "REPLACE", Name = "Alt" }
            ]
        };
        Assert.True(store.SaveUserOverrides(existing, out var saveError), saveError);

        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(
            legacyPath,
            """
            {
              "schema_version": 2,
              "templates": [
                {
                  "id": " replace ",
                  "name": " Neu ",
                  "lines": [
                    { "group": " Gruppe ", "item_ref": " POSITION ", "qty": "2,5" }
                  ]
                },
                { "id": "NEW", "name": "Neue Vorlage", "lines": [] }
              ]
            }
            """);
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(activePath, now.AddMinutes(-2));
        File.SetLastWriteTimeUtc(legacyPath, now);
        var dialogs = new DialogFake();

        _ = new MeasureTemplateEditorViewModel(
            projectPath: null,
            store,
            new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
            dialogs,
            legacyPath,
            activePath);

        var saved = store.LoadUserOverrides();
        Assert.Collection(
            saved.Measures,
            keep =>
            {
                Assert.Equal("KEEP", keep.Id);
                Assert.Equal("Behalten", keep.Name);
            },
            replacement =>
            {
                Assert.Equal("replace", replacement.Id);
                Assert.Equal("Neu", replacement.Name);
                var line = Assert.Single(replacement.Lines);
                Assert.Equal("Gruppe", line.Group);
                Assert.Equal("POSITION", line.ItemKey);
                Assert.Equal(2.5m, line.DefaultQty);
            },
            added =>
            {
                Assert.Equal("NEW", added.Id);
                Assert.Equal("Neue Vorlage", added.Name);
            });
        Assert.Contains("uebernommen", dialogs.LastInfo, StringComparison.Ordinal);
        Assert.Null(dialogs.LastError);
    }

    private static MeasureTemplateEditorViewModel CreateViewModel(
        string root,
        MeasureTemplateStore templateStore,
        IDialogService dialogs)
    {
        var legacyPath = Path.Combine(root, "legacy", "measure_templates.json");
        var activePath = Path.Combine(root, "measure_templates.user.json");
        return new MeasureTemplateEditorViewModel(
            null,
            templateStore,
            new CostCatalogStore(Path.Combine(root, "cost_catalog.user.json")),
            dialogs,
            legacyPath,
            activePath);
    }

    private sealed class DialogFake : IDialogService
    {
        public string? LastInfo { get; private set; }
        public string? LastError { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") => LastInfo = message;
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
                "SewerStudio_MeasureTemplateEditor_" + Guid.NewGuid().ToString("N"));
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
