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

    private sealed class SaveFileDialogFake : IDialogService
    {
        private readonly string _saveFileResult;

        public SaveFileDialogFake(string saveFileResult)
            => _saveFileResult = saveFileResult;

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => _saveFileResult;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
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
