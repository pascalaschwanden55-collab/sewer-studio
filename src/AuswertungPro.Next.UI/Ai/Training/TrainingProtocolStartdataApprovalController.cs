using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingProtocolStartdataApprovalResult(
    int ApprovedCount,
    int ItemCount,
    string StatusText,
    IReadOnlyList<string> ErrorLogTexts);

public static class TrainingProtocolStartdataApprovalController
{
    public static async Task<TrainingProtocolStartdataApprovalResult> ApproveAllAsync(
        IReadOnlyList<InfraSelfImproving.ReviewQueueItem> items,
        Func<InfraSelfImproving.ReviewQueueItem, CancellationToken, Task> approveAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(approveAsync);

        var approved = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            try
            {
                await approveAsync(item, ct).ConfigureAwait(false);
                approved++;
            }
            catch (Exception ex)
            {
                errors.Add($"Startdaten-Freigabe Fehler ({item.SelfTrainingVsaCode}): {ex.Message}");
            }
        }

        return new TrainingProtocolStartdataApprovalResult(
            approved,
            items.Count,
            $"{approved}/{items.Count} Protokoll-Startdaten freigegeben.",
            errors);
    }
}
