using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Hintergrund-Nachlauf fuer PDF-Stammdaten. Bewusst als eigener Teil gehalten,
/// damit das allgemeine Schacht-ViewModel nicht zur Grossklasse wird.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    private CancellationTokenSource? _stammdatenErgaenzungCts;

    [ObservableProperty] private bool _isStammdatenErgaenzungInProgress;
    [ObservableProperty] private double _stammdatenErgaenzungProgress;
    [ObservableProperty] private string _stammdatenErgaenzungText = string.Empty;

    public IAsyncRelayCommand ErgaenzeStammdatenAusPdfsCommand { get; }
    public IRelayCommand CancelStammdatenErgaenzungCommand { get; }

    partial void OnIsStammdatenErgaenzungInProgressChanged(bool value)
    {
        _ = value;
        ErgaenzeStammdatenAusPdfsCommand.NotifyCanExecuteChanged();
        (CancelStammdatenErgaenzungCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private bool CanErgaenzeStammdatenAusPdfs()
        => CanStartProtocolPdfOperation()
           && !IsStammdatenErgaenzungInProgress
           && Records.Count > 0;

    private void CancelStammdatenErgaenzung()
    {
        _stammdatenErgaenzungCts?.Cancel();
        StammdatenErgaenzungText = "Abbruch angefordert ...";
    }

    private async Task ErgaenzeStammdatenAusPdfsAsync()
    {
        if (!TryBeginProtocolPdfOperation("PDF-Stammdaten-Nachlauf"))
            return;

        try
        {
            await ErgaenzeStammdatenAusPdfsCoreAsync();
        }
        finally
        {
            EndProtocolPdfOperation();
        }
    }

    private async Task ErgaenzeStammdatenAusPdfsCoreAsync()
    {
        const string dialogTitle = "PDF-Stammdaten ergaenzen";
        var projectContext = new ProjectOperationContext(
            _shell.Project,
            _settings.LastProjectPath);
        var projectRecords = projectContext.Project.SchaechteData;
        var projektOrdner = ProjectFileLocator.ProjectRootFromFile(projectContext.ProjectPath);
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", dialogTitle);
            return;
        }

        if (!_dialogs.ConfirmWarn(
                "Fehlende Schachtform, Dimension und Schachttiefe werden aus den bereits vorhandenen PDFs ergaenzt.\n\n" +
                "Vorhandene Eintraege bleiben unveraendert. Der Vorgang kann bei vielen PDFs einige Minuten dauern.",
                dialogTitle))
            return;

        List<SchachtStammdatenQuelle> quellen;
        lock (_shell.CollectionLock)
        {
            quellen = projectRecords
                .Select(record => new SchachtStammdatenQuelle(
                    record.Id,
                    ResolveSchachtNummer(record),
                    record.GetFieldValue("PDF_Path"),
                    record.GetFieldValue("Link"),
                    record.GetFieldValue("Schachtform"),
                    record.GetFieldValue("Dimension"),
                    record.GetFieldValue("Schachttiefe")))
                .ToList();
        }

        _stammdatenErgaenzungCts?.Dispose();
        _stammdatenErgaenzungCts = new CancellationTokenSource();
        var cancellationToken = _stammdatenErgaenzungCts.Token;
        IsStammdatenErgaenzungInProgress = true;
        StammdatenErgaenzungProgress = 0;
        StammdatenErgaenzungText = "Vorhandene Schacht-PDFs werden geprueft ...";

        var progress = new Progress<SchachtStammdatenErgaenzungsFortschritt>(p =>
        {
            StammdatenErgaenzungProgress = p.Gesamt <= 0
                ? 0
                : Math.Clamp(p.Aktuell * 100d / p.Gesamt, 0d, 100d);
            StammdatenErgaenzungText = $"{p.Meldung} ({p.Aktuell}/{p.Gesamt})";
        });

        try
        {
            var result = await Task.Run(
                () => _schachtStammdatenErgaenzung.Ermitteln(
                    projektOrdner,
                    quellen,
                    progress,
                    cancellationToken),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (!ProjectIsStillOpen(
                    projectContext,
                    dialogTitle,
                    ProjectOperationImpact.None))
                return;

            var applyResult = SchachtStammdatenResultApplier.Apply(
                projectRecords,
                result,
                beforeApply: () =>
                {
                    if (result.Ergaenzungen.Count > 0)
                        _shell.TryCreateImportRestorePoint("Schacht-PDF-Stammdaten");
                });

            if (applyResult.ChangedShaftCount > 0)
            {
                var project = projectContext.Project;
                project.ModifiedAtUtc = DateTime.UtcNow;
                project.Dirty = true;
                if (!ProjectIsStillOpen(
                        projectContext,
                        dialogTitle,
                        ProjectOperationImpact.ProjectDataChanged))
                    return;

                _shell.MarkProjectDirty();
                if (!_saveProjectForProtocolImport())
                {
                    _dialogs.Warn(
                        "Die Werte wurden in der geoeffneten Ansicht ergaenzt, konnten aber noch nicht gespeichert werden. Bitte erneut speichern.",
                        dialogTitle);
                }
            }

            StammdatenErgaenzungProgress = 100;
            LastResult = applyResult.Summary;
            StammdatenErgaenzungText = applyResult.Summary;
            _dialogs.Info(applyResult.DialogText, dialogTitle);
        }
        catch (OperationCanceledException)
        {
            LastResult = "PDF-Stammdaten: Vorgang abgebrochen. Es wurden keine Werte uebernommen.";
            StammdatenErgaenzungText = LastResult;
        }
        catch (Exception ex)
        {
            LastResult = "PDF-Stammdaten konnten nicht ergaenzt werden: "
                         + UserError.DescribeAndReport(ex, "Schacht-PDF-Stammdaten");
            StammdatenErgaenzungText = LastResult;
            _dialogs.Warn(LastResult, dialogTitle);
        }
        finally
        {
            IsStammdatenErgaenzungInProgress = false;
            _stammdatenErgaenzungCts?.Dispose();
            _stammdatenErgaenzungCts = null;
        }
    }

}
