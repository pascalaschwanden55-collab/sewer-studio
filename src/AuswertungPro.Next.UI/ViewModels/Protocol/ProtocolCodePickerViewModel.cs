using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed partial class ProtocolCodePickerViewModel : ObservableObject
{
    private const string AllGroups = "Alle";

    private readonly AppProtocol.ICodeCatalogProvider _catalog;
    private readonly ProtocolEntryVM _entryVm;

    public ObservableCollection<AppProtocol.CodeDefinition> Codes { get; }
    public ObservableCollection<CodeTreeNode> CodeTree { get; } = new();
    public ObservableCollection<CodeTreeNode> LockedCodeTree { get; } = new();
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

    public IReadOnlyList<string> SeverityOptions { get; } = new[] { "low", "mid", "high" };
    public string SelectedSource => SelectedCode?.Source ?? string.Empty;
    public string SelectedCanonicalCode => SelectedCode?.CanonicalCode ?? string.Empty;
    public string SelectedStandardAnnotation => SelectedCode?.StandardAnnotation ?? string.Empty;

    public ProtocolCodePickerViewModel(AppProtocol.ICodeCatalogProvider catalog, ProtocolEntryVM entryVm)
    {
        _catalog = catalog;
        _entryVm = entryVm;

        Codes = new ObservableCollection<AppProtocol.CodeDefinition>(
            _catalog.GetAll().OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase));

        GroupOptions.Add(AllGroups);
        foreach (var group in Codes
                     .Where(c => c.IsSelectable && !c.IsObservedExtension)
                     .Select(c => AppProtocol.CodeGroupParser.NormalizeGroup(c.Group))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
        {
            GroupOptions.Add(group);
        }

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
        OnPropertyChanged(nameof(SelectedSource));
        OnPropertyChanged(nameof(SelectedCanonicalCode));
        OnPropertyChanged(nameof(SelectedStandardAnnotation));
    }

    partial void OnSelectedNodeChanged(CodeTreeNode? value)
    {
        if (value is null)
            return;
        if (value.Code is not null)
        {
            if (!value.IsSelectable)
            {
                ValidationMessage = "Dieser Code ist im Katalog sichtbar, aber nicht normal auswaehlbar.";
                return;
            }

            SelectedCode = value.Code;
        }
    }

    private void InitializeFromEntry()
    {
        MeterStartText = AppProtocol.ProtocolEntryInputNormalizer.FormatDouble(_entryVm.MeterStart);
        MeterEndText = AppProtocol.ProtocolEntryInputNormalizer.FormatDouble(_entryVm.MeterEnd);
        Severity = string.IsNullOrWhiteSpace(_entryVm.Severity) ? "mid" : _entryVm.Severity!;
        CountText = _entryVm.Count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
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
            var threshold = SelectedCode.RangeThresholdM.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
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
        LockedCodeTree.Clear();

        var filtered = Codes.Where(FilterCode).ToList();
        BuildTree(filtered, CodeTree);

        var locked = Codes.Where(FilterLockedCode).ToList();
        BuildTree(locked, LockedCodeTree);

        if (SelectedCode is not null)
        {
            var node = FindNodeByCode(CodeTree, SelectedCode.Code);
            if (node is not null)
                SelectedNode = node;
        }
    }

    private static void BuildTree(IReadOnlyList<AppProtocol.CodeDefinition> filtered, ObservableCollection<CodeTreeNode> target)
    {
        var majorGroups = filtered
            .Select(c => AppProtocol.CodeGroupParser.ParseGroup(c.Group).Major)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var major in majorGroups)
        {
            var majorNode = new CodeTreeNode(major);
            var baseGroups = filtered
                .Where(c => string.Equals(AppProtocol.CodeGroupParser.ParseGroup(c.Group).Major, major, StringComparison.OrdinalIgnoreCase))
                .Select(c => AppProtocol.CodeGroupParser.ParseGroup(c.Group).Base)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var baseGroup in baseGroups)
            {
                var baseNode = new CodeTreeNode(baseGroup);
                var codes = filtered
                    .Where(c => string.Equals(AppProtocol.CodeGroupParser.ParseGroup(c.Group).Major, major, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(AppProtocol.CodeGroupParser.ParseGroup(c.Group).Base, baseGroup, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var code in codes)
                {
                    var label = $"{code.Code} - {code.Title}";
                    baseNode.Children.Add(new CodeTreeNode(label, code));
                }

                majorNode.Children.Add(baseNode);
            }

            target.Add(majorNode);
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
        if (code.IsObservedExtension || !code.IsSelectable)
            return false;

        return MatchesActiveFilters(code);
    }

    private bool FilterLockedCode(AppProtocol.CodeDefinition code)
    {
        if (!code.IsObservedExtension && code.IsSelectable)
            return false;

        return MatchesActiveFilters(code);
    }

    private bool MatchesActiveFilters(AppProtocol.CodeDefinition code)
    {
        var group = AppProtocol.CodeGroupParser.NormalizeGroup(code.Group);

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
               || (code.Source?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
               || (code.CanonicalCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
               || (code.StandardAnnotation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
               || (code.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public bool ApplySelection()
    {
        ValidationMessage = string.Empty;

        if (SelectedCode is null)
        {
            ValidationMessage = "Bitte einen Code auswaehlen.";
            return false;
        }

        if (SelectedCode.IsObservedExtension || !SelectedCode.IsSelectable)
        {
            ValidationMessage = "Dieser Code ist nicht auswaehlbar.";
            return false;
        }

        if (!AppProtocol.ProtocolEntryInputNormalizer.TryParseOptionalDouble(MeterStartText, out var meterStart))
        {
            ValidationMessage = "MeterStart ist ungueltig.";
            return false;
        }

        if (!AppProtocol.ProtocolEntryInputNormalizer.TryParseOptionalDouble(MeterEndText, out var meterEnd))
        {
            ValidationMessage = "MeterEnd ist ungueltig.";
            return false;
        }
        if (SelectedCode.RequiresRange && (!meterStart.HasValue || !meterEnd.HasValue))
        {
            ValidationMessage = "Streckenschaden: MeterStart und MeterEnde sind Pflicht.";
            return false;
        }

        if (!AppProtocol.ProtocolEntryInputNormalizer.TryParseOptionalInt(CountText, out var count))
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

        AppProtocol.CatalogMetadataWriter.AddCatalogMetadata(parameters, SelectedCode);

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
            _entryVm.Beschreibung = AppProtocol.DefaultDescriptionBuilder.Build(SelectedCode, parameters, meterStart, meterEnd);

        return true;
    }

}

public sealed class CodeTreeNode
{
    public string Label { get; }
    public AppProtocol.CodeDefinition? Code { get; }
    public ObservableCollection<CodeTreeNode> Children { get; } = new();
    public bool IsSelectable => Code is null || (Code.IsSelectable && !Code.IsObservedExtension);
    public bool IsObserved => Code?.IsObservedExtension == true;
    public string Source => Code?.Source ?? string.Empty;
    public string SourceBadgeText => AppProtocol.CodeSourceBadgeFormatter.GetBadgeText(Code?.Source);
    public bool HasSourceBadge => !string.IsNullOrWhiteSpace(SourceBadgeText);
    public string CanonicalCode => Code?.CanonicalCode ?? string.Empty;
    public string StandardAnnotation => Code?.StandardAnnotation ?? string.Empty;

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
        => AppProtocol.ObservationParameterValidator.Validate(Name, Type, Required, AllowedValues, Value, out error);
}
