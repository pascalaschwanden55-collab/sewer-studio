namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingEingabemarkerSubmissionWorkflowOutcome
{
    EmptyKeyword,
    Duplicate,
    DirectEventAdded,
    AiFallbackStarted,
    PersistenceFailed,
    Error
}

public sealed record CodingEingabemarkerSubmissionWorkflowRequest(
    string? RawKeyword,
    bool HasCodingViewModel,
    bool HasCodingSessionService);

public sealed record CodingEingabemarkerDuplicateMatch(
    double MeterAtCapture);

public sealed record CodingEingabemarkerSubmissionWorkflowActions(
    Action HideInput,
    Action SetAnalyzingPhase,
    Func<string, string?> ResolveCodeHint,
    Func<string, CodingEingabemarkerDuplicateMatch?> FindDuplicate,
    Action<string, double> ShowDuplicateStatus,
    Action<string, string> AddDirectEvent,
    Action<string> ShowAiFallbackStatus,
    Func<string, Task> RunAiFallbackAsync,
    Action<string> ShowErrorStatus,
    Action CancelMarker,
    Func<string, string, Task<CodingTrainingSamplePersistenceResult>>? AddDirectEventAsync = null);

public sealed record CodingEingabemarkerSubmissionWorkflowResult(
    CodingEingabemarkerSubmissionWorkflowOutcome Outcome);

public static class CodingEingabemarkerSubmissionWorkflow
{
    public static async Task<CodingEingabemarkerSubmissionWorkflowResult> ExecuteAsync(
        CodingEingabemarkerSubmissionWorkflowRequest request,
        CodingEingabemarkerSubmissionWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var keyword = request.RawKeyword?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
            return Result(CodingEingabemarkerSubmissionWorkflowOutcome.EmptyKeyword);

        actions.HideInput();
        actions.SetAnalyzingPhase();

        try
        {
            var codeHint = actions.ResolveCodeHint(keyword);
            if (request.HasCodingViewModel && codeHint != null)
            {
                var duplicate = actions.FindDuplicate(codeHint);
                if (duplicate != null)
                {
                    actions.ShowDuplicateStatus(codeHint, duplicate.MeterAtCapture);
                    return Result(CodingEingabemarkerSubmissionWorkflowOutcome.Duplicate);
                }
            }

            if (codeHint != null && request.HasCodingViewModel && request.HasCodingSessionService)
            {
                if (actions.AddDirectEventAsync is not null)
                {
                    var persistence = await actions.AddDirectEventAsync(codeHint, keyword);
                    if (!persistence.Success)
                    {
                        actions.ShowErrorStatus(
                            persistence.Error ?? "Training konnte nicht gespeichert werden.");
                        return Result(CodingEingabemarkerSubmissionWorkflowOutcome.PersistenceFailed);
                    }
                }
                else
                {
                    actions.AddDirectEvent(codeHint, keyword);
                }
                return Result(CodingEingabemarkerSubmissionWorkflowOutcome.DirectEventAdded);
            }

            actions.ShowAiFallbackStatus(keyword);
            await actions.RunAiFallbackAsync(keyword);
            return Result(CodingEingabemarkerSubmissionWorkflowOutcome.AiFallbackStarted);
        }
        catch (Exception ex)
        {
            actions.ShowErrorStatus(ex.Message);
            return Result(CodingEingabemarkerSubmissionWorkflowOutcome.Error);
        }
        finally
        {
            actions.CancelMarker();
        }
    }

    private static CodingEingabemarkerSubmissionWorkflowResult Result(
        CodingEingabemarkerSubmissionWorkflowOutcome outcome)
        => new(outcome);
}
