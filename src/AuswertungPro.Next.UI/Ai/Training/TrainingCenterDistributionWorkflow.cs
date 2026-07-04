using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterDistributionWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<string?> SelectPdfPath,
    Func<string?> SelectVideoFolder,
    Func<string, string, string, Task<TrainingCenterImportService.DistributeResult>> DistributeAsync,
    IList<string> RootFolders,
    Action UpdateRootFolderDisplay,
    Action<string> SetLogText,
    Action<string> SetStatusText,
    Action<string> Log);

public static class TrainingCenterDistributionWorkflow
{
    public static async Task RunAsync(TrainingCenterDistributionWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        var pdfPath = request.SelectPdfPath();
        if (pdfPath is null)
            return;

        var videoFolder = request.SelectVideoFolder();
        if (videoFolder is null)
            return;

        var outputFolder = BuildOutputFolder(pdfPath, videoFolder);

        try
        {
            request.SetIsBusy(true);
            request.SetLogText("");
            request.SetStatusText("PDF nach Haltungen aufteilen...");
            request.Log($"PDF: {pdfPath}");
            request.Log($"Videos: {videoFolder}");
            request.Log($"Output: {outputFolder}");

            var result = await request.DistributeAsync(pdfPath, videoFolder, outputFolder);

            foreach (var msg in result.Messages)
                request.Log($"  {msg}");

            request.Log($"--- Fertig: {result.Distributed} Haltungen verteilt, {result.VideosMatched} Videos zugeordnet ---");

            if (result.Uncertain > 0)
                request.Log($"  {result.Uncertain} Chunks ohne Haltungs-ID uebersprungen.");

            request.SetStatusText($"Verteilt: {result.Distributed} Haltungen, {result.VideosMatched} Videos -> {result.OutputFolder}");

            if (result.Distributed > 0)
            {
                TrainingCenterStateController.AddRootFolder(request.RootFolders, result.OutputFolder);
                request.UpdateRootFolderDisplay();
                request.Log("Output-Ordner als Trainings-Ordner hinzugefuegt. Klicke 'Scannen' zum Laden.");
            }
        }
        catch (Exception ex)
        {
            request.Log($"Fehler: {ex.Message}");
            request.SetStatusText($"Fehler bei Verteilung: {ex.Message}");
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }

    public static string BuildOutputFolder(string pdfPath, string videoFolder)
    {
        var pdfDir = Path.GetDirectoryName(pdfPath) ?? videoFolder;
        var projectName = Path.GetFileNameWithoutExtension(pdfPath);
        return Path.Combine(Path.GetDirectoryName(pdfDir) ?? pdfDir, $"{projectName}_Training");
    }
}
