using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationPauseWorkflowTests
{
    [Fact]
    public void Execute_pauses_session_stores_pending_event_shows_panel_and_status()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService(calls);
        var codingEvent = new CodingEvent { Entry = new ProtocolEntry { Code = "BCA" } };
        var gate = Gate(TrafficLight.Yellow);

        var result = CodingConfirmationPauseWorkflow.Execute(
            new CodingConfirmationPauseWorkflowRequest(
                codingEvent,
                gate,
                "KI prueft",
                service),
            new CodingConfirmationPauseWorkflowActions(
                SetPause: paused => calls.Add($"pause:{paused}"),
                StorePendingConfirmation: (pendingEvent, pendingGate) =>
                {
                    Assert.Same(codingEvent, pendingEvent);
                    Assert.Same(gate, pendingGate);
                    calls.Add("pending");
                },
                ApplyConfirmationPanel: (panelEvent, panelGate) =>
                {
                    Assert.Same(codingEvent, panelEvent);
                    Assert.Same(gate, panelGate);
                    calls.Add("panel");
                    return Color.FromRgb(1, 2, 3);
                },
                ShowStatus: (status, color, detail) =>
                    calls.Add($"status:{status}:{color.R}:{detail}")));

        Assert.Equal(
            [
                "pause:True",
                "waiting",
                "pending",
                "panel",
                "status:KI prueft:1:QualityGate: Gelb"
            ],
            calls);
        Assert.Equal(Color.FromRgb(1, 2, 3), result.AmpelColor);
        Assert.Equal("QualityGate: Gelb", result.DetailText);
    }

    private static QualityGateResult Gate(TrafficLight trafficLight)
        => new(0.7, trafficLight, new Dictionary<string, double>(), "test");

    private sealed class RecordingCodingSessionService(List<string> calls) : ICodingSessionService
    {
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => [];
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotSupportedException();
        public void PauseSession() => throw new NotSupportedException();
        public void ResumeSession() => throw new NotSupportedException();
        public void SetWaitingForInput() => calls.Add("waiting");
        public void AbortSession(string reason) => throw new NotSupportedException();
        public ProtocolDocument CompleteSession() => throw new NotSupportedException();
        public void MoveNext(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MovePrevious(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MoveToMeter(double meter) => throw new NotSupportedException();
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotSupportedException();
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotSupportedException();
        public void RemoveEvent(Guid eventId) => throw new NotSupportedException();
        public Task IndexConfirmedSampleAsync(TrainingSample sample, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
