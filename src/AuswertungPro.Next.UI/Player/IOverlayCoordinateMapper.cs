using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public interface IOverlayCoordinateMapper
{
    Point ToPixel(NormalizedPoint point);
}

public sealed class DelegateOverlayCoordinateMapper : IOverlayCoordinateMapper
{
    private readonly Func<NormalizedPoint, Point> _toPixel;

    public DelegateOverlayCoordinateMapper(Func<NormalizedPoint, Point> toPixel)
        => _toPixel = toPixel ?? throw new ArgumentNullException(nameof(toPixel));

    public Point ToPixel(NormalizedPoint point)
        => _toPixel(point);
}
