using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed partial class ObservationCatalogViewModel : ObservableObject
{
    private readonly AppProtocol.ICodeCatalogProvider _catalog;
    private readonly ProtocolEntryVM _entryVm;
    private readonly IProtocolAiService? _aiService;
    private readonly string? _haltungId;
    private readonly string? _videoPathAbs;
    private readonly string? _projectFolderAbs;
    private readonly CatalogTreeNode _root = new("Root", "Root");
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
    [ObservableProperty] private bool _isKiBusy;
    [ObservableProperty] private string _kiStatus = string.Empty;

    public bool HasKiService => _aiService is not null;

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

    public ObservationCatalogViewModel(
        AppProtocol.ICodeCatalogProvider catalog,
        ProtocolEntry entry,
        IProtocolAiService? aiService = null,
        string? haltungId = null,
        string? videoPathAbs = null,
        string? projectFolderAbs = null)
    {
        _catalog = catalog;
        _entryVm = new ProtocolEntryVM(entry);
        _aiService = aiService;
        _haltungId = haltungId;
        _videoPathAbs = videoPathAbs;
        _projectFolderAbs = projectFolderAbs;

        _allCodes = _catalog.GetAll().OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToList();
        BuildTree();
        InitializeColumns();
        ApplySearchFilter();

        MeterStartText = FormatDouble(_entryVm.MeterStart);
        MeterEndText = FormatDouble(_entryVm.MeterEnd);
        ZeitText = _entryVm.Zeit is null ? string.Empty : FormatTime(_entryVm.Zeit.Value);
        MpegText = _entryVm.Mpeg ?? string.Empty;
        IsStreckenschaden = _entryVm.Model.IsStreckenschaden;

        // Fallback: Uhr-Werte aus Beschreibungstext parsen falls nicht in Parameters
        TryParseClockValuesFromDescription(entry);

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

    public async Task SuggestCodeWithKiAsync(CancellationToken ct = default)
    {
        if (_aiService is null)
        {
            KiStatus = "KI nicht verfügbar.";
            return;
        }

        if (IsKiBusy)
            return;

        IsKiBusy = true;
        KiStatus = "KI-Vorschlag wird berechnet...";

        try
        {
            var allowedCodes = _catalog.AllowedCodes();
            if (allowedCodes.Count == 0)
            {
                KiStatus = "Code-Katalog ist leer.";
                return;
            }

            var input = new AiInput(
                ProjectFolderAbs: _projectFolderAbs ?? string.Empty,
                HaltungId: string.IsNullOrWhiteSpace(_haltungId) ? null : _haltungId,
                Meter: _entryVm.MeterStart ?? _entryVm.MeterEnd,
                ExistingCode: string.IsNullOrWhiteSpace(_entryVm.Code) ? null : _entryVm.Code,
                ExistingText: string.IsNullOrWhiteSpace(_entryVm.Beschreibung) ? null : _entryVm.Beschreibung,
                AllowedCodes: allowedCodes,
                VideoPathAbs: ResolveExistingPath(_videoPathAbs),
                Zeit: _entryVm.Zeit,
                ImagePathsAbs: ResolveImagePaths(_entryVm.Model.FotoPaths));

            var suggestion = await _aiService.SuggestAsync(input, ct);
            if (suggestion is null)
            {
                KiStatus = "Kein KI-Vorschlag verfügbar.";
                return;
            }

            _entryVm.ApplyAiSuggestionToModelAndVm(suggestion);

            var suggestedCode = suggestion.SuggestedCode?.Trim();
            if (!string.IsNullOrWhiteSpace(suggestedCode)
                && _catalog.TryGet(suggestedCode, out var def))
            {
                SelectCode(def, syncColumns: true);
                KiStatus = $"KI-Vorschlag übernommen: {suggestedCode} ({suggestion.Confidence:P0})";
            }
            else if (!string.IsNullOrWhiteSpace(suggestedCode))
            {
                KiStatus = $"KI-Code '{suggestedCode}' ist nicht im Katalog.";
            }
            else
            {
                KiStatus = $"Kein Code vorgeschlagen ({suggestion.Confidence:P0}).";
            }

            if (!string.IsNullOrWhiteSpace(suggestion.ReasonShort))
                ValidationMessage = "KI-Hinweis: " + Truncate(suggestion.ReasonShort, 220);
        }
        catch (OperationCanceledException)
        {
            KiStatus = "KI-Vorschlag abgebrochen.";
        }
        catch (Exception ex)
        {
            KiStatus = $"KI-Fehler: {ex.Message}";
        }
        finally
        {
            IsKiBusy = false;
        }
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

        // Fallback: Wenn keine m+/m- Eingabe vorhanden ist, VSA-Distanz als MeterStart verwenden.
        if (!meterStart.HasValue && TryParseOptionalDouble(VsaDistanz ?? string.Empty, out var vsaDistanz) && vsaDistanz.HasValue)
        {
            meterStart = vsaDistanz;
            if (!meterEnd.HasValue)
                meterEnd = vsaDistanz;
        }
        else if (!meterEnd.HasValue && meterStart.HasValue && !IsStreckenschaden)
        {
            meterEnd = meterStart;
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
            {
                // DataKey als Schluessel verwenden (kompatibel mit WinCan-Import: Q1, Q2, Char1, ...)
                var key = parameter.DataKey ?? parameter.Name;
                parameters[key] = parameter.Value.Trim();
            }
        }

        // VSA-KEK Werte ins Parameter-Dictionary aufnehmen,
        // damit ApplyCodeSelection() sie nicht ueberschreibt
        MergeVsaParameters(parameters);

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

        // Beschreibung immer neu generieren (Code + Parameter)
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
        // Delegiert an VsaCatalogTreeBuilder; Baum in _root uebertragen
        var built = VsaCatalogTreeBuilder.BuildTree(_allCodes, _catalog);
        foreach (var kv in built.Children)
            _root.Children[kv.Key] = kv.Value;
        foreach (var code in built.Codes)
            _root.Codes.Add(code);
    }

    private void InitializeColumns()
    {
        Columns.Clear();
        Columns.Add(new CatalogColumnViewModel(0, _root.Children.Values.Select(CatalogItem.FromNode)));
    }

    private void SyncColumnsToCode(AppProtocol.CodeDefinition code)
    {
        InitializeColumns();

        // Bestimme den Pfad zum Code im Baum
        var path = BuildPathToCode(code);
        var node = _root;

        for (var i = 0; i < path.Count; i++)
        {
            var key = path[i];
            if (!node.Children.TryGetValue(key, out var child))
                break;

            if (i < Columns.Count)
            {
                var item = Columns[i].Items.FirstOrDefault(x => x.Node == child);
                if (item is not null)
                    Columns[i].SelectedItem = item;
            }

            node = child;
            while (Columns.Count > i + 1)
                Columns.RemoveAt(Columns.Count - 1);

            if (node.Children.Count > 0)
                Columns.Add(new CatalogColumnViewModel(i + 1, node.Children.Values.Select(CatalogItem.FromNode)));
            else if (node.Codes.Count > 0)
                Columns.Add(new CatalogColumnViewModel(i + 1, node.Codes.Select(CatalogItem.FromCode)));
        }

        // Selektiere den Code in der letzten Spalte
        var lastCol = Columns.LastOrDefault();
        if (lastCol is not null)
        {
            var codeItem = lastCol.Items.FirstOrDefault(x => x.Code != null
                                                             && string.Equals(x.Code.Code, code.Code, StringComparison.OrdinalIgnoreCase));
            if (codeItem is not null)
                lastCol.SelectedItem = codeItem;
        }
    }

    private static List<string> BuildPathToCode(AppProtocol.CodeDefinition code)
        => VsaCatalogTreeBuilder.BuildPathToCode(code);

    private void MergeVsaParameters(Dictionary<string, string> parameters)
        => VsaParameterMerger.Merge(
            parameters,
            vsaDistanz: VsaDistanz,
            vsaVideo: VsaVideo,
            vsaUhrVon: VsaUhrVon,
            vsaUhrBis: VsaUhrBis,
            vsaQ1: VsaQ1,
            vsaQ2: VsaQ2,
            vsaStrecke: VsaStrecke,
            vsaVerbindung: VsaVerbindung,
            vsaAnsicht: VsaAnsicht,
            vsaEz: VsaEz,
            vsaSchachtbereich: VsaSchachtbereich,
            vsaAnmerkung: VsaAnmerkung);

    private void TryParseClockValuesFromDescription(ProtocolEntry entry)
    {
        // Fallback: Uhr-Werte aus Beschreibungstext parsen (delegiert an DescriptionClockQuantParser)
        var uhrVon = VsaUhrVon;
        var uhrBis = VsaUhrBis;
        var q1 = VsaQ1;
        var q2 = VsaQ2;
        DescriptionClockQuantParser.TryParseFromDescription(entry.Beschreibung, ref uhrVon, ref uhrBis, ref q1, ref q2);
        VsaUhrVon = uhrVon;
        VsaUhrBis = uhrBis;
        VsaQ1 = q1;
        VsaQ2 = q2;
    }

    private void BuildParameters()
    {
        Parameters.Clear();
        if (SelectedCode is null)
            return;

        var existing = _entryVm.Parameters;
        foreach (var p in SelectedCode.Parameters)
        {
            // Wert mit DataKey (WinCan-Feldname) oder Name suchen
            string? existingValue = null;
            if (!string.IsNullOrWhiteSpace(p.DataKey))
                existing.TryGetValue(p.DataKey, out existingValue);
            if (existingValue is null)
                existing.TryGetValue(p.Name, out existingValue);
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

    private List<string>? ResolveImagePaths(IReadOnlyList<string> rawPaths)
    {
        if (rawPaths.Count == 0)
            return null;

        var list = new List<string>();
        foreach (var raw in rawPaths)
        {
            var resolved = ResolveExistingPath(raw);
            if (!string.IsNullOrWhiteSpace(resolved))
                list.Add(resolved);
        }

        return list.Count > 0 ? list : null;
    }

    private string? ResolveExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed))
            return File.Exists(trimmed) ? trimmed : null;

        if (!string.IsNullOrWhiteSpace(_projectFolderAbs))
        {
            var combined = Path.Combine(_projectFolderAbs, trimmed);
            if (File.Exists(combined))
                return combined;
        }

        return File.Exists(trimmed) ? trimmed : null;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;

        return text[..Math.Max(0, maxLength - 3)] + "...";
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
        => ProtocolDescriptionBuilder.Build(def, parameters, meterStart, meterEnd);
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
    public CatalogTreeNode? Node { get; }
    public AppProtocol.CodeDefinition? Code { get; }

    private CatalogItem(string label, CatalogTreeNode? node, AppProtocol.CodeDefinition? code)
    {
        Label = label;
        Node = node;
        Code = code;
    }

    public static CatalogItem FromNode(CatalogTreeNode node) => new(node.Label, node, null);

    public static CatalogItem FromCode(AppProtocol.CodeDefinition code)
        => new($"{code.Code}  {code.Title}", null, code);
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
        // Delegiert an ObservationParameterValidator (reine Logik ohne UI-Abhaengigkeiten)
        var ok = ObservationParameterValidator.Validate(
            Name, Type, Required, AllowedValues, Value, out error);
        IsValid = ok;
        ErrorMessage = ok ? string.Empty : error;
        return ok;
    }
}
