using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationPanelControlsOwnerTests
{
    [Fact]
    public void Initialize_apply_and_hide_delegate_to_controls()
    {
        RunOnStaThread(() =>
        {
            var owner = new CodingConfirmationPanelControlsOwner();
            var harness = CreateHarness();
            var codingEvent = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" }
            };
            var gate = new QualityGateResult(0.73, TrafficLight.Yellow, new Dictionary<string, double>(), "test");

            owner.Initialize(harness.Controls);
            var color = owner.Apply(codingEvent, gate);
            owner.Hide();

            Assert.True(owner.IsInitialized);
            Assert.Same(harness.Controls, owner.Controls);
            Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), color);
            Assert.Equal("(73%)", harness.Confidence.Text);
            Assert.Equal(Visibility.Collapsed, harness.Panel.Visibility);
        });
    }

    private static ConfirmationHarness CreateHarness()
    {
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
        harness.Controls = new CodingConfirmationPanelControls(
            harness.Panel,
            harness.Ampel,
            harness.Code,
            harness.Confidence,
            harness.Description,
            harness.Detail,
            harness.SaveErrorPanel,
            harness.SaveErrorText);
        return harness;
    }

    private sealed class ConfirmationHarness
    {
        public CodingConfirmationPanelControls Controls { get; set; } = null!;
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
