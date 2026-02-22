using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed partial class ObservationCatalogViewModel : ObservableObject
{
    private readonly AppProtocol.ICodeCatalogProvider _catalog;
    private readonly ProtocolEntryVM _entryVm;
    private readonly CategoryNode _root = new("Root", "Root");
    private readonly Dictionary<string, AppProtocol.CodeDefinition> _codeIndex;
    private readonly List<AppProtocol.CodeDefinition> _allCodes;

    public ObservableCollection<AppProtocol.CodeDefinition> FilteredCodes { get; } = new();
    public ObservableCollection<CatalogColumnViewModel> Columns { get; } = new();
    public ObservableCollection<ObservationParameterViewModel> Parameters { get; } = new();

    [ObservableProperty] private AppProtocol.CodeDefinition? _selectedCode;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _meterStartText = string.Empty;
    [ObservableProperty] private string _meterEndText = string.Empty;
    [ObservableProperty] private string _zeitText = string.Empty;
    [ObservableProperty] private string _mpegText = string.Empty;
    [ObservableProperty] private bool _isStreckenschaden;
    [ObservableProperty] private string _validationMessage = string.Empty;
    [ObservableProperty] private string _codeTitle = string.Empty;
    [ObservableProperty] private string _codeDescription = string.Empty;

    public string? VsaDistanz
    {
        get => _entryVm.VsaDistanz;
        set
        {
            if (string.Equals(_entryVm.VsaDistanz, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaDistanz = value;
            OnPropertyChanged();
        }
    }

    public string? VsaUhrVon
    {
        get => _entryVm.VsaUhrVon;
        set
        {
            if (string.Equals(_entryVm.VsaUhrVon, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaUhrVon = value;
            OnPropertyChanged();
        }
    }

    public string? VsaUhrBis
    {
        get => _entryVm.VsaUhrBis;
        set
        {
            if (string.Equals(_entryVm.VsaUhrBis, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaUhrBis = value;
            OnPropertyChanged();
        }
    }

    public string? VsaQ1
    {
        get => _entryVm.VsaQ1;
        set
        {
            if (string.Equals(_entryVm.VsaQ1, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaQ1 = value;
            OnPropertyChanged();
        }
    }

    public string? VsaQ2
    {
        get => _entryVm.VsaQ2;
        set
        {
            if (string.Equals(_entryVm.VsaQ2, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaQ2 = value;
            OnPropertyChanged();
        }
    }

    public string? VsaStrecke
    {
        get => _entryVm.VsaStrecke;
        set
        {
            if (string.Equals(_entryVm.VsaStrecke, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaStrecke = value;
            OnPropertyChanged();
        }
    }

    public bool VsaVerbindung
    {
        get => _entryVm.VsaVerbindung;
        set
        {
            if (_entryVm.VsaVerbindung == value)
                return;
            _entryVm.VsaVerbindung = value;
            OnPropertyChanged();
        }
    }

    public string? VsaVideo
    {
        get => _entryVm.VsaVideo;
        set
        {
            if (string.Equals(_entryVm.VsaVideo, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaVideo = value;
            OnPropertyChanged();
        }
    }

    public string? VsaAnsicht
    {
        get => _entryVm.VsaAnsicht;
        set
        {
            if (string.Equals(_entryVm.VsaAnsicht, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaAnsicht = value;
            OnPropertyChanged();
        }
    }

    public string? VsaEz
    {
        get => _entryVm.VsaEz;
        set
        {
            if (string.Equals(_entryVm.VsaEz, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaEz = value;
            OnPropertyChanged();
        }
    }

    public string? VsaSchachtbereich
    {
        get => _entryVm.VsaSchachtbereich;
        set
        {
            if (string.Equals(_entryVm.VsaSchachtbereich, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaSchachtbereich = value;
            OnPropertyChanged();
        }
    }

    public string? VsaAnmerkung
    {
        get => _entryVm.VsaAnmerkung;
        set
        {
            if (string.Equals(_entryVm.VsaAnmerkung, value, StringComparison.Ordinal))
                return;
            _entryVm.VsaAnmerkung = value;
            OnPropertyChanged();
        }
    }

    public ObservationCatalogViewModel(AppProtocol.ICodeCatalogProvider catalog, ProtocolEntry entry)
    {
        _catalog = catalog;
        _entryVm = new ProtocolEntryVM(entry);

        _allCodes = _catalog.GetAll().OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToList();
        _codeIndex = _allCodes
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        BuildTree();
        InitializeColumns();
        ApplySearchFilter();

        MeterStartText = FormatDouble(_entryVm.MeterStart);
        MeterEndText = FormatDouble(_entryVm.MeterEnd);
        ZeitText = _entryVm.Zeit is null ? string.Empty : FormatTime(_entryVm.Zeit.Value);
        MpegText = _entryVm.Mpeg ?? string.Empty;
        IsStreckenschaden = _entryVm.Model.IsStreckenschaden;

        if (!string.IsNullOrWhiteSpace(_entryVm.Code)
            && _catalog.TryGet(_entryVm.Code, out var def))
        {
            SelectCode(def, syncColumns: true);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    partial void OnSelectedCodeChanged(AppProtocol.CodeDefinition? value)
    {
        BuildParameters();
        UpdateHeader();
    }

    public void SelectColumnItem(int columnIndex, CatalogItem item)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return;

        Columns[columnIndex].SelectedItem = item;
        while (Columns.Count > columnIndex + 1)
            Columns.RemoveAt(Columns.Count - 1);

        if (item.Node is not null)
        {
            if (item.Node.Children.Count > 0)
            {
                Columns.Add(new CatalogColumnViewModel(columnIndex + 1, item.Node.Children.Values.Select(CatalogItem.FromNode)));
                return;
            }

            if (item.Node.Codes.Count > 0)
            {
                Columns.Add(new CatalogColumnViewModel(columnIndex + 1, item.Node.Codes.Select(CatalogItem.FromCode)));
                return;
            }
        }

        if (item.Code is not null)
            SelectCode(item.Code, syncColumns: false);
    }

    public void SelectCode(AppProtocol.CodeDefinition code, bool syncColumns)
    {
        SelectedCode = code;
        if (syncColumns)
            SyncColumnsToCode(code);
    }

    public bool ApplyToEntry()
    {
        ValidationMessage = string.Empty;

        if (SelectedCode is null)
        {
            ValidationMessage = "Bitte einen Code auswaehlen.";
            return false;
        }

        if (!TryParseOptionalDouble(MeterStartText, out var meterStart))
        {
            ValidationMessage = "MeterStart ist ungueltig.";
            return false;
        }

        if (!TryParseOptionalDouble(MeterEndText, out var meterEnd))
        {
            ValidationMessage = "MeterEnd ist ungueltig.";
            return false;
        }

        if (!TryParseOptionalTimeSpan(ZeitText, out var zeit))
        {
            ValidationMessage = "Zeit ist ungueltig.";
            return false;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in Parameters)
        {
            if (!parameter.Validate(out var error))
            {
                ValidationMessage = error;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Value))
                parameters[parameter.Name] = parameter.Value.Trim();
        }

        _entryVm.ApplyCodeSelection(
            SelectedCode.Code,
            parameters,
            meterStart,
            meterEnd,
            severity: null,
            count: null,
            notes: null);

        _entryVm.Zeit = zeit;
        _entryVm.Mpeg = string.IsNullOrWhiteSpace(MpegText) ? null : MpegText.Trim();
        _entryVm.Model.IsStreckenschaden = IsStreckenschaden;

        if (SelectedCode.RequiresRange)
            _entryVm.Model.IsStreckenschaden = true;

        if (string.IsNullOrWhiteSpace(_entryVm.Beschreibung))
            _entryVm.Beschreibung = BuildDefaultDescription(SelectedCode, parameters, meterStart, meterEnd);

        _entryVm.EnsureVsaDefaults();
        _entryVm.ApplyStreckenLogik();

        return true;
    }

    private void ApplySearchFilter()
    {
        FilteredCodes.Clear();
        var term = (SearchText ?? string.Empty).Trim();
        foreach (var code in _allCodes)
        {
            if (term.Length > 0)
            {
                var group = code.Group ?? "";
                if (!code.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                    && !code.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    && !group.Contains(term, StringComparison.OrdinalIgnoreCase)
                    && !(code.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    continue;
                }
            }

            FilteredCodes.Add(code);
        }
    }

    private void BuildTree()
    {
        foreach (var code in _allCodes)
        {
            var node = _root;
            if (code.CategoryPath is { Count: > 0 })
            {
                foreach (var level in code.CategoryPath)
                {
                    if (string.IsNullOrWhiteSpace(level))
                        continue;
                    if (!node.Children.TryGetValue(level, out var next))
                    {
                        next = new CategoryNode(level, ResolveCategoryLabel(level));
                        node.Children[level] = next;
                    }
                    node = next;
                }
            }
            node.Codes.Add(code);
        }
    }

    private void InitializeColumns()
    {
        Columns.Clear();
        Columns.Add(new CatalogColumnViewModel(0, _root.Children.Values.Select(CatalogItem.FromNode)));
    }

    private void SyncColumnsToCode(AppProtocol.CodeDefinition code)
    {
        InitializeColumns();
        var node = _root;
        for (var i = 0; i < code.CategoryPath.Count; i++)
        {
            var level = code.CategoryPath[i];
            if (string.IsNullOrWhiteSpace(level) || !node.Children.TryGetValue(level, out var child))
                break;

            var colIndex = i;
            if (colIndex < Columns.Count)
            {
                var item = Columns[colIndex].Items.FirstOrDefault(x => x.Node == child);
                if (item is not null)
                    Columns[colIndex].SelectedItem = item;
            }

            node = child;
            while (Columns.Count > colIndex + 1)
                Columns.RemoveAt(Columns.Count - 1);

            if (node.Children.Count > 0)
                Columns.Add(new CatalogColumnViewModel(colIndex + 1, node.Children.Values.Select(CatalogItem.FromNode)));
            else if (node.Codes.Count > 0)
                Columns.Add(new CatalogColumnViewModel(colIndex + 1, node.Codes.Select(CatalogItem.FromCode)));
        }

        var lastCol = Columns.LastOrDefault();
        if (lastCol is not null)
        {
            var codeItem = lastCol.Items.FirstOrDefault(x => x.Code != null
                                                             && string.Equals(x.Code.Code, code.Code, StringComparison.OrdinalIgnoreCase));
            if (codeItem is not null)
                lastCol.SelectedItem = codeItem;
        }
    }

    private void BuildParameters()
    {
        Parameters.Clear();
        if (SelectedCode is null)
            return;

        var existing = _entryVm.Parameters;
        foreach (var p in SelectedCode.Parameters)
        {
            existing.TryGetValue(p.Name, out var existingValue);
            Parameters.Add(new ObservationParameterViewModel(p, existingValue));
        }
    }

    private void UpdateHeader()
    {
        if (SelectedCode is null)
        {
            CodeTitle = string.Empty;
            CodeDescription = string.Empty;
            return;
        }

        CodeTitle = $"{SelectedCode.Code}  {SelectedCode.Title}";
        CodeDescription = SelectedCode.Description ?? string.Empty;
    }

    private static bool TryParseOptionalDouble(string raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var normalized = raw.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalTimeSpan(string raw, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static string FormatDouble(double? value)
        => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static string BuildDefaultDescription(
        AppProtocol.CodeDefinition def,
        IReadOnlyDictionary<string, string> parameters,
        double? meterStart,
        double? meterEnd)
    {
        var title = def.Title ?? string.Empty;
        var parts = new List<string>();

        if (parameters is not null && parameters.Count > 0)
        {
            foreach (var p in def.Parameters)
            {
                if (!parameters.TryGetValue(p.Name, out var value) || string.IsNullOrWhiteSpace(value))
                    continue;
                var unit = string.IsNullOrWhiteSpace(p.Unit) ? "" : $" {p.Unit}";
                parts.Add($"{p.Name}={value}{unit}".Trim());
            }
        }

        if (def.RequiresRange && meterStart.HasValue && meterEnd.HasValue)
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");

        if (parts.Count == 0)
            return title;

        return $"{title} ({string.Join(", ", parts)})";
    }

    private string ResolveCategoryLabel(string key)
    {
        if (_codeIndex.TryGetValue(key, out var def))
            return $"{def.Code}  {def.Title}";
        return key;
    }
}

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

    public IRelayCommand<string> SelectClockCommand { get; }

    public ObservationParameterViewModel(AppProtocol.CodeParameter parameter, string? existingValue)
    {
        Name = parameter.Name;
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

    public bool Validate(out string error)
    {
        error = string.Empty;
        var v = Value?.Trim() ?? string.Empty;

        if (Required && v.Length == 0)
        {
            error = $"Parameter '{Name}' ist erforderlich.";
            return false;
        }

        if (v.Length == 0)
            return true;

        if (IsEnum && AllowedValues.Count > 0 && !AllowedValues.Contains(v, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Parameter '{Name}' hat einen ungueltigen Wert.";
            return false;
        }

        if (IsNumber)
        {
            var normalized = v.Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                error = $"Parameter '{Name}' muss numerisch sein.";
                return false;
            }
        }

        return true;
    }
}
