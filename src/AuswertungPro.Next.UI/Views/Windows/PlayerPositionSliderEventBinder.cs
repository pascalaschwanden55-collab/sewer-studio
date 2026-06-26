using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerPositionSliderEventBinder
{
    public static void Bind(
        Slider slider,
        DragStartedEventHandler dragStarted,
        DragCompletedEventHandler dragCompleted,
        MouseButtonEventHandler previewMouseLeftButtonUp,
        MouseEventHandler lostMouseCapture)
    {
        ArgumentNullException.ThrowIfNull(slider);
        ArgumentNullException.ThrowIfNull(dragStarted);
        ArgumentNullException.ThrowIfNull(dragCompleted);
        ArgumentNullException.ThrowIfNull(previewMouseLeftButtonUp);
        ArgumentNullException.ThrowIfNull(lostMouseCapture);

        slider.AddHandler(Thumb.DragStartedEvent, dragStarted, true);
        slider.AddHandler(Thumb.DragCompletedEvent, dragCompleted, true);
        slider.PreviewMouseLeftButtonUp += previewMouseLeftButtonUp;
        slider.LostMouseCapture += lostMouseCapture;
    }
}
