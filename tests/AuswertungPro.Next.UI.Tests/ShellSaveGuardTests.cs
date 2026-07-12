using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Datenverlust-Schutz: TrySaveProject darf NIE ein fremdes Projekt ueberschreiben.
/// Gefahr-Szenario: LastProjectPath zeigt noch auf das zuletzt geoeffnete Projekt,
/// waehrend das aktuelle Projekt (Draft/frischer Import) nie gespeichert wurde —
/// "Speichern" haette dann still die Datei des ANDEREN Projekts ueberschrieben.
/// </summary>
public sealed class ShellSaveGuardTests
{
    [Fact]
    public void TrySaveProject_UeberschreibtNieFremdesProjekt_WennAktuellesNieGespeichert()
    {
        using var temp = new TempDir();

        // Fremdes, gespeichertes Projekt auf der Platte
        var fremdPath = Path.Combine(temp.Path, "Fremd", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fremdPath)!);
        var repo = new JsonProjectRepository();
        var save = repo.Save(new Project { Name = "Fremd" }, fremdPath);
        Assert.True(save.Ok, save.ErrorMessage);

        // Alt-Zustand: LastProjectPath zeigt noch auf das fremde Projekt
        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            LastProjectPath = fremdPath
        };

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        // "Speichern unter"-Dialog liefert einen EIGENEN Pfad
        var eigenPath = Path.Combine(temp.Path, "Eigen", "projekt.json");
        services.Dialogs = new SaveFileDialogFake(eigenPath);

        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        shell.Project.Name = "Neues Projekt";
        shell.Project.Dirty = true; // Draft mit Eingaben, nie gespeichert

        var ok = shell.TrySaveProject();

        Assert.True(ok);

        // Das fremde Projekt ist unangetastet ...
        var fremd = JsonSerializer.Deserialize<Project>(File.ReadAllText(fremdPath));
        Assert.Equal("Fremd", fremd!.Name);

        // ... und das eigene liegt unter dem im Dialog gewaehlten Pfad.
        Assert.True(File.Exists(eigenPath), "Eigenes Projekt wurde nicht unter dem Dialog-Pfad gespeichert.");
        Assert.True(shell.HasPersistedProject);
    }

    [Fact]
    public void TrySaveProjectAs_WennSpeichernFehlschlaegt_BleibtLastProjectPathUnveraendert()
    {
        using var temp = new TempDir();

        // Ausgangszustand: ein gespeichertes fremdes Projekt, LastProjectPath zeigt darauf.
        var fremdPath = Path.Combine(temp.Path, "Fremd", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fremdPath)!);
        var repo = new JsonProjectRepository();
        Assert.True(repo.Save(new Project { Name = "Fremd" }, fremdPath).Ok);

        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            LastProjectPath = fremdPath
        };

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        // Der "Speichern unter"-Dialog liefert einen Pfad, der in Wahrheit ein VERZEICHNIS ist.
        // Der Name endet auf .json, damit NormalizeProjectPath ihn nicht veraendert — so schlaegt
        // das Schreiben der Projektdatei deterministisch fehl (Ziel ist ein Ordner).
        var blockierterPfad = Path.Combine(temp.Path, "Blockiert.json");
        Directory.CreateDirectory(blockierterPfad);
        services.Dialogs = new SaveFileDialogFake(blockierterPfad);

        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        shell.Project.Name = "Neues Projekt";
        shell.Project.Dirty = true;

        var ok = shell.TrySaveProjectAs();

        Assert.False(ok); // Speichern ist fehlgeschlagen.
        // Kerngarantie (Audit P0-5b): Weil zuerst gespeichert und erst bei Erfolg die Merkliste
        // geschrieben wird, zeigt LastProjectPath weiter auf das fremde Projekt — NICHT auf die
        // nie erzeugte neue Datei. Vor dem Fix waere LastProjectPath = blockierterPfad gewesen.
        Assert.Equal(fremdPath, settings.LastProjectPath);
        Assert.False(shell.HasPersistedProject);
        var dialogs = Assert.IsType<SaveFileDialogFake>(services.Dialogs);
        Assert.Single(dialogs.Errors);
        Assert.Contains("nicht gespeichert", dialogs.Errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(blockierterPfad, dialogs.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SaveFileDialogFake : IDialogService
    {
        private readonly string _saveFileResult;

        public SaveFileDialogFake(string saveFileResult)
            => _saveFileResult = saveFileResult;

        public List<string> Errors { get; } = new();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => _saveFileResult;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Errors.Add(message);
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.No;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "shell-saveguard-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort-Cleanup
            }
        }
    }
}
