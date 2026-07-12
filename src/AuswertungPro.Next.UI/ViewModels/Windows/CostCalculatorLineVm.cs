using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CostLineVm : ObservableObject
{
    private readonly CostCalculatorLineSuggestionStateController _suggestionState = new();

    [ObservableProperty] private string _group = "";
    [ObservableProperty] private string _itemKey = "";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _transferMarked;
    [ObservableProperty] private bool _isPriceOverridden;
    [ObservableProperty] private bool _isQtyOverridden;
    [ObservableProperty] private bool _priceMissing;
    [ObservableProperty] private string _priceHint = "";

    public decimal LineTotal => Selected ? Qty * UnitPrice : 0m;

    public event Action? LineChanged;

    public void SetSuggestedPrice(decimal? price, bool hasPrice, string priceHint = "")
    {
        _suggestionState.ApplySuggestedPrice(price ?? 0m, value => UnitPrice = value);
        PriceMissing = !hasPrice;
        PriceHint = hasPrice ? priceHint : "";
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    public void SetSuggestedQty(decimal qty)
    {
        _suggestionState.ApplySuggestedQty(qty, value => Qty = value);
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnQtyChanged(decimal value)
    {
        if (_suggestionState.ShouldMarkManualQtyChange())
            IsQtyOverridden = true;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (_suggestionState.ShouldMarkManualPriceChange())
        {
            IsPriceOverridden = true;
            PriceMissing = false;
            PriceHint = "";
        }
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }
}
