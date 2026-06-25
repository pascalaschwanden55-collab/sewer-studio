using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingOverlayToolHost
{
    bool HasOverlayService { get; }
    OverlayToolType ActiveTool { get; }
    LevelMode ActiveLevelMode { get; }
    bool PipeBendSnapEnabled { get; }
    PipeCalibration? Calibration { get; }
    int? NominalDiameterMm { get; }
    bool IsCalibrated { get; }
    bool SetActiveTool(OverlayToolType tool);
    bool SetActiveLevelMode(LevelMode mode);
    bool SetCalibration(PipeCalibration calibration);
    bool CancelDraw();
}

public sealed class CodingOverlayToolHost : ICodingOverlayToolHost
{
    private readonly Func<IOverlayToolService?> _resolveOverlayService;

    public CodingOverlayToolHost(Func<IOverlayToolService?> resolveOverlayService)
    {
        ArgumentNullException.ThrowIfNull(resolveOverlayService);
        _resolveOverlayService = resolveOverlayService;
    }

    public bool HasOverlayService => _resolveOverlayService() is not null;

    public OverlayToolType ActiveTool => _resolveOverlayService()?.ActiveTool ?? OverlayToolType.None;

    public LevelMode ActiveLevelMode => _resolveOverlayService()?.ActiveLevelMode ?? LevelMode.Deposit;

    public bool PipeBendSnapEnabled => _resolveOverlayService()?.PipeBendSnapEnabled == true;

    public PipeCalibration? Calibration => _resolveOverlayService()?.Calibration;

    public int? NominalDiameterMm => Calibration?.NominalDiameterMm;

    public bool IsCalibrated => _resolveOverlayService()?.IsCalibrated == true;

    public bool SetActiveTool(OverlayToolType tool)
    {
        var overlayService = _resolveOverlayService();
        if (overlayService is null)
            return false;

        overlayService.ActiveTool = tool;
        return true;
    }

    public bool SetActiveLevelMode(LevelMode mode)
    {
        var overlayService = _resolveOverlayService();
        if (overlayService is null)
            return false;

        overlayService.ActiveLevelMode = mode;
        return true;
    }

    public bool SetCalibration(PipeCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        var overlayService = _resolveOverlayService();
        if (overlayService is null)
            return false;

        overlayService.SetCalibration(calibration);
        return true;
    }

    public bool CancelDraw()
    {
        var overlayService = _resolveOverlayService();
        if (overlayService is null)
            return false;

        overlayService.CancelDraw();
        return true;
    }
}
