namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterScanWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, CancellationToken, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action<IReadOnlyList<TrainingCase>> AppendCases,
    Action<string> SetStatusText,
    Func<Task> SaveStateAsync,
    Func<CancellationToken> ResetCancellation);

public static class TrainingCenterScanWorkflow
{
    public static async Task RunAsync(TrainingCenterScanWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        if (request.RootFolders.Count == 0)
        {
            request.SetStatusText("Bitte zuerst einen oder mehrere Ordner wählen.");
            return;
        }

        try
        {
            request.SetIsBusy(true);
            request.SetStatusText("Scanne Ordner...");
            request.ReplaceCases(Array.Empty<TrainingCase>());
            // Deepscan R6: «Abbrechen» wirkt auch auf den Scan; der Dienst prueft je Ordner.
            var cancellationToken = request.ResetCancellation();

            var allFound = new List<TrainingCase>();
            foreach (var folder in request.RootFolders)
            {
                if (!request.DirectoryExists(folder))
                    continue;

                var found = await request.ScanFolderAsync(folder, cancellationToken);
                allFound.AddRange(found);
                request.AppendCases(found);
            }

            var withProtocol = allFound.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var pdfOnly = allFound.Count(c =>
                string.IsNullOrEmpty(c.VideoPath) && !string.IsNullOrEmpty(c.ProtocolPath));
            request.SetStatusText(TrainingCenterDisplayFormatter.FormatScanSummary(
                allFound.Count,
                withProtocol,
                pdfOnly));

            await request.SaveStateAsync();
        }
        catch (OperationCanceledException)
        {
            // Bereits gefundene Faelle bleiben sichtbar; gespeichert wird ein abgebrochener Scan nicht.
            request.SetStatusText("Scan abgebrochen.");
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }
}
