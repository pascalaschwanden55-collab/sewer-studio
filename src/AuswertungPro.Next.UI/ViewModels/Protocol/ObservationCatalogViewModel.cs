using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed partial class ObservationCatalogViewModel : ObservableObject
{
    private readonly AppProtocol.ICodeCatalogProvider _catalog;
    private readonly ProtocolEntryVM _entryVm;
    private readonly IProtocolAiService? _aiService;
    private readonly string? _haltungId;
    private readonly string? _videoPathAbs;
    private readonly string? _projectFolderAbs;
    private readonly CategoryNode _root = new("Root", "Root");
    private readonly Dictionary<string, AppProtocol.CodeDefinition> _codeIndex;
    private readonly List<AppProtocol.CodeDefinition> _allCodes;
    private bool _isNavigating;

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
        if (_isNavigating)
            return;
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return;

        _isNavigating = true;
        try
        {
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
        finally
        {
            _isNavigating = false;
        }
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

    // SN EN 13508-2 Hauptkategorie-Labels (2-Zeichen-Prefix)
    private static readonly Dictionary<string, string> MainCategoryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = "Aenderungen der Grundlagen",
        ["BA"] = "Baulicher Zustand Leitung",
        ["BB"] = "Betrieblicher Zustand Leitung",
        ["BC"] = "Bestandsaufnahme Leitung",
        ["BD"] = "Weitere Codes Leitung",
    };

    private void BuildTree()
    {
        foreach (var code in _allCodes)
        {
            // Wenn categoryPath explizit gesetzt ist, diesen verwenden
            if (code.CategoryPath is { Count: > 0 })
            {
                var node = _root;
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
                node.Codes.Add(code);
                continue;
            }

            // Automatische Baumstruktur aus Code-Prefix (SN EN 13508-2)
            var codeStr = (code.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (codeStr.Length < 3)
            {
                _root.Codes.Add(code);
                continue;
            }

            // Ebene 1: Hauptkategorie (2 Zeichen, z.B. "BA", "BB", "BC")
            var mainPrefix = codeStr.Substring(0, 2);
            if (!_root.Children.TryGetValue(mainPrefix, out var mainNode))
            {
                var mainLabel = MainCategoryLabels.TryGetValue(mainPrefix, out var label)
                    ? label
                    : (code.Group ?? mainPrefix);
                mainNode = new CategoryNode(mainPrefix, mainLabel);
                _root.Children[mainPrefix] = mainNode;
            }

            // Ebene 2: Unterkategorie (3 Zeichen, z.B. "BBA", "BBC")
            var subPrefix = codeStr.Substring(0, 3);
            if (!mainNode.Children.TryGetValue(subPrefix, out var subNode))
            {
                var subLabel = ResolveSubCategoryLabel(subPrefix);
                subNode = new CategoryNode(subPrefix, subLabel);
                mainNode.Children[subPrefix] = subNode;
            }

            // Code als Blatt unter der Unterkategorie
            subNode.Codes.Add(code);
        }
    }

    private string ResolveSubCategoryLabel(string prefix)
    {
        // Versuche den 3-Zeichen-Prefix als eigenstaendigen Code zu finden
        if (_codeIndex.TryGetValue(prefix, out var def))
            return $"{def.Code}  {def.Title}";

        // Sonst: finde den ersten Code mit diesem Prefix und nutze dessen Gruppen-Info
        var first = _allCodes.FirstOrDefault(c =>
            c.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (first is not null && !string.IsNullOrWhiteSpace(first.Title))
        {
            // Extrahiere einen kurzen Label aus dem Titel des ersten Codes
            var groupPart = ExtractSubGroupName(prefix, first);
            if (!string.IsNullOrWhiteSpace(groupPart))
                return $"{prefix}  {groupPart}";
        }

        return prefix;
    }

    private static string ExtractSubGroupName(string prefix, AppProtocol.CodeDefinition firstCode)
    {
        // SN EN 13508-2 Unterkategorie-Labels
        // VSA-Richtlinie 2018, SN EN 13508-2 konforme Unterkategorie-Labels
        var subLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // BA - Baulicher Zustand Leitung
            ["BAA"] = "Verformung",
            ["BAB"] = "Risse",
            ["BAC"] = "Leitungsbruch / Einsturz",
            ["BAD"] = "Defektes Mauerwerk",
            ["BAE"] = "Fehlender Moertel",
            ["BAF"] = "Oberflaechenschaden",
            ["BAG"] = "Einragender Anschluss",
            ["BAH"] = "Schadhafter Anschluss",
            ["BAI"] = "Einragendes Dichtungsmaterial",
            ["BAJ"] = "Verschobene Rohrverbindung",
            ["BAK"] = "Feststellung Innenauskleidung",
            ["BAL"] = "Schadhafte Reparatur",
            ["BAM"] = "Schadhafte Schweissnaht",
            ["BAN"] = "Poroese Leitung",
            ["BAO"] = "Boden sichtbar",
            ["BAP"] = "Hohlraum sichtbar",
            // BB - Betrieblicher Zustand Leitung
            ["BBA"] = "Wurzeln",
            ["BBB"] = "Anhaftende Stoffe",
            ["BBC"] = "Ablagerungen",
            ["BBD"] = "Eindringen von Bodenmaterial",
            ["BBE"] = "Andere Hindernisse",
            ["BBF"] = "Infiltration",
            ["BBG"] = "Exfiltration",
            ["BBH"] = "Ungeziefer",
            // BC - Bestandsaufnahme Leitung
            ["BCA"] = "Seitlicher Anschluss",
            ["BCB"] = "Punktuelle Reparatur",
            ["BCC"] = "Bogen in der Leitung",
            ["BCD"] = "Anfangsknoten",
            ["BCE"] = "Endknoten",
            ["BCF"] = "Rohrmaterial Anschlussleitung",
            ["BCG"] = "Anschlussleitung",
            // BD - Weitere Codes Leitung
            ["BDA"] = "Allgemeines Foto",
            ["BDB"] = "Allgemeine Anmerkung",
            ["BDC"] = "Abbruch der Inspektion",
            ["BDD"] = "Wasserspiegel",
            ["BDE"] = "Abwasserzufluss / Fehlanschluss",
            ["BDF"] = "Gefaehrliche Atmosphaere",
            ["BDG"] = "Keine Sicht",
            // AE - Aenderungen der Grundlageninformationen
            ["AEC"] = "Profilwechsel",
            ["AED"] = "Materialwechsel",
            ["AEF"] = "Neue Baulaenge",
        };

        if (subLabels.TryGetValue(prefix, out var label))
            return label;

        // Fallback: Titel des ersten Codes kuerzen
        var title = firstCode.Title ?? string.Empty;
        if (title.Contains(':'))
            return title.Substring(0, title.IndexOf(':')).Trim();

        return title;
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

    private List<string> BuildPathToCode(AppProtocol.CodeDefinition code)
    {
        // Wenn categoryPath explizit gesetzt ist, diesen verwenden
        if (code.CategoryPath is { Count: > 0 })
            return code.CategoryPath;

        // Automatisch aus Code-Prefix ableiten
        var codeStr = (code.Code ?? string.Empty).Trim().ToUpperInvariant();
        var path = new List<string>();
        if (codeStr.Length >= 2)
            path.Add(codeStr.Substring(0, 2));
        if (codeStr.Length >= 3)
            path.Add(codeStr.Substring(0, 3));
        return path;
    }

    private void MergeVsaParameters(Dictionary<string, string> parameters)
    {
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters[key] = value.Trim();
        }

        void AddAliases(string? value, params string[] keys)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            foreach (var key in keys)
                Add(key, value);
        }

        AddAliases(VsaDistanz, "vsa.distanz", "Distance");
        AddAliases(VsaVideo, "vsa.video", "TimeCtr");
        AddAliases(VsaUhrVon, "vsa.uhr.von", "ClockPos1");
        AddAliases(VsaUhrBis, "vsa.uhr.bis", "ClockPos2");
        AddAliases(VsaQ1, "vsa.q1", "Q1", "Quantifizierung1");
        AddAliases(VsaQ2, "vsa.q2", "Q2", "Quantifizierung2");
        Add("vsa.strecke", VsaStrecke);
        if (VsaVerbindung)
            parameters["vsa.verbindung"] = "ja";
        Add("vsa.ansicht", VsaAnsicht);
        Add("vsa.ez", VsaEz);
        Add("vsa.schachtbereich", VsaSchachtbereich);
        Add("vsa.anmerkung", VsaAnmerkung);
    }

    private static readonly Regex ClockFromDescriptionRegex = new(
        @"von\s+(\d{1,2})\s*Uhr\s+bis\s+(\d{1,2})\s*Uhr",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuantFromDescriptionRegex = new(
        @"(\d+(?:[.,]\d+)?)\s*%",
        RegexOptions.Compiled);

    private void TryParseClockValuesFromDescription(ProtocolEntry entry)
    {
        var desc = entry.Beschreibung;
        if (string.IsNullOrWhiteSpace(desc))
            return;

        // Uhrzeit-Werte: "von 8 Uhr bis 3 Uhr"
        if (string.IsNullOrWhiteSpace(VsaUhrVon) || string.IsNullOrWhiteSpace(VsaUhrBis))
        {
            var match = ClockFromDescriptionRegex.Match(desc);
            if (match.Success)
            {
                if (string.IsNullOrWhiteSpace(VsaUhrVon))
                    VsaUhrVon = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(VsaUhrBis))
                    VsaUhrBis = match.Groups[2].Value.Trim();
            }
        }

        // Quantifizierung: "1%" oder "10%"
        if (string.IsNullOrWhiteSpace(VsaQ1))
        {
            var matches = QuantFromDescriptionRegex.Matches(desc);
            if (matches.Count > 0)
                VsaQ1 = matches[0].Groups[1].Value.Replace(',', '.').Trim();
            if (matches.Count > 1 && string.IsNullOrWhiteSpace(VsaQ2))
                VsaQ2 = matches[1].Groups[1].Value.Replace(',', '.').Trim();
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
    {
        var title = def.Title ?? string.Empty;
        var parts = new List<string>();

        if (parameters is not null && parameters.Count > 0)
        {
            // Code-spezifische Parameter (Hoehe, Breite, Kruemmungswinkel, etc.)
            foreach (var p in def.Parameters)
            {
                string? value = null;
                var key = p.DataKey ?? p.Name;
                if (!parameters.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
                {
                    if (!parameters.TryGetValue(p.Name, out value) || string.IsNullOrWhiteSpace(value))
                        continue;
                }
                var unit = string.IsNullOrWhiteSpace(p.Unit) ? "" : $"{p.Unit}";
                parts.Add($"{value}{unit}".Trim());
            }

            // Uhrzeiten (von/bis)
            var uhrVon = GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1");
            var uhrBis = GetFirstParameter(parameters, "vsa.uhr.bis", "ClockPos2");
            if (!string.IsNullOrWhiteSpace(uhrVon) && !string.IsNullOrWhiteSpace(uhrBis))
                parts.Add($"von {uhrVon} Uhr bis {uhrBis} Uhr");
            else if (!string.IsNullOrWhiteSpace(uhrVon))
                parts.Add($"bei {uhrVon} Uhr");

            // Quantifizierung
            var q1 = GetFirstParameter(parameters, "vsa.q1", "Q1", "Quantifizierung1");
            if (!string.IsNullOrWhiteSpace(q1))
                parts.Add($"{q1}%");
            var q2 = GetFirstParameter(parameters, "vsa.q2", "Q2", "Quantifizierung2");
            if (!string.IsNullOrWhiteSpace(q2))
                parts.Add($"{q2}%");
        }

        if (def.RequiresRange && meterStart.HasValue && meterEnd.HasValue)
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");

        if (parts.Count == 0)
            return title;

        return $"{title}: {string.Join(", ", parts)}";
    }

    private static string? GetFirstParameter(IReadOnlyDictionary<string, string> parameters, params string[] keys)
    {
        if (parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!parameters.TryGetValue(key, out var value))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            return value.Trim();
        }

        return null;
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
