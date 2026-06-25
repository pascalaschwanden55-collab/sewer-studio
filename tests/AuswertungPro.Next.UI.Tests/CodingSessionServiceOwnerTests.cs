using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionServiceOwnerTests
{
    [Fact]
    public void Owner_stores_and_clears_session_service()
    {
        var owner = CreateOwner();
        var service = new RecordingCodingSessionService();

        Assert.False(Get<bool>(owner, "HasService"));
        Assert.Null(Get<ICodingSessionService?>(owner, "Service"));

        Invoke(owner, "Set", service);

        Assert.True(Get<bool>(owner, "HasService"));
        Assert.Same(service, Get<ICodingSessionService?>(owner, "Service"));

        Invoke(owner, "Clear");

        Assert.False(Get<bool>(owner, "HasService"));
        Assert.Null(Get<ICodingSessionService?>(owner, "Service"));
    }

    private static object CreateOwner()
    {
        var ownerType = typeof(AuswertungPro.Next.UI.Player.CodingSessionHost).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingSessionServiceOwner");
        Assert.NotNull(ownerType);

        var constructor = ownerType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(constructor);

        return constructor.Invoke([]);
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(target)!;

    private static void Invoke(object target, string methodName, params object?[] args)
        => target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(target, args);

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => [];

        public event EventHandler<CodingSessionState>? StateChanged;
        public event EventHandler<double>? MeterChanged;
        public event EventHandler<CodingEvent>? EventAdded;

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => new();
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry, Overlay = overlay };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }
        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default) => Task.CompletedTask;

        public void RaiseState(CodingSessionState state) => StateChanged?.Invoke(this, state);
        public void RaiseMeter(double meter) => MeterChanged?.Invoke(this, meter);
        public void RaiseEvent(CodingEvent ev) => EventAdded?.Invoke(this, ev);
    }
}
