using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Dossiers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierHoldingActionControllerTests
{
    [Fact]
    public void Aktionen_verwenden_die_eindeutige_Haltung_aus_dem_aktuellen_Projekt()
    {
        var expected = new HaltungRecord();
        var other = new HaltungRecord();
        var project = new Project();
        project.Data.Add(other);
        project.Data.Add(expected);
        var dialogs = new CapturingDialogService();
        HaltungRecord? played = null;
        HaltungRecord? protocol = null;
        HaltungRecord? navigated = null;
        var controller = new DossierHoldingActionController(
            () => project,
            dialogs,
            record => played = record,
            record => protocol = record,
            record => navigated = record);

        controller.PlayVideo(expected.Id);
        controller.OpenProtocol(expected.Id);
        controller.NavigateToHolding(expected.Id);

        Assert.Same(expected, played);
        Assert.Same(expected, protocol);
        Assert.Same(expected, navigated);
        Assert.Null(dialogs.Warning);
    }

    [Fact]
    public void Fehlende_Haltung_zeigt_eine_klare_Warnung_und_startet_nichts()
    {
        var project = new Project();
        var dialogs = new CapturingDialogService();
        var actionCalls = 0;
        var controller = new DossierHoldingActionController(
            () => project,
            dialogs,
            _ => actionCalls++,
            _ => actionCalls++,
            _ => actionCalls++);

        controller.PlayVideo(Guid.NewGuid());

        Assert.Equal(0, actionCalls);
        Assert.Contains("nicht mehr vorhanden", dialogs.Warning, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public string? Warning { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => Warning = message;
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
