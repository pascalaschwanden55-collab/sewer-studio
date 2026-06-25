using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayToolHostTests
{
    [Fact]
    public void Null_overlay_service_reports_unavailable_and_cancel_does_nothing()
    {
        var host = CreateHost(() => null);

        Assert.False(Get<bool>(host, "HasOverlayService"));
        Assert.False((bool)host.GetType().GetMethod("CancelDraw")!.Invoke(host, [])!);
    }

    [Fact]
    public void CancelDraw_forwards_to_current_overlay_service()
    {
        var service = new RecordingOverlayToolService();
        var host = CreateHost(() => service);

        Assert.True(Get<bool>(host, "HasOverlayService"));
        Assert.True((bool)host.GetType().GetMethod("CancelDraw")!.Invoke(host, [])!);
        Assert.Equal(1, service.CancelDrawCalls);
    }

    [Fact]
    public void Calibration_state_is_read_from_current_overlay_service()
    {
        var calibration = new PipeCalibration
        {
            NominalDiameterMm = 400,
            NormalizedDiameter = 0.5,
            Source = CalibrationSource.Auto
        };
        var service = new RecordingOverlayToolService();
        service.SetCalibration(calibration);
        var host = CreateHost(() => service);

        Assert.Same(calibration, Get<PipeCalibration?>(host, "Calibration"));
        Assert.Equal(400, Get<int?>(host, "NominalDiameterMm"));
        Assert.True(Get<bool>(host, "IsCalibrated"));
    }

    [Fact]
    public void SetCalibration_returns_false_without_service_and_forwards_when_available()
    {
        var calibration = new PipeCalibration
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.4,
            Source = CalibrationSource.Manual
        };
        var nullHost = CreateHost(() => null);

        Assert.False(Invoke<bool>(nullHost, "SetCalibration", calibration));

        var service = new RecordingOverlayToolService();
        var host = CreateHost(() => service);

        Assert.True(Invoke<bool>(host, "SetCalibration", calibration));
        Assert.Same(calibration, service.Calibration);
    }

    [Fact]
    public void Tool_state_is_read_from_current_overlay_service()
    {
        var service = new RecordingOverlayToolService
        {
            ActiveTool = OverlayToolType.Level,
            ActiveLevelMode = LevelMode.Water,
            PipeBendSnapEnabled = true
        };
        var host = CreateHost(() => service);

        Assert.Equal(OverlayToolType.Level, Get<OverlayToolType>(host, "ActiveTool"));
        Assert.Equal(LevelMode.Water, Get<LevelMode>(host, "ActiveLevelMode"));
        Assert.True(Get<bool>(host, "PipeBendSnapEnabled"));
    }

    [Fact]
    public void SetActiveTool_and_level_mode_forward_when_service_available()
    {
        var nullHost = CreateHost(() => null);

        Assert.False(Invoke<bool>(nullHost, "SetActiveTool", OverlayToolType.Rectangle));
        Assert.False(Invoke<bool>(nullHost, "SetActiveLevelMode", LevelMode.Obstacle));

        var service = new RecordingOverlayToolService();
        var host = CreateHost(() => service);

        Assert.True(Invoke<bool>(host, "SetActiveTool", OverlayToolType.Rectangle));
        Assert.True(Invoke<bool>(host, "SetActiveLevelMode", LevelMode.Obstacle));
        Assert.Equal(OverlayToolType.Rectangle, service.ActiveTool);
        Assert.Equal(LevelMode.Obstacle, service.ActiveLevelMode);
    }

    [Fact]
    public void Drawing_state_is_read_from_current_overlay_service()
    {
        var nullHost = CreateHost(() => null);

        Assert.False(Get<bool>(nullHost, "IsDrawing"));
        Assert.False(Get<bool>(nullHost, "IsMultiPointTool"));
        Assert.Equal(0, Get<int>(nullHost, "DrawPointCount"));

        var service = new RecordingOverlayToolService
        {
            IsDrawing = true,
            IsMultiPointTool = true,
            DrawPointCount = 2
        };
        var host = CreateHost(() => service);

        Assert.True(Get<bool>(host, "IsDrawing"));
        Assert.True(Get<bool>(host, "IsMultiPointTool"));
        Assert.Equal(2, Get<int>(host, "DrawPointCount"));
    }

    private static object CreateHost(Func<IOverlayToolService?> resolveOverlayService)
    {
        var hostType = typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingOverlayToolHost");
        Assert.NotNull(hostType);

        var constructor = hostType.GetConstructor([typeof(Func<IOverlayToolService?>)]);
        Assert.NotNull(constructor);

        return constructor.Invoke([resolveOverlayService]);
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(target)!;

    private static T Invoke<T>(object target, string methodName, params object?[] args)
        => (T)target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(target, args)!;

    private sealed class RecordingOverlayToolService : IOverlayToolService
    {
        public int CancelDrawCalls { get; private set; }
        public OverlayToolType ActiveTool { get; set; }
        public LevelMode ActiveLevelMode { get; set; }
        public bool PipeBendSnapEnabled { get; set; }
        public PipeCalibration? Calibration { get; private set; }
        public bool IsCalibrated => Calibration?.IsCalibrated == true;
        public bool IsDrawing { get; set; }
        public bool IsMultiPointTool { get; set; }
        public int RequiredPointCount => 0;
        public int DrawPointCount { get; set; }
        public IReadOnlyList<NormalizedPoint> DrawPoints => [];
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => null;

        public event EventHandler<OverlayToolType>? ToolChanged;

        public void SetCalibration(PipeCalibration calibration) => Calibration = calibration;
        public void BeginDraw(NormalizedPoint startPoint) { }
        public void UpdateDraw(NormalizedPoint currentPoint) { }
        public OverlayGeometry? EndDraw() => null;
        public void CancelDraw() => CancelDrawCalls++;
        public bool AddDrawPoint(NormalizedPoint point) => false;
        public double PixelToMm(double normalizedPixels, double frameWidthPx) => 0;
        public double PointToClockHour(NormalizedPoint point) => 0;
        public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode) => null;
        public void ResizePipeCircle(double deltaNormalized) { }
        public void MovePipeCircle(NormalizedPoint newCenter) { }

        public void RaiseTool(OverlayToolType tool) => ToolChanged?.Invoke(this, tool);
    }
}
