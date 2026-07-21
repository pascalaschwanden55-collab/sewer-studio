using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace SewerStudio.Tools.StageAExporter;

public interface IStageAExporterRunner
{
    Task<TrainingYoloExportResult> RunAsync(
        StageAExporterCliOptions options,
        CancellationToken cancellationToken);
}

public static class StageAExporterApp
{
    public static Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
        => RunAsync(
            args,
            new StageAExporterRunner(TimeProvider.System),
            output,
            error,
            cancellationToken);

    public static async Task<int> RunAsync(
        string[] args,
        IStageAExporterRunner runner,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var options = StageAExporterCliOptions.Parse(args);
            if (options.ShowHelp)
            {
                StageAExporterCliOptions.PrintHelp(output);
                return 0;
            }

            if (options.WorkersSpecified)
            {
                await error.WriteLineAsync(
                    "HINWEIS: --workers ist nur noch ein Kompatibilitaetsargument; " +
                    "der zentrale Export steuert die Verarbeitung selbst.");
            }

            var result = await runner.RunAsync(options, cancellationToken).ConfigureAwait(false);
            WriteResult(output, options, result);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("ABGEBROCHEN: Der Export wurde durch den Benutzer beendet.");
            return 130;
        }
        catch (ArgumentException ex)
        {
            await error.WriteLineAsync($"CLI-FEHLER: {ex.Message}");
            return 2;
        }
        catch (Exception ex) when (ex is TrainingExportPlanException or TrainingYoloClassMapException)
        {
            await error.WriteLineAsync($"EXPORT-GESPERRT: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"EXPORT-FEHLER: {ex.Message}");
            return 2;
        }
    }

    private static void WriteResult(
        TextWriter output,
        StageAExporterCliOptions options,
        TrainingYoloExportResult result)
    {
        var status = result.Status switch
        {
            TrainingYoloExportResultStatus.Completed => "EXPORT_FERTIG",
            TrainingYoloExportResultStatus.Planned => "PLAN_GEPRUEFT",
            TrainingYoloExportResultStatus.NoImages => "KEINE_BILDER",
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
        output.WriteLine($"STATUS: {status}");
        output.WriteLine($"Wissensordner: {options.KnowledgeRoot}");
        output.WriteLine($"Plan-ID: {result.Plan.PlanId}");
        output.WriteLine($"Bilder: {result.Plan.Images.Count}");
        output.WriteLine($"Train: {result.Plan.Images.Count(image => image.Target == TrainingExportTarget.Train)}");
        output.WriteLine($"Val: {result.Plan.Images.Count(image => image.Target == TrainingExportTarget.Validation)}");
        output.WriteLine($"Feste Klassen: {result.Plan.Classes.Count}");
        output.WriteLine($"Ausgeschlossen: {result.Plan.Exclusions.Count}");
        foreach (var group in result.Plan.Exclusions
                     .GroupBy(item => item.Reason)
                     .OrderBy(item => item.Key))
        {
            output.WriteLine($"  {group.Key}: {group.Count()}");
        }

        if (result.Execution is null)
        {
            output.WriteLine("Es wurden keine Datensatzdateien geschrieben.");
            return;
        }

        output.WriteLine($"Weg: {result.Execution.Route}");
        output.WriteLine($"Datensatz: {result.Execution.Result.DatasetPath}");
        output.WriteLine($"data.yaml: {result.Execution.Result.DataYamlPath}");
        output.WriteLine($"Manifest: {result.Execution.Result.ManifestPath}");
    }
}
