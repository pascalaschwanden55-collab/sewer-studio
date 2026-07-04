using System.IO;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public enum TrainingCenterReviewCorrectionOutcome
{
    NoSelection,
    CatalogUnavailable,
    DialogCancelled,
    Applied,
    ApplyFailed
}

public sealed record TrainingCenterReviewCorrectionRequest(
    ReviewQueueItem? Item,
    IVsaCodeSelectionCatalog? Catalog);

public sealed record TrainingCenterReviewCorrectionActions(
    Func<VsaCodeExplorerViewModel, VsaCodeExplorerDialogResult> ShowCodeExplorer,
    Func<ReviewQueueItem, string, string?, Task> ApplyCorrectionAsync,
    Action<string, string> Warn);

public sealed record TrainingCenterReviewCorrectionResult(
    TrainingCenterReviewCorrectionOutcome Outcome);

public static class TrainingCenterReviewCorrectionWorkflow
{
    public static async Task<TrainingCenterReviewCorrectionResult> ExecuteAsync(
        TrainingCenterReviewCorrectionRequest request,
        TrainingCenterReviewCorrectionActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var item = request.Item;
        if (item is null)
            return Result(TrainingCenterReviewCorrectionOutcome.NoSelection);

        var catalog = request.Catalog;
        if (catalog is null)
        {
            actions.Warn("Code-Katalog nicht verfuegbar.", "Korrektur");
            return Result(TrainingCenterReviewCorrectionOutcome.CatalogUnavailable);
        }

        var entry = BuildReviewProtocolEntry(item);
        var explorerVm = new VsaCodeExplorerViewModel(entry, entry.MeterStart, entry.Zeit, catalog);
        var dialogResult = actions.ShowCodeExplorer(explorerVm);
        var selectedEntry = dialogResult.Accepted ? dialogResult.SelectedEntry : null;
        if (string.IsNullOrWhiteSpace(selectedEntry?.Code))
            return Result(TrainingCenterReviewCorrectionOutcome.DialogCancelled);

        try
        {
            await actions.ApplyCorrectionAsync(
                item,
                selectedEntry.Code,
                selectedEntry.Beschreibung).ConfigureAwait(false);
            return Result(TrainingCenterReviewCorrectionOutcome.Applied);
        }
        catch (Exception ex)
        {
            actions.Warn($"Fehler bei der Korrektur: {ex.Message}", "Korrektur");
            return Result(TrainingCenterReviewCorrectionOutcome.ApplyFailed);
        }
    }

    private static TrainingCenterReviewCorrectionResult Result(TrainingCenterReviewCorrectionOutcome outcome)
        => new(outcome);

    private static ProtocolEntry BuildReviewProtocolEntry(ReviewQueueItem item)
    {
        var code = FirstNonEmpty(
            item.SelfTrainingVsaCode,
            item.SuggestedCode,
            item.Entry?.SuggestedCode,
            item.Entry?.Detection.VsaCodeHint);
        var meterStart = item.SelfTrainingMeter ?? item.Entry?.Detection.MeterStart;
        var meterEnd = item.SelfTrainingMeter ?? item.Entry?.Detection.MeterEnd;

        var entry = new ProtocolEntry
        {
            Code = code,
            Beschreibung = item.Label,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            Source = ProtocolEntrySource.Manual
        };

        if (!string.IsNullOrWhiteSpace(code))
            entry.CodeMeta = new ProtocolEntryCodeMeta { Code = code };

        var framePath = item.SelfTrainingFramePath;
        if (!string.IsNullOrWhiteSpace(framePath)
            && File.Exists(framePath))
            entry.FotoPaths.Add(framePath);

        return entry;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
