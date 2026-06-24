using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionHostTests
{
    [Fact]
    public void Null_view_model_exposes_empty_navigation_state()
    {
        var host = CreateHost(() => null);
        var hostType = host.GetType();

        Assert.False(Get<bool>(host, "HasViewModel"));
        Assert.Equal(0, Get<double>(host, "CurrentMeter"));
        Assert.Equal(0, Get<double>(host, "EndMeter"));
        Assert.Null(Get<object?>(host, "CurrentOverlay"));
        Assert.Null(Get<object?>(host, "EventCollection"));
        Assert.Null(Get<object?>(host, "SelectedDefect"));
        Assert.Null(Get<string?>(host, "HaltungName"));
        Assert.Null(Get<object?>(host, "VideoPath"));
        Assert.Null(Get<object?>(host, "CurrentVideoTime"));
        Assert.Equal(string.Empty, Get<string>(host, "SelectedCode"));
        Assert.Equal(string.Empty, Get<string>(host, "SelectedCodeDescription"));
        Assert.Empty(GetEvents(host));
        Assert.False(InvokeBool(host, "ExecuteMoveNext"));
        Assert.False(InvokeBool(host, "ExecuteMovePrevious"));
        Assert.False(InvokeBool(host, "ExecuteAcceptDefect"));
        Assert.False(InvokeBool(host, "ExecuteEditDefect"));

        hostType.GetMethod("SetCurrentVideoTime")!.Invoke(host, [TimeSpan.FromSeconds(7)]);
        hostType.GetMethod("SelectDefect")!.Invoke(host, [new CodingEvent()]);
        hostType.GetMethod("ClearSelectedDefect")!.Invoke(host, []);
        hostType.GetMethod("ClearCurrentOverlay")!.Invoke(host, []);
        hostType.GetMethod("ClearSelectedCode")!.Invoke(host, []);
        hostType.GetMethod("BeginOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.1, 0.2)]);
        hostType.GetMethod("UpdateOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.2, 0.3)]);
        hostType.GetMethod("CompleteOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.3, 0.4)]);
    }

    [Fact]
    public void Host_exposes_navigation_state_and_forwards_move_commands()
    {
        var sessionService = new RecordingCodingSessionService();
        var overlayService = new FakeOverlayToolService();
        using var vm = new CodingSessionViewModel(
            sessionService,
            overlayService);
        var overlay = new OverlayGeometry();
        var previewOverlay = new OverlayGeometry { ToolType = OverlayToolType.Rectangle };
        var completedOverlay = new OverlayGeometry { ToolType = OverlayToolType.Ellipse };
        var codingEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BBA" },
            MeterAtCapture = 3.25,
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BBA",
                Confidence = 0.7,
                Reason = "test"
            }
        };

        vm.CurrentMeter = 3.25;
        vm.EndMeter = 17.5;
        vm.CurrentOverlay = overlay;
        vm.SelectedCode = "BCA";
        vm.SelectedCodeDescription = "Anschluss";
        vm.SelectedDefect = codingEvent;
        vm.HaltungName = "H-42";
        vm.VideoPath = "video.mp4";
        vm.CurrentVideoTime = TimeSpan.FromSeconds(4);
        vm.Events.Add(codingEvent);

        var host = CreateHost(() => vm);

        Assert.True(Get<bool>(host, "HasViewModel"));
        Assert.Equal(3.25, Get<double>(host, "CurrentMeter"));
        Assert.Equal(17.5, Get<double>(host, "EndMeter"));
        Assert.Same(overlay, Get<object?>(host, "CurrentOverlay"));
        Assert.Same(vm.Events, Get<object?>(host, "EventCollection"));
        Assert.Same(codingEvent, Get<object?>(host, "SelectedDefect"));
        Assert.Equal("H-42", Get<string?>(host, "HaltungName"));
        Assert.Equal("video.mp4", Get<string?>(host, "VideoPath"));
        Assert.Equal(TimeSpan.FromSeconds(4), Get<TimeSpan?>(host, "CurrentVideoTime"));
        Assert.Equal("BCA", Get<string>(host, "SelectedCode"));
        Assert.Equal("Anschluss", Get<string>(host, "SelectedCodeDescription"));
        Assert.Same(codingEvent, Assert.Single(GetEvents(host)));

        host.GetType().GetMethod("SetCurrentVideoTime")!.Invoke(host, [TimeSpan.FromSeconds(9)]);
        Assert.Equal(TimeSpan.FromSeconds(9), vm.CurrentVideoTime);

        Assert.True(InvokeBool(host, "ExecuteMoveNext"));
        Assert.True(InvokeBool(host, "ExecuteMovePrevious"));
        Assert.Equal(1, sessionService.MoveNextCalls);
        Assert.Equal(1, sessionService.MovePreviousCalls);

        Assert.True(InvokeBool(host, "ExecuteAcceptDefect"));
        Assert.Equal(CodingUserDecision.Accepted, vm.SelectedDefect!.AiContext!.Decision);

        Assert.True(InvokeBool(host, "ExecuteEditDefect"));
        Assert.Equal(CodingUserDecision.AcceptedWithEdit, vm.SelectedDefect!.AiContext!.Decision);

        var replacement = new CodingEvent { Entry = new ProtocolEntry { Code = "BBC" } };
        host.GetType().GetMethod("SelectDefect")!.Invoke(host, [replacement]);
        Assert.Same(replacement, vm.SelectedDefect);

        host.GetType().GetMethod("ClearSelectedDefect")!.Invoke(host, []);
        Assert.Null(vm.SelectedDefect);

        host.GetType().GetMethod("ClearCurrentOverlay")!.Invoke(host, []);
        Assert.Null(vm.CurrentOverlay);

        host.GetType().GetMethod("ClearSelectedCode")!.Invoke(host, []);
        Assert.Equal(string.Empty, vm.SelectedCode);
        Assert.Equal(string.Empty, vm.SelectedCodeDescription);

        overlayService.ActiveTool = OverlayToolType.Rectangle;
        overlayService.PreviewGeometryToReturn = previewOverlay;
        overlayService.EndDrawResult = completedOverlay;

        host.GetType().GetMethod("BeginOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.1, 0.2)]);
        Assert.Equal(1, overlayService.BeginDrawCalls);

        host.GetType().GetMethod("UpdateOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.2, 0.3)]);
        Assert.Equal(1, overlayService.UpdateDrawCalls);
        Assert.Same(previewOverlay, vm.CurrentOverlay);

        host.GetType().GetMethod("CompleteOverlayDraw")!.Invoke(host, [new NormalizedPoint(0.3, 0.4)]);
        Assert.Equal(2, overlayService.UpdateDrawCalls);
        Assert.Equal(1, overlayService.EndDrawCalls);
        Assert.Same(completedOverlay, vm.CurrentOverlay);
    }

    private static object CreateHost(Func<CodingSessionViewModel?> resolveViewModel)
    {
        var hostType = typeof(CodingSessionViewModel).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingSessionHost");
        Assert.NotNull(hostType);

        var constructor = hostType.GetConstructor([typeof(Func<CodingSessionViewModel?>)]);
        Assert.NotNull(constructor);

        return constructor.Invoke([resolveViewModel]);
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(target)!;

    private static IEnumerable<CodingEvent> GetEvents(object target)
        => Get<IEnumerable<CodingEvent>>(target, "Events");

    private static bool InvokeBool(object target, string methodName)
        => (bool)target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(target, [])!;

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public int MoveNextCalls { get; private set; }
        public int MovePreviousCalls { get; private set; }

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
        public void MoveNext(double stepSizeM = 0.5) => MoveNextCalls++;
        public void MovePrevious(double stepSizeM = 0.5) => MovePreviousCalls++;
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
        public OverlayGeometry? PreviewGeometryToReturn { get; set; }
        public OverlayGeometry? EndDrawResult { get; set; }
        public int BeginDrawCalls { get; private set; }
        public int UpdateDrawCalls { get; private set; }
        public int EndDrawCalls { get; private set; }
        public bool IsCalibrated => Calibration?.IsCalibrated == true;
        public bool IsDrawing { get; private set; }
        public bool IsMultiPointTool => false;
        public int RequiredPointCount => 0;
        public int DrawPointCount => 0;
        public IReadOnlyList<NormalizedPoint> DrawPoints => [];
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => PreviewGeometryToReturn;

        public event EventHandler<OverlayToolType>? ToolChanged;

        public void SetCalibration(PipeCalibration calibration) => Calibration = calibration;
        public void BeginDraw(NormalizedPoint startPoint)
        {
            BeginDrawCalls++;
            IsDrawing = true;
        }

        public void UpdateDraw(NormalizedPoint currentPoint) => UpdateDrawCalls++;

        public OverlayGeometry? EndDraw()
        {
            EndDrawCalls++;
            IsDrawing = false;
            return EndDrawResult;
        }
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
