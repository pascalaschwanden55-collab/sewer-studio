using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseCheckRequestFactoryRequest(
    bool IsBusy,
    Action<bool> SetBusy,
    Action<string> SetStatus,
    Func<int, Task<KnowledgeBaseDiagnosticsSummary>> ReadSummaryAsync,
    Func<Task> RefreshKbStatusAsync,
    Action<string> Log,
    CancellationToken CancellationToken);

public static class TrainingKnowledgeBaseCheckRequestFactory
{
    public static TrainingKnowledgeBaseCheckWorkflowRequest Create(
        TrainingKnowledgeBaseCheckRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SetBusy);
        ArgumentNullException.ThrowIfNull(request.SetStatus);
        ArgumentNullException.ThrowIfNull(request.ReadSummaryAsync);
        ArgumentNullException.ThrowIfNull(request.RefreshKbStatusAsync);
        ArgumentNullException.ThrowIfNull(request.Log);

        return new TrainingKnowledgeBaseCheckWorkflowRequest(
            request.IsBusy,
            request.SetBusy,
            request.SetStatus,
            request.ReadSummaryAsync,
            request.RefreshKbStatusAsync,
            request.Log,
            request.CancellationToken);
    }
}
