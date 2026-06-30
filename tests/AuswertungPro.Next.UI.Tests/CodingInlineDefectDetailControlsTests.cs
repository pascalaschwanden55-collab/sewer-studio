using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectDetailControlsTests
{
    [Fact]
    public void Apply_writes_detail_state_and_shows_panel()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateHarness();
            var state = new CodingInlineDefectDetailState(
                CodeText: "BAB",
                DescriptionText: "Riss",
                DistanceText: "1.23m",
                ConfidenceText: "88%",
                Confidence: 0.88,
                Status: DefectStatus.AutoAccepted,
                StatusText: "Auto-Akzeptiert",
                CanAct: true);
            var method = FindApplyMethod();
            Assert.NotNull(method);

            method.Invoke(controls.Instance, [state]);

            Assert.Equal("BAB", controls.Code.Text);
            Assert.Equal("Riss", controls.Description.Text);
            Assert.Equal("1.23m", controls.Distance.Text);
            Assert.Equal("88%", controls.Confidence.Text);
            Assert.Equal("Auto-Akzeptiert", controls.Status.Text);
            Assert.Equal(Visibility.Visible, controls.Accept.Visibility);
            Assert.Equal(Visibility.Visible, controls.Reject.Visibility);
            Assert.Equal(Visibility.Visible, controls.Panel.Visibility);
            Assert.Equal(300, controls.Column.Width.Value);
            Assert.Equal(
                BrushColor(CodingSessionViewModel.GetConfidenceBrush(0.88)),
                BrushColor(controls.Confidence.Foreground));
        });
    }

    [Fact]
    public void Apply_collapses_action_buttons_and_uses_muted_confidence_without_ai_confidence()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateHarness();
            var state = new CodingInlineDefectDetailState(
                CodeText: "BCA",
                DescriptionText: "Anschluss",
                DistanceText: "2.00m",
                ConfidenceText: "-",
                Confidence: null,
                Status: DefectStatus.Pending,
                StatusText: "Review empfohlen",
                CanAct: false);
            var method = FindApplyMethod();
            Assert.NotNull(method);

            method.Invoke(controls.Instance, [state]);

            Assert.Equal(Visibility.Collapsed, controls.Accept.Visibility);
            Assert.Equal(Visibility.Collapsed, controls.Reject.Visibility);
            Assert.Equal(Color.FromRgb(0x94, 0xA3, 0xB8), BrushColor(controls.Confidence.Foreground));
        });
    }

    [Fact]
    public void Hide_clears_preview_and_collapses_panel()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateHarness();
            controls.Preview.Source = new DrawingImage();
            controls.Preview.Visibility = Visibility.Visible;
            controls.PreviewStatus.Visibility = Visibility.Collapsed;
            controls.Panel.Visibility = Visibility.Visible;
            controls.Column.Width = new GridLength(300);
            var method = FindHideMethod();
            Assert.NotNull(method);

            method.Invoke(controls.Instance, []);

            Assert.Null(controls.Preview.Source);
            Assert.Equal(Visibility.Collapsed, controls.Preview.Visibility);
            Assert.Equal(Visibility.Visible, controls.PreviewStatus.Visibility);
            Assert.Equal(Visibility.Collapsed, controls.Panel.Visibility);
            Assert.Equal(0, controls.Column.Width.Value);
        });
    }

    [Fact]
    public void ApplyPreview_writes_preview_state()
    {
        RunOnStaThread(() =>
        {
            var controls = CreateHarness();
            var image = new DrawingImage();
            var state = new CodingInlineEvidencePreviewState(
                Source: image,
                ImageVisible: true,
                StatusText: "Bild vorhanden",
                StatusVisible: false);
            var method = FindApplyPreviewMethod();
            Assert.NotNull(method);

            method.Invoke(controls.Instance, [state]);

            Assert.Same(image, controls.Preview.Source);
            Assert.Equal(Visibility.Visible, controls.Preview.Visibility);
            Assert.Equal("Bild vorhanden", controls.PreviewStatus.Text);
            Assert.Equal(Visibility.Collapsed, controls.PreviewStatus.Visibility);
        });
    }

    private static InlineDetailHarness CreateHarness()
    {
        var type = ControlsType;
        Assert.NotNull(type);

        var harness = new InlineDetailHarness
        {
            Code = new TextBlock(),
            Description = new TextBlock(),
            Distance = new TextBlock(),
            Confidence = new TextBlock(),
            Status = new TextBlock(),
            Preview = new Image(),
            PreviewStatus = new TextBlock(),
            Accept = new Button(),
            Reject = new Button(),
            Panel = new Border(),
            Column = new ColumnDefinition()
        };

        harness.Instance = Activator.CreateInstance(type, [
            harness.Code,
            harness.Description,
            harness.Distance,
            harness.Confidence,
            harness.Status,
            harness.Preview,
            harness.PreviewStatus,
            harness.Accept,
            harness.Reject,
            harness.Panel,
            harness.Column
        ])!;

        return harness;
    }

    private static Color BrushColor(Brush brush)
        => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Type? ControlsType
        => typeof(CodingDefectStatusDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingInlineDefectDetailControls");

    private static MethodInfo? FindApplyMethod()
        => ControlsType?.GetMethod(
            "Apply",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(CodingInlineDefectDetailState)],
            modifiers: null);

    private static MethodInfo? FindHideMethod()
        => ControlsType?.GetMethod(
            "Hide",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

    private static MethodInfo? FindApplyPreviewMethod()
        => ControlsType?.GetMethod(
            "ApplyPreview",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(CodingInlineEvidencePreviewState)],
            modifiers: null);

    private sealed class InlineDetailHarness
    {
        public object Instance { get; set; } = null!;
        public TextBlock Code { get; set; } = null!;
        public TextBlock Description { get; set; } = null!;
        public TextBlock Distance { get; set; } = null!;
        public TextBlock Confidence { get; set; } = null!;
        public TextBlock Status { get; set; } = null!;
        public Image Preview { get; set; } = null!;
        public TextBlock PreviewStatus { get; set; } = null!;
        public Button Accept { get; set; } = null!;
        public Button Reject { get; set; } = null!;
        public Border Panel { get; set; } = null!;
        public ColumnDefinition Column { get; set; } = null!;
    }

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
