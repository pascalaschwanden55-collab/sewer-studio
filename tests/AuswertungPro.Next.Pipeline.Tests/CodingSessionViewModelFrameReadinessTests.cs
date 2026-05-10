using System;
using System.Collections.Generic;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Tests fuer das in Slice 8a.3 Step 2a aus PlayerWindow.CodingMode.cs
// migrierte Frame-Readiness-State-Machine. Spiegelt das alte Verhalten
// 1:1. Step 2b stellt die Caller darauf um.
public class CodingSessionViewModelFrameReadinessTests
{
    // --- Helpers ---

    private static CodingSessionViewModel BuildVm()
        => new CodingSessionViewModel(
            new StubCodingSessionService(),
            new StubOverlayToolService(),
            new StubDialogService());

    private static LiveDetection FrameWithMeter(double meter)
        => new LiveDetection(0.0, Array.Empty<LiveFrameFinding>(), meter, null);

    private static LiveDetection FrameWithoutMeter()
        => new LiveDetection(0.0, Array.Empty<LiveFrameFinding>(), null, null);

    // --- Initial State ---

    [Fact]
    public void NewVm_StartsInWaitingForVideo()
    {
        var vm = BuildVm();
        Assert.Equal(FrameReadiness.WaitingForVideo, vm.FrameReadinessState);
        Assert.False(vm.IsFrameReady);
        Assert.Null(vm.LastOsdMeter);
        Assert.Null(vm.PendingWarmupResult);
    }

    // --- Path 1: WaitingForVideo + Meter → Warmup ---

    [Fact]
    public void RecordFrame_FirstFrameWithMeter_TransitionsToWarmup()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90));
        Assert.Equal(FrameReadiness.Warmup, vm.FrameReadinessState);
        Assert.False(vm.IsFrameReady);
    }

    // --- Path 2: WaitingForVideo + 3x No-Meter → Ready (kein OSD vorhanden) ---

    [Fact]
    public void RecordFrame_ThreeFramesWithoutMeter_TransitionsToReady()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithoutMeter());
        Assert.Equal(FrameReadiness.WaitingForVideo, vm.FrameReadinessState);
        vm.RecordFrame(FrameWithoutMeter());
        Assert.Equal(FrameReadiness.WaitingForVideo, vm.FrameReadinessState);
        vm.RecordFrame(FrameWithoutMeter());
        Assert.Equal(FrameReadiness.Ready, vm.FrameReadinessState);
        Assert.True(vm.IsFrameReady);
    }

    // --- Path 3: Warmup + zweiter Meter → Ready ---

    [Fact]
    public void RecordFrame_WarmupConfirmedBySecondMeter_TransitionsToReady()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90));   // → Warmup
        Assert.Equal(FrameReadiness.Warmup, vm.FrameReadinessState);
        vm.RecordFrame(FrameWithMeter(8.20));   // → Ready (2x Meter)
        Assert.Equal(FrameReadiness.Ready, vm.FrameReadinessState);
        Assert.True(vm.IsFrameReady);
    }

    // --- Path 4: Warmup + 2x No-Meter → Ready (Deadlock-Fallback) ---

    [Fact]
    public void RecordFrame_WarmupTwoFramesWithoutMeter_FallbacksToReady()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90));   // → Warmup, OsdSkipped reset auf 0
        vm.RecordFrame(FrameWithoutMeter());    // OsdSkipped=1 in Warmup
        Assert.Equal(FrameReadiness.Warmup, vm.FrameReadinessState);
        vm.RecordFrame(FrameWithoutMeter());    // OsdSkipped=2 → Ready
        Assert.Equal(FrameReadiness.Ready, vm.FrameReadinessState);
        Assert.True(vm.IsFrameReady);
    }

    // --- Idempotenz: Ready → Ready ---

    [Fact]
    public void RecordFrame_AlreadyReady_StaysReady()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90));
        vm.RecordFrame(FrameWithMeter(8.20)); // → Ready
        Assert.True(vm.IsFrameReady);

        vm.RecordFrame(FrameWithoutMeter());
        vm.RecordFrame(FrameWithoutMeter());
        vm.RecordFrame(FrameWithMeter(9.50));
        Assert.Equal(FrameReadiness.Ready, vm.FrameReadinessState);
        Assert.True(vm.IsFrameReady);
    }

    // --- Reset ---

    [Fact]
    public void ResetFrameReadiness_ClearsStateAndBuffers()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90));
        vm.RecordFrame(FrameWithMeter(8.20));
        vm.LastOsdMeter = 8.20;
        vm.PendingWarmupResult = FrameWithMeter(99.0);
        Assert.True(vm.IsFrameReady);

        vm.ResetFrameReadiness();
        Assert.Equal(FrameReadiness.WaitingForVideo, vm.FrameReadinessState);
        Assert.False(vm.IsFrameReady);
        Assert.Null(vm.LastOsdMeter);
        Assert.Null(vm.PendingWarmupResult);
    }

    // --- LastOsdMeter / PendingWarmupResult sind plain Properties ---

    [Fact]
    public void LastOsdMeter_RoundTrip()
    {
        var vm = BuildVm();
        vm.LastOsdMeter = 7.90;
        Assert.Equal(7.90, vm.LastOsdMeter);
        vm.LastOsdMeter = null;
        Assert.Null(vm.LastOsdMeter);
    }

    [Fact]
    public void PendingWarmupResult_RoundTrip()
    {
        var vm = BuildVm();
        var frame = FrameWithMeter(7.90);
        vm.PendingWarmupResult = frame;
        Assert.Same(frame, vm.PendingWarmupResult);
        vm.PendingWarmupResult = null;
        Assert.Null(vm.PendingWarmupResult);
    }

    // --- Edge-Case: Reset waehrend Warmup ---

    [Fact]
    public void ResetFrameReadiness_DuringWarmup_ResetsCounter()
    {
        var vm = BuildVm();
        vm.RecordFrame(FrameWithMeter(7.90)); // → Warmup, count=1
        vm.ResetFrameReadiness();
        // Nach Reset: erster Meter erneut → Warmup
        vm.RecordFrame(FrameWithMeter(8.10));
        Assert.Equal(FrameReadiness.Warmup, vm.FrameReadinessState);
        // Nicht direkt Ready, weil count nach Reset wieder bei 0 startet
        Assert.False(vm.IsFrameReady);
    }

    // --- Service-Stubs (minimal — die Tests dispatchen nichts ueber sie) ---

    private sealed class StubCodingSessionService : ICodingSessionService
    {
        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotImplementedException();
        public void PauseSession() => throw new NotImplementedException();
        public void ResumeSession() => throw new NotImplementedException();
        public void SetWaitingForInput() => throw new NotImplementedException();
        public void AbortSession(string reason) => throw new NotImplementedException();
        public ProtocolDocument CompleteSession(bool allowOpenStreckenschaden = false) => throw new NotImplementedException();
        public IReadOnlyList<CodingEvent> GetOpenStreckenschaeden() => throw new NotImplementedException();
        public void CloseStreckenschaden(Guid eventId, double endMeter) => throw new NotImplementedException();
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public void MoveNext(double stepSizeM = 0.5) => throw new NotImplementedException();
        public void MovePrevious(double stepSizeM = 0.5) => throw new NotImplementedException();
        public void MoveToMeter(double meter) => throw new NotImplementedException();
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotImplementedException();
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotImplementedException();
        public void RemoveEvent(Guid eventId) => throw new NotImplementedException();
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

        public event EventHandler<CodingSessionState>? StateChanged;
        public event EventHandler<double>? MeterChanged;
        public event EventHandler<CodingEvent>? EventAdded;

        public void RaiseStateChanged(CodingSessionState s) => StateChanged?.Invoke(this, s);
        public void RaiseMeterChanged(double m) => MeterChanged?.Invoke(this, m);
        public void RaiseEventAdded(CodingEvent ev) => EventAdded?.Invoke(this, ev);
    }

    private sealed class StubOverlayToolService : IOverlayToolService
    {
        public OverlayToolType ActiveTool { get; set; } = OverlayToolType.None;
        public LevelMode ActiveLevelMode { get; set; } = LevelMode.Water;
        public bool PipeBendSnapEnabled { get; set; }
        public event EventHandler<OverlayToolType>? ToolChanged;
        public void SetCalibration(PipeCalibration calibration) => throw new NotImplementedException();
        public PipeCalibration? Calibration => null;
        public bool IsCalibrated => false;
        public PipeCalibration? ApplyManualCalibration(NormalizedPoint start, NormalizedPoint end, double pixelDiameter, int nominalDiameterMm) => throw new NotImplementedException();
        public void BeginDraw(NormalizedPoint startPoint) => throw new NotImplementedException();
        public void UpdateDraw(NormalizedPoint currentPoint) => throw new NotImplementedException();
        public OverlayGeometry? EndDraw() => throw new NotImplementedException();
        public void CancelDraw() => throw new NotImplementedException();
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int RequiredPointCount => 0;
        public int DrawPointCount => 0;
        public bool AddDrawPoint(NormalizedPoint point) => throw new NotImplementedException();
        public IReadOnlyList<NormalizedPoint> DrawPoints => Array.Empty<NormalizedPoint>();
        public double PixelToMm(double normalizedPixels, double frameWidthPx) => 0;
        public double PointToClockHour(NormalizedPoint point) => 0;
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => null;
        public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode) => null;
        public void ResizePipeCircle(double deltaNormalized) => throw new NotImplementedException();
        public void MovePipeCircle(NormalizedPoint newCenter) => throw new NotImplementedException();
        public void RaiseToolChanged(OverlayToolType t) => ToolChanged?.Invoke(this, t);
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public bool? ShowDialog(Window window) => null;
        public bool? ShowDialog(Func<Window> windowFactory) => null;
        public void Show(Window window) { }
        public MessageBoxResult ShowMessage(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.None)
            => MessageBoxResult.None;
    }
}
