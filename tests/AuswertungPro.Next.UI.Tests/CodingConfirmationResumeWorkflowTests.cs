using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationResumeWorkflowTests
{
    [Fact]
    public void Apply_resumes_waiting_session_unpauses_live_ai_and_returns_active_status()
    {
        var service = new RecordingCodingSessionService(new CodingSession { State = CodingSessionState.WaitingForUserInput });
        var pauseCalls = new List<bool>();
        var method = FindApplyMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [
            service,
            true,
            "models/qwen3-vl:2b",
            new Action<bool>(pauseCalls.Add)
        ]);

        Assert.Equal(1, service.ResumeCalls);
        Assert.Equal([false], pauseCalls);
        AssertResult(result, expectedResumed: true, expectedLiveAiEnabled: true);
        AssertStatus(result, "Automatische KI-Analyse aktiv", "qwen3-vl:2b");
    }

    [Fact]
    public void Apply_leaves_nonwaiting_session_paused_and_returns_ready_status_when_live_ai_is_off()
    {
        var service = new RecordingCodingSessionService(new CodingSession { State = CodingSessionState.Running });
        var pauseCalls = new List<bool>();
        var method = FindApplyMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [
            service,
            false,
            "qwen3-vl",
            new Action<bool>(pauseCalls.Add)
        ]);

        Assert.Equal(0, service.ResumeCalls);
        Assert.Empty(pauseCalls);
        AssertResult(result, expectedResumed: false, expectedLiveAiEnabled: false);
        AssertStatus(result, "K\u00fcnstliche Intelligenz bereit", "qwen3-vl");
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(CodingConfirmationDecisionWorkflow).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingConfirmationResumeWorkflow")
            ?.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(ICodingSessionService), typeof(bool), typeof(string), typeof(Action<bool>)],
                modifiers: null);

    private static void AssertResult(object? result, bool expectedResumed, bool expectedLiveAiEnabled)
    {
        Assert.NotNull(result);
        var type = result.GetType();
        Assert.Equal(expectedResumed, type.GetProperty("ResumedSession")?.GetValue(result));
        Assert.Equal(expectedLiveAiEnabled, type.GetProperty("IsLiveAiEnabled")?.GetValue(result));
    }

    private static void AssertStatus(object? result, string expectedStatusText, string expectedDetailSnippet)
    {
        Assert.NotNull(result);
        var status = result.GetType().GetProperty("Status")?.GetValue(result);
        Assert.NotNull(status);
        var statusType = status.GetType();

        Assert.Equal(expectedStatusText, statusType.GetProperty("StatusText")?.GetValue(status));
        Assert.Contains(expectedDetailSnippet, (string?)statusType.GetProperty("DetailText")?.GetValue(status));
    }

    private sealed class RecordingCodingSessionService(CodingSession? activeSession) : ICodingSessionService
    {
        public int ResumeCalls { get; private set; }
        public CodingSession? ActiveSession { get; } = activeSession;
        public IReadOnlyList<CodingEvent> Events => ActiveSession?.Events is { } events
            ? events
            : Array.Empty<CodingEvent>();
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotSupportedException();
        public void PauseSession() => throw new NotSupportedException();
        public void ResumeSession()
        {
            ResumeCalls++;
            if (ActiveSession != null)
                ActiveSession.State = CodingSessionState.Running;
        }
        public void SetWaitingForInput() => throw new NotSupportedException();
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
