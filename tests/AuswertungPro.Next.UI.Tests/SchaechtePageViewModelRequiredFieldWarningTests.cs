using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageViewModelRequiredFieldWarningTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public void Selected_wechsel_warnt_wenn_vorheriger_schacht_pflichtfelder_nicht_hat()
    {
        var dialogs = new DialogFake();
        var (_, vm) = CreateVm(dialogs);
        var first = Record("S-1");
        var second = Record("S-2", sanieren: "Ja", ausgefuehrtDurch: "Baumeister");
        vm.Records.Add(first);
        vm.Records.Add(second);

        vm.Selected = first;
        vm.Selected = second;

        Assert.Equal(1, dialogs.WarnCalls);
        Assert.Contains("S-1", dialogs.LastWarnMessage);
        Assert.Contains("Sanieren Ja/Nein", dialogs.LastWarnMessage);
        Assert.Contains("Ausgefuehrt durch", dialogs.LastWarnMessage);
    }

    [Fact]
    public void Selected_wechsel_warnt_nicht_wenn_vorheriger_schacht_pflichtfelder_hat()
    {
        var dialogs = new DialogFake();
        var (_, vm) = CreateVm(dialogs);
        var first = Record("S-1", sanieren: "Ja", ausgefuehrtDurch: "Baumeister");
        var second = Record("S-2", sanieren: "Ja", ausgefuehrtDurch: "Sanierer");
        vm.Records.Add(first);
        vm.Records.Add(second);

        vm.Selected = first;
        vm.Selected = second;

        Assert.Equal(0, dialogs.WarnCalls);
    }

    private (ShellViewModel Shell, SchaechtePageViewModel Vm) CreateVm(DialogFake dialogs)
    {
        var settings = new AppSettings();
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var vm = new SchaechtePageViewModel(shell, services);
        return (shell, vm);
    }

    private static SchachtRecord Record(string nummer, string? sanieren = null, string? ausgefuehrtDurch = null)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        if (sanieren is not null)
            record.SetFieldValue("Ja/Nein", sanieren);
        if (ausgefuehrtDurch is not null)
            record.SetFieldValue("Ausgefuehrt durch", ausgefuehrtDurch);
        return record;
    }

    public void Dispose() => _loggerFactory.Dispose();

    private sealed class DialogFake : IDialogService
    {
        public int WarnCalls { get; private set; }
        public string LastWarnMessage { get; private set; } = "";

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung")
        {
            WarnCalls++;
            LastWarnMessage = message;
        }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
