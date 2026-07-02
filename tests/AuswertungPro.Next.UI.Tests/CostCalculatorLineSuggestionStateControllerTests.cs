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

    [Fact]
    public void CostLineVm_SetSuggestedQty_markiert_menge_nicht_als_manuell()
    {
        var line = new CostLineVm();

        line.SetSuggestedQty(12.5m);

        Assert.Equal(12.5m, line.Qty);
        Assert.False(line.IsQtyOverridden);

        line.Qty = 13m;

        Assert.True(line.IsQtyOverridden);
    }

    [Fact]
    public void CostLineVm_SetSuggestedPrice_markiert_preis_nicht_als_manuell()
    {
        var line = new CostLineVm();

        line.SetSuggestedPrice(42m, hasPrice: true, priceHint: "Katalog");

        Assert.Equal(42m, line.UnitPrice);
        Assert.False(line.IsPriceOverridden);
        Assert.False(line.PriceMissing);
        Assert.Equal("Katalog", line.PriceHint);

        line.UnitPrice = 45m;

        Assert.True(line.IsPriceOverridden);
        Assert.False(line.PriceMissing);
        Assert.Equal("", line.PriceHint);
    }
}
