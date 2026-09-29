using System.Collections.Specialized;
using System.Windows;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodeCatalogEditorViewModelTests
{
    [Fact]
    public void Neuer_code_und_seine_aenderung_aktualisieren_die_gruppen_je_einmal()
    {
        StaTestRunner.Run(() =>
        {
            var viewModel = new CodeCatalogEditorViewModel(
                new CatalogProvider([]), CreateTestWindow(), new DialogFake());
            var resets = 0;
            viewModel.GroupOptions.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                    resets++;
            };

            viewModel.NewCommand.Execute(null);

            Assert.Equal(1, resets);
            var newCode = Assert.IsType<CodeDefinitionItem>(viewModel.SelectedCode);
            Assert.Contains(newCode, viewModel.CodesView.Cast<CodeDefinitionItem>());

            resets = 0;
            newCode.Title = "Neuer Titel";

            Assert.Equal(1, resets);
        });
    }

    [Fact]
    public void SaveCommand_blocks_invalid_catalog_and_keeps_dialog_open()
    {
        StaTestRunner.Run(() =>
        {
            var provider = new CatalogProvider(["Code fehlt"]);
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new CodeCatalogEditorViewModel(provider, window, dialogs);
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
            Assert.Equal(0, provider.SaveCount);
            Assert.Equal("Code fehlt", Assert.Single(viewModel.ValidationMessages));
            Assert.Contains("Code fehlt", dialogs.LastWarning, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SaveCommand_persists_valid_catalog_and_accepts_dialog()
    {
        StaTestRunner.Run(() =>
        {
            var provider = new CatalogProvider([]);
            var dialogs = new DialogFake();
            var window = CreateTestWindow();
            var viewModel = new CodeCatalogEditorViewModel(provider, window, dialogs);
            viewModel.Codes[0].Title = "Gepruefter Titel";
            window.Loaded += (_, _) => viewModel.SaveCommand.Execute(null);

            var result = window.ShowDialog();

            Assert.True(result);
            Assert.Equal(1, provider.SaveCount);
            Assert.Equal("Gepruefter Titel", Assert.Single(provider.SavedCodes).Title);
            Assert.Null(dialogs.LastWarning);
        });
    }

    [Fact]
    public void ValidateCommand_meldet_toast_statt_dialog_wenn_toastdienst_verfuegbar()
    {
        StaTestRunner.Run(() =>
        {
            var provider = new CatalogProvider([]);
            var dialogs = new DialogFake();
            var toasts = new ToastFake();
            var window = CreateTestWindow();
            var viewModel = new CodeCatalogEditorViewModel(provider, window, dialogs, toasts);
            window.Loaded += (_, _) =>
            {
                viewModel.ValidateCommand.Execute(null);
                window.Close();
            };

            window.ShowDialog();

            Assert.Null(dialogs.LastWarning);
            Assert.Contains("Validierung erfolgreich", toasts.Meldung, StringComparison.Ordinal);
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

    private sealed class CatalogProvider(IReadOnlyList<string> validationErrors) : ICodeCatalogProvider
    {
        public int SaveCount { get; private set; }
        public IReadOnlyList<CodeDefinition> SavedCodes { get; private set; } = [];

        public IReadOnlyList<CodeDefinition> GetAll()
            =>
            [
                new CodeDefinition
                {
                    Code = "BAB",
                    Title = "Riss",
                    Group = "Kanal/Schaden"
                }
            ];

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = GetAll()[0];
            return string.Equals(code, def.Code, StringComparison.OrdinalIgnoreCase);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
        {
            SaveCount++;
            SavedCodes = codes;
        }

        public IReadOnlyList<string> AllowedCodes() => ["BAB"];

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => validationErrors;
    }

    private sealed class DialogFake : IDialogService
    {
        public string? LastWarning { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => LastWarning = message;
        public void Error(string message, string title = "Fehler") => throw new Xunit.Sdk.XunitException(message);
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Yes;
    }

    private sealed class ToastFake : IToastService
    {
        public string? Meldung { get; private set; }

        public void Success(string message) => Meldung = message;
        public void Success(string message, string aktionText, Action aktion) => Meldung = message;
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => throw new Xunit.Sdk.XunitException(message);
    }
}
