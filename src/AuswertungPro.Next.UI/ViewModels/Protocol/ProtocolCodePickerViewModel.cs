using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed partial class ProtocolCodePickerViewModel : ObservableObject
{
    private const string AllGroups = "Alle";

    private readonly AppProtocol.ICodeCatalogProvider _catalog;
    private readonly ProtocolEntryVM _entryVm;

    public ObservableCollection<AppProtocol.CodeDefinition> Codes { get; }
    public ObservableCollection<CodeTreeNode> CodeTree { get; } = new();
    public ObservableCollection<string> GroupOptions { get; } = new();
    public ObservableCollection<ParameterValueViewModel> ParameterValues { get; } = new();

    [ObservableProperty] private CodeTreeNode? _selectedNode;
    [ObservableProperty] private AppProtocol.CodeDefinition? _selectedCode;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedGroup = AllGroups;
    [ObservableProperty] private string _meterStartText = string.Empty;
    [ObservableProperty] private string _meterEndText = string.Empty;
    [ObservableProperty] private string _severity = "mid";
    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _validationMessage = string.Empty;
    [ObservableProperty] private string _rangeHint = string.Empty;

    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public IReadOnlyList<string> SeverityOptions { get; } = new[] { "low", "mid", "high" };

    public ProtocolCodePickerViewModel(AppProtocol.ICodeCatalogProvider catalog, ProtocolEntryVM entryVm)
    {
        _catalog = catalog;
        _entryVm = entryVm;

        Codes = new ObservableCollection<AppProtocol.CodeDefinition>(
            _catalog.GetAll().OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase));

        GroupOptions.Add(AllGroups);
        foreach (var group in Codes
                     .Select(c => string.IsNullOrWhiteSpace(c.Group) ? "Unbekannt" : c.Group.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
        {
            GroupOptions.Add(group);
        }

        ApplyCommand = new RelayCommand(() => { });
        CancelCommand = new RelayCommand(() => { });

        InitializeFromEntry();
        RebuildTree();
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildTree();
    }

    partial void OnSelectedGroupChanged(string value)
    {
        RebuildTree();
    }

    partial void OnSelectedCodeChanged(AppProtocol.CodeDefinition? value)
    {
        BuildParameterEditors();
        BuildRangeHint();
    }

    partial void OnSelectedNodeChanged(CodeTreeNode? value)
    {
        if (value is null)
            return;
        if (value.Code is not null)
            SelectedCode = value.Code;
    }

    private void InitializeFromEntry()
    {
        MeterStartText = FormatDouble(_entryVm.MeterStart);
        MeterEndText = FormatDouble(_entryVm.MeterEnd);
        Severity = string.IsNullOrWhiteSpace(_entryVm.Severity) ? "mid" : _entryVm.Severity!;
        CountText = _entryVm.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Notes = _entryVm.CodeNotes ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_entryVm.Code)
            && _catalog.TryGet(_entryVm.Code, out var def))
        {
            SelectedCode = Codes.FirstOrDefault(c => string.Equals(c.Code, def.Code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void BuildParameterEditors()
    {
        ParameterValues.Clear();
        if (SelectedCode is null)
            return;

        var existing = _entryVm.Parameters;
        foreach (var p in SelectedCode.Parameters)
        {
            existing.TryGetValue(p.Name, out var existingValue);
            ParameterValues.Add(new ParameterValueViewModel(p, existingValue));
        }
    }

    private void BuildRangeHint()
    {
        if (SelectedCode is null || !SelectedCode.RequiresRange)
        {
            RangeHint = string.Empty;
            return;
        }

        if (SelectedCode.RangeThresholdM is not null)
        {
            var text = SelectedCode.RangeThresholdText;
            var threshold = SelectedCode.RangeThresholdM.Value.ToString("0.00", CultureInfo.InvariantCulture);
            RangeHint = string.IsNullOrWhiteSpace(text)
                ? $"Streckenschaden: Anfang/Ende erfassen (ab {threshold} m)."
                : $"Streckenschaden: {text}";
        }
        else
        {
            RangeHint = "Streckenschaden: Anfang/Ende erfassen.";
        }
    }

    private void RebuildTree()
    {
        CodeTree.Clear();

        var filtered = Codes.Where(FilterCode).ToList();
        var majorGroups = filtered
            .Select(c => ParseGroup(c.Group).Major)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var major in majorGroups)
        {
            var majorNode = new CodeTreeNode(major);
            var baseGroups = filtered
                .Where(c => string.Equals(ParseGroup(c.Group).Major, major, StringComparison.OrdinalIgnoreCase))
                .Select(c => ParseGroup(c.Group).Base)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var baseGroup in baseGroups)
            {
                var baseNode = new CodeTreeNode(baseGroup);
                var codes = filtered
                    .Where(c => string.Equals(ParseGroup(c.Group).Major, major, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(ParseGroup(c.Group).Base, baseGroup, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var code in codes)
                {
                    var label = $"{code.Code} - {code.Title}";
                    baseNode.Children.Add(new CodeTreeNode(label, code));
                }

                majorNode.Children.Add(baseNode);
            }

            CodeTree.Add(majorNode);
        }

        if (SelectedCode is not null)
        {
            var node = FindNodeByCode(CodeTree, SelectedCode.Code);
            if (node is not null)
                SelectedNode = node;
        }
    }

    private static CodeTreeNode? FindNodeByCode(IEnumerable<CodeTreeNode> nodes, string code)
    {
        foreach (var n in nodes)
        {
            if (n.Code is not null && string.Equals(n.Code.Code, code, StringComparison.OrdinalIgnoreCase))
                return n;
            var child = FindNodeByCode(n.Children, code);
            if (child is not null)
                return child;
        }
        return null;
    }

    private bool FilterCode(AppProtocol.CodeDefinition code)
    {
        var group = string.IsNullOrWhiteSpace(code.Group) ? "Unbekannt" : code.Group.Trim();
        if (!string.IsNullOrWhiteSpace(SelectedGroup)
            && !string.Equals(SelectedGroup, AllGroups, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(group, SelectedGroup, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var q = SearchText.Trim();
        return code.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
               || code.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
               || group.Contains(q, StringComparison.OrdinalIgnoreCase)
               || (code.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static (string Major, string Base) ParseGroup(string? group)
    {
        var g = (group ?? "Unbekannt").Trim();
        if (g.Contains('/'))
        {
            var parts = g.Split('/', 2, StringSplitOptions.TrimEntries);
            return (parts[0], parts.Length > 1 ? parts[1] : parts[0]);
        }
        return (g, g);
    }

    public bool ApplySelection()
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
        if (SelectedCode.RequiresRange && (!meterStart.HasValue || !meterEnd.HasValue))
        {
            ValidationMessage = "Streckenschaden: MeterStart und MeterEnde sind Pflicht.";
            return false;
        }

        if (!TryParseOptionalInt(CountText, out var count))
        {
            ValidationMessage = "Anzahl ist ungueltig.";
            return false;
        }

        if (!SeverityOptions.Contains(Severity))
        {
            ValidationMessage = "Severity muss low, mid oder high sein.";
            return false;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in ParameterValues)
        {
            if (!parameter.Validate(out var parameterError))
            {
                ValidationMessage = parameterError;
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
            Severity,
            count,
            Notes);
        if (SelectedCode.RequiresRange)
            _entryVm.Model.IsStreckenschaden = true;

        if (string.IsNullOrWhiteSpace(_entryVm.Beschreibung))
            _entryVm.Beschreibung = BuildDefaultDescription(SelectedCode, parameters, meterStart, meterEnd);

        return true;
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

    private static bool TryParseOptionalInt(string raw, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    private static string FormatDouble(double? value)
        => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

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
        {
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");
        }

        if (parts.Count == 0)
            return title;

        var suffix = string.Join(", ", parts);
        return $"{title} ({suffix})";
    }
}

public sealed class CodeTreeNode
{
    public string Label { get; }
    public AppProtocol.CodeDefinition? Code { get; }
    public ObservableCollection<CodeTreeNode> Children { get; } = new();

    public CodeTreeNode(string label, AppProtocol.CodeDefinition? code = null)
    {
        Label = label;
        Code = code;
    }
}

public sealed partial class ParameterValueViewModel : ObservableObject
{
    public string Name { get; }
    public string Type { get; }
    public string? Unit { get; }
    public bool Required { get; }
    public IReadOnlyList<string> AllowedValues { get; }

    [ObservableProperty] private string _value = string.Empty;

    public bool IsEnum => string.Equals(Type, "enum", StringComparison.OrdinalIgnoreCase);
    public bool IsNumber => string.Equals(Type, "number", StringComparison.OrdinalIgnoreCase);

    public ParameterValueViewModel(AppProtocol.CodeParameter parameter, string? existingValue)
    {
        Name = parameter.Name;
        Type = parameter.Type;
        Unit = parameter.Unit;
        Required = parameter.Required;
        AllowedValues = parameter.AllowedValues ?? new List<string>();
        Value = existingValue ?? string.Empty;
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
