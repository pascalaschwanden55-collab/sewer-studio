using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class PriceCatalogEditorViewModel : ObservableObject
{
    private readonly CostCalculationService _costService;
    private PriceCatalog _catalog = new();

    [ObservableProperty] private string _searchText = string.Empty;
    
    public ObservableCollection<PriceItemRow> Items { get; } = new();

    public PriceCatalogEditorViewModel(CostCalculationService costService)
    {
        _costService = costService;
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        _catalog = _costService.LoadCatalog();
        Items.Clear();
        foreach (var item in _catalog.Items)
        {
            Items.Add(new PriceItemRow
            {
                Id = item.Id,
                Group = item.Group,
                Label = item.Label,
                Unit = item.Unit,
                DnMin = item.DnMin,
                DnMax = item.DnMax,
                UnitPrice = item.UnitPrice,
                SourceFile = item.Source?.File,
                SourcePos = item.Source?.Pos,
                Notes = item.Notes
            });
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        var view = CollectionViewSource.GetDefaultView(Items);
        if (string.IsNullOrWhiteSpace(value))
        {
            view.Filter = null;
        }
        else
        {
            var q = value.ToLowerInvariant();
            view.Filter = obj =>
            {
                if (obj is not PriceItemRow row) return false;
                return (row.Id?.ToLowerInvariant().Contains(q) ?? false) ||
                       (row.Group?.ToLowerInvariant().Contains(q) ?? false) ||
                       (row.Label?.ToLowerInvariant().Contains(q) ?? false);
            };
        }
    }

    [RelayCommand]
    private void AddNew()
    {
        var newRow = new PriceItemRow
        {
            Id = "neu.id",
            Group = "Renovierung",
            Label = "Neue Position",
            Unit = "m",
            UnitPrice = 0
        };
        Items.Add(newRow);
    }

    [RelayCommand]
    private void Delete(PriceItemRow? row)
    {
        if (row != null)
            Items.Remove(row);
    }

    [RelayCommand]
    private void Save()
    {
        _catalog.Items = Items.Where(r => !string.IsNullOrWhiteSpace(r.Id)).Select(r => new PriceItem
        {
            Id = r.Id!,
            Group = r.Group ?? "",
            Label = r.Label ?? "",
            Unit = r.Unit ?? "",
            DnMin = r.DnMin,
            DnMax = r.DnMax,
            UnitPrice = r.UnitPrice,
            Source = (!string.IsNullOrWhiteSpace(r.SourceFile) || !string.IsNullOrWhiteSpace(r.SourcePos))
                ? new PriceSource { File = r.SourceFile, Pos = r.SourcePos }
                : null,
            Notes = r.Notes
        }).ToList();

        _costService.SaveCatalog(_catalog);
        MessageBox.Show($"Preiskatalog gespeichert: {_costService.GetCatalogPath()}", "OK", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public sealed partial class PriceItemRow : ObservableObject
{
    [ObservableProperty] private string? _id;
    [ObservableProperty] private string? _group;
    [ObservableProperty] private string? _label;
    [ObservableProperty] private string? _unit;
    [ObservableProperty] private int? _dnMin;
    [ObservableProperty] private int? _dnMax;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private string? _sourceFile;
    [ObservableProperty] private string? _sourcePos;
    [ObservableProperty] private string? _notes;
}
