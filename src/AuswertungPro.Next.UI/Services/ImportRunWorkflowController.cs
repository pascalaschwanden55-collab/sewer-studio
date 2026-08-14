using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

public sealed record ImportRunWorkflowRequest<TSource>(
    string Label,
    TSource Source,
    Func<TSource, Project, ImportRunContext, Result<ImportStats>> Import,
    bool DryRun = false,
    Func<TSource, Project, ImportRunContext, Task>? PostImportAsync = null,
    bool SaveProjectAfterCommit = false,
    Func<string?, IImportFileStagingSession?>? BeginFileStaging = null);

public sealed record ImportRunWorkflowActions(
    Func<Project> GetProject,
    Func<string?> GetProjectPath,
    Func<Project, Project> DeepCopyProject,
    Action<Project> ReplaceProject,
    Action<string> CreateRestorePoint,
    Func<string?> GetReportDir,
    Func<ImportRunLog, string, string> ExportReport,
    Func<ImportPreviewResult, string, bool> ShowPreview,
    Func<Project, IReadOnlyList<string>> ValidatePlausibility,
    Action<Project> DeduplicateAllPrimaryDamages,
    Func<Project, string, Task> RunAfterImportAsync,
    Func<bool> SaveProject,
    Action<string> SetStatus,
    Action<bool> SetCanCancel,
    Action<bool> SetIsImportInProgress,
    Action<double> SetProgressPercent,
    Action<string> SetPhase,
    Action<string> SetProgressText,
    Func<string> GetSummaryText,
    Action<string> SetSummaryText,
    Func<string> GetDetailsText,
    Action<string> SetDetailsText,
    Action<string> SetLastReportPath,
    object? CollectionLock = null,
    // Inhalts-Signatur des Live-Projekts (U4). Null = keine Signaturpruefung (Abwaertskompatibilitaet
    // fuer Tests, die den Konfliktschutz nicht betreffen).
    Func<Project, string>? ComputeSignature = null,
    // Transaktions-Journal fuer die Absturz-Atomaritaet. Null = kein Marker (z.B. Tests ohne
    // Datei-Staging). Wird nur zusammen mit einer File-Staging-Session genutzt.
    IImportTransactionJournal? Journal = null);

public static class ImportRunWorkflowController
{
    public static Task RunAsync<TSource>(
        ImportRunWorkflowRequest<TSource> request,
        ImportRunWorkflowActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var liveProject = actions.GetProject();
        var projectSnapshot = new ActiveProjectSnapshot(
            liveProject,
            NormalizeProjectPath(actions.GetProjectPath()),
            actions.ComputeSignature?.Invoke(liveProject) ?? string.Empty);
        var initialReportDirectory = actions.GetReportDir();
        return RunCoreAsync(
            request,
            actions,
            cancellationToken,
            projectSnapshot,
            initialReportDirectory);
    }

    private static async Task RunCoreAsync<TSource>(
        ImportRunWorkflowRequest<TSource> request,
        ImportRunWorkflowActions actions,
        CancellationToken cancellationToken,
        ActiveProjectSnapshot projectSnapshot,
        string? initialReportDirectory)
    {
        actions.SetCanCancel(true);
        actions.SetIsImportInProgress(true);
        actions.SetProgressPercent(0);
        actions.SetPhase(request.DryRun
            ? $"{request.Label}: Vorschau wird berechnet..."
            : $"{request.Label}: Import laeuft...");
        actions.SetProgressText("");
        actions.SetSummaryText($"{request.Label}: gestartet{(request.DryRun ? " (Vorschau)" : "")}");
        actions.SetDetailsText("");

        var runLog = new ImportRunLog
        {
            ImportType = request.Label,
            WasDryRun = request.DryRun,
            SourcePath = ResolveSourcePath(request.Source)
        };

        var progress = new Progress<ImportProgress>(p =>
        {
            actions.SetPhase(p.Phase);
            actions.SetProgressText(p.StatusText);
            if (p.Total > 0)
                actions.SetProgressPercent((double)p.Current / p.Total * 100.0);
            if (!string.IsNullOrWhiteSpace(p.CurrentFile))
                actions.SetStatus($"{request.Label}: {p.CurrentFile}");
        });

        IImportFileStagingSession? fileStaging = null;
        ImportFileTransaction? fileTransaction = null;
        var projectCommitted = false;
        var projectSaved = false;
        var followUpRunStarted = false;
        var postImportIncomplete = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureProjectIsStillCurrent(request.Label, actions, runLog, projectSnapshot))
                return;

            if (!request.DryRun)
                actions.CreateRestorePoint(request.Label);

            fileStaging = request.DryRun
                ? null
                : request.BeginFileStaging?.Invoke(projectSnapshot.ProjectPath);
            fileTransaction = new ImportFileTransaction(
                request.Label,
                fileStaging,
                actions.Journal);
            var ctx = new ImportRunContext(
                cancellationToken,
                progress,
                runLog,
                request.DryRun,
                actions.CollectionLock,
                fileStaging);

            // Vorschau UND echter Import arbeiten auf einer unabhaengigen Kopie.
            // Erst nach einem vollstaendig erfolgreichen Lauf wird die Live-Referenz getauscht.
            var targetProject = actions.DeepCopyProject(projectSnapshot.Project);

            var result = await Task.Run(() => RunImport(request, targetProject, ctx));
            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureProjectIsStillCurrent(request.Label, actions, runLog, projectSnapshot))
                return;

            if (!result.Ok || result.Value is null)
            {
                actions.SetSummaryText(
                    $"{request.Label} Import fehlgeschlagen - Projektdaten wurden nicht uebernommen: " +
                    result.ErrorMessage);
                actions.SetStatus(
                    $"{request.Label} Import fehlgeschlagen - Projektdaten wurden nicht uebernommen");
                return;
            }

            var stats = result.Value;
            actions.SetSummaryText($"{request.Label} Import{(request.DryRun ? " (Vorschau)" : "")}:\n" +
                                   $"  Haltungen: {stats.Found} gefunden, {stats.Created} neu, {stats.Updated} aktualisiert\n" +
                                   $"  Fehler: {stats.Errors}, Unklar: {stats.Uncertain}");
            actions.SetDetailsText(string.Join("\n", stats.Messages.Take(80)));

            if (request.DryRun)
            {
                var preview = ImportPreviewResult.FromLog(runLog);
                var doImport = actions.ShowPreview(preview, request.Label);
                if (doImport)
                {
                    followUpRunStarted = true;
                    await RunCoreAsync(
                        request with { DryRun = false },
                        actions,
                        cancellationToken,
                        projectSnapshot,
                        initialReportDirectory);
                }

                return;
            }

            if (request.PostImportAsync is not null)
            {
                try
                {
                    await request.PostImportAsync(request.Source, targetProject, ctx);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    postImportIncomplete = true;
                    var detail = $"Nacharbeiten unvollstaendig: {ex.Message}";
                    runLog.AddEntry(
                        request.Label,
                        "PostImport",
                        ImportLogStatus.Error,
                        detail: detail);
                    actions.SetSummaryText(actions.GetSummaryText()
                        + "\n  Hinweis: Nacharbeiten unvollstaendig - Importbericht pruefen.");
                    actions.SetDetailsText(AppendParagraph(actions.GetDetailsText(), detail));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureProjectIsStillCurrent(request.Label, actions, runLog, projectSnapshot))
                return;

            // Erst jetzt werden vorbereitete Kopien an ihren endgueltigen Orten sichtbar.
            // Bis zur Live-Projektuebernahme kann Dispose sie noch sicher zuruecknehmen.
            // Marker vor der Veroeffentlichung (Transaktion laeuft), danach mit den tatsaechlich
            // veroeffentlichten Zielen (fuer das Recovery-Rollback).
            fileTransaction.Publish();

            actions.DeduplicateAllPrimaryDamages(targetProject);
            await actions.RunAfterImportAsync(targetProject, request.Label);
            cancellationToken.ThrowIfCancellationRequested();

            if (!EnsureProjectIsStillCurrent(request.Label, actions, runLog, projectSnapshot))
                return;

            var plausibilityWarnings = actions.ValidatePlausibility(targetProject);
            if (plausibilityWarnings.Count > 0)
            {
                actions.SetSummaryText(actions.GetSummaryText()
                    + $"\n  Plausibilitaet: {plausibilityWarnings.Count} Warnung(en) - bitte pruefen.");
                actions.SetDetailsText(actions.GetDetailsText()
                    + "\n\n--- Plausibilitaets-Warnungen ---\n"
                    + string.Join("\n", plausibilityWarnings.Take(80)));
                foreach (var warning in plausibilityWarnings.Take(200))
                {
                    runLog.AddEntry(
                        request.Label,
                        "Plausibilitaet",
                        ImportLogStatus.Info,
                        detail: warning);
                }
            }

            // Finaler Check unmittelbar vor der Uebernahme: hier zusaetzlich die Inhalts-Signatur
            // pruefen (U4 — Live-Edit waehrend des Imports).
            if (!EnsureProjectIsStillCurrent(request.Label, actions, runLog, projectSnapshot, checkContentSignature: true))
                return;

            cancellationToken.ThrowIfCancellationRequested();
            actions.SetCanCancel(false);

            targetProject.Dirty = true;
            // Commit-Beweis im projekt.json: gleicht die Marker-TxId, sobald der atomare Save
            // durchgelaufen ist. Das Recovery unterscheidet daran „committed" von „abgebrochen".
            fileTransaction.StampProject(targetProject);
            actions.ReplaceProject(targetProject);
            projectCommitted = true;
            fileTransaction.MarkProjectCommitted();

            if (request.SaveProjectAfterCommit)
            {
                if (!TrySaveCommittedProject(request.Label, actions, runLog))
                {
                    ReportCommittedButNotSaved(request.Label, actions);
                    return;
                }

                projectSaved = true;
                fileTransaction.MarkProjectSaved();
            }

            actions.SetStatus(postImportIncomplete
                ? $"{request.Label} importiert mit Hinweisen"
                : $"{request.Label} importiert");
            actions.SetProgressPercent(100);
        }
        catch (OperationCanceledException)
        {
            runLog.WasCancelled = true;
            actions.SetSummaryText(
                $"{request.Label} Import abgebrochen - Projektdaten wurden nicht uebernommen.");
            actions.SetStatus(
                $"{request.Label} Import abgebrochen - Projektdaten wurden nicht uebernommen");
        }
        catch (Exception ex)
        {
            actions.SetSummaryText(projectCommitted
                ? actions.GetSummaryText()
                    + "\n  Hinweis: Import wurde uebernommen, aber der Abschluss ist fehlgeschlagen."
                : $"{request.Label} Import fehlgeschlagen - Projektdaten wurden nicht uebernommen: {ex.Message}");
            actions.SetDetailsText(ex.ToString());
            actions.SetStatus(projectCommitted
                ? $"{request.Label} importiert mit Abschlussfehler"
                : $"{request.Label} Import fehlgeschlagen - Projektdaten wurden nicht uebernommen");
        }
        finally
        {
            var cleanup = fileTransaction?.Cleanup()
                          ?? new ImportFileTransactionCleanupResult(true, null);
            if (!cleanup.StagingCleanupSucceeded && cleanup.StagingCleanupError is { } ex)
            {
                var detail = projectCommitted
                    ? $"Datei-Arbeitsordner konnte nicht vollstaendig aufgeraeumt werden: {ex.Message}"
                    : $"Vorbereitete Importdateien konnten nicht vollstaendig zurueckgenommen werden: {ex.Message}";
                runLog.AddEntry(
                    request.Label,
                    "Datei-Staging",
                    ImportLogStatus.Error,
                    detail: detail);
                actions.SetDetailsText(AppendParagraph(actions.GetDetailsText(), detail));
            }

            runLog.Complete();
            actions.SetCanCancel(false);
            actions.SetIsImportInProgress(false);
            actions.SetPhase("");

            try
            {
                var reportDir = projectSaved
                    ? actions.GetReportDir()
                    : initialReportDirectory;
                if (reportDir is not null)
                {
                    var reportPath = actions.ExportReport(runLog, reportDir);
                    if (!followUpRunStarted)
                        actions.SetLastReportPath(reportPath);
                }
            }
            catch
            {
                // Report-Fehler duerfen den Import nicht nachtraeglich brechen.
            }
        }
    }

    private static bool TrySaveCommittedProject(
        string label,
        ImportRunWorkflowActions actions,
        ImportRunLog runLog)
    {
        try
        {
            if (actions.SaveProject())
                return true;

            runLog.AddEntry(
                label,
                "Speichern",
                ImportLogStatus.Error,
                detail: "Import wurde uebernommen, konnte aber nicht gespeichert werden.");
        }
        catch (Exception ex)
        {
            runLog.AddEntry(
                label,
                "Speichern",
                ImportLogStatus.Error,
                detail: $"Import wurde uebernommen, Speichern schlug fehl: {ex.Message}");
        }

        return false;
    }

    private static void ReportCommittedButNotSaved(
        string label,
        ImportRunWorkflowActions actions)
    {
        actions.SetSummaryText(actions.GetSummaryText()
            + "\n  Hinweis: Import wurde uebernommen, aber nicht gespeichert.");
        actions.SetStatus($"{label} importiert, aber nicht gespeichert");
        actions.SetProgressPercent(99);
    }

    private static bool EnsureProjectIsStillCurrent(
        string label,
        ImportRunWorkflowActions actions,
        ImportRunLog runLog,
        ActiveProjectSnapshot projectSnapshot,
        bool checkContentSignature = false)
    {
        var projectIsUnchanged = ReferenceEquals(actions.GetProject(), projectSnapshot.Project);
        var pathIsUnchanged = string.Equals(
            NormalizeProjectPath(actions.GetProjectPath()),
            projectSnapshot.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
        if (projectIsUnchanged && pathIsUnchanged)
        {
            // U4: Wurde dasselbe Projekt waehrend des Imports inhaltlich bearbeitet, darf das
            // Importergebnis die manuellen Aenderungen nicht still ueberschreiben. Nur beim
            // finalen Check vor der Uebernahme geprueft (die Signaturberechnung ist nicht gratis).
            if (checkContentSignature && actions.ComputeSignature is { } compute)
            {
                var currentSignature = compute(actions.GetProject());
                if (!string.Equals(currentSignature, projectSnapshot.StartSignature, StringComparison.Ordinal))
                {
                    const string editDetail =
                        "Waehrend des Imports wurde das Projekt bearbeitet. Das Importergebnis wurde " +
                        "nicht uebernommen, damit die manuellen Aenderungen erhalten bleiben — " +
                        "bitte erneut importieren.";
                    runLog.AddEntry(label, "Projektinhalt", ImportLogStatus.Error, detail: editDetail);
                    actions.SetSummaryText(
                        $"{label} Import gestoppt: Projekt wurde waehrend des Imports bearbeitet. " +
                        "Das Importergebnis wurde nicht uebernommen.");
                    actions.SetDetailsText(AppendParagraph(actions.GetDetailsText(), editDetail));
                    actions.SetStatus($"{label} Import gestoppt - Projekt wurde bearbeitet");
                    return false;
                }
            }
            return true;
        }

        const string detail =
            "Waehrend des Imports wurde das aktive Projekt oder sein Speicherpfad gewechselt. " +
            "Das Importergebnis wurde aus Sicherheitsgruenden nicht uebernommen.";
        runLog.AddEntry(
            label,
            "Projektidentitaet",
            ImportLogStatus.Error,
            detail: detail);
        actions.SetSummaryText(
            $"{label} Import gestoppt: Projekt wurde gewechselt. " +
            "Das Importergebnis wurde nicht uebernommen.");
        actions.SetDetailsText(AppendParagraph(actions.GetDetailsText(), detail));
        actions.SetStatus($"{label} Import gestoppt - Projekt wurde gewechselt");
        return false;
    }

    private static string AppendParagraph(string currentText, string text)
        => string.IsNullOrWhiteSpace(currentText)
            ? text
            : currentText + "\n\n" + text;

    private static string? NormalizeProjectPath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        var trimmed = projectPath.Trim();
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    private static Result<ImportStats> RunImport<TSource>(
        ImportRunWorkflowRequest<TSource> request,
        Project targetProject,
        ImportRunContext ctx)
    {
        try
        {
            return request.Import(request.Source, targetProject, ctx);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<ImportStats>.Fail($"{request.Label}_EXCEPTION", ex.Message);
        }
    }

    private static string? ResolveSourcePath<TSource>(TSource source)
    {
        if (source is string path)
            return path;
        if (source is string[] paths && paths.Length > 0)
            return paths[0];
        return null;
    }

    private sealed record ActiveProjectSnapshot(Project Project, string? ProjectPath, string StartSignature);
}
