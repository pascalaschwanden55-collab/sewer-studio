using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerCodingConfirmationPanelInitializerTests
{
    [Fact]
    public void Initialize_maps_confirmation_controls_to_owner()
    {
        RunOnStaThread(() =>
        {
            var owner = new CodingConfirmationPanelControlsOwner();
            var panel = new Border();
            var ampel = new Ellipse();
            var code = new TextBlock();
            var confidence = new TextBlock();
            var description = new TextBlock();
            var detail = new TextBlock();

            PlayerCodingConfirmationPanelInitializer.Initialize(
                owner,
                panel,
                ampel,
                code,
                confidence,
                description,
                detail);

            var codingEvent = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" }
            };
            var gate = new QualityGateResult(0.73, TrafficLight.Yellow, new Dictionary<string, double>(), "test");

            var color = owner.Apply(codingEvent, gate);

            Assert.True(owner.IsInitialized);
            Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), color);
            Assert.Equal("BAB", code.Text);
            Assert.Equal("(73%)", confidence.Text);
            Assert.Equal("Riss", description.Text);
            Assert.Equal(Visibility.Visible, panel.Visibility);
        });
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
