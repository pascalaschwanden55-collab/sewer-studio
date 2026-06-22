using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerSliderTrackBounds
{
    public static (double offsetX, double trackWidth) Resolve(Slider positionSlider, FrameworkElement coordinateSurface)
    {
        ArgumentNullException.ThrowIfNull(positionSlider);
        ArgumentNullException.ThrowIfNull(coordinateSurface);

        if (positionSlider.Template?.FindName("PART_Track", positionSlider) is Track track
            && track.IsVisible
            && track.ActualWidth > 0)
        {
            var thumbHalf = (track.Thumb?.ActualWidth ?? 18) / 2.0;
            var start = track.TranslatePoint(new Point(thumbHalf, 0), coordinateSurface);
            var end = track.TranslatePoint(new Point(track.ActualWidth - thumbHalf, 0), coordinateSurface);
            return (start.X, end.X - start.X);
        }

        return ResolveFallback(coordinateSurface.ActualWidth);
    }

    public static (double offsetX, double trackWidth) ResolveFallback(double surfaceActualWidth)
        => (9, Math.Max(surfaceActualWidth - 18, 1));
}
