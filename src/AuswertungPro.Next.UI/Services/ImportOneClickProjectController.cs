using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportOneClickProjectActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    Func<Project, Project> DeepCopyProject,
    Action<Project> ReplaceProject,
    object CollectionLock,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails,
    Func<Project, string>? ComputeSignature = null,
    Func<string?>? GetProjectPath = null);

/// <summary>Steuert den vollständigen Ein-Knopf-Import eines Kanalfernseh-Projekts.</summary>
internal sealed class ImportOneClickProjectController
{
    private readonly IDialogService _dialogs;
    private readonly Func<IOneClickProjectImportService> _createImporter;
    private readonly IOneClickImportReportWriter _reportWriter;

    /// <summary>
    /// Nimmt die Dateien zurueck, wenn das Importergebnis verworfen wird
    /// (Gesamtaudit 2026-08-14, P1-5). Optional, damit bestehende Aufrufer und Tests
    /// ohne Ledger unveraendert laufen.
    /// </summary>
    private readonly IImportedFileLedger? _fileLedger;
    private readonly IImportFileStagingService? _fileStaging;
    private readonly IImportTransactionJournal? _transactionJournal;

    public ImportOneClickProjectController(
        IDialogService dialogs,
        Func<IOneClickProjectImportService> createImporter,
        IOneClickImportReportWriter reportWriter,
        IImportedFileLedger? fileLedger = null,
        IImportFileStagingService? fileStaging = null,
        IImportTransactionJournal? transactionJournal = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _createImporter = createImporter ?? throw new ArgumentNullException(nameof(createImporter));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _fileLedger = fileLedger;
        _fileStaging = fileStaging;
        _transactionJournal = transactionJournal;
    }

    public async Task ExecuteAsync(ImportOneClickProjectActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Bitte zuerst ein Projekt anlegen/speichern.",
                "Import Kanalfernseh-Projekt");
            return;
        }

        var sourceFolder = _dialogs.SelectFolder(
            "Quellordner der Kanalfernsehdaten waehlen (WinCan-, IKAS- oder KINS-Projektordner)",
            null);
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return;

        var liveProject = actions.GetProject();
        var projectContext = new ProjectOperationContext(liveProject, projectFolder);
        if (!TryComputeSignature(actions, liveProject, out var initialSignature))
            return;
        var targetProject = actions.DeepCopyProject(liveProject);

        ImportFileTransaction? fileTransaction = null;
        var legacyRollbackEnabled = true;
        var projectCommitted = false;
        try
        {
            var staging = _fileStaging?.Begin(actions.GetProjectPath?.Invoke());
            fileTransaction = new ImportFileTransaction(
                "Kanalfernseh-Projekt",
                staging,
                _transactionJournal);

            actions.SetProgress(
                "Kanalfernseh-Projekt importieren: erkennen -> archivieren -> parsen -> verteilen...");
            var context = new ImportRunContext(
                CancellationToken.None,
                null,
                new ImportRunLog(),
                collectionLock: actions.CollectionLock,
                fileStaging: staging);

            // Das alte Ledger bleibt bis zum Beginn der Veroeffentlichung ein zusaetzliches
            // Sicherheitsnetz fuer noch nicht migrierte Altpfade.
            var folderBeforeRun = TryCaptureFolder(projectFolder);

            OneClickProjectImportResult result;
            try
            {
                result = await Task.Run(() =>
                    _createImporter().Import(sourceFolder, projectFolder, targetProject, context));
            }
            catch (Exception ex)
            {
                actions.SetProgress(string.Empty);
                var userMessage = UserError.DescribeAndReport(ex, "Kanalfernseh-Projekt importieren");
                var rollback = legacyRollbackEnabled ? TryRollback(folderBeforeRun) : string.Empty;
                _dialogs.Error(
                    $"Import fehlgeschlagen - Projektdaten wurden nicht uebernommen:\n{userMessage}{rollback}",
                    "Import Kanalfernseh-Projekt");
                return;
            }
            actions.SetProgress(string.Empty);

            if (result.Format is OneClickProjectImportFormat.Unknown or OneClickProjectImportFormat.Ambiguous)
            {
                var hint = string.Join("\n", result.Messages.Take(6));
                var rollback = legacyRollbackEnabled ? TryRollback(folderBeforeRun) : string.Empty;
                _dialogs.Info(
                    $"Format nicht eindeutig erkannt ({result.Format}).\n{hint}\n\n"
                    + "Nutze ggf. die manuellen Import-Knoepfe (WinCan/XTF/PDF/IBAK/KINS)."
                    + rollback,
                    "Import Kanalfernseh-Projekt");
                return;
            }

            if (!ActiveProjectGuard.IsCurrent(
                    projectContext,
                    actions.GetProject(),
                    actions.GetProjectFolder()))
            {
                var rollback = legacyRollbackEnabled ? TryRollback(folderBeforeRun) : string.Empty;
                _dialogs.Error(
                    "Waehrend des Imports wurde das aktive Projekt oder sein Speicherpfad gewechselt. " +
                    "Das Importergebnis wurde aus Sicherheitsgruenden nicht uebernommen." + rollback,
                    "Import Kanalfernseh-Projekt");
                return;
            }

            if (!TryComputeSignature(actions, liveProject, out var currentSignature))
            {
                if (legacyRollbackEnabled)
                    TryRollback(folderBeforeRun);
                return;
            }

            if (actions.ComputeSignature is not null
                && !string.Equals(initialSignature, currentSignature, StringComparison.Ordinal))
            {
                var rollback = legacyRollbackEnabled ? TryRollback(folderBeforeRun) : string.Empty;
                _dialogs.Error(
                    "Das Projekt wurde waehrend des Imports bearbeitet. " +
                    "Das Importergebnis wurde aus Sicherheitsgruenden nicht uebernommen; " +
                    "die zwischenzeitlichen Aenderungen bleiben erhalten." + rollback,
                    "Import Kanalfernseh-Projekt");
                return;
            }

            // Ab hier besitzt ausschliesslich der persistente Marker die Ruecknahme.
            // Das Ordner-Ledger darf veroeffentlichte Dateien nie wieder loeschen.
            legacyRollbackEnabled = false;
            fileTransaction.Publish();
            fileTransaction.StampProject(targetProject);
            actions.ReplaceProject(targetProject);
            projectCommitted = true;
            fileTransaction.MarkProjectCommitted();
            _reportWriter.TryWrite(projectFolder, result);

            var saved = ProjectSaveAttempt.Try(
                actions.SaveProject,
                "Kanalfernseh-Projekt nach Ein-Knopf-Import speichern",
                out var saveError);
            if (saved)
                fileTransaction.MarkProjectSaved();

            var summary = saved
                ? $"Import abgeschlossen ({result.Format}):"
                : $"Import uebernommen, aber Speichern fehlgeschlagen ({result.Format}):";
            summary += $"\n  {result.Found} Haltungen ({result.Created} neu, {result.Updated} aktualisiert)"
                + $"\n  {result.Errors} Fehler, {result.Conflicts} Feld-Konflikte"
                + "\n  Rohdaten archiviert, Filme/Fotos verteilt (Report in __IMPORT_REPORTS\\)";
            if (!saved)
            {
                summary += "\n  Hinweis: Die Projektdaten liegen nur im Arbeitsspeicher. " +
                           "Bitte das Projekt manuell speichern, sonst geht der Import beim Schliessen verloren."
                           + ProjectSaveAttempt.ErrorDetails(saveError);
            }
            actions.AppendSummary("\n" + summary);
            if (result.Messages.Count > 0)
            {
                actions.AppendDetails(
                    "\n\nKanalfernseh-Import:\n" + string.Join("\n", result.Messages.Take(80)));
            }

            if (saved)
                _dialogs.Info(summary, "Import Kanalfernseh-Projekt");
            else
                _dialogs.Error(summary, "Import Kanalfernseh-Projekt");
        }
        catch (Exception ex)
        {
            actions.SetProgress(string.Empty);
            var userMessage = UserError.DescribeAndReport(ex, "Kanalfernseh-Projekt abschliessen");
            _dialogs.Error(
                projectCommitted
                    ? "Der Import wurde uebernommen, aber der Abschluss ist fehlgeschlagen. " +
                      "Bitte das Projekt manuell speichern.\n" + userMessage
                    : "Import fehlgeschlagen - Projektdaten wurden nicht uebernommen.\n" + userMessage,
                "Import Kanalfernseh-Projekt");
        }
        finally
        {
            var cleanup = fileTransaction?.Cleanup();
            if (cleanup is { StagingCleanupSucceeded: false, StagingCleanupError: { } error })
            {
                actions.AppendDetails(
                    "\n\nDatei-Arbeitsordner konnte nicht vollstaendig aufgeraeumt werden: " +
                    error.Message);
            }
        }
    }

    /// <summary>
    /// Momentaufnahme des Projektordners. Ein Fehler dabei darf den Import nicht
    /// verhindern — dann gibt es lediglich keine Ruecknahme.
    /// </summary>
    private ImportFolderSnapshot? TryCaptureFolder(string projectFolder)
    {
        if (_fileLedger is null)
            return null;

        try
        {
            return _fileLedger.Capture(projectFolder);
        }
        catch (Exception ex)
        {
            UserError.DescribeAndReport(ex, "Projektordner vor dem Import erfassen");
            return null;
        }
    }

    /// <summary>
    /// Nimmt die Dateien des verworfenen Laufs zurueck und liefert einen Zusatz fuer die
    /// Meldung. Der Benutzer soll immer erfahren, was mit den Dateien geschehen ist —
    /// auch dann, wenn die Ruecknahme aus Sicherheitsgruenden verweigert wurde.
    /// </summary>
    private string TryRollback(ImportFolderSnapshot? before)
    {
        if (_fileLedger is null || before is null)
            return string.Empty;

        try
        {
            var result = _fileLedger.RollbackNewFiles(before);
            if (result.RolledBack && result.DeletedFiles == 0 && result.KeptFiles == 0)
                return "\n\nEs waren keine neuen Dateien im Projektordner zurueckzunehmen.";

            if (result.RolledBack)
            {
                var rest = result.KeptFiles > 0
                    ? $" {result.KeptFiles} Datei(en) blieben liegen und sollten geprueft werden."
                    : string.Empty;
                return "\n\nDie waehrend des Laufs angelegten Dateien wurden zurueckgenommen "
                       + $"({result.DeletedFiles} entfernt).{rest}";
            }

            return "\n\nAchtung: Die angelegten Dateien konnten nicht sicher zurueckgenommen werden "
                   + "und liegen weiterhin im Projektordner. Grund: "
                   + string.Join(" ", result.Messages.Take(2));
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Importdateien zuruecknehmen");
            return $"\n\nAchtung: Die angelegten Dateien konnten nicht zurueckgenommen werden: {userMessage}";
        }
    }

    private bool TryComputeSignature(
        ImportOneClickProjectActions actions,
        Project project,
        out string? signature)
    {
        signature = null;
        if (actions.ComputeSignature is null)
            return true;

        try
        {
            signature = actions.ComputeSignature(project);
            return true;
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(
                ex,
                "Projektinhalt fuer Kanalfernseh-Import pruefen");
            _dialogs.Error(
                "Der aktuelle Projektstand konnte nicht sicher geprueft werden. " +
                $"Das Importergebnis wurde nicht uebernommen.\n{userMessage}",
                "Import Kanalfernseh-Projekt");
            return false;
        }
    }
}
