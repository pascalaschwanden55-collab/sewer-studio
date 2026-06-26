using System.ComponentModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionRuntimeFactoryTests
{
    [Fact]
    public void Create_builds_owner_backed_session_and_overlay_hosts()
    {
        var propertyNames = new List<string>();
        IOverlayToolService? overlayService = null;

        var runtime = CodingSessionRuntimeFactory.Create(
            OnPropertyChanged,
            () => overlayService);

        Assert.False(runtime.ViewModelOwner.HasViewModel);
        Assert.False(runtime.SessionHost.HasViewModel);
        Assert.False(runtime.OverlayToolHost.HasOverlayService);

        using var viewModel = new CodingSessionViewModel(
            new RecordingCodingSessionService(),
            new FakeOverlayToolService());

        runtime.ViewModelOwner.Set(viewModel, observePropertyChanged: true);
        viewModel.CurrentMeter = 2.5;

        Assert.True(runtime.ViewModelOwner.HasViewModel);
        Assert.True(runtime.SessionHost.HasViewModel);
        Assert.Equal(2.5, runtime.SessionHost.CurrentMeter);
        Assert.Contains(nameof(CodingSessionViewModel.CurrentMeter), propertyNames);

        overlayService = new FakeOverlayToolService { ActiveTool = OverlayToolType.Rectangle };

        Assert.True(runtime.OverlayToolHost.HasOverlayService);
        Assert.Equal(OverlayToolType.Rectangle, runtime.OverlayToolHost.ActiveTool);

        void OnPropertyChanged(object? _, PropertyChangedEventArgs args)
            => propertyNames.Add(args.PropertyName ?? string.Empty);
    }

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
        public void RaiseEvent(CodingEvent codingEvent) => EventAdded?.Invoke(this, codingEvent);
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
