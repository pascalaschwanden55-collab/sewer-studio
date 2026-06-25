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

    private sealed class RecordingOverlayToolService : IOverlayToolService
    {
        public int CancelDrawCalls { get; private set; }
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
