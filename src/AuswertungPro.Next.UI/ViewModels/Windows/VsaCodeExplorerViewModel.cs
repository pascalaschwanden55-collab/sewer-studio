using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// ViewModel fuer den hierarchischen VSA-Code-Explorer.
/// Navigation: Gruppe (0) -> Hauptcode (1) -> Char1 (2) -> Char2 (3)
/// </summary>
public sealed partial class VsaCodeExplorerViewModel : ObservableObject
{
    // -- Navigation --
    [ObservableProperty] private int _currentLevel;
    [ObservableProperty] private string? _selectedGroupKey;
    [ObservableProperty] private string? _selectedCodeKey;
    [ObservableProperty] private string? _selectedChar1Key;
    [ObservableProperty] private string? _selectedChar2Key;

    // -- Result Panel --
    [ObservableProperty] private string _finalCode = "";
    [ObservableProperty] private string _finalLabel = "";
    [ObservableProperty] private string? _finalSublabel;
    [ObservableProperty] private string? _warnMessage;
    [ObservableProperty] private bool _showResultPanel;

    // -- Quantifizierung --
    [ObservableProperty] private string _q1Value = "";
    [ObservableProperty] private string _q2Value = "";
    [ObservableProperty] private QuantField? _q1Rule;
    [ObservableProperty] private QuantField? _q2Rule;
    [ObservableProperty] private string? _q1Error;
    [ObservableProperty] private string? _q2Error;

    // -- Uhrposition --
    [ObservableProperty] private string _clockMode = "range";
    [ObservableProperty] private string? _clockHint;
    [ObservableProperty] private string _clockVon = "";
    [ObservableProperty] private string _clockBis = "";

    // -- Meter / Zeit --
    [ObservableProperty] private string _meterStart = "";
    [ObservableProperty] private string _meterEnd = "";
    [ObservableProperty] private bool _isStreckenschaden;
    [ObservableProperty] private string _streckenschadenTyp = "";
    [ObservableProperty] private string _zeit = "";

    // -- Zusatzfelder (WinCan-kompatibel) --
    [ObservableProperty] private bool _anRohrverbindung;
    [ObservableProperty] private string _bemerkungen = "";

    // -- Foto --
    public ObservableCollection<string> FotoPaths { get; } = new();
    public ObservableCollection<string> OriginalFotoPaths { get; } = new();

    /// <summary>
    /// Echter Haltungs-/DN-Kontext fuer persoenliche Foto-Goldbeispiele.
    /// Nur der Codiermodus setzt diesen Kontext; allgemeine Katalogdialoge bleiben
    /// dadurch frei von Trainings-Nebenwirkungen.
    /// </summary>
    public PhotoAnnotationSessionContext? PhotoAnnotationContext { get; set; }

    // -- Validation --
    [ObservableProperty] private string _validationMessage = "";
    [ObservableProperty] private bool _canConfirm;

    // -- Breadcrumb --
    public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();

    // -- Tiles (Legacy, Kompatibilitaet) --
    public ObservableCollection<TileItem> CurrentTiles { get; } = new();

    // -- Multi-Column Tiles (WinCan-Stil) --
    public ObservableCollection<TileItem> GroupTiles { get; } = new();
    public ObservableCollection<TileItem> CodeTiles { get; } = new();
    public ObservableCollection<TileItem> Char1Tiles { get; } = new();
    public ObservableCollection<TileItem> Char2Tiles { get; } = new();

    // -- Progress --
    [ObservableProperty] private string? _currentGroupColor;

    // Vorherige Auswahl (fuer Edit-Modus)
    private readonly ProtocolEntry? _existingEntry;
    private readonly IVsaCodeSelectionCatalog _catalog;
    private readonly VsaCodePathResolver _codePathResolver;

    public VsaCodeExplorerViewModel(ProtocolEntry? existingEntry = null,
                                     double? presetMeter = null,
                                     TimeSpan? presetZeit = null,
                                     IVsaCodeSelectionCatalog? catalog = null)
    {
        _existingEntry = existingEntry;
        _catalog = catalog ?? EmptyVsaCodeSelectionCatalog.Instance;
        _codePathResolver = new VsaCodePathResolver(
            _catalog.Groups,
            (cd, c1) => _catalog.GetChar2Options(cd, c1));

        if (presetMeter.HasValue)
            MeterStart = presetMeter.Value.ToString("F2", CultureInfo.InvariantCulture);

        if (presetZeit.HasValue)
            Zeit = presetZeit.Value.TotalHours >= 1
                ? presetZeit.Value.ToString(@"hh\:mm\:ss")
                : presetZeit.Value.ToString(@"mm\:ss");

        if (existingEntry is not null)
        {
            if (existingEntry.MeterStart.HasValue)
                MeterStart = existingEntry.MeterStart.Value.ToString("F2", CultureInfo.InvariantCulture);
            if (existingEntry.MeterEnd.HasValue)
                MeterEnd = existingEntry.MeterEnd.Value.ToString("F2", CultureInfo.InvariantCulture);
            if (existingEntry.Zeit.HasValue)
                Zeit = existingEntry.Zeit.Value.TotalHours >= 1
                    ? existingEntry.Zeit.Value.ToString(@"hh\:mm\:ss")
                    : existingEntry.Zeit.Value.ToString(@"mm\:ss");
            IsStreckenschaden = existingEntry.IsStreckenschaden;

            foreach (var foto in existingEntry.FotoPaths)
                FotoPaths.Add(foto);
            var originalFotoPaths = existingEntry.OriginalFotoPaths ?? [];
            for (var i = 0; i < existingEntry.FotoPaths.Count; i++)
            {
                var originalFoto = originalFotoPaths.Count > i
                                   && !string.IsNullOrWhiteSpace(originalFotoPaths[i])
                    ? originalFotoPaths[i]
                    : existingEntry.FotoPaths[i];
                OriginalFotoPaths.Add(originalFoto);
            }

            // Vorhandene Code-Meta auslesen
            if (existingEntry.CodeMeta is not null)
            {
                var p = existingEntry.CodeMeta.Parameters;
                if (p.TryGetValue("vsa.q1", out var q1)) Q1Value = q1;
                if (p.TryGetValue("vsa.q2", out var q2)) Q2Value = q2;
                if (p.TryGetValue("vsa.uhr.von", out var uv)) ClockVon = uv;
                if (p.TryGetValue("vsa.uhr.bis", out var ub)) ClockBis = ub;
                if (p.TryGetValue("vsa.rohrverbindung", out var rv)) AnRohrverbindung = rv == "1";
                if (p.TryGetValue("vsa.strecke.typ", out var st)) StreckenschadenTyp = st;
                if (p.TryGetValue("vsa.bemerkungen", out var bem)) Bemerkungen = bem;
            }
        }

        if (!TryInitializeFromExistingCode())
            NavigateToLevel(0);
    }

    // =================================================================
    // Navigation
    // =================================================================

    [RelayCommand]
    public void SelectTile(TileItem tile)
    {
        switch (CurrentLevel)
        {
            case 0: // Gruppe gewaehlt
                SelectedGroupKey = tile.Key;
                CurrentGroupColor = tile.GroupColor;
                NavigateToLevel(1);
                break;

            case 1: // Hauptcode gewaehlt
                SelectedCodeKey = tile.Key;
                if (tile.IsFinal)
                {
                    var codeDef = GetCurrentVsaCodeDef();
                    var finalMainCode = codeDef?.FinalCode ?? tile.Key;
                    if (_catalog.IsSelectableCode(finalMainCode))
                        ShowFinalResult(finalMainCode, null, null);
                    return;
                }
                NavigateToLevel(2);
                break;

            case 2: // Char1 gewaehlt
                SelectedChar1Key = tile.Key;
                if (tile.IsFinal)
                {
                    var cd = GetCurrentVsaCodeDef();
                    var prefix = cd?.XPrefix == true ? "X" : "";
                    var finalChar1Code = $"{SelectedCodeKey}{prefix}{tile.Key}";
                    if (_catalog.IsSelectableCode(finalChar1Code))
                        ShowFinalResult(finalChar1Code, tile.Key, null);
                    return;
                }
                NavigateToLevel(3);
                break;

            case 3: // Char2 gewaehlt
                SelectedChar2Key = tile.Key;
                var cd2 = GetCurrentVsaCodeDef();
                var prefix2 = cd2?.XPrefix == true ? "X" : "";
                var finalCode = $"{SelectedCodeKey}{prefix2}{SelectedChar1Key}{tile.Key}";
                if (_catalog.IsSelectableCode(finalCode))
                    ShowFinalResult(finalCode, SelectedChar1Key, tile.Key);
                break;
        }
    }

    [RelayCommand]
    public void NavigateBack()
    {
        if (ShowResultPanel)
        {
            // Zurueck zur letzten Auswahl-Ebene
            ShowResultPanel = false;
            // Aktuelles Level beibehalten, Tiles neu laden
            LoadTilesForCurrentLevel();
            Validate();
            return;
        }

        if (CurrentLevel > 0)
            NavigateToLevel(CurrentLevel - 1);
    }

    [RelayCommand]
    public void NavigateToBreadcrumb(int level)
    {
        ShowResultPanel = false;
        NavigateToLevel(level);
    }

    [RelayCommand]
    public void ResetToMainCodes()
    {
        ShowResultPanel = false;

        // Nur Code-Selektion zuruecksetzen; Positionsdaten/Fotos bleiben erhalten.
        SelectedCodeKey = null;
        SelectedChar1Key = null;
        SelectedChar2Key = null;
        FinalCode = string.Empty;
        FinalLabel = string.Empty;
        FinalSublabel = null;
        WarnMessage = null;
        Q1Rule = null;
        Q2Rule = null;
        Q1Error = null;
        Q2Error = null;

        if (!string.IsNullOrWhiteSpace(SelectedGroupKey)
            && _catalog.Groups.ContainsKey(SelectedGroupKey))
        {
            CurrentLevel = 1;
        }
        else
        {
            SelectedGroupKey = null;
            CurrentGroupColor = null;
            CurrentLevel = 0;
        }

        UpdateBreadcrumb();
        LoadTilesForCurrentLevel();
        Validate();
    }

    private bool TryInitializeFromExistingCode()
    {
        var rawCode = _existingEntry?.CodeMeta?.Code;
        if (string.IsNullOrWhiteSpace(rawCode))
            rawCode = _existingEntry?.Code;

        if (!TryResolveCodePath(rawCode, out var groupKey, out var codeKey, out var c1Key, out var c2Key, out var level, out var finalCode))
            return false;

        if (!string.IsNullOrWhiteSpace(finalCode)
            && !_catalog.IsSelectableCode(finalCode))
        {
            return false;
        }

        SelectedGroupKey = groupKey;
        SelectedCodeKey = codeKey;
        SelectedChar1Key = c1Key;
        SelectedChar2Key = c2Key;
        CurrentGroupColor = _catalog.Groups[groupKey].Color;
        CurrentLevel = level;
        UpdateBreadcrumb();

        if (!string.IsNullOrWhiteSpace(finalCode))
        {
            ShowFinalResult(finalCode, c1Key, c2Key);
        }
        else
        {
            ShowResultPanel = false;
            LoadTilesForCurrentLevel();
            Validate();
        }

        return true;
    }

    private void NavigateToLevel(int level)
    {
        CurrentLevel = level;
        ShowResultPanel = false;

        // Selektion ab Level zuruecksetzen
        if (level <= 0) { SelectedGroupKey = null; SelectedCodeKey = null; SelectedChar1Key = null; SelectedChar2Key = null; CurrentGroupColor = null; }
        if (level <= 1) { SelectedCodeKey = null; SelectedChar1Key = null; SelectedChar2Key = null; }
        if (level <= 2) { SelectedChar1Key = null; SelectedChar2Key = null; }
        if (level <= 3) { SelectedChar2Key = null; }

        UpdateBreadcrumb();
        LoadTilesForCurrentLevel();
        Validate();
    }

    // =================================================================
    // Tiles laden
    // =================================================================

    private void LoadTilesForCurrentLevel()
    {
        // Inkonsistente Zwischenzustaende nach Reset/Bearbeitung abfangen.
        if (CurrentLevel >= 1 && (string.IsNullOrWhiteSpace(SelectedGroupKey) || !_catalog.Groups.ContainsKey(SelectedGroupKey)))
        {
            NavigateToLevel(0);
            return;
        }

        if (CurrentLevel >= 2 && GetCurrentVsaCodeDef() is null)
        {
            NavigateToLevel(1);
            return;
        }

        if (CurrentLevel >= 3 && string.IsNullOrWhiteSpace(SelectedChar1Key))
        {
            NavigateToLevel(2);
            return;
        }

        CurrentTiles.Clear();

        switch (CurrentLevel)
        {
            case 0: // Gruppen (Anordnung kommt aus dem Katalog = ISYBAU-Baum)
                foreach (var (key, grp) in _catalog.Groups)
                    CurrentTiles.Add(ToTileItem(VsaTileDataFactory.ForGroup(key, grp)));
                break;

            case 1: // Hauptcodes
                if (SelectedGroupKey is not null && _catalog.Groups.TryGetValue(SelectedGroupKey, out var group))
                {
                    foreach (var (key, cd) in group.Codes)
                    {
                        if (cd.FinalCode is not null
                            && !_catalog.IsSelectableCode(cd.FinalCode))
                        {
                            continue;
                        }

                        var (q1, _) = _catalog.GetQuantRule(key, null);
                        CurrentTiles.Add(ToTileItem(VsaTileDataFactory.ForCode(
                            key,
                            cd,
                            q1,
                            group.Color,
                            catalogLabel: _catalog.LookupExactLabel(key))));
                    }
                }
                break;

            case 2: // Char1
            {
                var cd = GetCurrentVsaCodeDef();
                if (cd?.Char1 is not null)
                {
                    foreach (var (key, charDef) in cd.Char1)
                    {
                        var hasC2 = _catalog.GetChar2Options(cd, key) is not null;
                        var (q1, _) = _catalog.GetQuantRule(SelectedCodeKey!, key);
                        var fullCode = BuildChar1Code(SelectedCodeKey!, cd.XPrefix, key);
                        if (!hasC2 && !_catalog.IsSelectableCode(fullCode))
                            continue;

                        CurrentTiles.Add(ToTileItem(VsaTileDataFactory.ForChar1(
                            key,
                            charDef,
                            SelectedCodeKey!,
                            cd.XPrefix,
                            hasC2,
                            q1,
                            CurrentGroupColor,
                            catalogLabel: hasC2
                                ? _catalog.LookupNavigationLabel(fullCode)
                                : _catalog.LookupExactLabel(fullCode),
                            parentCatalogLabel: _catalog.LookupExactLabel(SelectedCodeKey!))));
                    }
                }
                break;
            }

            case 3: // Char2
            {
                var cd = GetCurrentVsaCodeDef();
                if (cd is not null && SelectedChar1Key is not null)
                {
                    var c2Options = _catalog.GetChar2Options(cd, SelectedChar1Key);
                    if (c2Options is not null)
                    {
                        foreach (var (key, label) in c2Options)
                        {
                            var invalid = _catalog.IsInvalidCombo(cd, SelectedChar1Key, key);
                            var char1Code = BuildChar1Code(
                                SelectedCodeKey!,
                                cd.XPrefix,
                                SelectedChar1Key);
                            var fullCode = $"{char1Code}{key}";
                            if (!_catalog.IsSelectableCode(fullCode))
                                continue;

                            CurrentTiles.Add(ToTileItem(VsaTileDataFactory.ForChar2(
                                key,
                                label,
                                SelectedCodeKey!,
                                SelectedChar1Key,
                                cd.XPrefix,
                                invalid,
                                CurrentGroupColor,
                                catalogLabel: _catalog.LookupExactLabel(fullCode),
                                parentCatalogLabel: _catalog.LookupNavigationLabel(char1Code))));
                        }
                    }
                }
                break;
            }
        }
    }

    // =================================================================
    // Final Result
    // =================================================================

    private void ShowFinalResult(string code, string? c1Key, string? c2Key)
    {
        if (!_catalog.IsSelectableCode(code))
        {
            ShowResultPanel = false;
            FinalCode = string.Empty;
            FinalLabel = string.Empty;
            FinalSublabel = null;
            Q1Rule = null;
            Q2Rule = null;
            Validate();
            return;
        }

        FinalCode = code;
        var cd = GetCurrentVsaCodeDef();
        var exactCodeDef = _catalog.LookupExactCodeDef(code);
        if (exactCodeDef is not null)
        {
            FinalLabel = exactCodeDef.Label;
            FinalSublabel = null;
        }
        else
        {
            FinalLabel = cd?.Label ?? "";

            if (c1Key is not null
                && cd?.Char1 is not null
                && cd.Char1.TryGetValue(c1Key, out var c1Def))
            {
                FinalSublabel = c1Def.Label;
                if (c2Key is not null)
                {
                    var c2Options = _catalog.GetChar2Options(cd, c1Key);
                    if (c2Options is not null && c2Options.TryGetValue(c2Key, out var c2Label))
                        FinalSublabel = $"{c1Def.Label} - {c2Label}";
                }
            }
            else
            {
                FinalSublabel = null;
            }
        }

        WarnMessage = exactCodeDef?.Warn ?? cd?.Warn;

        // Quant + Clock Regeln aktualisieren
        var (q1, q2) = _catalog.GetQuantRule(SelectedCodeKey ?? code, c1Key);
        Q1Rule = q1;
        Q2Rule = q2;

        var clockRule = _catalog.GetClockRule(SelectedCodeKey ?? code);
        ClockMode = clockRule.Mode;
        ClockHint = clockRule.Hint;

        ShowResultPanel = true;
        CurrentTiles.Clear();
        Validate();
    }

    // =================================================================
    // Breadcrumb
    // =================================================================

    private void UpdateBreadcrumb()
    {
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new BreadcrumbItem("Start", 0));

        if (SelectedGroupKey is not null)
            BreadcrumbItems.Add(new BreadcrumbItem(SelectedGroupKey, 1));
        if (SelectedCodeKey is not null)
            BreadcrumbItems.Add(new BreadcrumbItem(SelectedCodeKey, 2));
        if (SelectedChar1Key is not null)
        {
            var cd = GetCurrentVsaCodeDef();
            var prefix = cd?.XPrefix == true ? "X" : "";
            BreadcrumbItems.Add(new BreadcrumbItem($"{SelectedCodeKey}{prefix}{SelectedChar1Key}", 3));
        }
    }

    // =================================================================
    // Validierung
    // =================================================================

    partial void OnQ1ValueChanged(string value) => Validate();
    partial void OnQ2ValueChanged(string value) => Validate();
    partial void OnClockVonChanged(string value) => Validate();
    partial void OnClockBisChanged(string value) => Validate();
    partial void OnMeterStartChanged(string value) => Validate();
    partial void OnMeterEndChanged(string value) => Validate();
    partial void OnZeitChanged(string value) => Validate();
    partial void OnIsStreckenschadenChanged(bool value) => Validate();

    private void Validate()
    {
        var errors = new List<string>();

        if (!ShowResultPanel)
        {
            CanConfirm = false;
            ValidationMessage = "";
            Q1Error = null;
            Q2Error = null;
            return;
        }

        // Q1 Validierung
        Q1Error = ValidateQuantField(Q1Value, Q1Rule);
        if (Q1Error is not null) errors.Add($"Q1: {Q1Error}");

        // Q2 Validierung
        Q2Error = ValidateQuantField(Q2Value, Q2Rule);
        if (Q2Error is not null) errors.Add($"Q2: {Q2Error}");

        // Meter
        if (!string.IsNullOrWhiteSpace(MeterStart) && !TryParseDouble(MeterStart, out _))
            errors.Add("Meter Start: ungueltige Zahl.");
        if (!string.IsNullOrWhiteSpace(MeterEnd) && !TryParseDouble(MeterEnd, out _))
            errors.Add("Meter Ende: ungueltige Zahl.");
        if (string.IsNullOrWhiteSpace(MeterStart))
            errors.Add("Meter Start ist erforderlich.");

        // Zeit
        if (!string.IsNullOrWhiteSpace(Zeit) && !TryParseTime(Zeit, out _))
            errors.Add("Zeit: ungueltiges Format (mm:ss oder hh:mm:ss).");

        // Clock
        if (ClockMode != "none")
        {
            if (!string.IsNullOrWhiteSpace(ClockVon) && !IsValidClock(ClockVon))
                errors.Add("Uhr von: nur 00 bis 12.");
            if (!string.IsNullOrWhiteSpace(ClockBis) && !IsValidClock(ClockBis))
                errors.Add("Uhr bis: nur 00 bis 12.");
        }

        ValidationMessage = string.Join(Environment.NewLine, errors.Take(5));
        CanConfirm = errors.Count == 0;
    }

    // Delegaten an VsaCodeEntryValidator (reine Logik, kein UI-Bezug)
    private static string? ValidateQuantField(string value, QuantField? rule)
        => VsaCodeEntryValidator.ValidateQuantField(value, rule);

    private static bool TryParseDouble(string raw, out double value)
        => VsaCodeEntryValidator.TryParseDouble(raw, out value);

    private static bool TryParseTime(string raw, out TimeSpan ts)
        => VsaCodeEntryValidator.TryParseTime(raw, out ts);

    private static bool IsValidClock(string raw)
        => VsaCodeEntryValidator.IsValidClock(raw);

    // =================================================================
    // ProtocolEntry bauen
    // =================================================================

    private bool TryResolveCodePath(
        string? rawCode,
        out string groupKey,
        out string codeKey,
        out string? c1Key,
        out string? c2Key,
        out int level,
        out string? finalCode)
        => _codePathResolver.TryResolveCodePath(rawCode, out groupKey, out codeKey, out c1Key, out c2Key, out level, out finalCode);

    public ProtocolEntry BuildProtocolEntry()
    {
        EnsureSelectableFinalCode();

        // Delegiert an ProtocolEntryFromVsaSelectionBuilder (reine Logik, kein UI-Bezug)
        return ProtocolEntryFromVsaSelectionBuilder.Build(
            BuildSelectionInput(),
            _existingEntry);
    }

    /// <summary>
    /// Baut eine noch nicht an den bestehenden Protokolleintrag gebundene Vorschau.
    /// Der asynchrone Foto-Goldspeicher kann damit fehlschlagen, ohne einen editierten
    /// Eintrag bereits vorzeitig zu veraendern.
    /// </summary>
    public ProtocolEntry BuildProtocolEntryPreview()
    {
        EnsureSelectableFinalCode();
        return ProtocolEntryFromVsaSelectionBuilder.Build(BuildSelectionInput());
    }

    private void EnsureSelectableFinalCode()
    {
        if (!_catalog.IsSelectableCode(FinalCode))
        {
            throw new InvalidOperationException(
                $"Der VSA-Code '{FinalCode}' ist im aktiven Katalog nicht zur Auswahl freigegeben.");
        }
    }

    private VsaSelectionInput BuildSelectionInput()
        => new()
        {
            FinalCode = FinalCode,
            FinalLabel = FinalLabel,
            FinalSublabel = FinalSublabel,
            IsStreckenschaden = IsStreckenschaden,
            MeterStart = MeterStart,
            MeterEnd = MeterEnd,
            Zeit = Zeit,
            Q1Value = Q1Value,
            Q2Value = Q2Value,
            ClockMode = ClockMode,
            ClockVon = ClockVon,
            ClockBis = ClockBis,
            AnRohrverbindung = AnRohrverbindung,
            StreckenschadenTyp = StreckenschadenTyp,
            Bemerkungen = Bemerkungen,
            FotoPaths = FotoPaths.ToList(),
            OriginalFotoPaths = OriginalFotoPaths.ToList(),
            CurrentVsaCodeDef = _catalog.LookupExactCodeDef(FinalCode)
                                ?? GetCurrentVsaCodeDef()
        };

    // =================================================================
    // Multi-Column Navigation (WinCan-Stil)
    // =================================================================

    /// <summary>Befuellt alle 4 Spalten-Collections basierend auf aktuellem Zustand.</summary>
    public void PopulateAllColumns()
    {
        PopulateGroupColumn();
        PopulateCodeColumn();
        PopulateChar1Column();
        PopulateChar2Column();
    }

    private void PopulateGroupColumn()
    {
        GroupTiles.Clear();
        foreach (var (key, grp) in _catalog.Groups)
        {
            GroupTiles.Add(ToTileItem(VsaTileDataFactory.ForGroup(
                key, grp, isSelected: string.Equals(key, SelectedGroupKey, StringComparison.Ordinal))));
        }
    }

    private void PopulateCodeColumn()
    {
        CodeTiles.Clear();
        if (SelectedGroupKey is null || !_catalog.Groups.TryGetValue(SelectedGroupKey, out var group))
            return;

        foreach (var (key, cd) in group.Codes)
        {
            if (cd.FinalCode is not null
                && !_catalog.IsSelectableCode(cd.FinalCode))
            {
                continue;
            }

            var (q1, _) = _catalog.GetQuantRule(key, null);
            CodeTiles.Add(ToTileItem(VsaTileDataFactory.ForCode(
                key, cd, q1, group.Color,
                isSelected: string.Equals(key, SelectedCodeKey, StringComparison.Ordinal),
                catalogLabel: _catalog.LookupExactLabel(key))));
        }
    }

    private void PopulateChar1Column()
    {
        Char1Tiles.Clear();
        var cd = GetCurrentVsaCodeDef();
        if (cd?.Char1 is null) return;

        foreach (var (key, charDef) in cd.Char1)
        {
            var hasC2 = _catalog.GetChar2Options(cd, key) is not null;
            var (q1, _) = _catalog.GetQuantRule(SelectedCodeKey!, key);
            var fullCode = BuildChar1Code(SelectedCodeKey!, cd.XPrefix, key);
            if (!hasC2 && !_catalog.IsSelectableCode(fullCode))
                continue;

            Char1Tiles.Add(ToTileItem(VsaTileDataFactory.ForChar1(
                key, charDef, SelectedCodeKey!, cd.XPrefix, hasC2, q1, CurrentGroupColor,
                isSelected: string.Equals(key, SelectedChar1Key, StringComparison.Ordinal),
                catalogLabel: hasC2
                    ? _catalog.LookupNavigationLabel(fullCode)
                    : _catalog.LookupExactLabel(fullCode),
                parentCatalogLabel: _catalog.LookupExactLabel(SelectedCodeKey!))));
        }
    }

    private void PopulateChar2Column()
    {
        Char2Tiles.Clear();
        var cd = GetCurrentVsaCodeDef();
        if (cd is null || SelectedChar1Key is null) return;

        var c2Options = _catalog.GetChar2Options(cd, SelectedChar1Key);
        if (c2Options is null) return;

        foreach (var (key, label) in c2Options)
        {
            var invalid = _catalog.IsInvalidCombo(cd, SelectedChar1Key, key);
            var char1Code = BuildChar1Code(
                SelectedCodeKey!,
                cd.XPrefix,
                SelectedChar1Key);
            var fullCode = $"{char1Code}{key}";
            if (!_catalog.IsSelectableCode(fullCode))
                continue;

            Char2Tiles.Add(ToTileItem(VsaTileDataFactory.ForChar2(
                key, label, SelectedCodeKey!, SelectedChar1Key, cd.XPrefix, invalid, CurrentGroupColor,
                isSelected: string.Equals(key, SelectedChar2Key, StringComparison.Ordinal),
                catalogLabel: _catalog.LookupExactLabel(fullCode),
                parentCatalogLabel: _catalog.LookupNavigationLabel(char1Code))));
        }
    }

    private static string BuildChar1Code(string codeKey, bool xPrefix, string char1Key)
        => $"{codeKey}{(xPrefix ? "X" : "")}{char1Key}";

    /// <summary>Gruppe waehlen (Multi-Column Modus).</summary>
    public void SelectGroup(string key)
    {
        if (string.Equals(SelectedGroupKey, key, StringComparison.Ordinal))
            return;

        SelectedGroupKey = key;
        var grp = _catalog.Groups[key];
        CurrentGroupColor = grp.Color;

        SelectedCodeKey = null;
        SelectedChar1Key = null;
        SelectedChar2Key = null;
        ShowResultPanel = false;
        FinalCode = "";

        PopulateGroupColumn();
        PopulateCodeColumn();
        Char1Tiles.Clear();
        Char2Tiles.Clear();
        UpdateBreadcrumb();
        Validate();
    }

    /// <summary>Hauptcode waehlen (Multi-Column Modus).</summary>
    public void SelectCode(string key)
    {
        if (string.Equals(SelectedCodeKey, key, StringComparison.Ordinal))
            return;

        if (SelectedGroupKey is null
            || !_catalog.Groups.TryGetValue(SelectedGroupKey, out var selectedGroup)
            || !selectedGroup.Codes.TryGetValue(key, out var selectedCodeDef))
        {
            return;
        }

        if (selectedCodeDef.FinalCode is not null
            && !_catalog.IsSelectableCode(selectedCodeDef.FinalCode))
        {
            return;
        }

        SelectedCodeKey = key;
        SelectedChar1Key = null;
        SelectedChar2Key = null;

        PopulateCodeColumn();

        var cd = GetCurrentVsaCodeDef();
        if (cd?.FinalCode is not null || cd?.Char1 is null)
        {
            Char1Tiles.Clear();
            Char2Tiles.Clear();
            ShowFinalResult(cd?.FinalCode ?? key, null, null);
        }
        else
        {
            ShowResultPanel = false;
            FinalCode = "";
            PopulateChar1Column();
            Char2Tiles.Clear();
        }

        UpdateBreadcrumb();
        Validate();
    }

    /// <summary>Char1 waehlen (Multi-Column Modus).</summary>
    public void SelectChar1(string key)
    {
        if (string.Equals(SelectedChar1Key, key, StringComparison.Ordinal))
            return;

        var selectedCodeDef = GetCurrentVsaCodeDef();
        var selectedHasC2 = selectedCodeDef is not null
                            && _catalog.GetChar2Options(selectedCodeDef, key) is not null;
        var selectedFullCode = selectedCodeDef is null
            ? string.Empty
            : BuildChar1Code(SelectedCodeKey!, selectedCodeDef.XPrefix, key);
        if (!selectedHasC2 && !_catalog.IsSelectableCode(selectedFullCode))
            return;

        SelectedChar1Key = key;
        SelectedChar2Key = null;

        PopulateChar1Column();

        var cd = GetCurrentVsaCodeDef();
        var hasC2 = cd is not null && _catalog.GetChar2Options(cd, key) is not null;

        if (!hasC2)
        {
            var prefix = cd?.XPrefix == true ? "X" : "";
            Char2Tiles.Clear();
            ShowFinalResult($"{SelectedCodeKey}{prefix}{key}", key, null);
        }
        else
        {
            ShowResultPanel = false;
            FinalCode = "";
            PopulateChar2Column();
        }

        UpdateBreadcrumb();
        Validate();
    }

    /// <summary>Char2 waehlen (Multi-Column Modus).</summary>
    public void SelectChar2(string key)
    {
        var cd = GetCurrentVsaCodeDef();
        var prefix = cd?.XPrefix == true ? "X" : "";
        var finalCode = $"{SelectedCodeKey}{prefix}{SelectedChar1Key}{key}";
        if (!_catalog.IsSelectableCode(finalCode))
            return;

        SelectedChar2Key = key;
        PopulateChar2Column();

        var invalidCombo = cd is not null
            && SelectedChar1Key is not null
            && _catalog.IsInvalidCombo(cd, SelectedChar1Key, key);
        ShowFinalResult(finalCode, SelectedChar1Key, key);
        if (invalidCombo)
        {
            var invalidMsg = "Kombination im Katalog als ungueltig markiert - manuelle Pruefung erforderlich.";
            WarnMessage = string.IsNullOrWhiteSpace(WarnMessage)
                ? invalidMsg
                : $"{WarnMessage} {invalidMsg}";
        }
        UpdateBreadcrumb();
    }

    // =================================================================
    // Helpers
    // =================================================================

    private VsaCodeDef? GetCurrentVsaCodeDef()
    {
        if (SelectedGroupKey is null || SelectedCodeKey is null) return null;
        if (!_catalog.Groups.TryGetValue(SelectedGroupKey, out var grp)) return null;
        return grp.Codes.TryGetValue(SelectedCodeKey, out var cd) ? cd : null;
    }

    public string? LookupCodeLabel(string code)
        => _catalog.IsSelectableCode(code)
            ? _catalog.LookupExactLabel(code)
            : null;

    /// <summary>Konvertiert VsaTileData (Application-Schicht) in das UI-interne TileItem.</summary>
    private static TileItem ToTileItem(VsaTileData data) => new()
    {
        Key = data.Key,
        Label = data.Label,
        Description = data.Description,
        BadgeText = data.BadgeText,
        BadgeColor = data.BadgeColor,
        IsInvalid = data.IsInvalid,
        IsFinal = data.IsFinal,
        IsSteuer = data.IsSteuer,
        GroupColor = data.GroupColor,
        Icon = data.Icon,
        IsSelected = data.IsSelected
    };

    /// <summary>Stufen-Labels fuer den Fortschrittsbalken.</summary>
    public static readonly string[] LevelLabels = { "Gruppe", "Hauptcode", "Char 1", "Char 2" };
}

public sealed record BreadcrumbItem(string Label, int Level);
