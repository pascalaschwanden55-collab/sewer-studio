using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeChromeControlsTests
{
    [Fact]
    public void ShowCodingSurface_opens_overlay_and_shows_side_panel_and_toolbar()
    {
        RunOnStaThread(() =>
        {
            var popup = new Popup { IsOpen = false };
            var canvas = new Canvas { IsHitTestVisible = false };
            var sidePanel = new Border { Visibility = Visibility.Collapsed };
            var sidePanelColumn = new ColumnDefinition { Width = new GridLength(0) };
            var toolbar = new Border { Visibility = Visibility.Collapsed };
            var show = FindShowCodingSurfaceMethod();
            Assert.NotNull(show);

            show.Invoke(null, [popup, canvas, sidePanel, sidePanelColumn, toolbar, 320d]);

            Assert.True(popup.IsOpen);
            Assert.True(canvas.IsHitTestVisible);
            Assert.Equal(Visibility.Visible, sidePanel.Visibility);
            Assert.Equal(320d, sidePanelColumn.Width.Value);
            Assert.Equal(GridUnitType.Pixel, sidePanelColumn.Width.GridUnitType);
            Assert.Equal(Visibility.Visible, toolbar.Visibility);
        });
    }

    private static MethodInfo? FindShowCodingSurfaceMethod()
        => typeof(CodingOverlayMeasurementFormatter).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingModeChromeControls")
            ?.GetMethod(
                "ShowCodingSurface",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(Popup),
                    typeof(Canvas),
                    typeof(FrameworkElement),
                    typeof(ColumnDefinition),
                    typeof(FrameworkElement),
                    typeof(double)
                ],
                modifiers: null);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
