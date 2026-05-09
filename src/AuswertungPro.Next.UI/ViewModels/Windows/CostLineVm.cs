using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// CostLineVm — einzelne Kostenzeile innerhalb eines MeasureBlockVm.
// Aus CostCalculatorViewModel.cs in eigene Datei extrahiert (Slice 11b).
public sealed partial class CostLineVm : ObservableObject
{
    private bool _suppressOverride;
    private bool _suppressQtyOverride;

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

    public decimal LineTotal => Selected ? Qty * UnitPrice : 0m;

    public event Action? LineChanged;

    public void SetSuggestedPrice(decimal? price, bool hasPrice)
    {
        _suppressOverride = true;
        UnitPrice = price ?? 0m;
        _suppressOverride = false;
        PriceMissing = !hasPrice;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    public void SetSuggestedQty(decimal qty)
    {
        _suppressQtyOverride = true;
        Qty = qty;
        _suppressQtyOverride = false;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnQtyChanged(decimal value)
    {
        if (!_suppressQtyOverride)
            IsQtyOverridden = true;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (!_suppressOverride)
        {
            IsPriceOverridden = true;
            PriceMissing = false;
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
