using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureInputStateControllerTests
{
    [Fact]
    public void ApplyDnText_unterdrueckt_nur_dn_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var text = "";
        var dnTriggered = false;
        var lengthTriggered = false;
        var connectionsTriggered = false;

        controller.ApplyDnText(
            "300",
            value =>
            {
                text = value;
                dnTriggered = controller.ShouldHandleDnTextChange();
                lengthTriggered = controller.ShouldHandleLengthTextChange();
                connectionsTriggered = controller.ShouldHandleConnectionsTextChange();
            });

        Assert.Equal("300", text);
        Assert.False(dnTriggered);
        Assert.True(lengthTriggered);
        Assert.True(connectionsTriggered);
        Assert.True(controller.ShouldHandleDnTextChange());
    }

    [Fact]
    public void ApplyLengthText_unterdrueckt_nur_length_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var lengthTriggered = false;
        var dnTriggered = false;

        controller.ApplyLengthText(
            "12.50",
            _ =>
            {
                lengthTriggered = controller.ShouldHandleLengthTextChange();
                dnTriggered = controller.ShouldHandleDnTextChange();
            });

        Assert.False(lengthTriggered);
        Assert.True(dnTriggered);
        Assert.True(controller.ShouldHandleLengthTextChange());
    }

    [Fact]
    public void ApplyConnectionsText_unterdrueckt_nur_connections_change_waehrend_des_updates()
    {
        var controller = new CostCalculatorMeasureInputStateController();
        var connectionsTriggered = false;
        var dnTriggered = false;

        controller.ApplyConnectionsText(
            "2",
            _ =>
            {
                connectionsTriggered = controller.ShouldHandleConnectionsTextChange();
                dnTriggered = controller.ShouldHandleDnTextChange();
            });

        Assert.False(connectionsTriggered);
        Assert.True(dnTriggered);
        Assert.True(controller.ShouldHandleConnectionsTextChange());
    }

    [Fact]
    public void Suppression_wird_auch_bei_exception_zurueckgesetzt()
    {
        var controller = new CostCalculatorMeasureInputStateController();

        Assert.Throws<InvalidOperationException>(() =>
            controller.ApplyLengthText("1", _ => throw new InvalidOperationException()));

        Assert.True(controller.ShouldHandleLengthTextChange());
    }
}
