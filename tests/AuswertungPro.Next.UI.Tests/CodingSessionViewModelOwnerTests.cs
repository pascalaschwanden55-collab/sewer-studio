using System.ComponentModel;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionViewModelOwnerTests
{
    [Fact]
    public void Owner_observes_and_detaches_property_changed_when_requested()
    {
        var propertyNames = new List<string>();
        var owner = CreateOwner((_, args) => propertyNames.Add(args.PropertyName ?? string.Empty));
        using var vm = CreateViewModel();

        Assert.False(Get<bool>(owner, "HasViewModel"));
        Assert.Null(Get<CodingSessionViewModel?>(owner, "ViewModel"));

        Invoke(owner, "Set", vm, true);

        Assert.True(Get<bool>(owner, "HasViewModel"));
        Assert.Same(vm, Get<CodingSessionViewModel?>(owner, "ViewModel"));

        vm.CurrentMeter = 4.2;
        Assert.Contains(nameof(CodingSessionViewModel.CurrentMeter), propertyNames);

        Invoke(owner, "DetachPropertyChanged");
        propertyNames.Clear();

        vm.CurrentMeter = 5.1;
        Assert.Empty(propertyNames);

        Invoke(owner, "Clear");
        Assert.False(Get<bool>(owner, "HasViewModel"));
        Assert.Null(Get<CodingSessionViewModel?>(owner, "ViewModel"));
    }

    [Fact]
    public void Owner_can_store_view_model_without_observing_property_changed()
    {
        var propertyNames = new List<string>();
        var owner = CreateOwner((_, args) => propertyNames.Add(args.PropertyName ?? string.Empty));
        using var vm = CreateViewModel();

        Invoke(owner, "Set", vm, false);

        Assert.True(Get<bool>(owner, "HasViewModel"));
        Assert.Same(vm, Get<CodingSessionViewModel?>(owner, "ViewModel"));

        vm.CurrentMeter = 2.5;
        Assert.Empty(propertyNames);

        Invoke(owner, "Clear");
        Assert.False(Get<bool>(owner, "HasViewModel"));
    }

    private static object CreateOwner(PropertyChangedEventHandler handler)
    {
        var ownerType = typeof(CodingSessionViewModel).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingSessionViewModelOwner");
        Assert.NotNull(ownerType);

        var constructor = ownerType.GetConstructor([typeof(PropertyChangedEventHandler)]);
        Assert.NotNull(constructor);

        return constructor.Invoke([handler]);
    }

    private static CodingSessionViewModel CreateViewModel()
        => new(new RecordingCodingSessionService(), new FakeOverlayToolService());

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

    private sealed class FakeOverlayToolService : IOverlayToolService
    {
        public OverlayToolType ActiveTool { get; set; }
        public LevelMode ActiveLevelMode { get; set; }
        public bool PipeBendSnapEnabled { get; set; }
        public PipeCalibration? Calibration { get; private set; }
        public bool IsCalibrated => Calibration?.IsCalibrated == true;
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int RequiredPointCount => 0;
        public int DrawPointCount => 0;
        public IReadOnlyList<NormalizedPoint> DrawPoints => [];
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => null;

        public event EventHandler<OverlayToolType>? ToolChanged;

        public void SetCalibration(PipeCalibration calibration) => Calibration = calibration;
        public void BeginDraw(NormalizedPoint startPoint) { }
        public void UpdateDraw(NormalizedPoint currentPoint) { }
        public OverlayGeometry? EndDraw() => null;
        public void CancelDraw() { }
        public bool AddDrawPoint(NormalizedPoint point) => false;
        public double PixelToMm(double normalizedPixels, double frameWidthPx) => 0;
        public double PointToClockHour(NormalizedPoint point) => 0;
        public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode) => null;
        public void ResizePipeCircle(double deltaNormalized) { }
        public void MovePipeCircle(NormalizedPoint newCenter) { }

        public void RaiseTool(OverlayToolType tool) => ToolChanged?.Invoke(this, tool);
    }
}
