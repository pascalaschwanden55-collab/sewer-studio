using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationControllerTests
{
    [Fact]
    public void PauseAndAsk_preserves_pause_pending_panel_and_status_order()
    {
        var calls = new List<string>();
        var pendingState = new CodingPendingConfirmationStateController();
        var codingEvent = new CodingEvent { Entry = new ProtocolEntry { Code = "BCA" } };
        var gate = Gate();
        var controller = new CodingConfirmationController(
            pendingState,
            Bindings(
                calls,
                applyPanel: (actualEvent, actualGate) =>
                {
                    Assert.Same(codingEvent, actualEvent);
                    Assert.Same(gate, actualGate);
                    calls.Add("panel");
                    return Color.FromRgb(1, 2, 3);
                }));

        controller.PauseAndAsk(codingEvent, gate);

        Assert.True(pendingState.HasPendingConfirmation);
        Assert.Same(codingEvent, pendingState.CodingEvent);
        Assert.Same(gate, pendingState.GateResult);
        Assert.Equal(
            ["pause:True", "panel", "status:KI prueft:1:QualityGate: Gelb"],
            calls);
    }

    [Fact]
    public async Task Decision_methods_delegate_to_the_existing_decision_controller()
    {
        var calls = new List<string>();
        var controller = new CodingConfirmationController(
            new CodingPendingConfirmationStateController(),
            Bindings(calls));

        var accepted = await controller.Accept();
        var edited = controller.Edit();
        var rejected = await controller.Reject();

        Assert.True(accepted.Applied);
        Assert.True(edited.Selected);
        Assert.True(rejected.Applied);
        Assert.Equal(["accept", "edit", "reject"], calls);
    }

    private static CodingConfirmationControllerBindings Bindings(
        List<string> calls,
        Func<CodingEvent, QualityGateResult, Color>? applyPanel = null)
        => new(
            ResolveCurrentStatusText: () => "KI prueft",
            ResolveCodingSessionService: () => null,
            SetPause: paused => calls.Add($"pause:{paused}"),
            ApplyConfirmationPanel: applyPanel ?? ((_, _) => Colors.Transparent),
            ShowStatus: (status, color, detail) =>
                calls.Add($"status:{status}:{color.R}:{detail}"),
            Accept: () =>
            {
                calls.Add("accept");
                return Task.FromResult(new CodingConfirmationDecisionCommandResult(
                    CodingConfirmationDecisionCommandOutcome.Applied));
            },
            Edit: () =>
            {
                calls.Add("edit");
                return new CodingConfirmationEditCommandWorkflowResult(
                    CodingConfirmationEditCommandWorkflowOutcome.Selected);
            },
            Reject: () =>
            {
                calls.Add("reject");
                return Task.FromResult(new CodingConfirmationDecisionCommandResult(
                    CodingConfirmationDecisionCommandOutcome.Applied));
            },
            RetrySave: () =>
            {
                calls.Add("retry");
                return Task.FromResult(new CodingConfirmationDecisionCommandResult(
                    CodingConfirmationDecisionCommandOutcome.Applied));
            });

    private static QualityGateResult Gate()
        => new(
            0.7,
            TrafficLight.Yellow,
            new Dictionary<string, double>(),
            "test");
}
