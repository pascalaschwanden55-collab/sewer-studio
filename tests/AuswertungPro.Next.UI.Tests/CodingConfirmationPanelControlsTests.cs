using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationPanelControlsTests
{
    [Fact]
    public void Apply_writes_confirmation_state_and_shows_panel()
    {
        RunOnStaThread(() =>
        {
            var harness = CreateHarness();
            var ev = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" }
            };
            var gate = Gate(TrafficLight.Yellow, confidence: 0.734);
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            var result = apply.Invoke(harness.Instance, [ev, gate]);

            Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), result);
            Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), FillColor(harness.Ampel));
            Assert.Equal("BAB", harness.Code.Text);
            Assert.Equal("(73%)", harness.Confidence.Text);
            Assert.Equal("Riss", harness.Description.Text);
            Assert.Equal(CodingConfirmationDisplayPolicy.ConfirmationDetail(gate), harness.Detail.Text);
            Assert.Equal(Visibility.Visible, harness.Panel.Visibility);
        });
    }

    [Fact]
    public void Apply_uses_ai_reason_when_entry_description_is_missing()
    {
        RunOnStaThread(() =>
        {
            var harness = CreateHarness();
            var ev = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = null!, Beschreibung = null! },
                AiContext = new CodingEventAiContext { Reason = "KI-Grund" }
            };
            var gate = Gate(TrafficLight.Green, confidence: 0.9);
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(harness.Instance, [ev, gate]);

            Assert.Equal("???", harness.Code.Text);
            Assert.Equal("KI-Grund", harness.Description.Text);
        });
    }

    [Fact]
    public void Hide_collapses_confirmation_panel()
    {
        RunOnStaThread(() =>
        {
            var harness = CreateHarness();
            harness.Panel.Visibility = Visibility.Visible;
            var hide = FindHideMethod();
            Assert.NotNull(hide);

            hide.Invoke(harness.Instance, []);

            Assert.Equal(Visibility.Collapsed, harness.Panel.Visibility);
        });
    }

    private static ConfirmationHarness CreateHarness()
    {
        var type = ControlsType;
        Assert.NotNull(type);

        var harness = new ConfirmationHarness
        {
            Panel = new Border(),
            Ampel = new Ellipse(),
            Code = new TextBlock(),
            Confidence = new TextBlock(),
            Description = new TextBlock(),
            Detail = new TextBlock(),
            SaveErrorPanel = new StackPanel { Visibility = Visibility.Collapsed },
            SaveErrorText = new TextBlock()
        };

        harness.Instance = Activator.CreateInstance(type, [
            harness.Panel,
            harness.Ampel,
            harness.Code,
            harness.Confidence,
            harness.Description,
            harness.Detail,
            harness.SaveErrorPanel,
            harness.SaveErrorText
        ])!;

        return harness;
    }

    private static Color FillColor(Shape shape)
        => Assert.IsType<SolidColorBrush>(shape.Fill).Color;

    private static Type? ControlsType
        => typeof(CodingConfirmationDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingConfirmationPanelControls");

    private static MethodInfo? FindApplyMethod()
        => ControlsType?.GetMethod(
            "Apply",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(CodingEvent), typeof(QualityGateResult)],
            modifiers: null);

    private static MethodInfo? FindHideMethod()
        => ControlsType?.GetMethod(
            "Hide",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

    private static QualityGateResult Gate(TrafficLight trafficLight, double confidence)
        => new(confidence, trafficLight, new Dictionary<string, double>(), "test");

    private sealed class ConfirmationHarness
    {
        public object Instance { get; set; } = null!;
        public Border Panel { get; set; } = null!;
        public Shape Ampel { get; set; } = null!;
        public TextBlock Code { get; set; } = null!;
        public TextBlock Confidence { get; set; } = null!;
        public TextBlock Description { get; set; } = null!;
        public TextBlock Detail { get; set; } = null!;
        public StackPanel SaveErrorPanel { get; set; } = null!;
        public TextBlock SaveErrorText { get; set; } = null!;
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
