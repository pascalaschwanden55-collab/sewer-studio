using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Audit A08 (23.09.2026): «Speichern unter» in einen anderen Ordner liess Videos, Fotos, PDFs, Kosten und
/// Dossiers am alten Ort zurueck; das neue aktive Projekt fand keines davon. Entscheid: nur im selben Ordner.
/// </summary>
public sealed class ShellSpeichernUnterOrdnerTests
{
    [Fact]
    public void Gespeichertes_projekt_wird_nicht_in_einen_anderen_ordner_gespeichert()
    {
        var root = Path.Combine(Path.GetTempPath(), "shell-speichernunter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var erst = Path.Combine(root, "Zone1", "Projektdateien", "projekt.json");
            var fremd = Path.Combine(root, "Zone2", "Projektdateien", "projekt.json");
            var dialogs = new DialogFake(erst, fremd);
            var settings = new AppSettings { EnableRestorePoints = false };
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(settings, new DiagnosticsOptions(), loggerFactory.CreateLogger("test"), loggerFactory)
            {
                Dialogs = dialogs,
            };
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            shell.Project.Name = "Zone 1";

            Assert.True(shell.TrySaveProjectAs());           // erstes Speichern: noch kein Projektordner
            Assert.False(shell.TrySaveProjectAs());          // danach nicht in einen anderen Ordner

            Assert.False(File.Exists(fremd));
            Assert.Equal(Path.GetFullPath(erst), Path.GetFullPath(settings.LastProjectPath!));
            var warnung = Assert.Single(dialogs.Warnungen);
            Assert.Contains("Explorer", warnung);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class DialogFake(params string[] pfade) : IDialogService
    {
        private readonly Queue<string> _pfade = new(pfade);
        public List<string> Warnungen { get; } = new();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => _pfade.Count > 0 ? _pfade.Dequeue() : null;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => Warnungen.Add(message);
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.No;
    }
}
