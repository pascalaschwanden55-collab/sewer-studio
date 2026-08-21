using System.Reflection;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageProtocolOperationGuardTests
{
    [Fact]
    public void Einzel_und_ordnerimport_verwenden_nur_den_guard_gebundenen_save()
    {
        var main = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "SchaechtePageViewModel.cs"));
        var folder = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "SchaechtePageViewModel.ProtocolFolderImport.cs"));
        var operationGuard = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "SchaechtePageViewModel.ProtocolOperationGuard.cs"));
        var protocol = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "SchaechtePageViewModel.ProtocolImport.cs"));
        var stammdaten = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "SchaechtePageViewModel.Stammdaten.cs"));
        var page = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages",
            "SchaechtePage.xaml"));
        var pageCodeBehind = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages",
            "SchaechtePage.xaml.cs"));

        Assert.Contains("IConfirmLeave, IDisposable", main, StringComparison.Ordinal);
        Assert.Contains("CreateActiveProjectOperationSaveDelegate", main, StringComparison.Ordinal);
        Assert.Contains("SaveProject: _saveProjectForProtocolImport", main, StringComparison.Ordinal);
        Assert.Contains("_saveProjectForProtocolImport()", folder, StringComparison.Ordinal);
        Assert.DoesNotContain("_shell.TrySaveProject()", folder, StringComparison.Ordinal);
        Assert.Contains("ConditionalWeakTable<", operationGuard, StringComparison.Ordinal);
        Assert.Contains("_shell.TryAcquireProjectOperation", operationGuard, StringComparison.Ordinal);
        Assert.Contains("_shell.ReleaseProjectOperation", operationGuard, StringComparison.Ordinal);
        Assert.Contains("_sharedProtocolImportState.TryAcquire(", operationGuard, StringComparison.Ordinal);
        Assert.Contains("_protocolImportShellGuard", operationGuard, StringComparison.Ordinal);
        Assert.Contains("AllowsInternalProjectSave => _state.IsOwnedBy(this)", operationGuard, StringComparison.Ordinal);
        Assert.Contains("TryBeginProtocolPdfOperation", protocol, StringComparison.Ordinal);
        Assert.Contains("finally", protocol, StringComparison.Ordinal);
        Assert.Contains("TryBeginProtocolPdfOperation", stammdaten, StringComparison.Ordinal);
        Assert.Contains("ProjectFileLocator.ProjectRootFromFile(projectContext.ProjectPath)", stammdaten, StringComparison.Ordinal);
        Assert.Contains("projectContext.Project.SchaechteData", stammdaten, StringComparison.Ordinal);
        Assert.Contains("_saveProjectForProtocolImport()", stammdaten, StringComparison.Ordinal);
        Assert.DoesNotContain("_shell.TrySaveProject()", stammdaten, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly=\"{Binding IsShaftDataReadOnly}\"", page, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanMutateShaftData}\"", page, StringComparison.Ordinal);
        Assert.Contains("CanMutateRecord", pageCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_sperrt_die_shell_bereits_waehrend_der_quellenauswahl()
    {
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var viewModel = harness.CreateViewModel(dialog, new ProtocolImportFake());
        var observedBlocked = false;
        dialog.OnConfirmCancel = () =>
        {
            observedBlocked = !harness.Shell.SaveAsProjectCommand.CanExecute(null);
        };

        await ExecuteImportCommandAsync(viewModel);

        Assert.True(observedBlocked);
        var leaveGuard = Assert.IsAssignableFrom<IConfirmLeave>(viewModel);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
        Assert.True(leaveGuard.ConfirmLeave());
    }

    [Fact]
    public async Task Fehlerhafte_startbenachrichtigung_gibt_die_gemeinsame_reservierung_frei()
    {
        using var harness = new Harness();
        var failedViewModel = harness.CreateViewModel(
            new DialogFake(harness.SourcePdf, DialogConfirm.Cancel),
            new ProtocolImportFake());
        var guard = GetProtocolOperationGuard(failedViewModel);
        EventHandler throwingHandler = (_, _) =>
            throw new InvalidOperationException("Testfehler in Startbenachrichtigung");
        guard.OperationAvailabilityChanged += throwingHandler;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeImportProtocolAsync(failedViewModel));

        guard.OperationAvailabilityChanged -= throwingHandler;
        var nextDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var nextViewModel = harness.CreateViewModel(nextDialog, new ProtocolImportFake());
        await ExecuteImportCommandAsync(nextViewModel);

        Assert.Equal(1, nextDialog.ConfirmCancelCalls);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Startbenachrichtigung_kann_keinen_reentranten_doppellauf_oeffnen()
    {
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var viewModel = harness.CreateViewModel(dialog, new ProtocolImportFake());
        var guard = GetProtocolOperationGuard(viewModel);
        Task? nestedRun = null;
        var notificationCount = 0;
        EventHandler nestedStartHandler = (_, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
                nestedRun = InvokeImportProtocolAsync(viewModel);
        };
        guard.OperationAvailabilityChanged += nestedStartHandler;

        try
        {
            await ExecuteImportCommandAsync(viewModel);
            if (nestedRun is not null)
                await nestedRun;

            Assert.Equal(1, dialog.ConfirmCancelCalls);
            Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
        }
        finally
        {
            guard.OperationAvailabilityChanged -= nestedStartHandler;
        }
    }

    [Fact]
    public async Task Fehlerhafte_freigabebenachrichtigung_verhindert_dispose_unregistration_nicht()
    {
        using var harness = new Harness();
        var firstDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var firstViewModel = harness.CreateViewModel(firstDialog, new ProtocolImportFake());
        firstDialog.OnConfirmCancel = () =>
            Assert.IsAssignableFrom<IDisposable>(firstViewModel).Dispose();
        var guard = GetProtocolOperationGuard(firstViewModel);
        var notificationCount = 0;
        EventHandler throwingHandler = (_, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) >= 2)
            {
                throw new InvalidOperationException(
                    "Testfehler in Freigabebenachrichtigung");
            }
        };
        guard.OperationAvailabilityChanged += throwingHandler;

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvokeImportProtocolAsync(firstViewModel));

            Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
            var nextDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
            var nextViewModel = harness.CreateViewModel(nextDialog, new ProtocolImportFake());
            await ExecuteImportCommandAsync(nextViewModel);
            Assert.Equal(1, nextDialog.ConfirmCancelCalls);
        }
        finally
        {
            guard.OperationAvailabilityChanged -= throwingHandler;
        }
    }

    [Fact]
    public async Task Gehaltener_import_sperrt_doppellauf_zweite_viewmodel_und_shellwechsel()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var firstDialog = new DialogFake(
            harness.SourcePdf,
            DialogConfirm.Yes,
            DialogConfirm.Cancel);
        var secondDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var firstProtocol = new ProtocolImportFake
        {
            Entered = entered,
            Release = release,
            ParseResult = InvalidParseResult()
        };
        var firstViewModel = harness.CreateViewModel(firstDialog, firstProtocol);
        var secondViewModel = harness.CreateViewModel(secondDialog, new ProtocolImportFake());

        var refreshable = new SchachtRecord();
        refreshable.SetFieldValue("Schachtnummer", "S-ALT");
        refreshable.SetFieldValue("PDF_Path", "Schaechte_Verteilt/S-ALT/protokoll.pdf");
        firstViewModel.Records.Add(refreshable);
        var secondRecord = new SchachtRecord();
        secondRecord.SetFieldValue("Schachtnummer", "S-NEU");
        firstViewModel.Records.Add(secondRecord);
        firstViewModel.Selected = refreshable;
        harness.Project.Dirty = false;

        var running = ExecuteImportCommandAsync(firstViewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der erste Import wurde nicht erreicht.");

        try
        {
            await InvokeImportProtocolAsync(firstViewModel);
            await InvokeImportProtocolAsync(secondViewModel);

            Assert.Equal(1, firstDialog.ConfirmCancelCalls);
            Assert.Equal(0, secondDialog.ConfirmCancelCalls);
            Assert.Equal(1, firstProtocol.ParseCalls);
            Assert.False(Assert.IsAssignableFrom<IConfirmLeave>(firstViewModel).ConfirmLeave());
            Assert.False(Assert.IsAssignableFrom<IConfirmLeave>(secondViewModel).ConfirmLeave());
            Assert.False(harness.Shell.ConfirmDiscardUnsavedChanges());
            Assert.False(harness.Shell.SaveCommand.CanExecute(null));
            Assert.False(harness.Shell.SaveAsProjectCommand.CanExecute(null));
            Assert.False(harness.Shell.TrySaveProject());
            Assert.False(harness.Shell.TrySaveProjectAs());
            Assert.False(harness.Shell.TryOpenProject(harness.SourcePdf));
            Assert.False(firstViewModel.RefreshProtocolCommand.CanExecute(null));
            Assert.False(firstViewModel.ErgaenzeStammdatenAusPdfsCommand.CanExecute(null));
            Assert.False(firstViewModel.CanMutateShaftData);
            Assert.True(firstViewModel.IsShaftDataReadOnly);
            Assert.False(firstViewModel.AddCommand.CanExecute(null));
            Assert.False(firstViewModel.RemoveCommand.CanExecute(null));
            Assert.False(firstViewModel.MoveUpCommand.CanExecute(null));
            Assert.False(firstViewModel.MoveDownCommand.CanExecute(null));
            Assert.False(firstViewModel.SaveCommand.CanExecute(null));
            Assert.False(secondViewModel.AddCommand.CanExecute(null));
            Assert.False(firstViewModel.MoveToPosition(2));
            InvokePrivateVoid(firstViewModel, "Add");
            InvokePrivateVoid(firstViewModel, "Remove");
            InvokePrivateVoid(firstViewModel, "MoveDown");
            InvokePrivateVoid(firstViewModel, "Save");
            Assert.Equal(new[] { refreshable, secondRecord }, firstViewModel.Records);
            Assert.False(harness.Project.Dirty);
            await InvokeRefreshProtocolAsync(firstViewModel);
            await InvokeStammdatenErgaenzungAsync(firstViewModel);
            Assert.Equal(0, firstDialog.ConfirmWarnCalls);

            harness.Shell.EnterLauncher();
            Assert.Equal(ShellMode.Workspace, harness.Shell.CurrentMode);
        }
        finally
        {
            release.Set();
        }

        await running;
        Assert.True(Assert.IsAssignableFrom<IConfirmLeave>(firstViewModel).ConfirmLeave());
        Assert.True(harness.Shell.SaveCommand.CanExecute(null));
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
        Assert.True(firstViewModel.RefreshProtocolCommand.CanExecute(null));
        Assert.True(firstViewModel.ErgaenzeStammdatenAusPdfsCommand.CanExecute(null));
        Assert.True(firstViewModel.CanMutateShaftData);
        Assert.False(firstViewModel.IsShaftDataReadOnly);
        Assert.True(firstViewModel.AddCommand.CanExecute(null));
        Assert.True(firstViewModel.RemoveCommand.CanExecute(null));
        Assert.True(firstViewModel.MoveDownCommand.CanExecute(null));
        Assert.True(firstViewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Entferntes_ziel_wird_beim_neueinlesen_nicht_mutiert_oder_wieder_eingefuegt()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var protocol = new ProtocolImportFake
        {
            Entered = entered,
            Release = release,
            ParseResult = ValidParseResult()
        };
        var viewModel = harness.CreateViewModel(
            new DialogFake(harness.SourcePdf),
            protocol);
        var record = CreateLinkedRecord(harness.SourcePdf);
        record.SetFieldValue("Schachtform", "Alt");
        viewModel.Records.Add(record);
        viewModel.Selected = record;

        var running = ExecuteRefreshCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Das Neueinlesen wurde nicht erreicht.");

        harness.Project.SchaechteData.Remove(record);
        release.Set();
        await running;

        Assert.Equal(0, protocol.ApplyCalls);
        Assert.Equal("Alt", record.GetFieldValue("Schachtform"));
        Assert.DoesNotContain(record, harness.Project.SchaechteData);
        Assert.Contains("entfernt", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Beim_einzelimport_entferntes_bestandsziel_wird_nicht_mutiert_oder_wieder_eingefuegt()
    {
        using var harness = new Harness();
        var existing = CreateLinkedRecord(harness.SourcePdf);
        existing.SetFieldValue("Schachtform", "Alt");
        harness.Project.SchaechteData.Add(existing);
        var protocol = new ProtocolImportFake
        {
            FindResult = existing,
            ParseResult = ValidParseResult(),
            OnDistribute = () => harness.Project.SchaechteData.Remove(existing)
        };
        var viewModel = harness.CreateViewModel(
            new DialogFake(
                harness.SourcePdf,
                DialogConfirm.Yes,
                DialogConfirm.Yes),
            protocol);

        await ExecuteImportCommandAsync(viewModel);

        Assert.Equal(0, protocol.ApplyCalls);
        Assert.Equal("Alt", existing.GetFieldValue("Schachtform"));
        Assert.DoesNotContain(existing, harness.Project.SchaechteData);
        Assert.Contains("entfernt", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gehaltenes_neueinlesen_sperrt_import_und_stammdaten_nachlauf()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var observedBlockedInDialog = false;
        dialog.OnConfirmWarn = () =>
            observedBlockedInDialog = !harness.Shell.SaveAsProjectCommand.CanExecute(null);
        var protocol = new ProtocolImportFake
        {
            Entered = entered,
            Release = release,
            ParseResult = InvalidParseResult()
        };
        var viewModel = harness.CreateViewModel(dialog, protocol);
        var record = CreateLinkedRecord(harness.SourcePdf);
        viewModel.Records.Add(record);
        viewModel.Selected = record;

        var running = ExecuteRefreshCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Das Neueinlesen wurde nicht erreicht.");
        Assert.True(observedBlockedInDialog);

        try
        {
            Assert.False(viewModel.ImportProtocolCommand.CanExecute(null));
            Assert.False(viewModel.ErgaenzeStammdatenAusPdfsCommand.CanExecute(null));
            await InvokeImportProtocolAsync(viewModel);
            await InvokeStammdatenErgaenzungAsync(viewModel);
            Assert.Equal(0, dialog.ConfirmCancelCalls);
            Assert.Equal(1, dialog.ConfirmWarnCalls);
        }
        finally
        {
            release.Set();
        }

        await running;
        Assert.True(viewModel.ImportProtocolCommand.CanExecute(null));
        Assert.True(viewModel.ErgaenzeStammdatenAusPdfsCommand.CanExecute(null));
    }

    [Fact]
    public async Task Neueinlesen_darf_seinen_guard_gebundenen_abschlusssave_ausfuehren()
    {
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf);
        var protocol = new ProtocolImportFake { ParseResult = ValidParseResult() };
        var viewModel = harness.CreateViewModel(dialog, protocol);
        var record = CreateLinkedRecord(harness.SourcePdf);
        viewModel.Records.Add(record);
        viewModel.Selected = record;

        await ExecuteRefreshCommandAsync(viewModel);

        Assert.True(File.Exists(harness.ProjectPath));
        Assert.False(harness.Project.Dirty);
        Assert.Equal(1, protocol.ApplyCalls);
        Assert.Contains("aktualisiert", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nicht gespeichert", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fehlerhaftes_neueinlesen_gibt_die_sperre_wieder_frei()
    {
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var protocol = new ProtocolImportFake
        {
            ParseException = new IOException("Testfehler")
        };
        var viewModel = harness.CreateViewModel(dialog, protocol);
        var record = CreateLinkedRecord(harness.SourcePdf);
        viewModel.Records.Add(record);
        viewModel.Selected = record;

        await ExecuteRefreshCommandAsync(viewModel);
        await ExecuteImportCommandAsync(viewModel);

        Assert.Equal(1, dialog.ConfirmWarnCalls);
        Assert.Equal(1, dialog.ConfirmCancelCalls);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Gehaltener_stammdaten_nachlauf_sperrt_import_und_neueinlesen_und_speichert_intern()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var record = CreateLinkedRecord(harness.SourcePdf);
        var stammdaten = new StammdatenFake
        {
            Entered = entered,
            Release = release,
            Result = StammdatenResult(record.Id)
        };
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var observedBlockedInDialog = false;
        dialog.OnConfirmWarn = () =>
            observedBlockedInDialog = !harness.Shell.SaveAsProjectCommand.CanExecute(null);
        var viewModel = harness.CreateViewModel(
            dialog,
            new ProtocolImportFake(),
            stammdaten);
        viewModel.Records.Add(record);
        viewModel.Selected = record;

        var running = ExecuteStammdatenCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der Stammdaten-Nachlauf wurde nicht erreicht.");
        Assert.True(observedBlockedInDialog);

        try
        {
            Assert.False(viewModel.ImportProtocolCommand.CanExecute(null));
            Assert.False(viewModel.RefreshProtocolCommand.CanExecute(null));
            Assert.False(harness.Shell.SaveAsProjectCommand.CanExecute(null));
            await InvokeImportProtocolAsync(viewModel);
            await InvokeRefreshProtocolAsync(viewModel);
            Assert.Equal(0, dialog.ConfirmCancelCalls);
            Assert.Equal(1, dialog.ConfirmWarnCalls);
        }
        finally
        {
            release.Set();
        }

        await running;
        Assert.Equal("Rund", record.GetFieldValue("Schachtform"));
        Assert.True(File.Exists(harness.ProjectPath));
        Assert.False(harness.Project.Dirty);
        Assert.True(viewModel.ImportProtocolCommand.CanExecute(null));
        Assert.True(viewModel.RefreshProtocolCommand.CanExecute(null));
    }

    [Fact]
    public async Task Stammdaten_nachlauf_mutiert_und_speichert_nach_projektwechsel_keinen_ersatz()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var originalRecord = CreateLinkedRecord(harness.SourcePdf);
        var stammdaten = new StammdatenFake
        {
            Entered = entered,
            Release = release,
            Result = StammdatenResult(originalRecord.Id)
        };
        var viewModel = harness.CreateViewModel(
            new DialogFake(harness.SourcePdf),
            new ProtocolImportFake(),
            stammdaten);
        viewModel.Records.Add(originalRecord);

        var running = ExecuteStammdatenCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der Stammdaten-Nachlauf wurde nicht erreicht.");

        var replacementRecord = CreateLinkedRecord(harness.SourcePdf);
        replacementRecord.Id = originalRecord.Id;
        var replacement = new Project { Name = "Ersatzprojekt" };
        replacement.SchaechteData.Add(replacementRecord);
        var replacementPath = harness.CreateProjectPath("Ersatzprojekt");
        harness.Settings.LastProjectPath = replacementPath;
        harness.Shell.ReplaceProject(replacement);
        harness.Shell.HasPersistedProject = true;
        release.Set();
        await running;

        Assert.Equal(string.Empty, originalRecord.GetFieldValue("Schachtform"));
        Assert.Equal(string.Empty, replacementRecord.GetFieldValue("Schachtform"));
        Assert.False(replacement.Dirty);
        Assert.False(File.Exists(replacementPath));
        Assert.Contains("Projekt wurde gewechselt", viewModel.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stammdaten_nachlauf_prueft_auch_den_beim_start_gebundenen_projektpfad()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var record = CreateLinkedRecord(harness.SourcePdf);
        var stammdaten = new StammdatenFake
        {
            Entered = entered,
            Release = release,
            Result = StammdatenResult(record.Id)
        };
        var viewModel = harness.CreateViewModel(
            new DialogFake(harness.SourcePdf),
            new ProtocolImportFake(),
            stammdaten);
        viewModel.Records.Add(record);

        var running = ExecuteStammdatenCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der Stammdaten-Nachlauf wurde nicht erreicht.");

        var replacementPath = harness.CreateProjectPath("Anderer-Pfad");
        harness.Settings.LastProjectPath = replacementPath;
        release.Set();
        await running;

        Assert.Equal(string.Empty, record.GetFieldValue("Schachtform"));
        Assert.False(harness.Project.Dirty);
        Assert.False(File.Exists(replacementPath));
        Assert.Contains("Projekt wurde gewechselt", viewModel.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abgebrochener_stammdaten_nachlauf_gibt_die_sperre_wieder_frei()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var record = CreateLinkedRecord(harness.SourcePdf);
        var stammdaten = new StammdatenFake
        {
            Entered = entered,
            Release = release,
            Result = StammdatenResult(record.Id)
        };
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var viewModel = harness.CreateViewModel(
            dialog,
            new ProtocolImportFake(),
            stammdaten);
        viewModel.Records.Add(record);

        var running = ExecuteStammdatenCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der Stammdaten-Nachlauf wurde nicht erreicht.");

        viewModel.CancelStammdatenErgaenzungCommand.Execute(null);
        await running;
        await ExecuteImportCommandAsync(viewModel);

        Assert.Contains("abgebrochen", viewModel.StammdatenErgaenzungText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, dialog.ConfirmCancelCalls);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Fehlerhafter_stammdaten_nachlauf_gibt_die_sperre_wieder_frei()
    {
        using var harness = new Harness();
        var record = CreateLinkedRecord(harness.SourcePdf);
        var stammdaten = new StammdatenFake
        {
            Exception = new IOException("Testfehler")
        };
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var viewModel = harness.CreateViewModel(
            dialog,
            new ProtocolImportFake(),
            stammdaten);
        viewModel.Records.Add(record);

        await ExecuteStammdatenCommandAsync(viewModel);
        await ExecuteImportCommandAsync(viewModel);

        Assert.Contains("konnten nicht", viewModel.StammdatenErgaenzungText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, dialog.ConfirmCancelCalls);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Fehler_gibt_die_gemeinsame_sperre_fuer_eine_zweite_viewmodel_frei()
    {
        using var harness = new Harness();
        var failedDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Yes);
        var nextDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var failedProtocol = new ProtocolImportFake
        {
            ParseException = new IOException("Testfehler")
        };
        var failedViewModel = harness.CreateViewModel(failedDialog, failedProtocol);
        var nextViewModel = harness.CreateViewModel(nextDialog, new ProtocolImportFake());

        await ExecuteImportCommandAsync(failedViewModel);
        await ExecuteImportCommandAsync(nextViewModel);

        Assert.Equal(1, nextDialog.ConfirmCancelCalls);
        Assert.True(Assert.IsAssignableFrom<IConfirmLeave>(failedViewModel).ConfirmLeave());
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Einzelimport_darf_nur_seinen_guard_gebundenen_abschlusssave_ausfuehren()
    {
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Yes);
        var protocol = new ProtocolImportFake
        {
            ParseResult = ValidParseResult()
        };
        var viewModel = harness.CreateViewModel(dialog, protocol);

        await ExecuteImportCommandAsync(viewModel);

        Assert.True(File.Exists(harness.ProjectPath));
        Assert.False(harness.Project.Dirty);
        Assert.Single(harness.Project.SchaechteData);
        Assert.Equal(1, protocol.ApplyCalls);
        Assert.DoesNotContain("nicht gespeichert", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Dispose_entfernt_den_guard_und_hinterlaesst_keine_reservierung()
    {
        using var harness = new Harness();
        var firstDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var firstViewModel = harness.CreateViewModel(firstDialog, new ProtocolImportFake());
        var disposable = Assert.IsAssignableFrom<IDisposable>(firstViewModel);

        disposable.Dispose();

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
        var nextDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var nextViewModel = harness.CreateViewModel(nextDialog, new ProtocolImportFake());
        await ExecuteImportCommandAsync(nextViewModel);
        Assert.Equal(1, nextDialog.ConfirmCancelCalls);
    }

    [Fact]
    public async Task Dispose_waehrend_import_gibt_navigation_erst_nach_abschluss_frei()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var dialog = new DialogFake(harness.SourcePdf, DialogConfirm.Yes);
        var protocol = new ProtocolImportFake
        {
            Entered = entered,
            Release = release,
            ParseResult = InvalidParseResult()
        };
        var viewModel = harness.CreateViewModel(dialog, protocol);

        var running = ExecuteImportCommandAsync(viewModel);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Der Import wurde nicht erreicht.");

        Assert.IsAssignableFrom<IDisposable>(viewModel).Dispose();
        Assert.False(Assert.IsAssignableFrom<IConfirmLeave>(viewModel).ConfirmLeave());
        Assert.False(harness.Shell.ConfirmDiscardUnsavedChanges());

        release.Set();
        await running;

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
        var nextDialog = new DialogFake(harness.SourcePdf, DialogConfirm.Cancel);
        var nextViewModel = harness.CreateViewModel(nextDialog, new ProtocolImportFake());
        await ExecuteImportCommandAsync(nextViewModel);
        Assert.Equal(1, nextDialog.ConfirmCancelCalls);
    }

    private static Task InvokeImportProtocolAsync(SchaechtePageViewModel viewModel)
    {
        var method = typeof(SchaechtePageViewModel).GetMethod(
            "ImportProtocolAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ImportProtocolAsync fehlt.");
        return method.Invoke(viewModel, null) as Task
               ?? throw new InvalidOperationException("ImportProtocolAsync lieferte keinen Task.");
    }

    private static Task InvokeRefreshProtocolAsync(SchaechtePageViewModel viewModel)
        => InvokePrivateTask(viewModel, "RefreshProtocolAsync");

    private static Task InvokeStammdatenErgaenzungAsync(SchaechtePageViewModel viewModel)
        => InvokePrivateTask(viewModel, "ErgaenzeStammdatenAusPdfsAsync");

    private static Task InvokePrivateTask(
        SchaechtePageViewModel viewModel,
        string methodName)
    {
        var method = typeof(SchaechtePageViewModel).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{methodName} fehlt.");
        return method.Invoke(viewModel, null) as Task
               ?? throw new InvalidOperationException($"{methodName} lieferte keinen Task.");
    }

    private static void InvokePrivateVoid(
        SchaechtePageViewModel viewModel,
        string methodName)
    {
        var method = typeof(SchaechtePageViewModel).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{methodName} fehlt.");
        method.Invoke(viewModel, null);
    }

    private static IShellOperationGuard GetProtocolOperationGuard(
        SchaechtePageViewModel viewModel)
    {
        var field = typeof(SchaechtePageViewModel).GetField(
            "_protocolImportShellGuard",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Protokollimport-Guard fehlt.");
        return Assert.IsAssignableFrom<IShellOperationGuard>(field.GetValue(viewModel));
    }

    private static Task ExecuteImportCommandAsync(SchaechtePageViewModel viewModel)
        => Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.ImportProtocolCommand)
            .ExecuteAsync(null);

    private static Task ExecuteRefreshCommandAsync(SchaechtePageViewModel viewModel)
        => Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.RefreshProtocolCommand)
            .ExecuteAsync(null);

    private static Task ExecuteStammdatenCommandAsync(SchaechtePageViewModel viewModel)
        => viewModel.ErgaenzeStammdatenAusPdfsCommand.ExecuteAsync(null);

    private static SchachtRecord CreateLinkedRecord(string pdfPath)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-1");
        record.SetFieldValue("PDF_Path", pdfPath);
        return record;
    }

    private static SchachtStammdatenErgaenzungsErgebnis StammdatenResult(Guid recordId)
        => new(
            Gesamt: 1,
            BereitsVollstaendig: 0,
            PdfGefunden: 1,
            MitErgaenzung: 1,
            PdfNichtGefunden: 0,
            NichtLesbar: 0,
            Ergaenzungen:
            [
                new SchachtStammdatenErgaenzung(
                    recordId,
                    "protokoll.pdf",
                    "Rund",
                    null,
                    null)
            ],
            Meldungen: Array.Empty<string>());

    private static SchachtProtocolParseResult ValidParseResult()
        => new(
            IstSchachtprotokoll: true,
            Schachtnummer: "S-1",
            Datum: "20.08.2026",
            Funktion: null,
            Schachtform: null,
            Dimension: null,
            Schachttiefe: null,
            PrimaereSchaeden: null,
            Bemerkungen: null,
            Status: null,
            Link: null,
            Schaeden: Array.Empty<(string Bauteil, string Schaden)>());

    private static SchachtProtocolParseResult InvalidParseResult()
        => ValidParseResult() with
        {
            IstSchachtprotokoll = false,
            Schachtnummer = null,
            Lesehinweis = "Kein Schachtprotokoll"
        };

    private sealed class Harness : IDisposable
    {
        private readonly DirectoryInfo _tempDirectory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ServiceProvider _services;
        private readonly List<IDisposable> _viewModels = [];

        internal Harness()
        {
            _tempDirectory = Directory.CreateTempSubdirectory("schacht-protocol-operation-");
            var projectRoot = Path.Combine(_tempDirectory.FullName, "Projekt");
            Directory.CreateDirectory(projectRoot);
            ProjectPath = ProjectFileLocator.TargetPath(projectRoot);
            SourcePdf = Path.Combine(_tempDirectory.FullName, "quelle.pdf");
            File.WriteAllText(SourcePdf, "PDF");

            Settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = ProjectPath
            };
            _loggerFactory = LoggerFactory.Create(_ => { });
            _services = new ServiceProvider(
                Settings,
                new DiagnosticsOptions(),
                _loggerFactory.CreateLogger("test"),
                _loggerFactory);
            Shell = new ShellViewModel(
                _services,
                new SystemMonitorService(enableHardwareSensorInit: false));
            Project = new Project { Name = "Guard-Test" };
            Shell.ReplaceProject(Project);
            Shell.HasPersistedProject = true;
            Shell.EnterWorkspaceOn("Uebersicht");
        }

        internal AppSettings Settings { get; }
        internal ShellViewModel Shell { get; }
        internal Project Project { get; }
        internal string ProjectPath { get; }
        internal string SourcePdf { get; }

        internal string CreateProjectPath(string folderName)
        {
            var root = Path.Combine(_tempDirectory.FullName, folderName);
            Directory.CreateDirectory(root);
            return ProjectFileLocator.TargetPath(root);
        }

        internal SchaechtePageViewModel CreateViewModel(
            IDialogService dialogs,
            ISchachtProtocolImportService protocolImport,
            ISchachtStammdatenErgaenzungsService? stammdaten = null)
        {
            var viewModel = new SchaechtePageViewModel(
                Shell,
                Settings,
                dialogs,
                protocolImport,
                stammdaten ?? _services.SchachtStammdatenErgaenzung,
                _services.SchachtMassnahmenKatalog,
                _services.CostStores.CreateProjectCostStore("schacht_empfehlungen.json"),
                _services.DropdownOptions,
                _services.PdfTextLayerRewrite,
                _services.ShellOpen,
                _services.ShaftRename,
                _services.ExplorerReveal,
                _services.SchaechteTemplateColumns,
                _services.SchachtFileTargets,
                _services.SchachtProtocolFiles);
            if ((object)viewModel is IDisposable disposable)
                _viewModels.Add(disposable);
            return viewModel;
        }

        public void Dispose()
        {
            foreach (var viewModel in _viewModels)
                viewModel.Dispose();
            Shell.Dispose();
            _loggerFactory.Dispose();
            try
            {
                _tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }
    }

    private sealed class DialogFake : IDialogService
    {
        private readonly object _gate = new();
        private readonly Queue<DialogConfirm> _confirmCancelResults;
        private readonly string _sourcePdf;

        internal DialogFake(string sourcePdf, params DialogConfirm[] confirmCancelResults)
        {
            _sourcePdf = sourcePdf;
            _confirmCancelResults = new Queue<DialogConfirm>(confirmCancelResults);
        }

        internal int ConfirmCancelCalls { get; private set; }
        internal int ConfirmWarnCalls { get; private set; }
        internal Action? OnConfirmCancel { get; set; }
        internal Action? OnConfirmWarn { get; set; }
        internal bool ConfirmWarnResult { get; set; } = true;

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => _sourcePdf;

        public string? SaveFile(
            string title,
            string filter,
            string? defaultExt = null,
            string? defaultFileName = null)
            => null;

        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
        {
            Action? callback;
            bool result;
            lock (_gate)
            {
                ConfirmWarnCalls++;
                callback = OnConfirmWarn;
                result = ConfirmWarnResult;
            }

            callback?.Invoke();
            return result;
        }

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
        {
            Action? callback;
            DialogConfirm result;
            lock (_gate)
            {
                ConfirmCancelCalls++;
                callback = OnConfirmCancel;
                result = _confirmCancelResults.Count > 0
                    ? _confirmCancelResults.Dequeue()
                    : DialogConfirm.Cancel;
            }

            callback?.Invoke();
            return result;
        }
    }

    private sealed class ProtocolImportFake :
        ISchachtProtocolImportService,
        ISchachtProtocolDistributionResultService
    {
        private int _parseCalls;
        private int _applyCalls;

        internal ManualResetEventSlim? Entered { get; init; }
        internal ManualResetEventSlim? Release { get; init; }
        internal Exception? ParseException { get; init; }
        internal SchachtProtocolParseResult ParseResult { get; init; } = InvalidParseResult();
        internal SchachtRecord? FindResult { get; init; }
        internal Action? OnDistribute { get; init; }
        internal int ParseCalls => Volatile.Read(ref _parseCalls);
        internal int ApplyCalls => Volatile.Read(ref _applyCalls);

        public SchachtProtocolParseResult Parse(string pdfPfad)
        {
            Interlocked.Increment(ref _parseCalls);
            Entered?.Set();
            if (Release is not null
                && !Release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Testfreigabe fehlt.");
            }

            if (ParseException is not null)
                throw ParseException;

            return ParseResult;
        }

        public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
        {
            _ = project;
            _ = schachtnummer;
            return FindResult;
        }

        public void Apply(
            SchachtRecord ziel,
            SchachtProtocolParseResult ergebnis,
            string pdfPfadFuerFeld)
        {
            Interlocked.Increment(ref _applyCalls);
            ziel.SetFieldValue("Schachtnummer", ergebnis.Schachtnummer);
            ziel.SetFieldValue("PDF_Path", pdfPfadFuerFeld);
        }

        public string DistributePdf(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
            => $"Schaechte_Verteilt/{schachtnummer}/protokoll.pdf";

        public SchachtProtocolDistributionResult DistributePdfWithResult(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
        {
            OnDistribute?.Invoke();
            return new(
                DistributePdf(projektOrdner, schachtnummer, pdfQuelle),
                FileCreated: false);
        }
    }

    private sealed class StammdatenFake : ISchachtStammdatenErgaenzungsService
    {
        internal ManualResetEventSlim? Entered { get; init; }
        internal ManualResetEventSlim? Release { get; init; }
        internal Exception? Exception { get; init; }
        internal SchachtStammdatenErgaenzungsErgebnis Result { get; init; }
            = StammdatenResult(Guid.NewGuid());

        public SchachtStammdatenErgaenzungsErgebnis Ermitteln(
            string projektOrdner,
            IReadOnlyList<SchachtStammdatenQuelle> schaechte,
            IProgress<SchachtStammdatenErgaenzungsFortschritt>? fortschritt = null,
            CancellationToken cancellationToken = default)
        {
            _ = projektOrdner;
            _ = schaechte;
            _ = fortschritt;
            Entered?.Set();
            if (Release is not null
                && !Release.Wait(TimeSpan.FromSeconds(5), cancellationToken))
            {
                throw new TimeoutException("Testfreigabe fehlt.");
            }

            if (Exception is not null)
                throw Exception;

            return Result;
        }
    }
}
