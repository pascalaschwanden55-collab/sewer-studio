using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterScanWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, ICollection<string>, CancellationToken, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action<IReadOnlyList<TrainingCase>> AppendCases,
    Action<string> SetStatusText,
    Func<Task> SaveStateAsync,
    Func<CancellationToken> ResetCancellation,
    Action<string> Log);

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
            var uebersprungeneOrdner = new List<string>();

            var allFound = new List<TrainingCase>();
            foreach (var folder in request.RootFolders)
            {
                if (!request.DirectoryExists(folder))
                    continue;

                var found = await request.ScanFolderAsync(folder, uebersprungeneOrdner, cancellationToken);
                allFound.AddRange(found);
                request.AppendCases(found);
            }

            var withProtocol = allFound.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var pdfOnly = allFound.Count(c =>
                string.IsNullOrEmpty(c.VideoPath) && !string.IsNullOrEmpty(c.ProtocolPath));
            var summary = TrainingCenterDisplayFormatter.FormatScanSummary(
                allFound.Count,
                withProtocol,
                pdfOnly);

            // Deepscan R8: nicht lesbare Ordner fehlen nicht still, sondern stehen im Protokoll.
            var uebersprungen = UebersprungeneOrdner.Meldungen(uebersprungeneOrdner);
            foreach (var meldung in uebersprungen)
                request.Log(meldung);
            request.SetStatusText(uebersprungen.Count == 0
                ? summary
                : $"{summary} · {uebersprungen.Count} Ordner übersprungen (siehe Protokoll)");

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
