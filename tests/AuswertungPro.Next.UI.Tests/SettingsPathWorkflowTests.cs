using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsPathWorkflowTests
{
    [Fact]
    public void SelectProjectPath_uses_current_project_name_as_default_file_name()
    {
        var dialogs = new DialogFake { SavePath = @"D:\Projekte\Neu.json" };

        var selected = SettingsPathWorkflow.SelectProjectPath(
            dialogs,
            @"C:\Alt\Uri Kontrolle.json");

        Assert.Equal(@"D:\Projekte\Neu.json", selected);
        Assert.Equal("Projektpfad waehlen", dialogs.SaveTitle);
        Assert.Equal("Projekt (*.json)|*.json", dialogs.SaveFilter);
        Assert.Equal(".json", dialogs.DefaultExt);
        Assert.Equal("Uri Kontrolle", dialogs.DefaultFileName);
    }

    [Fact]
    public void SelectProjectPath_uses_neutral_default_for_blank_project_path()
    {
        var dialogs = new DialogFake { SavePath = @"D:\Projekte\Projekt.json" };

        SettingsPathWorkflow.SelectProjectPath(dialogs, " ");

        Assert.Equal("Projekt", dialogs.DefaultFileName);
    }

    [Fact]
    public void SelectAbwasserkatasterXtfPath_uses_xtf_filter_and_current_directory()
    {
        var dialogs = new DialogFake { OpenPath = @"D:\QGIS_V4.03\Export_Sewer_Studio\netz.xtf" };

        var selected = SettingsPathWorkflow.SelectAbwasserkatasterXtfPath(
            dialogs,
            @"D:\QGIS_V4.03\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf");

        Assert.Equal(@"D:\QGIS_V4.03\Export_Sewer_Studio\netz.xtf", selected);
        Assert.Equal("Abwasserkataster-XTF waehlen", dialogs.OpenTitle);
        Assert.Equal("XTF-Dateien (*.xtf)|*.xtf|Alle Dateien|*.*", dialogs.OpenFilter);
        Assert.Equal(@"D:\QGIS_V4.03\Export_Sewer_Studio", dialogs.OpenInitialDirectory);
    }

    [Fact]
    public void OpenFolder_blank_path_does_not_create_or_open()
    {
        var dialogs = new DialogFake();
        var calls = new List<string>();

        SettingsPathWorkflow.OpenFolder(new SettingsOpenFolderRequest(
            Path: " ",
            Dialogs: dialogs,
            CreateDirectory: path => calls.Add("create:" + path),
            TryOpen: path =>
            {
                calls.Add("open:" + path);
                return new SettingsOpenFolderResult(true, null);
            }));

        Assert.Empty(calls);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public void OpenFolder_success_creates_directory_and_opens_it()
    {
        var dialogs = new DialogFake();
        var calls = new List<string>();

        SettingsPathWorkflow.OpenFolder(new SettingsOpenFolderRequest(
            Path: @"D:\SewerStudio\logs",
            Dialogs: dialogs,
            CreateDirectory: path => calls.Add("create:" + path),
            TryOpen: path =>
            {
                calls.Add("open:" + path);
                return new SettingsOpenFolderResult(true, null);
            }));

        Assert.Equal(["create:D:\\SewerStudio\\logs", "open:D:\\SewerStudio\\logs"], calls);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public void OpenFolder_failure_shows_error_dialog()
    {
        var dialogs = new DialogFake();

        SettingsPathWorkflow.OpenFolder(new SettingsOpenFolderRequest(
            Path: @"D:\SewerStudio\logs",
            Dialogs: dialogs,
            CreateDirectory: _ => { },
            TryOpen: _ => new SettingsOpenFolderResult(false, "kein Zugriff")));

        Assert.Single(dialogs.Errors);
        Assert.Contains("Ordner konnte nicht geoeffnet werden", dialogs.Errors[0]);
        Assert.Contains("kein Zugriff", dialogs.Errors[0]);
        Assert.Equal("SewerStudio", dialogs.ErrorTitles[0]);
    }

    private sealed class DialogFake : IDialogService
    {
        public string? SavePath { get; set; }
        public string? OpenPath { get; set; }
        public string? OpenTitle { get; private set; }
        public string? OpenFilter { get; private set; }
        public string? OpenInitialDirectory { get; private set; }
        public string? SaveTitle { get; private set; }
        public string? SaveFilter { get; private set; }
        public string? DefaultExt { get; private set; }
        public string? DefaultFileName { get; private set; }
        public List<string> Errors { get; } = new();
        public List<string> ErrorTitles { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
        {
            OpenTitle = title;
            OpenFilter = filter;
            OpenInitialDirectory = initialDirectory;
            return OpenPath;
        }

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveTitle = title;
            SaveFilter = filter;
            DefaultExt = defaultExt;
            DefaultFileName = defaultFileName;
            return SavePath;
        }

        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }

        public void Error(string message, string title = "Fehler")
        {
            Errors.Add(message);
            ErrorTitles.Add(title);
        }

        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
