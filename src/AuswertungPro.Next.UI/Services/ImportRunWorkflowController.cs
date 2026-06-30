using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

public sealed record ImportRunWorkflowRequest<TSource>(
    string Label,
    TSource Source,
    Func<TSource, Project, ImportRunContext, Result<ImportStats>> Import,
    bool DryRun = false,
    Func<TSource, ImportRunContext, Task>? PostImportAsync = null,
    bool SaveProjectAfterCommit = false);

public sealed record ImportRunWorkflowActions(
    Func<Project> GetProject,
    Func<Project, Project> DeepCopyProject,
    Func<string?> GetReportDir,
    Func<ImportRunLog, string, string> ExportReport,
    Func<ImportPreviewResult, string, bool> ShowPreview,
    Func<Project, IReadOnlyList<string>> ValidatePlausibility,
    Action DeduplicateAllPrimaryDamages,
    Func<string, Task> RunAfterImportAsync,
    Action SaveProject,
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
    object? CollectionLock = null);

public static class ImportRunWorkflowController
{
    public static async Task RunAsync<TSource>(
        ImportRunWorkflowRequest<TSource> request,
        ImportRunWorkflowActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

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

        var ctx = new ImportRunContext(cancellationToken, progress, runLog, request.DryRun, actions.CollectionLock);

        try
        {
            var targetProject = request.DryRun
                ? actions.DeepCopyProject(actions.GetProject())
                : actions.GetProject();

            var result = await Task.Run(() => RunImport(request, targetProject, ctx));

            if (!result.Ok || result.Value is null)
            {
                actions.SetSummaryText($"{request.Label} Import fehlgeschlagen: {result.ErrorMessage}");
                actions.SetStatus($"{request.Label} Import fehlgeschlagen");
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
                    await RunAsync(
                        request with { DryRun = false },
                        actions,
                        cancellationToken);
                }

                return;
            }

            if (request.PostImportAsync is not null)
            {
                try
                {
                    await request.PostImportAsync(request.Source, ctx);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    runLog.AddEntry(
                        request.Label,
                        "PostImport",
                        ImportLogStatus.Error,
                        detail: $"PostImport-Fehler: {ex.Message}");
                }
            }

            actions.DeduplicateAllPrimaryDamages();
            await actions.RunAfterImportAsync(request.Label);

            var plausibilityWarnings = actions.ValidatePlausibility(actions.GetProject());
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

            if (request.SaveProjectAfterCommit)
                actions.SaveProject();

            actions.SetStatus($"{request.Label} importiert");
            actions.SetProgressPercent(100);
        }
        catch (OperationCanceledException)
        {
            runLog.WasCancelled = true;
            actions.SetSummaryText($"{request.Label} Import abgebrochen.");
            actions.SetStatus($"{request.Label} Import abgebrochen");
        }
        catch (Exception ex)
        {
            actions.SetSummaryText($"{request.Label} Import fehlgeschlagen: {ex.Message}");
            actions.SetDetailsText(ex.ToString());
            actions.SetStatus($"{request.Label} Import fehlgeschlagen");
        }
        finally
        {
            runLog.Complete();
            actions.SetCanCancel(false);
            actions.SetIsImportInProgress(false);
            actions.SetPhase("");

            var reportDir = actions.GetReportDir();
            if (reportDir is not null)
            {
                try
                {
                    actions.SetLastReportPath(actions.ExportReport(runLog, reportDir));
                }
                catch
                {
                    // Report-Fehler duerfen den Import nicht nachtraeglich brechen.
                }
            }
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
}
