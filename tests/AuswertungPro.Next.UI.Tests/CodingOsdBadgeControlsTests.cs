using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdBadgeControlsTests
{
    [Fact]
    public void Show_writes_text_and_shows_badge()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBlock();
            var show = FindShowMethod();
            Assert.NotNull(show);

            show.Invoke(null, [badge, text, "OSD erkannt"]);

            Assert.Equal("OSD erkannt", text.Text);
            Assert.Equal(Visibility.Visible, badge.Visibility);
        });
    }

    [Fact]
    public void ShowInitial_writes_initial_osd_placeholder()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBlock();
            var showInitial = FindShowInitialMethod();
            Assert.NotNull(showInitial);

            showInitial.Invoke(null, [badge, text]);

            Assert.Equal("OSD: --", text.Text);
            Assert.Equal(Visibility.Visible, badge.Visibility);
        });
    }

    [Fact]
    public void ShowMeter_formats_osd_meter_through_policy()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBlock();
            var showMeter = FindShowMeterMethod();
            Assert.NotNull(showMeter);

            showMeter.Invoke(null, [badge, text, 12.345]);

            Assert.Equal("12.35m (OSD)", text.Text);
            Assert.Equal(Visibility.Visible, badge.Visibility);
        });
    }

    [Fact]
    public void Hide_collapses_badge()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Visible };
            var hide = FindHideMethod();
            Assert.NotNull(hide);

            hide.Invoke(null, [badge]);

            Assert.Equal(Visibility.Collapsed, badge.Visibility);
        });
    }

    private static Type? ControlsType
        => typeof(CodingOsdBadgeDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingOsdBadgeControls");

    private static MethodInfo? FindShowMethod()
        => ControlsType?.GetMethod(
            "Show",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(FrameworkElement), typeof(TextBlock), typeof(string)],
            modifiers: null);

    private static MethodInfo? FindShowInitialMethod()
        => ControlsType?.GetMethod(
            "ShowInitial",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(FrameworkElement), typeof(TextBlock)],
            modifiers: null);

    private static MethodInfo? FindShowMeterMethod()
        => ControlsType?.GetMethod(
            "ShowMeter",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(FrameworkElement), typeof(TextBlock), typeof(double)],
            modifiers: null);

    private static MethodInfo? FindHideMethod()
        => ControlsType?.GetMethod(
            "Hide",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(FrameworkElement)],
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
