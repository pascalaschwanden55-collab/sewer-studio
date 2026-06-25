using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingOverlayToolHost
{
    bool HasOverlayService { get; }
    PipeCalibration? Calibration { get; }
    int? NominalDiameterMm { get; }
    bool IsCalibrated { get; }
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

    public PipeCalibration? Calibration => _resolveOverlayService()?.Calibration;

    public int? NominalDiameterMm => Calibration?.NominalDiameterMm;

    public bool IsCalibrated => _resolveOverlayService()?.IsCalibrated == true;

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
