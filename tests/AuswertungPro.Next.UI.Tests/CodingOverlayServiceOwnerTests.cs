using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayServiceOwnerTests
{
    [Fact]
    public void Owner_stores_and_clears_overlay_service()
    {
        var owner = CreateOwner();
        var service = new RecordingOverlayToolService();

        Assert.False(Get<bool>(owner, "HasService"));
        Assert.Null(Get<IOverlayToolService?>(owner, "Service"));

        Invoke(owner, "Set", service);

        Assert.True(Get<bool>(owner, "HasService"));
        Assert.Same(service, Get<IOverlayToolService?>(owner, "Service"));

        Invoke(owner, "Clear");

        Assert.False(Get<bool>(owner, "HasService"));
        Assert.Null(Get<IOverlayToolService?>(owner, "Service"));
    }

    private static object CreateOwner()
    {
        var ownerType = typeof(AuswertungPro.Next.UI.Player.CodingSessionHost).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingOverlayServiceOwner");
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

    private sealed class RecordingOverlayToolService : IOverlayToolService
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
