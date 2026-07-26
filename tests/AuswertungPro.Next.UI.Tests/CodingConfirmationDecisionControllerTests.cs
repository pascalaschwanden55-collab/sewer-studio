using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationDecisionControllerTests
{
    [Fact]
    public async Task Accept_persists_closes_and_resumes()
    {
        var calls = new List<string>();
        var pendingState = PendingState(out var codingEvent);
        var controller = CreateController(pendingState, [codingEvent], calls);

        var result = await controller.Accept();

        Assert.True(result.Applied);
        Assert.Equal(CodingUserDecision.Accepted, codingEvent.AiContext!.Decision);
        Assert.False(pendingState.HasPendingConfirmation);
        Assert.Equal(
            ["persist:TrainingSaveAccept", "hide", "pause:False", "status"],
            calls);
    }

    [Fact]
    public void Edit_closes_selects_event_and_resumes()
    {
        var calls = new List<string>();
        var pendingState = PendingState(out var codingEvent);
        var controller = CreateController(pendingState, [codingEvent], calls);

        var result = controller.Edit();

        Assert.True(result.Selected);
        Assert.Equal(CodingUserDecision.AcceptedWithEdit, codingEvent.AiContext!.Decision);
        Assert.False(pendingState.HasPendingConfirmation);
        Assert.Equal(["hide", "select", "pause:False", "status"], calls);
    }

    [Fact]
    public async Task Reject_persists_removes_refreshes_closes_and_resumes()
    {
        var calls = new List<string>();
        var pendingState = PendingState(out var codingEvent);
        var codingEvents = new List<CodingEvent> { codingEvent };
        var controller = CreateController(pendingState, codingEvents, calls);

        var result = await controller.Reject();

        Assert.True(result.Applied);
        Assert.Equal(CodingUserDecision.Rejected, codingEvent.AiContext!.Decision);
        Assert.Empty(codingEvents);
        Assert.False(pendingState.HasPendingConfirmation);
        Assert.Equal(
            ["refresh", "persist:TrainingSaveReject", "hide", "pause:False", "status"],
            calls);
    }

    private static CodingConfirmationDecisionController CreateController(
        CodingPendingConfirmationStateController pendingState,
        ICollection<CodingEvent> codingEvents,
        List<string> calls)
        => new(
            pendingState,
            new CodingConfirmationDecisionControllerActions(
                ResolveCodingSessionService: () => null,
                ResolveCodingEvents: () => codingEvents,
                PersistTrainingSample: (_, operation) =>
                {
                    calls.Add($"persist:{operation}");
                    return Task.FromResult(CodingTrainingSamplePersistenceResult.Ok);
                },
                RefreshCodingEvents: () => calls.Add("refresh"),
                HideConfirmationPanel: () => calls.Add("hide"),
                ShowPersistenceError: _ => calls.Add("error"),
                SelectEvent: _ => calls.Add("select"),
                IsLiveAiEnabled: () => true,
                ResolveModelName: () => "qwen",
                SetPause: paused => calls.Add($"pause:{paused}"),
                ApplyResumeStatus: _ => calls.Add("status")));

    private static CodingPendingConfirmationStateController PendingState(out CodingEvent codingEvent)
    {
        codingEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BBA" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BBA",
                Confidence = 0.8,
                Reason = "KI-Vorschlag"
            }
        };
        var state = new CodingPendingConfirmationStateController();
        state.Store(
            codingEvent,
            new QualityGateResult(
                0.8,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "test"));
        return state;
    }
}
