using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services.CodeCatalog;
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

    public VsaCodeExplorerViewModel(ProtocolEntry? existingEntry = null,
                                     double? presetMeter = null,
                                     TimeSpan? presetZeit = null)
    {
        _existingEntry = existingEntry;

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
        if (tile.IsInvalid) return;

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
                    ShowFinalResult(tile.Key, null, null);
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
                    ShowFinalResult($"{SelectedCodeKey}{prefix}{tile.Key}", tile.Key, null);
                    return;
                }
                NavigateToLevel(3);
                break;

            case 3: // Char2 gewaehlt
                SelectedChar2Key = tile.Key;
                var cd2 = GetCurrentVsaCodeDef();
                var prefix2 = cd2?.XPrefix == true ? "X" : "";
                ShowFinalResult($"{SelectedCodeKey}{prefix2}{SelectedChar1Key}{tile.Key}", SelectedChar1Key, tile.Key);
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
            && VsaCodeTree.Groups.ContainsKey(SelectedGroupKey))
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

        SelectedGroupKey = groupKey;
        SelectedCodeKey = codeKey;
        SelectedChar1Key = c1Key;
        SelectedChar2Key = c2Key;
        CurrentGroupColor = VsaCodeTree.Groups[groupKey].Color;
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
        if (CurrentLevel >= 1 && (string.IsNullOrWhiteSpace(SelectedGroupKey) || !VsaCodeTree.Groups.ContainsKey(SelectedGroupKey)))
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
            case 0: // Gruppen
                foreach (var (key, grp) in VsaCodeTree.Groups)
                {
                    CurrentTiles.Add(new TileItem
                    {
                        Key = key, Label = key, Description = grp.Label, GroupColor = grp.Color, Icon = grp.Icon
                    });
                }
                break;

            case 1: // Hauptcodes
                if (SelectedGroupKey is not null && VsaCodeTree.Groups.TryGetValue(SelectedGroupKey, out var group))
                {
                    foreach (var (key, cd) in group.Codes)
                    {
                        var (q1, _) = VsaCodeTree.GetQuantRule(key, null);
                        var badge = q1 is not null ? q1.Einheit ?? "Q" : null;
                        var badgeColor = q1 is { Pflicht: "P" } ? "#DC2626" : q1 is not null ? "#F59E0B" : null;

                        CurrentTiles.Add(new TileItem
                        {
                            Key = key, Label = key, Description = cd.Label,
                            IsFinal = cd.FinalCode is not null,
                            IsSteuer = cd.IsSteuer,
                            BadgeText = badge, BadgeColor = badgeColor,
                            GroupColor = group.Color
                        });
                    }
                }
                break;

            case 2: // Char1
            {
                var cd = GetCurrentVsaCodeDef();
                if (cd?.Char1 is not null)
                {
                    var grpColor = CurrentGroupColor;
                    foreach (var (key, charDef) in cd.Char1)
                    {
                        var hasC2 = VsaCodeTree.GetChar2Options(cd, key) is not null;
                        var prefix = cd.XPrefix ? "X" : "";
                        var fullCode = $"{SelectedCodeKey}{prefix}{key}";
                        var (q1, _) = VsaCodeTree.GetQuantRule(SelectedCodeKey!, key);

                        CurrentTiles.Add(new TileItem
                        {
                            Key = key, Label = fullCode, Description = charDef.Label,
                            IsFinal = !hasC2,
                            BadgeText = q1?.Einheit, BadgeColor = q1 is { Pflicht: "P" } ? "#DC2626" : q1 is not null ? "#F59E0B" : null,
                            GroupColor = grpColor
                        });
                    }
                }
                break;
            }

            case 3: // Char2
            {
                var cd = GetCurrentVsaCodeDef();
                if (cd is not null && SelectedChar1Key is not null)
                {
                    var c2Options = VsaCodeTree.GetChar2Options(cd, SelectedChar1Key);
                    if (c2Options is not null)
                    {
                        var prefix = cd.XPrefix ? "X" : "";
                        foreach (var (key, label) in c2Options)
                        {
                            var invalid = VsaCodeTree.IsInvalidCombo(cd, SelectedChar1Key, key);
                            var fullCode = $"{SelectedCodeKey}{prefix}{SelectedChar1Key}{key}";
                            CurrentTiles.Add(new TileItem
                            {
                                Key = key, Label = fullCode, Description = label,
                                IsFinal = true, IsInvalid = invalid,
                                GroupColor = CurrentGroupColor
                            });
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
        FinalCode = code;
        var cd = GetCurrentVsaCodeDef();
        FinalLabel = cd?.Label ?? "";

        if (c1Key is not null && cd?.Char1 is not null && cd.Char1.TryGetValue(c1Key, out var c1Def))
        {
            FinalSublabel = c1Def.Label;
            if (c2Key is not null)
            {
                var c2Options = VsaCodeTree.GetChar2Options(cd, c1Key);
                if (c2Options is not null && c2Options.TryGetValue(c2Key, out var c2Label))
                    FinalSublabel = $"{c1Def.Label} - {c2Label}";
            }
        }
        else
        {
            FinalSublabel = null;
        }

        WarnMessage = cd?.Warn;

        // Quant + Clock Regeln aktualisieren
        var (q1, q2) = VsaCodeTree.GetQuantRule(SelectedCodeKey ?? code, c1Key);
        Q1Rule = q1;
        Q2Rule = q2;

        var clockRule = VsaCodeTree.GetClockRule(SelectedCodeKey ?? code);
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

    private static string? ValidateQuantField(string value, QuantField? rule)
    {
        if (rule is null) return null;

        if (string.IsNullOrWhiteSpace(value))
            return rule.Pflicht == "P" ? "Pflichtfeld" : null;

        if (!TryParseDouble(value, out var num))
            return "Ungueltige Zahl";

        if (rule.Min.HasValue && num < rule.Min.Value)
            return $">= {rule.Min.Value}";

        if (rule.Max.HasValue && num > rule.Max.Value)
            return $"<= {rule.Max.Value}";

        return null;
    }

    private static bool TryParseDouble(string raw, out double value)
    {
        var normalized = raw.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseTime(string raw, out TimeSpan ts)
    {
        ts = default;
        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out ts))
            return true;
        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts);
    }

    private static bool IsValidClock(string raw)
    {
        var text = raw.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 12;
    }

    private static string? NormalizeClockValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v < 0 || v > 12)
            return null;

        return v.ToString("00", CultureInfo.InvariantCulture);
    }

    // =================================================================
    // ProtocolEntry bauen
    // =================================================================

    private static bool TryResolveCodePath(
        string? rawCode,
        out string groupKey,
        out string codeKey,
        out string? c1Key,
        out string? c2Key,
        out int level,
        out string? finalCode)
    {
        groupKey = string.Empty;
        codeKey = string.Empty;
        c1Key = null;
        c2Key = null;
        level = 0;
        finalCode = null;

        var normalized = NormalizeCode(rawCode);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        foreach (var (grpKey, group) in VsaCodeTree.Groups)
        {
            foreach (var (candidateCodeKey, codeDef) in group.Codes)
            {
                if (!normalized.StartsWith(candidateCodeKey, StringComparison.Ordinal))
                    continue;

                var rest = normalized[candidateCodeKey.Length..];

                // Endcode ohne Char1/Char2.
                if (rest.Length == 0)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    level = 1;

                    if (codeDef.FinalCode is not null || codeDef.Char1 is null)
                        finalCode = codeDef.FinalCode ?? candidateCodeKey;
                    else
                        level = 2;

                    return true;
                }

                if (codeDef.Char1 is null)
                    continue;

                if (codeDef.XPrefix && rest.StartsWith("X", StringComparison.Ordinal))
                    rest = rest[1..];

                if (rest.Length == 0)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    level = 2;
                    return true;
                }

                var char1 = rest[0].ToString();
                if (!codeDef.Char1.ContainsKey(char1))
                    continue;

                var c2Options = VsaCodeTree.GetChar2Options(codeDef, char1);
                if (rest.Length == 1)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    c1Key = char1;
                    level = c2Options is null ? 2 : 3;
                    finalCode = c2Options is null ? BuildCode(candidateCodeKey, codeDef, char1, null) : null;
                    return true;
                }

                if (rest.Length != 2 || c2Options is null)
                    continue;

                var char2 = rest[1].ToString();
                if (!c2Options.ContainsKey(char2))
                    continue;

                groupKey = grpKey;
                codeKey = candidateCodeKey;
                c1Key = char1;
                c2Key = char2;
                level = 3;
                finalCode = BuildCode(candidateCodeKey, codeDef, char1, char2);
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return string.Empty;

        var chars = rawCode
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private static string BuildCode(string codeKey, VsaCodeDef codeDef, string? c1Key, string? c2Key)
    {
        if (c1Key is null)
            return codeDef.FinalCode ?? codeKey;

        var prefix = codeDef.XPrefix ? "X" : string.Empty;
        return c2Key is null
            ? $"{codeKey}{prefix}{c1Key}"
            : $"{codeKey}{prefix}{c1Key}{c2Key}";
    }

    public ProtocolEntry BuildProtocolEntry()
    {
        var entry = _existingEntry ?? new ProtocolEntry();

        entry.Code = FinalCode;
        entry.Beschreibung = BuildBeschreibung();
        entry.IsStreckenschaden = IsStreckenschaden;

        if (TryParseDouble(MeterStart, out var ms)) entry.MeterStart = ms;
        if (!string.IsNullOrWhiteSpace(MeterEnd) && TryParseDouble(MeterEnd, out var me))
            entry.MeterEnd = me;
        else
            entry.MeterEnd = null;

        if (!string.IsNullOrWhiteSpace(Zeit) && TryParseTime(Zeit, out var zeit))
            entry.Zeit = zeit;

        // CodeMeta
        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        entry.CodeMeta.Code = FinalCode;
        entry.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;

        var p = entry.CodeMeta.Parameters;
        SetOrRemove(p, "vsa.q1", Q1Value);
        SetOrRemove(p, "vsa.q2", Q2Value);

        var clockVon = NormalizeClockValue(ClockVon);
        var clockBis = NormalizeClockValue(ClockBis);

        if (ClockMode == "none")
        {
            clockVon = null;
            clockBis = null;
        }
        else if (ClockMode == "single")
        {
            // Einzelpunkt: Bis immer "00"
            clockBis = string.IsNullOrWhiteSpace(clockVon) ? null : "00";
        }
        else if (ClockMode == "range")
        {
            // Bereich: Bis leer = Punktschaden, automatisch "00"
            if (!string.IsNullOrWhiteSpace(clockVon) && string.IsNullOrWhiteSpace(clockBis))
                clockBis = "00";
        }

        SetOrRemove(p, "vsa.uhr.von", clockVon);
        SetOrRemove(p, "vsa.uhr.bis", clockBis);

        // Zusatzfelder
        SetOrRemove(p, "vsa.rohrverbindung", AnRohrverbindung ? "1" : null);
        SetOrRemove(p, "vsa.strecke.typ", string.IsNullOrWhiteSpace(StreckenschadenTyp) ? null : StreckenschadenTyp);
        SetOrRemove(p, "vsa.bemerkungen", string.IsNullOrWhiteSpace(Bemerkungen) ? null : Bemerkungen);

        // Fotos
        entry.FotoPaths.Clear();
        foreach (var foto in FotoPaths)
            entry.FotoPaths.Add(foto);

        return entry;
    }

    private string BuildBeschreibung()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(FinalLabel)) parts.Add(FinalLabel);
        if (!string.IsNullOrEmpty(FinalSublabel)) parts.Add(FinalSublabel);
        return string.Join(" - ", parts);
    }

    private static void SetOrRemove(Dictionary<string, string> dict, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            dict.Remove(key);
        else
            dict[key] = value.Trim();
    }

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
        foreach (var (key, grp) in VsaCodeTree.Groups)
        {
            GroupTiles.Add(new TileItem
            {
                Key = key, Label = key, Description = grp.Label,
                GroupColor = grp.Color, Icon = grp.Icon,
                IsSelected = string.Equals(key, SelectedGroupKey, StringComparison.Ordinal)
            });
        }
    }

    private void PopulateCodeColumn()
    {
        CodeTiles.Clear();
        if (SelectedGroupKey is null || !VsaCodeTree.Groups.TryGetValue(SelectedGroupKey, out var group))
            return;

        foreach (var (key, cd) in group.Codes)
        {
            var (q1, _) = VsaCodeTree.GetQuantRule(key, null);
            var badge = q1 is not null ? q1.Einheit ?? "Q" : null;
            var badgeColor = q1 is { Pflicht: "P" } ? "#DC2626" : q1 is not null ? "#F59E0B" : null;

            CodeTiles.Add(new TileItem
            {
                Key = key, Label = key, Description = cd.Label,
                IsFinal = cd.FinalCode is not null,
                IsSteuer = cd.IsSteuer,
                BadgeText = badge, BadgeColor = badgeColor,
                GroupColor = group.Color,
                IsSelected = string.Equals(key, SelectedCodeKey, StringComparison.Ordinal)
            });
        }
    }

    private void PopulateChar1Column()
    {
        Char1Tiles.Clear();
        var cd = GetCurrentVsaCodeDef();
        if (cd?.Char1 is null) return;

        foreach (var (key, charDef) in cd.Char1)
        {
            var hasC2 = VsaCodeTree.GetChar2Options(cd, key) is not null;
            var prefix = cd.XPrefix ? "X" : "";
            var fullCode = $"{SelectedCodeKey}{prefix}{key}";
            var (q1, _) = VsaCodeTree.GetQuantRule(SelectedCodeKey!, key);

            Char1Tiles.Add(new TileItem
            {
                Key = key, Label = fullCode, Description = charDef.Label,
                IsFinal = !hasC2,
                BadgeText = q1?.Einheit,
                BadgeColor = q1 is { Pflicht: "P" } ? "#DC2626" : q1 is not null ? "#F59E0B" : null,
                GroupColor = CurrentGroupColor,
                IsSelected = string.Equals(key, SelectedChar1Key, StringComparison.Ordinal)
            });
        }
    }

    private void PopulateChar2Column()
    {
        Char2Tiles.Clear();
        var cd = GetCurrentVsaCodeDef();
        if (cd is null || SelectedChar1Key is null) return;

        var c2Options = VsaCodeTree.GetChar2Options(cd, SelectedChar1Key);
        if (c2Options is null) return;

        var prefix = cd.XPrefix ? "X" : "";
        foreach (var (key, label) in c2Options)
        {
            var invalid = VsaCodeTree.IsInvalidCombo(cd, SelectedChar1Key, key);
            var fullCode = $"{SelectedCodeKey}{prefix}{SelectedChar1Key}{key}";
            Char2Tiles.Add(new TileItem
            {
                Key = key, Label = fullCode, Description = label,
                IsFinal = true, IsInvalid = invalid,
                GroupColor = CurrentGroupColor,
                IsSelected = string.Equals(key, SelectedChar2Key, StringComparison.Ordinal)
            });
        }
    }

    /// <summary>Gruppe waehlen (Multi-Column Modus).</summary>
    public void SelectGroup(string key)
    {
        if (string.Equals(SelectedGroupKey, key, StringComparison.Ordinal))
            return;

        SelectedGroupKey = key;
        var grp = VsaCodeTree.Groups[key];
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

        SelectedChar1Key = key;
        SelectedChar2Key = null;

        PopulateChar1Column();

        var cd = GetCurrentVsaCodeDef();
        var hasC2 = cd is not null && VsaCodeTree.GetChar2Options(cd, key) is not null;

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
        SelectedChar2Key = key;
        PopulateChar2Column();

        var cd = GetCurrentVsaCodeDef();
        var prefix = cd?.XPrefix == true ? "X" : "";
        ShowFinalResult($"{SelectedCodeKey}{prefix}{SelectedChar1Key}{key}", SelectedChar1Key, key);
        UpdateBreadcrumb();
    }

    // =================================================================
    // Helpers
    // =================================================================

    private VsaCodeDef? GetCurrentVsaCodeDef()
    {
        if (SelectedGroupKey is null || SelectedCodeKey is null) return null;
        if (!VsaCodeTree.Groups.TryGetValue(SelectedGroupKey, out var grp)) return null;
        return grp.Codes.TryGetValue(SelectedCodeKey, out var cd) ? cd : null;
    }

    /// <summary>Stufen-Labels fuer den Fortschrittsbalken.</summary>
    public static readonly string[] LevelLabels = { "Gruppe", "Hauptcode", "Char 1", "Char 2" };
}

public sealed record BreadcrumbItem(string Label, int Level);
