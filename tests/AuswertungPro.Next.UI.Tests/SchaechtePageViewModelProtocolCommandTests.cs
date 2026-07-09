using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageViewModelProtocolCommandTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public void RefreshProtocolCommand_IstNurMitAusgewaehltemPdfPfadAktiv()
    {
        var vm = CreateVm();

        Assert.False(vm.RefreshProtocolCommand.CanExecute(null));

        var ohnePdf = VollstaendigerSchacht("S-1");
        vm.Records.Add(ohnePdf);
        vm.Selected = ohnePdf;

        Assert.False(vm.RefreshProtocolCommand.CanExecute(null));

        var mitPdf = VollstaendigerSchacht("S-2");
        mitPdf.SetFieldValue("PDF_Path", "Schaechte_Verteilt/S-2/protokoll.pdf");
        vm.Records.Add(mitPdf);
        vm.Selected = mitPdf;

        Assert.True(vm.RefreshProtocolCommand.CanExecute(null));
    }

    private SchaechtePageViewModel CreateVm()
    {
        var settings = new AppSettings();
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = new DialogFake()
        };
        var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        return new SchaechtePageViewModel(shell, services);
    }

    private static SchachtRecord VollstaendigerSchacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        record.SetFieldValue("Ja/Nein", "Ja");
        record.SetFieldValue("Ausgefuehrt durch", "Baumeister");
        return record;
    }

    public void Dispose() => _loggerFactory.Dispose();

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
