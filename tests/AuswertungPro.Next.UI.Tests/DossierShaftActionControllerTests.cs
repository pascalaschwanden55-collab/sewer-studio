using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Dossiers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierShaftActionControllerTests
{
    [Fact]
    public void Aktionen_verwenden_den_eindeutigen_Schacht_aus_dem_aktuellen_Projekt()
    {
        var expected = new SchachtRecord();
        var other = new SchachtRecord();
        var project = new Project();
        project.SchaechteData.Add(other);
        project.SchaechteData.Add(expected);
        var dialogs = new CapturingDialogService();
        SchachtRecord? protocol = null;
        SchachtRecord? navigated = null;
        var controller = new DossierShaftActionController(
            () => project,
            dialogs,
            record => protocol = record,
            record => navigated = record);

        controller.OpenProtocol(expected.Id);
        controller.NavigateToShaft(expected.Id);

        Assert.Same(expected, protocol);
        Assert.Same(expected, navigated);
        Assert.Null(dialogs.Warning);
    }

    [Fact]
    public void Fehlender_Schacht_zeigt_eine_klare_Warnung_und_startet_nichts()
    {
        var project = new Project();
        var dialogs = new CapturingDialogService();
        var actionCalls = 0;
        var controller = new DossierShaftActionController(
            () => project,
            dialogs,
            _ => actionCalls++,
            _ => actionCalls++);

        controller.OpenProtocol(Guid.NewGuid());

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
