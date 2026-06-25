using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingOverlayToolHost
{
    bool HasOverlayService { get; }
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

    public bool CancelDraw()
    {
        var overlayService = _resolveOverlayService();
        if (overlayService is null)
            return false;

        overlayService.CancelDraw();
        return true;
    }
}
