using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteFileActionControllerTests
{
    [Fact]
    public void OpenProtocol_meldet_fehlende_auswahl_ohne_dienstaufruf()
    {
        var harness = new Harness();

        harness.Controller.OpenProtocol(null, @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.", "Protokoll")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.Targets.PdfRequests);
        Assert.Empty(harness.ShellOpen.Paths);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenProtocol_meldet_fehlenden_pfad_ohne_schachtnummer(string? resolvedPath)
    {
        var harness = new Harness
        {
            ResolvePdf = (_, _) => resolvedPath
        };

        harness.Controller.OpenProtocol(new SchachtRecord(), @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Kein Schachtprotokoll-PDF verknüpft.", "Protokoll")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.ShellOpen.Paths);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Fact]
    public void OpenProtocol_nennt_schachtnummer_bei_fehlendem_pfad()
    {
        var harness = new Harness();
        var record = CreateRecord("S-17");

        harness.Controller.OpenProtocol(record, @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Kein Schachtprotokoll-PDF verknüpft für Schacht S-17.", "Protokoll")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.ShellOpen.Paths);
    }

    [Fact]
    public void OpenProtocol_loest_aktuellen_projektpfad_auf_und_oeffnet_pdf()
    {
        var harness = new Harness();
        var record = CreateRecord("S-17");
        harness.ResolvePdf = (candidate, projectPath) =>
        {
            Assert.Same(record, candidate);
            Assert.Equal(@"C:\Projekt\projekt.json", projectPath);
            return @"C:\Projekt\Schaechte\S-17\protokoll.pdf";
        };

        harness.Controller.OpenProtocol(record, @"C:\Projekt\projekt.json");

        Assert.Equal([@"C:\Projekt\Schaechte\S-17\protokoll.pdf"], harness.ShellOpen.Paths);
        Assert.Empty(harness.ExplorerReveal.Paths);
        Assert.Empty(harness.Dialogs.Infos);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Fact]
    public void OpenProtocol_zeigt_vollstaendigen_oeffnungsfehler()
    {
        var harness = new Harness
        {
            ResolvePdf = (_, _) => @"C:\Projekt\protokoll.pdf",
            OpenShell = _ => (false, "Datei ist gesperrt")
        };

        harness.Controller.OpenProtocol(new SchachtRecord(), null);

        Assert.Equal(
            [("PDF konnte nicht geöffnet werden:\nDatei ist gesperrt", "Protokoll")],
            harness.Dialogs.Errors);
        Assert.Single(harness.Targets.PdfRequests);
        Assert.Equal([@"C:\Projekt\protokoll.pdf"], harness.ShellOpen.Paths);
        Assert.Empty(harness.Targets.ExplorerRequests);
        Assert.Empty(harness.ExplorerReveal.Paths);
        Assert.Empty(harness.Dialogs.Infos);
    }

    [Fact]
    public void RevealContainingFolder_meldet_fehlende_auswahl_ohne_dienstaufruf()
    {
        var harness = new Harness();

        harness.Controller.RevealContainingFolder(null, @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.", "Ordner")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.Targets.ExplorerRequests);
        Assert.Empty(harness.ExplorerReveal.Paths);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RevealContainingFolder_meldet_fehlendes_ziel_ohne_schachtnummer(string? resolvedTarget)
    {
        var harness = new Harness
        {
            ResolveExplorer = (_, _) => resolvedTarget
        };

        harness.Controller.RevealContainingFolder(new SchachtRecord(), @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Kein Datei- oder Ordnerpfad verknüpft.", "Ordner")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.ExplorerReveal.Paths);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Fact]
    public void RevealContainingFolder_nennt_schachtnummer_bei_fehlendem_ziel()
    {
        var harness = new Harness();
        var record = CreateRecord("S-17");

        harness.Controller.RevealContainingFolder(record, @"C:\Projekt\projekt.json");

        Assert.Equal(
            [("Kein Datei- oder Ordnerpfad verknüpft für Schacht S-17.", "Ordner")],
            harness.Dialogs.Infos);
        Assert.Empty(harness.ExplorerReveal.Paths);
    }

    [Fact]
    public void RevealContainingFolder_loest_aktuellen_projektpfad_auf_und_zeigt_ziel()
    {
        var harness = new Harness();
        var record = CreateRecord("S-17");
        harness.ResolveExplorer = (candidate, projectPath) =>
        {
            Assert.Same(record, candidate);
            Assert.Equal(@"C:\Projekt\projekt.json", projectPath);
            return @"C:\Projekt\Schaechte\S-17";
        };

        harness.Controller.RevealContainingFolder(record, @"C:\Projekt\projekt.json");

        Assert.Equal([@"C:\Projekt\Schaechte\S-17"], harness.ExplorerReveal.Paths);
        Assert.Empty(harness.ShellOpen.Paths);
        Assert.Empty(harness.Dialogs.Infos);
        Assert.Empty(harness.Dialogs.Errors);
    }

    [Fact]
    public void RevealContainingFolder_zeigt_vollstaendigen_explorerfehler()
    {
        var harness = new Harness
        {
            ResolveExplorer = (_, _) => @"C:\Projekt\Schaechte\S-17",
            RevealExplorer = _ => (false, "Explorer nicht verfügbar")
        };

        harness.Controller.RevealContainingFolder(new SchachtRecord(), null);

        Assert.Equal(
            [("Ordner konnte nicht geöffnet werden:\nExplorer nicht verfügbar", "Ordner")],
            harness.Dialogs.Errors);
        Assert.Single(harness.Targets.ExplorerRequests);
        Assert.Equal([@"C:\Projekt\Schaechte\S-17"], harness.ExplorerReveal.Paths);
        Assert.Empty(harness.Targets.PdfRequests);
        Assert.Empty(harness.ShellOpen.Paths);
        Assert.Empty(harness.Dialogs.Infos);
    }

    private static SchachtRecord CreateRecord(string schachtnummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", schachtnummer);
        return record;
    }

    private sealed class Harness
    {
        public Harness()
        {
            Targets = new RecordingTargetResolver(
                (record, projectPath) => ResolvePdf(record, projectPath),
                (record, projectPath) => ResolveExplorer(record, projectPath));
            ShellOpen = new RecordingShellOpen(path => OpenShell(path));
            ExplorerReveal = new RecordingExplorerReveal(path => RevealExplorer(path));
            Dialogs = new CapturingDialogService();
            Controller = new SchaechteFileActionController(
                Targets,
                ShellOpen,
                ExplorerReveal,
                Dialogs);
        }

        public Func<SchachtRecord, string?, string?> ResolvePdf { get; set; } = (_, _) => null;

        public Func<SchachtRecord, string?, string?> ResolveExplorer { get; set; } = (_, _) => null;

        public Func<string?, (bool Success, string? Error)> OpenShell { get; set; } =
            _ => (true, null);

        public Func<string?, (bool Success, string? Error)> RevealExplorer { get; set; } =
            _ => (true, null);

        public RecordingTargetResolver Targets { get; }

        public RecordingShellOpen ShellOpen { get; }

        public RecordingExplorerReveal ExplorerReveal { get; }

        public CapturingDialogService Dialogs { get; }

        public SchaechteFileActionController Controller { get; }
    }

    private sealed class RecordingTargetResolver(
        Func<SchachtRecord, string?, string?> resolvePdf,
        Func<SchachtRecord, string?, string?> resolveExplorer) : ISchachtFileTargetResolver
    {
        public List<(SchachtRecord Record, string? ProjectPath)> PdfRequests { get; } = [];

        public List<(SchachtRecord Record, string? ProjectPath)> ExplorerRequests { get; } = [];

        public string? ResolvePdfPath(SchachtRecord record, string? projectFilePath)
        {
            PdfRequests.Add((record, projectFilePath));
            return resolvePdf(record, projectFilePath);
        }

        public string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath)
        {
            ExplorerRequests.Add((record, projectFilePath));
            return resolveExplorer(record, projectFilePath);
        }
    }

    private sealed class RecordingShellOpen(
        Func<string?, (bool Success, string? Error)> open) : ISafeShellOpenService
    {
        public List<string?> Paths { get; } = [];

        public bool TryOpen(string? path, out string? error)
        {
            Paths.Add(path);
            var result = open(path);
            error = result.Error;
            return result.Success;
        }
    }

    private sealed class RecordingExplorerReveal(
        Func<string?, (bool Success, string? Error)> reveal) : IExplorerRevealService
    {
        public List<string?> Paths { get; } = [];

        public bool TryReveal(string? targetPath, out string? error)
        {
            Paths.Add(targetPath);
            var result = reveal(targetPath);
            error = result.Error;
            return result.Success;
        }
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public List<(string Message, string Title)> Infos { get; } = [];

        public List<(string Message, string Title)> Errors { get; } = [];

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(
            string title,
            string filter,
            string? defaultExt = null,
            string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => Infos.Add((message, title));

        public void Warn(string message, string title = "Warnung")
            => throw new NotSupportedException();

        public void Error(string message, string title = "Fehler")
            => Errors.Add((message, title));

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
