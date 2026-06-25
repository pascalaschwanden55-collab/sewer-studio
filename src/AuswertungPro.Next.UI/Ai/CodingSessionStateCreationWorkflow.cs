using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingSessionStateCreationWorkflowOutcome
{
    Created
}

public sealed record CodingSessionStateCreationWorkflowActions(
    Func<CodingSessionStateComponents> CreateState,
    Action<ICodingSessionService> SetSessionService,
    Action<IOverlayToolService> SetOverlayService,
    Action CancelSchema,
    Action ClearSchemaType,
    Action<CodingSessionViewModel, bool> SetViewModel);

public sealed record CodingSessionStateCreationWorkflowResult(
    CodingSessionStateCreationWorkflowOutcome Outcome)
{
    public bool Created => Outcome == CodingSessionStateCreationWorkflowOutcome.Created;
}

public static class CodingSessionStateCreationWorkflow
{
    public static CodingSessionStateCreationWorkflowResult Execute(
        CodingSessionStateCreationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var state = actions.CreateState();
        actions.SetSessionService(state.SessionService);
        actions.SetOverlayService(state.OverlayService);
        actions.CancelSchema();
        actions.ClearSchemaType();
        actions.SetViewModel(state.ViewModel, true);

        return new CodingSessionStateCreationWorkflowResult(
            CodingSessionStateCreationWorkflowOutcome.Created);
    }
}
