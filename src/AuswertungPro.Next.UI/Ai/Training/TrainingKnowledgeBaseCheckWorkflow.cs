using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseCheckWorkflowRequest(
    bool IsBusy,
    Action<bool> SetBusy,
    Action<string> SetStatus,
    Func<int, Task<KnowledgeBaseDiagnosticsSummary>> ReadSummaryAsync,
    Func<Task> RefreshKbStatusAsync,
    Action<string> Log,
    CancellationToken CancellationToken);

public static class TrainingKnowledgeBaseCheckWorkflow
{
    public static async Task RunAsync(TrainingKnowledgeBaseCheckWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var start = TrainingKnowledgeBaseCheckRunController.TryStart(request.IsBusy);
        if (start.ShouldStop)
            return;

        try
        {
            request.SetBusy(start.IsBusy);
            request.SetStatus(start.StatusText ?? "");

            var summary = await request.ReadSummaryAsync(12).ConfigureAwait(false);

            var presentation = TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary);
            TrainingKnowledgeBaseCheckRunController.ApplySuccess(
                presentation,
                request.Log,
                request.SetStatus);

            await request.RefreshKbStatusAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TrainingKnowledgeBaseCheckRunController.ApplyFailure(
                ex,
                request.Log,
                request.SetStatus);
        }
        finally
        {
            request.SetBusy(false);
        }
    }
}
