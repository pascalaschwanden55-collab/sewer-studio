using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.DataPage;
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

    [Fact]
    public void Selected_wechsel_warnt_nicht_bei_sanieren_nein()
    {
        var dialogs = new DialogFake();
        var (_, vm) = CreateVm(dialogs);
        var first = Record("80441", sanieren: "Nein");
        var second = Record("S-2", sanieren: "Ja", ausgefuehrtDurch: "Sanierer");
        vm.Records.Add(first);
        vm.Records.Add(second);

        vm.Selected = first;
        vm.Selected = second;

        Assert.Equal(0, dialogs.WarnCalls);
    }

    [Fact]
    public void AddCommand_waehlt_neuen_Schacht_ohne_Pflichtfeldwarnung_und_aktualisiert_Status()
    {
        var dialogs = new DialogFake();
        var (shell, vm) = CreateVm(dialogs);
        var incomplete = Record("S-1");
        vm.Records.Add(incomplete);
        vm.Selected = incomplete;
        vm.SearchText = "S";
        shell.Project.Dirty = false;

        vm.AddCommand.Execute(null);

        Assert.Equal(0, dialogs.WarnCalls);
        Assert.Equal(2, vm.Records.Count);
        Assert.NotNull(vm.Selected);
        Assert.NotSame(incomplete, vm.Selected);
        Assert.Equal("2 von 2 Schaechten", vm.SearchResultInfo);
        Assert.True(shell.Project.Dirty);
        Assert.True(vm.RemoveCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveCommand_waehlt_naechsten_Schacht_ohne_Pflichtfeldwarnung_und_aktualisiert_Status()
    {
        var dialogs = new DialogFake();
        var (shell, vm) = CreateVm(dialogs);
        var first = Record("S-1", sanieren: "Ja", ausgefuehrtDurch: "Baumeister");
        var incomplete = Record("S-2");
        var third = Record("S-3", sanieren: "Ja", ausgefuehrtDurch: "Sanierer");
        vm.Records.Add(first);
        vm.Records.Add(incomplete);
        vm.Records.Add(third);
        vm.Selected = incomplete;
        vm.SearchText = "S";
        shell.Project.Dirty = false;

        vm.RemoveCommand.Execute(null);

        Assert.Equal(0, dialogs.WarnCalls);
        Assert.Equal(new[] { first, third }, vm.Records);
        Assert.Same(third, vm.Selected);
        Assert.Equal("2 von 2 Schaechten", vm.SearchResultInfo);
        Assert.True(shell.Project.Dirty);
        Assert.True(vm.RemoveCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void MoveCommands_markieren_und_melden_nur_bei_echter_Bewegung()
    {
        var dialogs = new DialogFake();
        var (shell, vm) = CreateVm(dialogs);
        var first = Record("S-1", sanieren: "Ja", ausgefuehrtDurch: "Baumeister");
        var second = Record("S-2", sanieren: "Ja", ausgefuehrtDurch: "Sanierer");
        vm.Records.Add(first);
        vm.Records.Add(second);
        vm.Selected = first;
        shell.Project.Dirty = false;
        var moveUpChanges = 0;
        var moveDownChanges = 0;
        vm.MoveUpCommand.CanExecuteChanged += (_, _) => moveUpChanges++;
        vm.MoveDownCommand.CanExecuteChanged += (_, _) => moveDownChanges++;

        vm.MoveUpCommand.Execute(null);

        Assert.False(shell.Project.Dirty);
        Assert.Equal(0, moveUpChanges);
        Assert.Equal(0, moveDownChanges);

        vm.MoveDownCommand.Execute(null);

        Assert.Equal(new[] { second, first }, vm.Records);
        Assert.True(shell.Project.Dirty);
        Assert.Equal(1, moveUpChanges);
        Assert.Equal(1, moveDownChanges);
    }

    [Fact]
    public void MoveToPosition_renummeriert_markiert_und_meldet_nur_bei_echter_Bewegung()
    {
        var dialogs = new DialogFake();
        var (shell, vm) = CreateVm(dialogs);
        var first = Record("S-1", sanieren: "Ja", ausgefuehrtDurch: "Baumeister");
        var selected = Record("S-2", sanieren: "Ja", ausgefuehrtDurch: "Sanierer");
        var third = Record("S-3", sanieren: "Nein");
        vm.Records.Add(first);
        vm.Records.Add(selected);
        vm.Records.Add(third);
        var nrField = Assert.IsType<string>(SchaechteFieldLogic.ResolveNrColumnName(vm.Columns, vm.Records));
        first.Fields[nrField] = "9";
        selected.Fields[nrField] = "8";
        third.Fields[nrField] = "7";
        vm.Selected = selected;
        shell.Project.Dirty = false;
        var moveUpChanges = 0;
        var moveDownChanges = 0;
        vm.MoveUpCommand.CanExecuteChanged += (_, _) => moveUpChanges++;
        vm.MoveDownCommand.CanExecuteChanged += (_, _) => moveDownChanges++;

        var moved = vm.MoveToPosition(99);

        Assert.True(moved);
        Assert.Equal(new[] { first, third, selected }, vm.Records);
        Assert.Equal(new[] { "1", "2", "3" }, vm.Records.Select(x => x.GetFieldValue(nrField)).ToArray());
        Assert.True(shell.Project.Dirty);
        Assert.Equal(1, moveUpChanges);
        Assert.Equal(1, moveDownChanges);

        shell.Project.Dirty = false;
        var unchanged = vm.MoveToPosition(3);

        Assert.False(unchanged);
        Assert.False(shell.Project.Dirty);
        Assert.Equal(1, moveUpChanges);
        Assert.Equal(1, moveDownChanges);
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
