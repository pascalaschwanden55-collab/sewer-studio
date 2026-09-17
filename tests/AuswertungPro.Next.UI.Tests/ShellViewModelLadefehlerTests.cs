using System;
using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// F1 (Fehleranalyse 17.09.2026): Nur eine belegt beschaedigte Projektdatei darf eine
/// Sicherung einspielen. Eine gesperrte oder zu neue Datei ist in Ordnung — wer sie
/// wiederherstellt, ersetzt den aktuellen Arbeitsstand durch einen alten.
/// </summary>
public sealed class ShellViewModelLadefehlerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shell-ladefehler-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static string ProjektMitSicherung(TempDir temp, string aktuellerStand)
    {
        var projectFile = System.IO.Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(projectFile)!);
        var repo = new JsonProjectRepository();
        // Zwei Speichervorgaenge: der zweite legt die .bak mit dem alten Stand an.
        Assert.True(repo.Save(new Project { Name = "Alte Sicherung" }, projectFile).Ok);
        Assert.True(repo.Save(new Project { Name = aktuellerStand }, projectFile).Ok);
        Assert.True(File.Exists(projectFile + ".bak"));
        return projectFile;
    }

    private static (ShellViewModel Shell, DialogFake Dialogs, ILoggerFactory Logs) Shell()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var dialogs = new DialogFake();
        services.Dialogs = dialogs;
        var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        shell.Project.Name = "Offenes Projekt";
        return (shell, dialogs, loggerFactory);
    }

    [Fact]
    public void TryOpenProject_GesperrteProjektdatei_SpieltKeineAlteSicherungEin()
    {
        using var temp = new TempDir();
        var projectFile = ProjektMitSicherung(temp, "Aktueller Stand");
        var inhaltVorher = File.ReadAllText(projectFile);
        var (shell, dialogs, logs) = Shell();
        using var _ = shell;
        using var __ = logs;

        // Virenscanner, OneDrive oder Netzlaufwerk halten die Datei kurz exklusiv.
        using (new FileStream(projectFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(shell.TryOpenProject(projectFile));
        }

        Assert.Equal("Offenes Projekt", shell.Project.Name);
        Assert.Equal(inhaltVorher, File.ReadAllText(projectFile));
        Assert.Contains("nicht gelesen werden", dialogs.LastErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryOpenProject_NeueresProjektformat_BleibtUnveraendertUndNenntDenGrund()
    {
        using var temp = new TempDir();
        var projectFile = ProjektMitSicherung(temp, "Aktueller Stand");
        var neuererStand = "{\"Version\":4,\"Name\":\"Neuer Stand\"}";
        File.WriteAllText(projectFile, neuererStand);
        var (shell, dialogs, logs) = Shell();
        using var _ = shell;
        using var __ = logs;

        Assert.False(shell.TryOpenProject(projectFile));

        Assert.Equal("Offenes Projekt", shell.Project.Name);
        Assert.Equal(neuererStand, File.ReadAllText(projectFile)); // nicht in Quarantaene verschoben
        Assert.Contains("neueren SewerStudio-Version", dialogs.LastErrorMessage, StringComparison.Ordinal);
    }

    private sealed class DialogFake : IDialogService
    {
        public string LastErrorMessage { get; private set; } = string.Empty;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => LastErrorMessage = message;
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.No;
    }
}
