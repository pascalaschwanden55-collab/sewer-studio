using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
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

    [Fact]
    public void HideCodingSurface_closes_overlay_and_hides_chrome_panels()
    {
        RunOnStaThread(() =>
        {
            var popup = new Popup { IsOpen = true };
            var canvas = new Canvas
            {
                IsHitTestVisible = true,
                Cursor = Cursors.Cross
            };
            canvas.Children.Add(new Rectangle());
            var sidePanel = new Border { Visibility = Visibility.Visible };
            var sidePanelColumn = new ColumnDefinition { Width = new GridLength(320) };
            var toolbar = new Border { Visibility = Visibility.Visible };
            var timeline = new Border { Visibility = Visibility.Visible };
            var calibrationHint = new Border { Visibility = Visibility.Visible };
            var measurementPanel = new Border { Visibility = Visibility.Visible };
            var hide = FindHideCodingSurfaceMethod();
            Assert.NotNull(hide);

            hide.Invoke(null, [popup, canvas, sidePanel, sidePanelColumn, toolbar, timeline, calibrationHint, measurementPanel]);

            Assert.False(popup.IsOpen);
            Assert.Empty(canvas.Children);
            Assert.False(canvas.IsHitTestVisible);
            Assert.Equal(Cursors.Arrow, canvas.Cursor);
            Assert.Equal(Visibility.Collapsed, sidePanel.Visibility);
            Assert.Equal(0d, sidePanelColumn.Width.Value);
            Assert.Equal(GridUnitType.Pixel, sidePanelColumn.Width.GridUnitType);
            Assert.Equal(Visibility.Collapsed, toolbar.Visibility);
            Assert.Equal(Visibility.Collapsed, timeline.Visibility);
            Assert.Equal(Visibility.Collapsed, calibrationHint.Visibility);
            Assert.Equal(Visibility.Collapsed, measurementPanel.Visibility);
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

    private static MethodInfo? FindHideCodingSurfaceMethod()
        => typeof(CodingOverlayMeasurementFormatter).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingModeChromeControls")
            ?.GetMethod(
                "HideCodingSurface",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(Popup),
                    typeof(Canvas),
                    typeof(FrameworkElement),
                    typeof(ColumnDefinition),
                    typeof(FrameworkElement),
                    typeof(FrameworkElement),
                    typeof(FrameworkElement),
                    typeof(FrameworkElement)
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
