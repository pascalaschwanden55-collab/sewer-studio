using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

// CatalogColumnViewModel + CatalogItem + CategoryNode + Observation
// ParameterViewModel — eigenstaendige Klassen, die zur ObservationCatalog-
// ViewModel gehoeren aber in eigene Datei verschoben (Slice 31).
public sealed partial class CatalogColumnViewModel : ObservableObject
{
    public int Index { get; }
    public ObservableCollection<CatalogItem> Items { get; }

    [ObservableProperty] private CatalogItem? _selectedItem;

    public CatalogColumnViewModel(int index, IEnumerable<CatalogItem> items)
    {
        Index = index;
        Items = new ObservableCollection<CatalogItem>(items.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase));
    }
}

public sealed class CatalogItem
{
    public string Label { get; }
    public CategoryNode? Node { get; }
    public AppProtocol.CodeDefinition? Code { get; }

    private CatalogItem(string label, CategoryNode? node, AppProtocol.CodeDefinition? code)
    {
        Label = label;
        Node = node;
        Code = code;
    }

    public static CatalogItem FromNode(CategoryNode node) => new(node.Label, node, null);

    public static CatalogItem FromCode(AppProtocol.CodeDefinition code)
        => new($"{code.Code}  {code.Title}", null, code);
}

public sealed class CategoryNode
{
    public string Key { get; }
    public string Label { get; }
    public Dictionary<string, CategoryNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<AppProtocol.CodeDefinition> Codes { get; } = new();

    public CategoryNode(string key, string label)
    {
        Key = key;
        Label = label;
    }
}

public sealed partial class ObservationParameterViewModel : ObservableObject
{
    public string Name { get; }
    public string? DataKey { get; }
    public string Type { get; }
    public string? Unit { get; }
    public bool Required { get; }
    public IReadOnlyList<string> AllowedValues { get; }
    public bool IsEnum => string.Equals(Type, "enum", StringComparison.OrdinalIgnoreCase);
    public bool IsNumber => string.Equals(Type, "number", StringComparison.OrdinalIgnoreCase);
    public bool IsClock => string.Equals(Type, "clock", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => Required ? $"{Name} *" : Name;
    public string? UnitSuffix => string.IsNullOrWhiteSpace(Unit) ? null : Unit;

    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private bool _isValid = true;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public IRelayCommand<string> SelectClockCommand { get; }

    public ObservationParameterViewModel(AppProtocol.CodeParameter parameter, string? existingValue)
    {
        Name = parameter.Name;
        DataKey = parameter.DataKey;
        Type = parameter.Type;
        Unit = parameter.Unit;
        Required = parameter.Required;
        AllowedValues = parameter.AllowedValues?.ToList() ?? new List<string>();
        Value = existingValue ?? string.Empty;
        SelectClockCommand = new RelayCommand<string>(SetClockValue);
    }

    private void SetClockValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        Value = value.Trim();
    }

    partial void OnValueChanged(string value)
    {
        Validate(out _);
    }

    public bool Validate(out string error)
    {
        error = string.Empty;
        var v = Value?.Trim() ?? string.Empty;

        if (Required && v.Length == 0)
        {
            error = $"Parameter '{Name}' ist erforderlich.";
            IsValid = false;
            ErrorMessage = error;
            return false;
        }

        if (v.Length == 0)
        {
            IsValid = true;
            ErrorMessage = string.Empty;
            return true;
        }

        if (IsEnum && AllowedValues.Count > 0 && !AllowedValues.Contains(v, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Parameter '{Name}' hat einen ungueltigen Wert.";
            IsValid = false;
            ErrorMessage = error;
            return false;
        }

        if (IsNumber)
        {
            var normalized = v.Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                error = $"Parameter '{Name}' muss numerisch sein.";
                IsValid = false;
                ErrorMessage = error;
                return false;
            }
        }

        if (IsClock)
        {
            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clockValue)
                || clockValue < 0
                || clockValue > 12)
            {
                error = $"Parameter '{Name}' muss zwischen 00 und 12 liegen.";
                IsValid = false;
                ErrorMessage = error;
                return false;
            }
        }

        IsValid = true;
        ErrorMessage = string.Empty;

        return true;
    }
}
