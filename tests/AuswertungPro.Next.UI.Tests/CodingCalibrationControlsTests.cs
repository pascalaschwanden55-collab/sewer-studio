using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationControlsTests
{
    [Fact]
    public void ApplyToggle_sets_hint_visibility_and_text()
    {
        RunOnStaThread(() =>
        {
            var hintPanel = new Border { Visibility = Visibility.Collapsed };
            var hintText = new TextBlock();
            var state = new CodingCalibrationToggleState(
                IsCalibrating: true,
                ActiveTool: OverlayToolType.None,
                ActiveToolName: "BtnCodingCalibrate",
                ToolLabel: "Kalibrieren",
                ShowHint: true,
                HintText: "Linie zeichnen");
            var apply = FindMethod("ApplyToggle", typeof(FrameworkElement), typeof(TextBlock), typeof(CodingCalibrationToggleState));
            Assert.NotNull(apply);

            apply.Invoke(null, [hintPanel, hintText, state]);

            Assert.Equal(Visibility.Visible, hintPanel.Visibility);
            Assert.Equal("Linie zeichnen", hintText.Text);
        });
    }

    [Fact]
    public void ShowHint_sets_hint_text()
    {
        RunOnStaThread(() =>
        {
            var hintText = new TextBlock { Text = "alt" };
            var show = FindMethod("ShowHint", typeof(TextBlock), typeof(string));
            Assert.NotNull(show);

            show.Invoke(null, [hintText, "Linie zu kurz"]);

            Assert.Equal("Linie zu kurz", hintText.Text);
        });
    }

    [Fact]
    public void ApplyManualResult_sets_status_and_hint_text()
    {
        RunOnStaThread(() =>
        {
            var statusText = new TextBlock();
            var hintText = new TextBlock();
            var result = new CodingManualCalibrationResult(
                IsValid: true,
                Calibration: null,
                StatusText: "Kalibriert",
                HintText: "DN 300");
            var apply = FindMethod("ApplyManualResult", typeof(TextBlock), typeof(TextBlock), typeof(CodingManualCalibrationResult));
            Assert.NotNull(apply);

            apply.Invoke(null, [statusText, hintText, result]);

            Assert.Equal("Kalibriert", statusText.Text);
            Assert.Equal("DN 300", hintText.Text);
        });
    }

    [Fact]
    public void ApplyPreview_sets_preview_hint_text()
    {
        RunOnStaThread(() =>
        {
            var hintText = new TextBlock();
            var preview = new CodingCalibrationPreviewState(
                Start: new Point(1, 2),
                End: new Point(4, 6),
                PixelLength: 5,
                HintText: "Referenzlinie: 5 px");
            var apply = FindMethod("ApplyPreview", typeof(TextBlock), typeof(CodingCalibrationPreviewState));
            Assert.NotNull(apply);

            apply.Invoke(null, [hintText, preview]);

            Assert.Equal("Referenzlinie: 5 px", hintText.Text);
        });
    }

    [Fact]
    public void HideHint_collapses_hint_panel()
    {
        RunOnStaThread(() =>
        {
            var hintPanel = new Border { Visibility = Visibility.Visible };
            var hide = FindMethod("HideHint", typeof(FrameworkElement));
            Assert.NotNull(hide);

            hide.Invoke(null, [hintPanel]);

            Assert.Equal(Visibility.Collapsed, hintPanel.Visibility);
        });
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(CodingCalibrationTogglePolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingCalibrationControls")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
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
