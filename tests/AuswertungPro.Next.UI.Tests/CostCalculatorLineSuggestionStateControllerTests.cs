using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorLineSuggestionStateControllerTests
{
    [Fact]
    public void ApplySuggestedQty_unterdrueckt_manuelle_qty_override_markierung()
    {
        var controller = new CostCalculatorLineSuggestionStateController();
        var qty = 0m;
        var markedManual = false;

        controller.ApplySuggestedQty(
            12.5m,
            value =>
            {
                qty = value;
                if (controller.ShouldMarkManualQtyChange())
                    markedManual = true;
            });

        Assert.Equal(12.5m, qty);
        Assert.False(markedManual);
    }

    [Fact]
    public void ShouldMarkManualQtyChange_ist_ausserhalb_suggested_update_true()
    {
        var controller = new CostCalculatorLineSuggestionStateController();

        Assert.True(controller.ShouldMarkManualQtyChange());
    }

    [Fact]
    public void ApplySuggestedPrice_unterdrueckt_manuelle_price_override_markierung()
    {
        var controller = new CostCalculatorLineSuggestionStateController();
        var price = 0m;
        var markedManual = false;

        controller.ApplySuggestedPrice(
            42m,
            value =>
            {
                price = value;
                if (controller.ShouldMarkManualPriceChange())
                    markedManual = true;
            });

        Assert.Equal(42m, price);
        Assert.False(markedManual);
    }

    [Fact]
    public void ShouldMarkManualPriceChange_ist_ausserhalb_suggested_update_true()
    {
        var controller = new CostCalculatorLineSuggestionStateController();

        Assert.True(controller.ShouldMarkManualPriceChange());
    }

    [Fact]
    public void Suppression_wird_auch_bei_exception_zurueckgesetzt()
    {
        var controller = new CostCalculatorLineSuggestionStateController();

        Assert.Throws<InvalidOperationException>(() =>
            controller.ApplySuggestedPrice(1m, _ => throw new InvalidOperationException()));

        Assert.True(controller.ShouldMarkManualPriceChange());
    }
}
