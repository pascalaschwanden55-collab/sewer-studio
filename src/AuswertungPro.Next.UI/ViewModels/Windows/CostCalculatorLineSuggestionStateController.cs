namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorLineSuggestionStateController
{
    private bool _suppressPriceOverride;
    private bool _suppressQtyOverride;

    public void ApplySuggestedPrice(
        decimal price,
        Action<decimal> setUnitPrice)
    {
        ArgumentNullException.ThrowIfNull(setUnitPrice);

        _suppressPriceOverride = true;
        try
        {
            setUnitPrice(price);
        }
        finally
        {
            _suppressPriceOverride = false;
        }
    }

    public void ApplySuggestedQty(
        decimal qty,
        Action<decimal> setQty)
    {
        ArgumentNullException.ThrowIfNull(setQty);

        _suppressQtyOverride = true;
        try
        {
            setQty(qty);
        }
        finally
        {
            _suppressQtyOverride = false;
        }
    }

    public bool ShouldMarkManualPriceChange()
        => !_suppressPriceOverride;

    public bool ShouldMarkManualQtyChange()
        => !_suppressQtyOverride;
}
