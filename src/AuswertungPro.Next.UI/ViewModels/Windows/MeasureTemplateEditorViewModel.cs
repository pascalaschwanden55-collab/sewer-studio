using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class MeasureTemplateEditorViewModel : ObservableObject
{
    private readonly CostCalculationService _costService;
    private MeasureTemplates _templates;
    private PriceCatalog _catalog;

    public ObservableCollection<TemplateRow> Templates { get; } = new();
    public ObservableCollection<CatalogItemRow> AvailablePrices { get; } = new();
    public ObservableCollection<TemplateLineRow> CurrentLines { get; } = new();

    [ObservableProperty] private TemplateRow? _selectedTemplate;
    [ObservableProperty] private TemplateLineRow? _selectedLine;
    [ObservableProperty] private CatalogItemRow? _selectedAvailablePrice;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private string _templateId = string.Empty;
    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _templateDescription = string.Empty;

    public IRelayCommand AddCatalogItemCommand { get; }
    public IRelayCommand RemoveCatalogItemCommand { get; }
    public IRelayCommand SaveCatalogCommand { get; }

    public MeasureTemplateEditorViewModel(CostCalculationService costService)
    {
        _costService = costService;
        _templates = costService.LoadTemplates();
        _catalog = costService.LoadCatalog();

        LoadTemplates();
        LoadAvailablePrices();

        AddCatalogItemCommand = new RelayCommand(AddCatalogItem);
        RemoveCatalogItemCommand = new RelayCommand(RemoveCatalogItem, () => SelectedAvailablePrice is not null);
        SaveCatalogCommand = new RelayCommand(SaveCatalog);

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTemplate))
                LoadTemplateLines();
            if (e.PropertyName == nameof(SearchText))
                FilterPrices();
            if (e.PropertyName == nameof(SelectedAvailablePrice))
                RemoveCatalogItemCommand.NotifyCanExecuteChanged();
        };
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var t in _templates.Templates)
            Templates.Add(new TemplateRow(t));
    }

    private void LoadAvailablePrices()
    {
        AvailablePrices.Clear();
        foreach (var item in _catalog.Items)
            AvailablePrices.Add(new CatalogItemRow(item));
    }

    private void FilterPrices()
    {
        foreach (var row in AvailablePrices)
        {
            var match = string.IsNullOrWhiteSpace(SearchText) ||
                       row.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       row.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            row.IsVisible = match;
        }
    }

    private void LoadTemplateLines()
    {
        CurrentLines.Clear();
        if (SelectedTemplate == null) return;

        TemplateId = SelectedTemplate.Template.Id;
        TemplateName = SelectedTemplate.Template.Name;
        TemplateDescription = SelectedTemplate.Template.Description;

        foreach (var line in SelectedTemplate.Template.Lines)
        {
            // Find matching price item to get Label and Unit
            var priceItem = _catalog.Items.FirstOrDefault(i => i.Id == line.ItemRef);
            var label = priceItem?.Label ?? line.ItemRef;
            var unit = priceItem?.Unit ?? "Stk";
            var qtyStr = line.Qty.ValueKind == System.Text.Json.JsonValueKind.String 
                ? line.Qty.GetString() ?? "1"
                : line.Qty.GetRawText();

            CurrentLines.Add(new TemplateLineRow(
                line.Group,
                label,
                unit,
                qtyStr,
                line.ItemRef
            ));
        }
    }

    [RelayCommand]
    private void NewTemplate()
    {
        TemplateId = $"template_{Templates.Count + 1}";
        TemplateName = "Neue Maßnahme";
        TemplateDescription = string.Empty;
        CurrentLines.Clear();
        SelectedTemplate = null;
    }

    [RelayCommand]
    private void SaveTemplate()
    {
        if (string.IsNullOrWhiteSpace(TemplateId) || string.IsNullOrWhiteSpace(TemplateName))
        {
            MessageBox.Show("ID und Name müssen ausgefüllt sein.", "Hinweis",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var lines = CurrentLines.Select(r => new TemplateLine
        {
            Group = r.Group,
            ItemRef = r.ItemRef,
            Qty = System.Text.Json.JsonSerializer.SerializeToElement(r.Qty)
        }).ToList();

        var template = new MeasureTemplate
        {
            Id = TemplateId,
            Name = TemplateName,
            Description = TemplateDescription,
            Lines = lines
        };

        // Remove old version if exists
        var existing = _templates.Templates.FirstOrDefault(t => t.Id == TemplateId);
        if (existing != null)
            _templates.Templates.Remove(existing);

        _templates.Templates.Add(template);
        _costService.SaveTemplates(_templates);

        LoadTemplates();
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == TemplateId);

        MessageBox.Show("Template gespeichert.", "OK",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void DeleteTemplate()
    {
        if (SelectedTemplate == null) return;

        var result = MessageBox.Show($"Template '{SelectedTemplate.Name}' wirklich löschen?",
            "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var template = _templates.Templates.FirstOrDefault(t => t.Id == SelectedTemplate.Id);
        if (template != null)
        {
            _templates.Templates.Remove(template);
            _costService.SaveTemplates(_templates);
            LoadTemplates();
            NewTemplate();
        }
    }

    [RelayCommand]
    private void AddLine(CatalogItemRow? priceRow)
    {
        if (priceRow == null) return;

        var line = new TemplateLineRow(
            priceRow.Group,
            priceRow.Label,
            priceRow.Unit,
            "1",
            priceRow.Id
        );

        CurrentLines.Add(line);
    }

    [RelayCommand]
    private void DeleteLine()
    {
        if (SelectedLine != null)
            CurrentLines.Remove(SelectedLine);
    }

    [RelayCommand]
    private void MoveLineUp()
    {
        if (SelectedLine == null) return;
        var idx = CurrentLines.IndexOf(SelectedLine);
        if (idx > 0)
        {
            CurrentLines.Move(idx, idx - 1);
        }
    }

    [RelayCommand]
    private void MoveLineDown()
    {
        if (SelectedLine == null) return;
        var idx = CurrentLines.IndexOf(SelectedLine);
        if (idx < CurrentLines.Count - 1)
        {
            CurrentLines.Move(idx, idx + 1);
        }
    }

    private void AddCatalogItem()
    {
        var id = CreateNewCatalogId();
        var item = new PriceItem
        {
            Id = id,
            Group = "Neue Gruppe",
            Label = "Neue Position",
            Unit = "m",
            UnitPrice = 0m
        };

        var row = new CatalogItemRow(item);
        AvailablePrices.Add(row);
        SelectedAvailablePrice = row;
        FilterPrices();
    }

    private void RemoveCatalogItem()
    {
        if (SelectedAvailablePrice is null)
            return;

        var label = string.IsNullOrWhiteSpace(SelectedAvailablePrice.Label)
            ? SelectedAvailablePrice.Id
            : SelectedAvailablePrice.Label;

        var result = MessageBox.Show(
            $"Position '{label}' wirklich löschen?",
            "Position löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        AvailablePrices.Remove(SelectedAvailablePrice);
        SelectedAvailablePrice = null;
    }

    private void SaveCatalog()
    {
        _catalog.Items = AvailablePrices
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .Select(r => r.Item)
            .ToList();

        _costService.SaveCatalog(_catalog);
        MessageBox.Show("Positionen gespeichert.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private string CreateNewCatalogId()
    {
        var index = AvailablePrices.Count + 1;
        while (true)
        {
            var candidate = $"neu_{index}";
            if (!AvailablePrices.Any(r => string.Equals(r.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }
}

public sealed partial class TemplateRow : ObservableObject
{
    public MeasureTemplate Template { get; }
    public string Id => Template.Id;
    public string Name => Template.Name;
    public string Description => Template.Description;

    public TemplateRow(MeasureTemplate template)
    {
        Template = template;
    }
}

public sealed class CatalogItemRow : ObservableObject
{
    public PriceItem Item { get; }

    public string Id
    {
        get => Item.Id;
        set
        {
            if (string.Equals(Item.Id, value, StringComparison.Ordinal)) return;
            Item.Id = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Group
    {
        get => Item.Group;
        set
        {
            if (string.Equals(Item.Group, value, StringComparison.Ordinal)) return;
            Item.Group = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Label
    {
        get => Item.Label;
        set
        {
            if (string.Equals(Item.Label, value, StringComparison.Ordinal)) return;
            Item.Label = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Unit
    {
        get => Item.Unit;
        set
        {
            if (string.Equals(Item.Unit, value, StringComparison.Ordinal)) return;
            Item.Unit = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public decimal Price
    {
        get => Item.UnitPrice;
        set
        {
            if (Item.UnitPrice == value) return;
            Item.UnitPrice = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible { get; set; } = true;

    public CatalogItemRow(PriceItem item)
    {
        Item = item;
    }
}

public sealed partial class TemplateLineRow : ObservableObject
{
    [ObservableProperty] private string _group;
    [ObservableProperty] private string _label;
    [ObservableProperty] private string _unit;
    [ObservableProperty] private string _qty;
    [ObservableProperty] private string _itemRef;

    public TemplateLineRow(string group, string label, string unit, string qty, string itemRef)
    {
        _group = group;
        _label = label;
        _unit = unit;
        _qty = qty;
        _itemRef = itemRef;
    }
}
