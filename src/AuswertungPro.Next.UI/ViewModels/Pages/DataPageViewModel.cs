using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using System.Net.Http;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Sanierung;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.UI.Hydraulik;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel : ObservableObject
{
    private const int MinimumSamplesForModelTraining = 25;
    private const int StrongModelThreshold = 100;
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
    public event Action? RecordsOrderChanged;
    /// <summary>
    /// Aktualisiert die laufende Nummer (NR) aller Records entsprechend der aktuellen Reihenfolge.
    /// </summary>
    private void UpdateNr()
    {
        for (int i = 0; i < Records.Count; i++)
        {
            Records[i].SetFieldValue("NR", (i + 1).ToString(), FieldSource.Manual, true);
        }
    }
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _saveBannerTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly IMeasureRecommendationService _measureRecommendationService;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand EditSanierenOptionsCommand { get; }
    public IRelayCommand PreviewSanierenOptionsCommand { get; }
    public IRelayCommand ResetSanierenOptionsCommand { get; }
    public IRelayCommand<object?> AddSanierenOptionCommand { get; }
    public IRelayCommand<object?> RemoveSanierenOptionCommand { get; }
    public IRelayCommand EditEigentuemerOptionsCommand { get; }
    public IRelayCommand PreviewEigentuemerOptionsCommand { get; }
    public IRelayCommand ResetEigentuemerOptionsCommand { get; }
    public IRelayCommand<object?> AddEigentuemerOptionCommand { get; }
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand { get; }
    public IRelayCommand EditPruefungsresultatOptionsCommand { get; }
    public IRelayCommand PreviewPruefungsresultatOptionsCommand { get; }
    public IRelayCommand ResetPruefungsresultatOptionsCommand { get; }
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand { get; }
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand { get; }
    public IRelayCommand EditReferenzpruefungOptionsCommand { get; }
    public IRelayCommand PreviewReferenzpruefungOptionsCommand { get; }
    public IRelayCommand ResetReferenzpruefungOptionsCommand { get; }
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand { get; }
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand { get; }
    public IRelayCommand EditEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand PreviewEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand ResetEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand<object?> AddEmpfohleneSanierungsmassnahmenOptionCommand { get; }
    public IRelayCommand<object?> RemoveEmpfohleneSanierungsmassnahmenOptionCommand { get; }
    public IRelayCommand<HaltungRecord?> PlayVideoCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenProtocolCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenVideoAiPipelineCommand { get; }
    public IRelayCommand<HaltungRecord?> RelinkVideoCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenOriginalPdfCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintAwuHaltungsprotokollCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> RestoreCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> SuggestMeasuresCommand { get; }
    public IRelayCommand SuggestAllMeasuresCommand { get; }
    public IRelayCommand<HaltungRecord?> OptimizeSanierungKiCommand { get; }
    public IRelayCommand ShowModelStatusCommand { get; }
    public IRelayCommand SearchAndLinkMediaCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenHydraulikCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintHydraulikCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintDossierCommand { get; }

    public IReadOnlyList<string> Columns => FieldCatalog.ColumnOrder;
    public ObservableCollection<HaltungRecord> Records => _shell.Project.Data;
    public Project Project => _shell.Project;

    public ObservableCollection<string> SanierenOptions { get; }
    public ObservableCollection<string> EigentuemerOptions { get; }
    public ObservableCollection<string> PruefungsresultatOptions { get; }
    public ObservableCollection<string> ReferenzpruefungOptions { get; }
    public ObservableCollection<string> EmpfohleneSanierungsmassnahmenOptions { get; }
    public ObservableCollection<string> AusgefuehrtDurchOptions { get; }
    public ObservableCollection<ProtocolEntry> SelectedProtocolEntries { get; } = new();

    [ObservableProperty] private HaltungRecord? _selected;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private bool _isSaveStatusVisible;
    [ObservableProperty] private string _learningInfo = string.Empty;
    [ObservableProperty] private bool _isLearningInfoVisible;
    [ObservableProperty] private string _learningTrafficLightColor = "#C62828";
    [ObservableProperty] private string _learningTrafficLightText = "Rot";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _searchResultInfo = string.Empty;

    /// <summary>
    /// Normalisierte Haltungsnamen die im Training Center erfasst sind.
    /// Wird beim Start geladen; DataPage nutzt dieses Set für die rote Zeilenmarkierung.
    /// </summary>
    public HashSet<string> TrainedHaltungen { get; } = new(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private double _gridMinRowHeight = 38d;
    [ObservableProperty] private double _gridZoom = 1.0d;
    [ObservableProperty] private bool _isColumnReorderEnabled;
    public IRelayCommand ClearSearchCommand { get; }
    public bool IsProjectReady => _shell.IsProjectReady;
    public bool IsDataGridReadOnly => !_shell.IsProjectReady;

    public DataPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _measureRecommendationService = _sp.MeasureRecommendation;
        _saveBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveBannerTimer.Tick += (_, __) =>
        {
            _saveBannerTimer.Stop();
            IsSaveStatusVisible = false;
        };
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoSaveTimer.Tick += (_, __) => AutoSaveOnTimerTick();
        _shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
            {
                OnPropertyChanged(nameof(IsProjectReady));
                OnPropertyChanged(nameof(IsDataGridReadOnly));
            }
            else if (e.PropertyName == nameof(ShellViewModel.Project))
            {
                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(Records));
                UpdateSearchResultInfo(Records.Count);
            }
        };

        var uiLayout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 240d
            ? uiLayout.GridMinRowHeight
            : 38d;
        GridZoom = uiLayout.GridZoom is >= 0.5d and <= 2.0d
            ? uiLayout.GridZoom
            : 1.0d;
        IsColumnReorderEnabled = uiLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadReferenzpruefungOptions());
        EmpfohleneSanierungsmassnahmenOptions = new ObservableCollection<string>(
            DropdownOptionsStore.LoadEmpfohleneSanierungsmassnahmenOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));

        // Seed measure template names from Offerten into dropdown if missing
        SeedMeasureTemplateNames();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        SaveCommand = new RelayCommand(Save);
        EditSanierenOptionsCommand = new RelayCommand(EditSanierenOptions);
        PreviewSanierenOptionsCommand = new RelayCommand(PreviewSanierenOptions);
        ResetSanierenOptionsCommand = new RelayCommand(ResetSanierenOptions);
        AddSanierenOptionCommand = new RelayCommand<object?>(AddSanierenOption);
        RemoveSanierenOptionCommand = new RelayCommand<object?>(RemoveSanierenOption);
        EditEigentuemerOptionsCommand = new RelayCommand(EditEigentuemerOptions);
        PreviewEigentuemerOptionsCommand = new RelayCommand(PreviewEigentuemerOptions);
        ResetEigentuemerOptionsCommand = new RelayCommand(ResetEigentuemerOptions);
        AddEigentuemerOptionCommand = new RelayCommand<object?>(AddEigentuemerOption);
        RemoveEigentuemerOptionCommand = new RelayCommand<object?>(RemoveEigentuemerOption);
        EditPruefungsresultatOptionsCommand = new RelayCommand(EditPruefungsresultatOptions);
        PreviewPruefungsresultatOptionsCommand = new RelayCommand(PreviewPruefungsresultatOptions);
        ResetPruefungsresultatOptionsCommand = new RelayCommand(ResetPruefungsresultatOptions);
        AddPruefungsresultatOptionCommand = new RelayCommand<object?>(AddPruefungsresultatOption);
        RemovePruefungsresultatOptionCommand = new RelayCommand<object?>(RemovePruefungsresultatOption);
        EditReferenzpruefungOptionsCommand = new RelayCommand(EditReferenzpruefungOptions);
        PreviewReferenzpruefungOptionsCommand = new RelayCommand(PreviewReferenzpruefungOptions);
        ResetReferenzpruefungOptionsCommand = new RelayCommand(ResetReferenzpruefungOptions);
        AddReferenzpruefungOptionCommand = new RelayCommand<object?>(AddReferenzpruefungOption);
        RemoveReferenzpruefungOptionCommand = new RelayCommand<object?>(RemoveReferenzpruefungOption);
        EditEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(EditEmpfohleneSanierungsmassnahmenOptions);
        PreviewEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(PreviewEmpfohleneSanierungsmassnahmenOptions);
        ResetEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(ResetEmpfohleneSanierungsmassnahmenOptions);
        AddEmpfohleneSanierungsmassnahmenOptionCommand = new RelayCommand<object?>(AddEmpfohleneSanierungsmassnahmenOption);
        RemoveEmpfohleneSanierungsmassnahmenOptionCommand = new RelayCommand<object?>(RemoveEmpfohleneSanierungsmassnahmenOption);
        PlayVideoCommand = new RelayCommand<HaltungRecord?>(PlayVideo);
        OpenProtocolCommand = new RelayCommand<HaltungRecord?>(OpenProtocol);
        OpenVideoAiPipelineCommand = new RelayCommand<HaltungRecord?>(OpenVideoAiPipeline);
        RelinkVideoCommand = new RelayCommand<HaltungRecord?>(RelinkVideo);
        OpenOriginalPdfCommand = new RelayCommand<HaltungRecord?>(OpenOriginalPdf);
        PrintAwuHaltungsprotokollCommand = new RelayCommand<HaltungRecord?>(PrintAwuHaltungsprotokollPdf);
        OpenCostsCommand = new RelayCommand<HaltungRecord?>(OpenCosts, CanOpenCosts);
        RestoreCostsCommand = new RelayCommand<HaltungRecord?>(RestoreCosts, CanRestoreCosts);
        SuggestMeasuresCommand = new RelayCommand<HaltungRecord?>(SuggestMeasures, CanSuggestMeasures);
        SuggestAllMeasuresCommand = new RelayCommand(SuggestAllMeasures);
        OptimizeSanierungKiCommand = new RelayCommand<HaltungRecord?>(OpenSanierungOptimizationWindow, CanOpenCosts);
        ShowModelStatusCommand = new RelayCommand(ShowModelStatus);
        SearchAndLinkMediaCommand = new RelayCommand(OpenMediaSearchWindow);
        OpenHydraulikCommand = new RelayCommand<HaltungRecord?>(OpenHydraulikPanel);
        PrintHydraulikCommand = new RelayCommand<HaltungRecord?>(PrintHydraulikPdf);
        PrintDossierCommand = new RelayCommand<HaltungRecord?>(PrintDossierPdf);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        PropertyChanged += DataPageViewModel_PropertyChanged;
        UpdateLearningInfo();
        _ = LoadTrainedHaltungenAsync();
    }

    partial void OnGridMinRowHeightChanged(double value)
    {
        var clamped = Math.Clamp(value, 24d, 240d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
            return;
        }

        PersistDataPageBasicUiSettings();
    }

    partial void OnGridZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.5d, 2.0d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridZoom = clamped;
            return;
        }

        PersistDataPageBasicUiSettings();
    }

    partial void OnIsColumnReorderEnabledChanged(bool value)
    {
        _ = value;
        PersistDataPageBasicUiSettings();
    }

    private void DataPageViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Selected))
        {
            (RemoveCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (OpenCostsCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (RestoreCostsCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (SuggestMeasuresCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (OptimizeSanierungKiCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();

            if (Selected is not null)
            {
                NormalizeSelectedFindings(Selected);
                SyncSelectedProtocolFromFindings(Selected);
            }

            RefreshSelectedProtocolEntries();
        }
    }

    private bool _isSyncingSelectedProtocol;

    private void SyncSelectedProtocolFromFindings(HaltungRecord record)
    {
        if (_isSyncingSelectedProtocol)
            return;

        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var needsProtocol = record.Protocol is null
                            || (record.Protocol.Current?.Entries.Count ?? 0) == 0
                            && (record.Protocol.Original?.Entries.Count ?? 0) == 0;
        if (!needsProtocol)
            return;

        _isSyncingSelectedProtocol = true;
        try
        {
            var entries = BuildEntriesFromFindings(record.VsaFindings);
            record.Protocol = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();
        }
        finally
        {
            _isSyncingSelectedProtocol = false;
        }
    }

    private void RefreshSelectedProtocolEntries()
    {
        SelectedProtocolEntries.Clear();
        var list = Selected?.Protocol?.Current?.Entries;
        if (list is null || list.Count == 0)
            return;

        foreach (var entry in list.Where(e => !e.IsDeleted))
        {
            // Beschreibung aus dem VSA-Katalog auflösen, wenn sie leer ist
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) || entry.Beschreibung.Length <= 3)
            {
                if (!string.IsNullOrWhiteSpace(entry.Code) &&
                    _sp.CodeCatalog.TryGet(entry.Code, out var def) &&
                    !string.IsNullOrWhiteSpace(def.Title))
                {
                    entry.Beschreibung = def.Title;
                }
            }

            SelectedProtocolEntries.Add(entry);
        }
    }

    private IReadOnlyList<ProtocolEntry> BuildEntriesFromFindings(IEnumerable<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in findings)
        {
            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            var time = ParseMpegTime(f.MPEG) ?? (f.Timestamp?.TimeOfDay);

            var beschreibung = f.Raw?.Trim() ?? string.Empty;
            // Beschreibung aus dem VSA-Katalog auflösen, wenn Raw leer oder nur Kuerzel
            var code = f.KanalSchadencode?.Trim() ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(beschreibung) || beschreibung.Length <= 3) &&
                !string.IsNullOrWhiteSpace(code) &&
                _sp.CodeCatalog.TryGet(code, out var codeDef) &&
                !string.IsNullOrWhiteSpace(codeDef.Title))
            {
                beschreibung = codeDef.Title;
            }

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = beschreibung,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    private static readonly Regex MeterRegex = new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);
    private static readonly Regex ContinuousDefectMarkerRegex = new(@"^[AB]\d{2}$", RegexOptions.Compiled);
    private static readonly Regex EmbeddedVsaCodeRegex = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    private void NormalizeSelectedFindings(HaltungRecord record)
    {
        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var changed = false;
        foreach (var f in record.VsaFindings)
        {
            var raw = f.Raw ?? string.Empty;

            if (f.MeterStart is null && f.SchadenlageAnfang is null && !string.IsNullOrWhiteSpace(raw))
            {
                var meter = TryParseMeter(raw);
                if (meter is not null)
                {
                    f.MeterStart = meter;
                    changed = true;
                }
            }

            if (f.MeterEnd is null && f.SchadenlageEnde is null && !string.IsNullOrWhiteSpace(raw))
            {
                // If no explicit end, leave empty; but if text has a second meter, use it.
                var meterRange = TryParseSecondMeter(raw);
                if (meterRange is not null)
                {
                    f.MeterEnd = meterRange;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(f.MPEG) && !string.IsNullOrWhiteSpace(raw))
            {
                var mpeg = TryParseTime(raw);
                if (!string.IsNullOrWhiteSpace(mpeg))
                {
                    f.MPEG = mpeg;
                    changed = true;
                }
            }
        }

        var deduped = new List<VsaFinding>(record.VsaFindings.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in record.VsaFindings)
        {
            var effectiveCode = ResolveFindingEffectiveCode(finding.KanalSchadencode, finding.Raw);
            if (!string.Equals(finding.KanalSchadencode, effectiveCode, StringComparison.OrdinalIgnoreCase))
            {
                finding.KanalSchadencode = effectiveCode;
                changed = true;
            }

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
            var meterKey = meter.HasValue
                ? meter.Value.ToString("F2", CultureInfo.InvariantCulture)
                : string.Empty;
            var key = $"{effectiveCode}|{meterKey}";
            if (!seen.Add(key))
            {
                changed = true;
                continue;
            }

            deduped.Add(finding);
        }

        if (deduped.Count != record.VsaFindings.Count)
        {
            record.VsaFindings = deduped;
            changed = true;
        }

        if (!changed)
            return;

        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);
    }

    private static double? TryParseMeter(string raw)
    {
        var match = MeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var valueText = match.Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static double? TryParseSecondMeter(string raw)
    {
        var matches = MeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var valueText = matches[1].Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static string? TryParseTime(string raw)
    {
        var match = TimeRegex.Match(raw);
        if (!match.Success)
            return null;

        return match.Groups[1].Value;
    }

    private static double? TryParseDnMm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
            && value > 0)
        {
            return value;
        }

        if (text.Contains(',') && text.Contains('.'))
        {
            var commaAsDecimal = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            if (double.TryParse(commaAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;

            var dotAsDecimal = text.Replace(",", string.Empty, StringComparison.Ordinal);
            if (double.TryParse(dotAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }
        else if (text.Contains(','))
        {
            var normalized = text.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }

        var digitsOnly = text.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(digitsOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 50)
            return value;

        return null;
    }

    private bool CanMoveUp()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx > 0;
    }

    private bool CanMoveDown()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx >= 0 && idx < Records.Count - 1;
    }

    private void Add()
    {
        var record = _shell.Project.CreateNewRecord();
        _shell.Project.AddRecord(record);
        Selected = record;
        ScheduleAutoSave();
    }

    private void Remove()
    {
        if (Selected is null) return;

        var idx = Records.IndexOf(Selected);
        var removedId = Selected.Id;
        var removed = _shell.Project.RemoveRecord(removedId);
        if (!removed)
        {
            return;
        }

        if (Records.Count == 0)
        {
            Selected = null;
            ScheduleAutoSave();
            return;
        }

        if (idx >= Records.Count) idx = Records.Count - 1;
        Selected = Records[idx];
        ScheduleAutoSave();
    }

    private void MoveUp()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx <= 0) return;
        Records.Move(idx, idx - 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    private void MoveDown()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx < 0 || idx >= Records.Count - 1) return;
        Records.Move(idx, idx + 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    /// <summary>
    /// Verschiebt die aktuell selektierte Haltung an die angegebene 1-basierte Position.
    /// Alle Zeilen ab dieser Position rutschen um eins nach unten.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        if (idx < 0) return false;

        // 1-basiert -> 0-basiert
        int targetIdx = targetPosition - 1;
        if (targetIdx < 0) targetIdx = 0;
        if (targetIdx >= Records.Count) targetIdx = Records.Count - 1;
        if (targetIdx == idx) return false;

        Records.Move(idx, targetIdx);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
        return true;
    }

    private void Save()
    {
        var learnedAny = false;
        foreach (var record in Records)
            learnedAny |= _measureRecommendationService.Learn(record);
        if (learnedAny)
            _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
        UpdateLearningInfo();

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        ShowSaveStatus(_shell.Subtitle);
        if (!ok)
            IsSaveStatusVisible = true;
    }

    /// <summary>
    /// Schedules auto-save according to settings.
    /// </summary>
    public void ScheduleAutoSave()
    {
        _shell.Project.Dirty = true;
        var mode = _sp.Settings.DataAutoSaveMode.Normalize();
        switch (mode)
        {
            case AutoSaveMode.OnEachChange:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
            case AutoSaveMode.Every5Minutes:
            case AutoSaveMode.Every10Minutes:
                ScheduleIntervalAutoSave(mode);
                break;
            case AutoSaveMode.Disabled:
                _autoSaveTimer.Stop();
                break;
            default:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
        }
    }

    private void ScheduleIntervalAutoSave(AutoSaveMode mode)
    {
        var interval = mode.GetInterval();
        if (interval is null)
        {
            _autoSaveTimer.Stop();
            return;
        }

        if (_autoSaveTimer.Interval != interval.Value)
            _autoSaveTimer.Interval = interval.Value;

        if (!_autoSaveTimer.IsEnabled)
            _autoSaveTimer.Start();
    }

    private void AutoSaveOnTimerTick()
    {
        var mode = _sp.Settings.DataAutoSaveMode.Normalize();
        if (mode is not (AutoSaveMode.Every5Minutes or AutoSaveMode.Every10Minutes))
        {
            _autoSaveTimer.Stop();
            return;
        }

        AutoSave();

        // No pending changes left -> no need to keep ticking.
        if (!_shell.Project.Dirty)
            _autoSaveTimer.Stop();
    }

    private void AutoSave()
    {
        if (!_shell.IsProjectReady || !_shell.Project.Dirty)
            return;

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        if (ok)
            ShowSaveStatus("Automatisch gespeichert");
    }

    private void PlayVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var options = new PlayerWindowOptions(
                EnableHardwareDecoding: _sp.Settings.VideoHwDecoding,
                DropLateFrames: _sp.Settings.VideoDropLateFrames,
                SkipFrames: _sp.Settings.VideoSkipFrames,
                FileCachingMs: _sp.Settings.VideoFileCachingMs,
                NetworkCachingMs: _sp.Settings.VideoNetworkCachingMs,
                CodecThreads: _sp.Settings.VideoCodecThreads,
                VideoOutput: _sp.Settings.VideoOutput);

            // Build damage overlay markers from protocol entries
            PlayerDamageOverlayData? damageOverlay = null;
            var lengthStr = record.GetFieldValue("Haltungslaenge_m");
            if (double.TryParse(lengthStr?.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var pipeLength)
                && pipeLength > 0)
            {
                var markers = new System.Collections.Generic.List<DamageMarkerInfo>();

                if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
                {
                    foreach (var e in entries.Where(e => !e.IsDeleted && e.MeterStart.HasValue))
                    {
                        markers.Add(new DamageMarkerInfo(
                            e.Code ?? "",
                            e.Beschreibung,
                            e.MeterStart!.Value,
                            e.MeterEnd,
                            e.IsStreckenschaden));
                    }
                }
                else if (record.VsaFindings is { Count: > 0 } findings)
                {
                    foreach (var f in findings)
                    {
                        var mStart = f.MeterStart ?? f.SchadenlageAnfang;
                        if (mStart is null) continue;
                        var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
                        markers.Add(new DamageMarkerInfo(
                            f.KanalSchadencode?.Trim() ?? "",
                            f.Raw,
                            mStart.Value,
                            mEnd,
                            mEnd.HasValue && mEnd.Value > mStart.Value));
                    }
                }

                if (markers.Count > 0)
                    damageOverlay = new PlayerDamageOverlayData(pipeLength, markers);
            }

            var window = new PlayerWindow(path, options,
                damageOverlay: damageOverlay,
                serviceProvider: _sp,
                haltungId: record.Id.ToString(),
                haltungRecord: record)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.Show();
        }
        catch (Exception ex)
        {
            var logPath = TryWriteVideoStartErrorLog(ex, path);
            var nativeHint = ex.Message.Contains("native side", StringComparison.OrdinalIgnoreCase)
                ? "\n\nHinweis: Bitte pruefen, ob 'VideoLAN.LibVLC.Windows' fuer dieses Projekt/Plattform installiert ist."
                : string.Empty;
            var msg = logPath is null
                ? $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\n(Details: ex.ToString() nicht gespeichert)"
                : $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\nDetails gespeichert in:\n{logPath}";
            MessageBox.Show(msg, "Video", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenProtocol(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath)
            ? null
            : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var resolvedVideoPath = ResolveExistingPath(record.GetFieldValue("Link"));
        var dlg = new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow(
            record,
            _shell.Project,
            _sp,
            resolvedVideoPath,
            projectFolder,
            markDirty: () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
                ScheduleAutoSave();
            });
        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        dlg.ShowDialog();

        // Protokoll-Änderungen in die Haltungsfelder zurückschreiben.
        SyncObservationsToHoldingFields(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();
    }

    public void SyncObservationsToHoldingFields(HaltungRecord? record, bool showStatus = false)
    {
        if (record is null)
            return;

        var entries = record.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries is null)
            return;

        var changed = false;

        var primaryLines = BuildPrimaryDamageLinesFromProtocolEntries(entries);
        var primaryText = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", primaryLines));
        var currentPrimary = record.GetFieldValue("Primaere_Schaeden") ?? string.Empty;
        if (!string.Equals(currentPrimary, primaryText, StringComparison.Ordinal))
        {
            record.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
            changed = true;
        }

        var mergedFindings = BuildFindingsFromProtocolEntries(entries, record.VsaFindings);
        if (HasFindingChanges(record.VsaFindings, mergedFindings))
        {
            record.VsaFindings = mergedFindings;
            changed = true;
        }

        if (!changed)
            return;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();

        ScheduleAutoSave();
        if (showStatus)
            _shell.SetStatus("Beobachtungen in Haltungen-Feldern aktualisiert");
    }

    private static List<string> BuildPrimaryDamageLinesFromProtocolEntries(IEnumerable<ProtocolEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            // Deduplicate by code + meter position
            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            var key = $"{code.ToUpperInvariant()}|{meterKey}";
            if (!seen.Add(key))
                continue;

            var parts = new List<string>();
            if (meter.HasValue)
                parts.Add($"{meter.Value:0.00}m");

            parts.Add(code);

            var description = NormalizeInlineText(entry.Beschreibung);
            if (!string.IsNullOrWhiteSpace(description))
                parts.Add(description);

            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                parts.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                parts.Add($"Q2={q2}");

            lines.Add(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        return lines;
    }

    private static List<VsaFinding> BuildFindingsFromProtocolEntries(
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyList<VsaFinding>? existingFindings)
    {
        var existing = existingFindings ?? Array.Empty<VsaFinding>();
        var list = new List<VsaFinding>(entries.Count);

        foreach (var entry in entries)
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var meterStart = entry.MeterStart;
            var meterEnd = entry.MeterEnd;
            var q1 = GetCodeMetaParameter(entry, "Quantifizierung1", "vsa.q1");
            var q2 = GetCodeMetaParameter(entry, "Quantifizierung2", "vsa.q2");
            var photo = entry.FotoPaths?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            var template = existing.FirstOrDefault(f =>
                AreCodesCompatible(code, f.KanalSchadencode) && AreMetersClose(meterStart, f.MeterStart ?? f.SchadenlageAnfang, 0.15));

            var finding = new VsaFinding
            {
                KanalSchadencode = code,
                Raw = (entry.Beschreibung ?? string.Empty).Trim(),
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                SchadenlageAnfang = meterStart,
                SchadenlageEnde = meterEnd,
                Quantifizierung1 = q1,
                Quantifizierung2 = q2,
                MPEG = string.IsNullOrWhiteSpace(entry.Mpeg) ? template?.MPEG : entry.Mpeg,
                FotoPath = string.IsNullOrWhiteSpace(photo) ? template?.FotoPath : photo,
                EZD = template?.EZD,
                EZS = template?.EZS,
                EZB = template?.EZB
            };

            if (entry.Zeit.HasValue)
                finding.Timestamp = DateTime.Today.Add(entry.Zeit.Value);
            else
                finding.Timestamp = template?.Timestamp;

            if (entry.IsStreckenschaden && meterStart.HasValue && meterEnd.HasValue && meterEnd.Value >= meterStart.Value)
                finding.LL = meterEnd.Value - meterStart.Value;
            else
                finding.LL = template?.LL;

            list.Add(finding);
        }

        return list;
    }

    private static bool HasFindingChanges(IReadOnlyList<VsaFinding>? oldFindings, IReadOnlyList<VsaFinding> newFindings)
    {
        var oldList = oldFindings ?? Array.Empty<VsaFinding>();
        if (oldList.Count != newFindings.Count)
            return true;

        for (var i = 0; i < oldList.Count; i++)
        {
            if (!string.Equals(BuildFindingFingerprint(oldList[i]), BuildFindingFingerprint(newFindings[i]), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string BuildFindingFingerprint(VsaFinding finding)
    {
        return string.Join("|",
            NormalizeCodeToken(finding.KanalSchadencode),
            FormatNullableDouble(finding.MeterStart),
            FormatNullableDouble(finding.MeterEnd),
            FormatNullableDouble(finding.SchadenlageAnfang),
            FormatNullableDouble(finding.SchadenlageEnde),
            finding.Raw?.Trim() ?? string.Empty,
            finding.Quantifizierung1?.Trim() ?? string.Empty,
            finding.Quantifizierung2?.Trim() ?? string.Empty,
            finding.MPEG?.Trim() ?? string.Empty,
            finding.FotoPath?.Trim() ?? string.Empty);
    }

    private static string NormalizeCodeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, "[^A-Z0-9]", string.Empty);
    }

    private static string ResolveFindingEffectiveCode(string? code, string? rawDescription)
    {
        var normalizedCode = NormalizeCodeToken(code);
        if (!ContinuousDefectMarkerRegex.IsMatch(normalizedCode) || string.IsNullOrWhiteSpace(rawDescription))
            return normalizedCode;

        var text = rawDescription.Trim();
        if (text.StartsWith("("))
            text = text.Substring(1).TrimStart();

        var match = EmbeddedVsaCodeRegex.Match(text);
        return match.Success ? NormalizeCodeToken(match.Groups[1].Value) : normalizedCode;
    }

    private static bool AreCodesCompatible(string? left, string? right)
    {
        var a = NormalizeCodeToken(left);
        var b = NormalizeCodeToken(right);
        if (a.Length == 0 || b.Length == 0)
            return false;
        return string.Equals(a, b, StringComparison.Ordinal)
               || a.StartsWith(b, StringComparison.Ordinal)
               || b.StartsWith(a, StringComparison.Ordinal);
    }

    private static bool AreMetersClose(double? left, double? right, double tolerance)
    {
        if (!left.HasValue || !right.HasValue)
            return false;
        return Math.Abs(left.Value - right.Value) <= tolerance;
    }

    private static string NormalizeInlineText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var oneLine = string.Join(" ",
            value.Replace("\r\n", "\n")
                 .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                 .Select(s => s.Trim())
                 .Where(s => s.Length > 0));

        return string.Join(" ", oneLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? GetCodeMetaParameter(ProtocolEntry entry, params string[] keys)
    {
        if (entry.CodeMeta?.Parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (entry.CodeMeta.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string FormatNullableDouble(double? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

    private void OpenVideoAiPipeline(HaltungRecord? record)
    {
        if (record is null) return;

        var videoPath = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(videoPath)) return;

        var allowedCodes = _sp.CodeCatalog.AllowedCodes();
        if (allowedCodes is null || allowedCodes.Count == 0)
        {
            MessageBox.Show("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cfg = AiRuntimeConfig.Load();
        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var timeout = cfg.OllamaRequestTimeout > TimeSpan.Zero
            ? cfg.OllamaRequestTimeout
            : TimeSpan.FromMinutes(30);
        using var http = new HttpClient { Timeout = timeout };
        var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
        var plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var pipeline = _sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);

        var haltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        var request = new PipelineRequest(haltungId, videoPath, allowedCodes);

        var win = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var ok = win.ShowDialog() == true;

        if (ok && win.Result?.IsSuccess == true && win.Result.Document is not null)
        {
            record.Protocol = win.Result.Document;

            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;

            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();

            ScheduleAutoSave();
        }
    }


    private static ProtocolEntry CloneProtocolEntry(ProtocolEntry source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProtocolEntry>(json) ?? new ProtocolEntry();
    }

    private string? EnsureProtocolPath(HaltungRecord record)
    {
        var holdingTokens = BuildHoldingTokens(record);

        var resolvedLink = ResolveExistingPath(record.GetFieldValue("Link"));
        var fromLink = TryResolveProtocolFromLink(resolvedLink, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromLink))
            return fromLink;

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var fromInitial = TryFindProtocolFromRoot(initial, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromInitial))
            return fromInitial;

        if (!string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
        {
            var projectDir = Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                var fromHoldings = TryFindProtocolFromRoot(Path.Combine(projectDir, "Haltungen"), holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromHoldings))
                    return fromHoldings;

                var fromStored = TryFindProtocolFromStoredPdfFiles(projectDir, holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromStored))
                    return fromStored;
            }
        }

        return null;
    }

    private string? TryResolveProtocolFromLink(string? resolvedLink, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(resolvedLink))
            return null;

        if (string.Equals(Path.GetExtension(resolvedLink), ".pdf", StringComparison.OrdinalIgnoreCase))
            return resolvedLink;

        var folder = Path.GetDirectoryName(resolvedLink);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var inSameFolder = TryFindPdfInDirectory(folder, holdingTokens, SearchOption.TopDirectoryOnly);
        if (!string.IsNullOrWhiteSpace(inSameFolder))
            return inSameFolder;

        try
        {
            var parent = Directory.GetParent(folder);
            if (parent is not null && string.Equals(parent.Name, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
            {
                var gemeindeRoot = parent.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(gemeindeRoot))
                {
                    var inGemeinde = TryFindProtocolFromRoot(gemeindeRoot, holdingTokens);
                    if (!string.IsNullOrWhiteSpace(inGemeinde))
                        return inGemeinde;
                }
            }
        }
        catch
        {
            // Continue with other lookup strategies.
        }

        return null;
    }

    private string? TryFindProtocolFromRoot(string? rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return null;

        var holdingDir = TryFindHoldingDirectory(rootDir, holdingTokens);
        if (!string.IsNullOrWhiteSpace(holdingDir))
        {
            var inHolding = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.TopDirectoryOnly);
            if (!string.IsNullOrWhiteSpace(inHolding))
                return inHolding;

            var inHoldingRecursive = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.AllDirectories);
            if (!string.IsNullOrWhiteSpace(inHoldingRecursive))
                return inHoldingRecursive;
        }

        return TryFindPdfInDirectory(rootDir, holdingTokens, SearchOption.AllDirectories);
    }

    private string? TryFindProtocolFromStoredPdfFiles(string projectDir, IReadOnlyList<string> holdingTokens)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var candidates = new List<string>();
        foreach (var stored in ParseStoredPathList(raw))
        {
            var resolved = TryResolveStoredPath(projectDir, stored);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;
            if (!string.Equals(Path.GetExtension(resolved), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(resolved);
        }

        return PickBestPdfCandidate(candidates, holdingTokens);
    }

    private static string? TryFindHoldingDirectory(string rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (holdingTokens.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var direct = Path.Combine(rootDir, token);
            if (Directory.Exists(direct))
                return direct;
        }

        foreach (var sub in SafeEnumerateDirectories(rootDir))
        {
            if (string.Equals(Path.GetFileName(sub), "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var token in holdingTokens)
            {
                var candidate = Path.Combine(sub, token);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? TryFindPdfInDirectory(string directory, IReadOnlyList<string> holdingTokens, SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var files = SafeEnumerateFiles(directory, "*.pdf", searchOption);
        return PickBestPdfCandidate(files, holdingTokens);
    }

    private static string? PickBestPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
    {
        var list = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var expectedSuffix = "_" + token + ".pdf";
            var exact = list
                .Where(path => Path.GetFileName(path).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exact.Count > 0)
                return exact[0];
        }

        return list
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
    {
        var holdingRaw = (record.GetFieldValue("Haltungsname") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return Array.Empty<string>();

        var sanitized = SanitizePathSegment(holdingRaw);
        return new[] { sanitized, holdingRaw }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SanitizePathSegment(string value)
        => AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(value);

    private static IReadOnlyList<string> ParseStoredPathList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed is null)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }

    private static string? TryResolveStoredPath(string projectDir, string rawPath)
    {
        var path = (rawPath ?? string.Empty).Trim();
        if (path.Length == 0)
            return null;

        try
        {
            var full = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(projectDir, path));
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryWriteVideoStartErrorLog(Exception ex, string path)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            var safeName = Path.GetFileNameWithoutExtension(path);
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "video";

            var file = $"video_start_error_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}.txt";
            var logPath = Path.Combine(logsDir, file);

            var content =
                $"Time: {DateTime.Now:O}{Environment.NewLine}" +
                $"VideoPath: {path}{Environment.NewLine}" +
                $"Exception:{Environment.NewLine}{ex}{Environment.NewLine}";
            File.WriteAllText(logPath, content);
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private void RelinkVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var path = _sp.Dialogs.OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            initial);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var selectedDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(selectedDir))
        {
            _sp.Settings.LastVideoSourceFolder = selectedDir;
            _sp.Settings.LastVideoFolder = selectedDir; // legacy compatibility
            _sp.Settings.Save();
        }

        SaveVideoLink(record, path, userEdited: true);
    }

    private bool CanOpenCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanRestoreCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanSuggestMeasures(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private void RestoreCosts(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            MessageBox.Show("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            MessageBox.Show("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var store = new ProjectCostStoreRepository().Load(projectPath);
        if (!store.ByHolding.TryGetValue(holding, out var cost))
        {
            var dir = Path.GetDirectoryName(projectPath);
            var storePath = string.IsNullOrWhiteSpace(dir) ? "" : ProjectCostStoreRepository.GetStorePath(dir);
            MessageBox.Show($"Keine gespeicherten Kosten/Massnahmen gefunden fuer:\n{holding}\n\nDatei:\n{storePath}",
                "Kosten/Massnahmen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyCostsToRecord(record, cost, learn: false);
        _shell.SetStatus($"Kosten/Massnahmen wiederhergestellt: {holding}");
    }

    private void OpenCosts(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator);
    }

    private void SuggestMeasures(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
        if (recommendation.Measures.Count == 0)
        {
            MessageBox.Show(
                "Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.",
                "Massnahmen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var value = string.Join(Environment.NewLine, recommendation.Measures);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);
        foreach (var suggestion in recommendation.Measures)
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

        if (recommendation.EstimatedTotalCost is not null)
            record.SetFieldValue("Kosten", recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerM is not null)
            record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerStk is not null)
            record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.AnschluesseVerpressen is not null)
            record.SetFieldValue("Anschluesse_verpressen", FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturManschette is not null)
            record.SetFieldValue("Reparatur_Manschette", FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturKurzliner is not null)
            record.SetFieldValue("Reparatur_Kurzliner", FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        var sourceText = recommendation.UsedTrainedModel ? "KI-Modell" : "Lernlogik";
        _shell.SetStatus(recommendation.EstimatedTotalCost is null
            ? $"Massnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Massnahmenvorschlag mit Kostenschaetzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
        UpdateLearningInfo(recommendation.SimilarCasesCount, recommendation.EstimatedTotalCost);

        // Show result dialog so user sees the suggested measures
        var summary = string.Join("\n", recommendation.Measures);
        if (recommendation.EstimatedTotalCost is not null)
            summary += $"\n\nGeschaetzte Kosten: {recommendation.EstimatedTotalCost.Value:N2}";
        summary += $"\n\nQuelle: {sourceText}";
        if (recommendation.SimilarCasesCount > 0)
            summary += $" ({recommendation.SimilarCasesCount} aehnliche Faelle)";
        MessageBox.Show(summary, "Empfohlene Sanierungsmassnahmen",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Batch: Fuer alle Haltungen mit Sanierungsbedarf (oder fehlenden Massnahmen)
    /// automatisch Sanierungsmassnahmen vorschlagen.
    /// </summary>
    public void SuggestAllMeasures()
    {
        var records = _shell.Project.Data;
        if (records.Count == 0)
        {
            MessageBox.Show("Keine Haltungen vorhanden.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var filled = 0;
        var skipped = 0;
        var noSuggestion = 0;

        foreach (var record in records)
        {
            // Nur Records mit Sanierungsbedarf oder schlechter Zustandsnote beruecksichtigen
            var pruefung = (record.GetFieldValue("Pruefungsresultat") ?? "").Trim();
            var existingMeasures = (record.GetFieldValue("Empfohlene_Sanierungsmassnahmen") ?? "").Trim();
            var hasDamageCodes = record.VsaFindings is not null && record.VsaFindings.Count > 0
                || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"));

            // Ueberspringe Records die bereits manuell bearbeitete Massnahmen haben
            if (!string.IsNullOrWhiteSpace(existingMeasures))
            {
                var meta = record.FieldMeta.GetValueOrDefault("Empfohlene_Sanierungsmassnahmen");
                if (meta is not null && meta.UserEdited)
                {
                    skipped++;
                    continue;
                }
            }

            // Nur Records mit Sanierungsbedarf oder Schadenscodes verarbeiten
            if (!string.Equals(pruefung, "Sanierungsbedarf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pruefung, "beobachten", StringComparison.OrdinalIgnoreCase)
                && !hasDamageCodes)
            {
                skipped++;
                continue;
            }

            var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            if (recommendation.Measures.Count == 0)
            {
                noSuggestion++;
                continue;
            }

            var value = string.Join(Environment.NewLine, recommendation.Measures);
            record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);
            foreach (var suggestion in recommendation.Measures)
                AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

            if (recommendation.EstimatedTotalCost is not null)
                record.SetFieldValue("Kosten", recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (recommendation.RenovierungInlinerM is not null)
                record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.RenovierungInlinerStk is not null)
                record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.AnschluesseVerpressen is not null)
                record.SetFieldValue("Anschluesse_verpressen", FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.ReparaturManschette is not null)
                record.SetFieldValue("Reparatur_Manschette", FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.ReparaturKurzliner is not null)
                record.SetFieldValue("Reparatur_Kurzliner", FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);

            filled++;
        }

        if (filled > 0)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        _shell.SetStatus($"Massnahmen: {filled} Haltungen befuellt, {skipped} uebersprungen, {noSuggestion} ohne Vorschlag");
    }

    private void OpenSanierungOptimizationWindow(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.AiOptimization);
    }

    private void OpenSanierungsmassnahmenWindow(HaltungRecord? record, InitialFocusMode focus)
    {
        record ??= Selected;
        if (record is null) return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            MessageBox.Show("Haltungsname fehlt in der Zeile.", "Sanierungsmassnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Build CostCalculatorViewModel
        var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        var costCalcVm = new CostCalculatorViewModel(
            holding,
            null,
            recommended,
            _sp.Settings.LastProjectPath,
            cost => ApplyCostsToRecord(record, cost),
            haltungRecord: record,
            projectRecords: Records);

        // Build SanierungOptimizationViewModel (nullable when AI disabled)
        SanierungOptimizationViewModel? optimizationVm = null;
        var cfg = AiRuntimeConfig.Load();
        if (cfg.Enabled)
        {
            var ruleResult = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            RuleRecommendationDto? ruleDto = null;
            if (ruleResult.Measures.Count > 0)
            {
                ruleDto = new RuleRecommendationDto
                {
                    Measures         = ruleResult.Measures,
                    EstimatedCost    = ruleResult.EstimatedTotalCost,
                    UsedTrainedModel = ruleResult.UsedTrainedModel
                };
            }

            var aiService = _sp.CreateSanierungOptimization(cfg);
            optimizationVm = new SanierungOptimizationViewModel(record, aiService, ruleDto);

            optimizationVm.TransferredToPrimary += _ =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty         = true;
                RefreshRecordInGrid(record);
                ScheduleAutoSave();
                _shell.SetStatus($"KI-Sanierungsvorschlag uebertragen: {holding}");
            };
        }

        var vm = new SanierungsmassnahmenViewModel(costCalcVm, optimizationVm, record, focus);
        var win = new SanierungsmassnahmenWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        win.ShowDialog();
    }

    private void ShowModelStatus()
    {
        var stats = _measureRecommendationService.GetStats();
        var status = stats.TrainedModelAvailable ? "Aktiv" : "Noch nicht trainiert";
        var trainedAt = stats.TrainedAtUtc is null
            ? "-"
            : stats.TrainedAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var modelSamples = stats.TrainedModelSamples?.ToString(CultureInfo.InvariantCulture) ?? "0";

        var message =
            $"Lernfaelle gesamt: {stats.TotalSamples}\n" +
            $"Schadenscodes: {stats.DistinctDamageCodes}\n" +
            $"Code-Signaturen: {stats.CodeSignatures}\n" +
            $"KI-Modell: {status}\n" +
            $"Modell-Faelle: {modelSamples}\n" +
            $"Letztes Training: {trainedAt}\n" +
            $"Modell-Datei:\n{stats.ModelPath}";

        MessageBox.Show(message, "KI-Modell Status", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private string? EnsureVideoPath(HaltungRecord record)
    {
        var resolved = ResolveExistingPath(record.GetFieldValue("Link"));
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            if (!string.Equals(resolved, record.GetFieldValue("Link")?.Trim(), StringComparison.OrdinalIgnoreCase))
                SaveVideoLink(record, resolved, userEdited: false);
            return resolved;
        }

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
        {
            var tool = new VideoSearchTool(initial);
            var res = tool.ResolveForRecord(record);
            if (res.Success && !string.IsNullOrWhiteSpace(res.VideoPath))
                return SaveVideoLink(record, res.VideoPath!, userEdited: false);
        }

        var folder = _sp.Dialogs.SelectFolder("Video-Ordner auswaehlen", initial);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        _sp.Settings.LastVideoSourceFolder = folder;
        _sp.Settings.LastVideoFolder = folder; // legacy compatibility
        _sp.Settings.Save();

        var toolManual = new VideoSearchTool(folder);
        var resManual = toolManual.ResolveForRecord(record);
        if (resManual.Success && !string.IsNullOrWhiteSpace(resManual.VideoPath))
            return SaveVideoLink(record, resManual.VideoPath!, userEdited: false);

        MessageBox.Show(resManual.Message, "Video", MessageBoxButton.OK, MessageBoxImage.Information);

        var manual = _sp.Dialogs.OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            folder);
        if (string.IsNullOrWhiteSpace(manual))
            return null;

        return SaveVideoLink(record, manual, userEdited: true);
    }

    private string SaveVideoLink(HaltungRecord record, string path, bool userEdited)
    {
        record.SetFieldValue("Link", path, FieldSource.Unknown, userEdited: userEdited);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        return path;
    }

    public void OpenMediaSearchWindow()
    {
        if (Records.Count == 0)
        {
            _shell.SetStatus("Keine Haltungen vorhanden.");
            return;
        }

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
                : null;

        var win = new MediaSearchWindow(Records.ToList(), initial);
        win.Owner = System.Windows.Application.Current?.MainWindow;

        if (win.ShowDialog() == true && win.Applied)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
            OnPropertyChanged(nameof(Records));
            _shell.SetStatus($"Medien verlinkt: {win.AppliedVideoCount} Videos, {win.AppliedPdfCount} PDFs, {win.AppliedFotoCount} Fotos");
        }
    }

    private void OpenHydraulikPanel(HaltungRecord? record)
    {
        var vm = new HydraulikPanelViewModel();

        if (record is not null)
        {
            var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
            var material = record.GetFieldValue("Rohrmaterial");
            vm.LoadFromRecord(dn, material, null);
        }

        var win = new HydraulikPanelWindow(vm);
        win.Owner = System.Windows.Application.Current?.MainWindow;
        win.ShowDialog();
    }

    private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = EnsureProtocolDocumentForPdf(record);
        var holding = record.GetFieldValue("Haltungsname");
        var defaultName = $"Haltungsprotokoll_AWU_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Haltungsprotokoll AWU als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new Application.Reports.HaltungsprotokollPdfOptions
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var projectFolder = _shell.GetProjectFolder() ?? string.Empty;
            var pdf = _sp.ProtocolPdfExporter.BuildHaltungsprotokollPdf(
                _shell.Project,
                record,
                doc,
                projectFolder,
                options);

            File.WriteAllBytes(output, pdf);
            MessageBox.Show($"AWU-Haltungsprotokoll wurde erstellt:\n{output}", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{ex.Message}", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ProtocolDocument EnsureProtocolDocumentForPdf(HaltungRecord record)
    {
        if (record.Protocol is not null)
        {
            record.Protocol.Current ??= new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = new List<ProtocolEntry>()
            };

            if ((record.Protocol.Original.Entries.Count == 0)
                && (record.Protocol.Current.Entries.Count == 0)
                && record.VsaFindings is { Count: > 0 })
            {
                var imported = BuildEntriesFromFindings(record.VsaFindings);
                record.Protocol = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", imported, null);
            }

            return record.Protocol;
        }

        var entries = record.VsaFindings is { Count: > 0 }
            ? BuildEntriesFromFindings(record.VsaFindings)
            : Array.Empty<ProtocolEntry>();
        return _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
    }

    private void PrintHydraulikPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build input from record
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm")) ?? 300;
        var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
        var vm = new HydraulikPanelViewModel();
        vm.LoadFromRecord(dn, materialRaw, null);

        var mat = vm.SelectedMaterial;
        double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
        double wasserstand = dn / 2; // default half-fill

        var input = new HydraulikInput(
            DN_mm: dn,
            Wasserstand_mm: wasserstand,
            Gefaelle_Promille: vm.Gefaelle,
            Kb: kb,
            AbwasserTyp: "MR",
            Temperatur_C: vm.Temperatur);

        var result = HydraulikEngine.Berechne(input);
        if (result is null)
        {
            MessageBox.Show("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show print options dialog
        var dialog = new HydraulikPrintDialog();
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        if (dialog.ShowDialog() != true || dialog.SelectedOptions is null)
            return;

        // SaveFile dialog
        var holding = record.GetFieldValue("Haltungsname") ?? "Haltung";
        var defaultName = $"Hydraulik_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Hydraulik-Bericht als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var calc = new Application.Reports.HydraulikCalcResult
            {
                DN_mm = input.DN_mm,
                Wasserstand_mm = input.Wasserstand_mm,
                Gefaelle_Promille = input.Gefaelle_Promille,
                Kb = input.Kb,
                AbwasserTyp = input.AbwasserTyp,
                Temperatur_C = input.Temperatur_C,
                Material = mat.Label,
                V_T = result.V_T,
                Q_T = result.Q_T,
                A_T = result.A_T,
                Lu_T = result.Lu_T,
                Rhy_T = result.Rhy_T,
                Bsp = result.Bsp,
                V_V = result.V_V,
                Q_V = result.Q_V,
                Re = result.Re,
                Fr = result.Fr,
                Lambda = result.Lambda,
                Tau = result.Tau,
                Ny = result.Ny,
                Vc = result.Abl.Vc,
                Ic = result.Abl.Ic,
                TauC = result.Abl.TauC,
                Auslastung = result.Auslastung,
                VelocityOk = result.VelocityOk,
                ShearOk = result.ShearOk,
                FroudeOk = result.Fr <= 1,
                AblagerungOk = result.AblagerungOk,
            };

            var pdf = Application.Reports.HydraulikPdfBuilder.Build(record, calc, options);
            File.WriteAllBytes(output, pdf);
            MessageBox.Show($"PDF wurde erstellt:\n{output}", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintDossierPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine Haltung auswaehlen.", "Dossier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "";
        var (vonNr, bisNr) = Application.Reports.ProtocolPdfExporter.SplitHoldingNodes(holdingLabel);

        var schachtVon = FindSchachtByNummer(vonNr);
        var schachtBis = FindSchachtByNummer(bisNr);

        // Hydraulik pruefen
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
        var gefaelleRaw = record.GetFieldValue("Gefaelle_Promille");
        double? gefaelle = null;
        if (!string.IsNullOrWhiteSpace(gefaelleRaw))
        {
            var gText = gefaelleRaw.Trim().Replace(',', '.');
            if (double.TryParse(gText, NumberStyles.Float, CultureInfo.InvariantCulture, out var gVal))
                gefaelle = gVal;
        }
        var hydraulikAvailable = dn.HasValue && dn.Value > 0 && gefaelle.HasValue && gefaelle.Value > 0;

        // Kosten pruefen
        var projectFolder = _shell.GetProjectFolder() ?? "";
        var costRepo = new Infrastructure.Costs.ProjectCostStoreRepository();
        var costStore = costRepo.Load(_sp.Settings.LastProjectPath);
        Domain.Models.HoldingCost? holdingCost = null;
        if (costStore.ByHolding.TryGetValue(holdingLabel.Trim(), out var hc))
            holdingCost = hc;
        var kostenField = record.GetFieldValue("Kosten");
        var kostenAvailable = holdingCost?.Measures is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(kostenField)
            || !string.IsNullOrWhiteSpace(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));

        // Original-PDFs pruefen (Haltung + Schaechte)
        var originalPdfPaths = ResolveOriginalPdfPaths(record, projectFolder);
        if (schachtVon != null)
            ResolveSchachtPdfPaths(schachtVon, projectFolder, originalPdfPaths);
        if (schachtBis != null)
            ResolveSchachtPdfPaths(schachtBis, projectFolder, originalPdfPaths);

        // Dialog oeffnen
        var dialog = new DossierPrintDialog();
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        dialog.SetAvailability(
            schachtVon != null, vonNr,
            schachtBis != null, bisNr,
            hydraulikAvailable,
            kostenAvailable,
            originalPdfPaths.Count);

        if (dialog.ShowDialog() != true || dialog.SelectedOptions is null)
            return;

        // SaveFileDialog
        var defaultName = $"Dossier_{SanitizeFilenamePart(holdingLabel)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Haltungsdossier als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            // Hydraulik berechnen falls gewuenscht
            Application.Reports.HydraulikCalcResult? calcResult = null;
            if (dialog.SelectedOptions.IncludeHydraulik && hydraulikAvailable)
            {
                var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
                var vm = new HydraulikPanelViewModel();
                vm.LoadFromRecord(dn!.Value, materialRaw, gefaelle);

                var mat = vm.SelectedMaterial;
                double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
                double wasserstand = dn.Value / 2;

                var input = new HydraulikInput(
                    DN_mm: dn.Value,
                    Wasserstand_mm: wasserstand,
                    Gefaelle_Promille: vm.Gefaelle,
                    Kb: kb,
                    AbwasserTyp: "MR",
                    Temperatur_C: vm.Temperatur);

                var result = HydraulikEngine.Berechne(input);
                if (result != null)
                {
                    calcResult = new Application.Reports.HydraulikCalcResult
                    {
                        DN_mm = input.DN_mm,
                        Wasserstand_mm = input.Wasserstand_mm,
                        Gefaelle_Promille = input.Gefaelle_Promille,
                        Kb = input.Kb,
                        AbwasserTyp = input.AbwasserTyp,
                        Temperatur_C = input.Temperatur_C,
                        Material = mat.Label,
                        V_T = result.V_T, Q_T = result.Q_T, A_T = result.A_T,
                        Lu_T = result.Lu_T, Rhy_T = result.Rhy_T, Bsp = result.Bsp,
                        V_V = result.V_V, Q_V = result.Q_V,
                        Re = result.Re, Fr = result.Fr, Lambda = result.Lambda,
                        Tau = result.Tau, Ny = result.Ny,
                        Vc = result.Abl.Vc, Ic = result.Abl.Ic, TauC = result.Abl.TauC,
                        Auslastung = result.Auslastung,
                        VelocityOk = result.VelocityOk, ShearOk = result.ShearOk,
                        FroudeOk = result.Fr <= 1, AblagerungOk = result.AblagerungOk,
                    };
                }
            }

            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null,
                HoldingCost = dialog.SelectedOptions.IncludeKostenschaetzung ? holdingCost : null,
                OriginalPdfPaths = dialog.SelectedOptions.IncludeOriginalProtokolle ? originalPdfPaths : null,
            };

            var hasDossierBaseSection =
                options.IncludeDeckblatt
                || options.IncludeHaltungsprotokoll
                || (options.IncludeFotos && HasPrintableDossierPhotos(record, projectFolder))
                || (options.IncludeSchachtVon && schachtVon != null)
                || (options.IncludeSchachtBis && schachtBis != null)
                || (options.IncludeHydraulik && calcResult != null)
                || (options.IncludeKostenschaetzung && kostenAvailable);

            var originalsAlreadyMerged = false;
            byte[] pdf;
            if (hasDossierBaseSection)
            {
                pdf = Application.Reports.HaltungsDossierPdfBuilder.Build(
                    _shell.Project, record, schachtVon, schachtBis, calcResult, projectFolder, options);
            }
            else if (options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
            {
                pdf = Infrastructure.Media.PdfMergeHelper.MergeOriginals(originalPdfPaths);
                if (pdf.Length == 0)
                    throw new InvalidOperationException("Die Original-Protokolle konnten nicht zusammengefuehrt werden.");
                originalsAlreadyMerged = true;
            }
            else
            {
                MessageBox.Show(
                    "Die ausgewaehlte Kombination enthaelt keine druckbaren Inhalte.",
                    "Dossier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Original-PDFs anhaengen
            if (!originalsAlreadyMerged && options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
                pdf = Infrastructure.Media.PdfMergeHelper.MergeWithOriginals(pdf, originalPdfPaths);

            File.WriteAllBytes(output, pdf);
            MessageBox.Show($"Dossier wurde erstellt:\n{output}", "Dossier", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dossier konnte nicht erstellt werden:\n{ex.Message}", "Dossier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenOriginalPdf(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = _shell.GetProjectFolder() ?? "";
        var paths = ResolveOriginalPdfPaths(record, projectFolder);

        if (paths.Count == 0)
        {
            var name = record.GetFieldValue("Haltungsname") ?? "(unbekannt)";
            MessageBox.Show(
                $"Kein PDF gefunden fuer Haltung '{name}'.\n\nPruefen Sie, ob das Original-PDF im Projektordner liegt.",
                "Haltungsprotokoll (PDF)",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(paths[0]) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht geoeffnet werden:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
    {
        var paths = new List<string>();

        // PDF_Path
        var pdfPath = record.GetFieldValue("PDF_Path")?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        // PDF_All (semikolon-getrennt)
        var pdfAll = record.GetFieldValue("PDF_All")?.Trim();
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            foreach (var part in pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries))
                AddResolvedPdf(paths, part.Trim(), projectFolder);
        }

        return paths;
    }

    private static bool HasPrintableDossierPhotos(HaltungRecord record, string projectFolder)
    {
        var entries = record.Protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return false;

        foreach (var entry in entries)
        {
            if (entry.IsDeleted || entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            foreach (var raw in entry.FotoPaths)
            {
                var resolved = ResolveDossierPhotoPath(raw, projectFolder);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    return true;
            }
        }

        return false;
    }

    private static string? ResolveDossierPhotoPath(string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return normalized;

        if (string.IsNullOrWhiteSpace(projectFolder))
            return null;

        return Path.GetFullPath(Path.Combine(projectFolder, normalized));
    }

    private static void ResolveSchachtPdfPaths(SchachtRecord schacht, string projectFolder, List<string> paths)
    {
        var pdfPath = schacht.GetFieldValue("PDF_Path")?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        var link = schacht.GetFieldValue("Link")?.Trim();
        if (!string.IsNullOrWhiteSpace(link) && link.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            AddResolvedPdf(paths, link, projectFolder);
    }

    private static void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);

        // Absoluter Pfad
        if (Path.IsPathRooted(normalized))
        {
            if (File.Exists(normalized))
            {
                if (!paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    paths.Add(normalized);
                return;
            }

            // Fallback: absoluter Pfad existiert nicht (Laufwerk nicht gemountet) → Dateinamen im Projektordner suchen
            if (!string.IsNullOrWhiteSpace(projectFolder))
            {
                var fallback = TryFindPdfInProject(Path.GetFileName(normalized), projectFolder);
                if (fallback != null && !paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                    paths.Add(fallback);
            }
            return;
        }

        // Relativer Pfad
        if (!string.IsNullOrWhiteSpace(projectFolder))
        {
            var combined = Path.GetFullPath(Path.Combine(projectFolder, normalized));
            if (File.Exists(combined))
            {
                if (!paths.Contains(combined, StringComparer.OrdinalIgnoreCase))
                    paths.Add(combined);
                return;
            }

            // Fallback: relativer Pfad nicht aufloesbar → Dateinamen im Projektordner suchen
            var fallback = TryFindPdfInProject(Path.GetFileName(normalized), projectFolder);
            if (fallback != null && !paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                paths.Add(fallback);
        }
    }

    private static string? TryFindPdfInProject(string fileName, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // 1. Direkt im Projektordner
        var direct = Path.Combine(projectFolder, fileName);
        if (File.Exists(direct))
            return direct;

        // 2. In Haltungen/<ID>/ Unterordnern
        var haltungenDir = Path.Combine(projectFolder, "Haltungen");
        if (Directory.Exists(haltungenDir))
        {
            try
            {
                var found = Directory.GetFiles(haltungenDir, fileName, SearchOption.AllDirectories);
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        // 3. In typischen Unterordnern (Misc, Docu, PDF, Protokolle)
        foreach (var sub in new[] { "Misc", "Docu", "PDF", "Protokolle", "Dokumente" })
        {
            var subDir = Path.Combine(projectFolder, sub);
            if (!Directory.Exists(subDir))
                continue;
            try
            {
                var found = Directory.GetFiles(subDir, fileName, SearchOption.AllDirectories);
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        return null;
    }

    private SchachtRecord? FindSchachtByNummer(string? nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            return null;
        return _shell.Project.SchaechteData.FirstOrDefault(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), nummer, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');
        return text.Trim();
    }

    private string? ResolveExistingPath(string? raw)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
        {
            var baseDir = Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(combined))
                    return combined;
            }
        }

        return null;
    }

    private void ShowSaveStatus(string? text)
    {
        SaveStatus = string.IsNullOrWhiteSpace(text) ? "Gespeichert" : text;
        IsSaveStatusVisible = true;
        _saveBannerTimer.Stop();
        _saveBannerTimer.Start();
    }

    private void UpdateLearningInfo(int? similarCases = null, decimal? estimatedCost = null)
    {
        var stats = _measureRecommendationService.GetStats();
        if (stats.TotalSamples <= 0)
        {
            LearningInfo = "Lernbasis: 0 Faelle";
            UpdateLearningTrafficLight(0);
            IsLearningInfoVisible = true;
            return;
        }

        var suffix = string.Empty;
        if (similarCases is not null && similarCases.Value > 0)
        {
            suffix = estimatedCost is null
                ? $" / letzte Schaetzung aus {similarCases.Value} aehnlichen Haltungen"
                : $" / letzte Kostenschaetzung {estimatedCost.Value:0.00} aus {similarCases.Value} aehnlichen Haltungen";
        }

        var modelText = stats.TrainedModelAvailable
            ? $" / KI-Modell aktiv ({stats.TrainedModelSamples ?? 0} Faelle)"
            : $" / KI-Modell ab {MinimumSamplesForModelTraining} Faellen";

        LearningInfo = $"Lernbasis: {stats.TotalSamples} Faelle{suffix}{modelText}";
        UpdateLearningTrafficLight(stats.TotalSamples);
        IsLearningInfoVisible = true;
    }

    private void UpdateLearningTrafficLight(int totalSamples)
    {
        if (totalSamples >= StrongModelThreshold)
        {
            LearningTrafficLightColor = "#2E7D32";
            LearningTrafficLightText = "Gruen";
            return;
        }

        if (totalSamples >= MinimumSamplesForModelTraining)
        {
            LearningTrafficLightColor = "#F9A825";
            LearningTrafficLightText = "Gelb";
            return;
        }

        LearningTrafficLightColor = "#C62828";
        LearningTrafficLightText = "Rot";
    }

    /// <summary>
    /// Lädt die CaseIds aus dem Training Center und normalisiert sie zu Haltungsnamen.
    /// </summary>
    private async Task LoadTrainedHaltungenAsync()
    {
        try
        {
            var store = new TrainingCenterStore();
            var state = await store.LoadAsync();
            TrainedHaltungen.Clear();
            foreach (var tc in state.Cases)
            {
                var name = NormalizeTrainingCaseId(tc.CaseId);
                if (!string.IsNullOrWhiteSpace(name))
                    TrainedHaltungen.Add(name);
            }
        }
        catch
        {
            // Training-Daten nicht verfügbar – kein Fehler
        }
    }

    /// <summary>
    /// Normalisiert eine Training-CaseId zu einem Haltungsnamen.
    /// Entfernt Datums-Prefixe wie "20250602_" und Knoten-Prefixe wie "07.", "10.".
    /// </summary>
    private static string NormalizeTrainingCaseId(string caseId)
    {
        var v = (caseId ?? "").Trim();
        // Datums-Prefix entfernen (z.B. "20250602_06.24341-35625" → "06.24341-35625")
        v = Regex.Replace(v, @"^\d{8}_", "");
        return v;
    }

    /// <summary>
    /// Prüft ob eine Haltung im Training Center erfasst ist.
    /// </summary>
    public bool IsTrainedCase(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname) || TrainedHaltungen.Count == 0)
            return false;
        // Exakter Match
        if (TrainedHaltungen.Contains(haltungsname))
            return true;
        // Ohne Knoten-Prefixe vergleichen (z.B. "07.1028055" → "1028055")
        var stripped = StripNodePrefixes(haltungsname);
        foreach (var trained in TrainedHaltungen)
        {
            if (string.Equals(StripNodePrefixes(trained), stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly Regex NodePrefixRx = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    private static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRx.Replace(holdingKey, "");
        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        return $"{NodePrefixRx.Replace(left, "")}-{NodePrefixRx.Replace(right, "")}";
    }

    public void EnsureOptionForField(string fieldName, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (fieldName == "Sanieren_JaNein")
            AddOptionIfMissing(SanierenOptions, text);
        else if (fieldName == "Eigentuemer")
            return;
        else if (fieldName == "Pruefungsresultat")
            AddOptionIfMissing(PruefungsresultatOptions, text);
        else if (fieldName == "Referenzpruefung")
            AddOptionIfMissing(ReferenzpruefungOptions, text);
        else if (fieldName == "Empfohlene_Sanierungsmassnahmen")
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, text);
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!AddOptionIfMissingCore(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return false;
        if (options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return false;
        options.Insert(0, text);
        return true;
    }

    /// <summary>
    /// Seeds measure template names from Offerten (MeasureTemplateStore) into the dropdown.
    /// Ensures all known template names are available for selection.
    /// </summary>
    private void SeedMeasureTemplateNames()
    {
        try
        {
            var store = new MeasureTemplateStore();
            var catalog = store.LoadMerged(_sp.Settings.LastProjectPath);
            foreach (var measure in catalog.Measures)
            {
                if (measure.Disabled)
                    continue;
                var name = measure.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (EmpfohleneSanierungsmassnahmenOptions.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                EmpfohleneSanierungsmassnahmenOptions.Add(name);
            }
        }
        catch
        {
            // Non-critical: template seeding failure should not block startup
        }
    }

    private void EditSanierenOptions()
    {
        var vm = new OptionsEditorViewModel(SanierenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            SanierenOptions.Clear();
            foreach (var item in vm.Items)
                SanierenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        System.Windows.MessageBox.Show(items, "Sanieren-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetSanierenOptions()
    {
        SanierenOptions.Clear();
        foreach (var item in new[] { "Nein", "Ja" })
            SanierenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddSanierenOption(object? value)
        => AddOptionIfMissing(SanierenOptions, ExtractText(value));

    private void RemoveSanierenOption(object? value)
        => RemoveOptionFromList(SanierenOptions, ExtractText(value));

    private void EditEigentuemerOptions()
    {
        var vm = new OptionsEditorViewModel(EigentuemerOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            EigentuemerOptions.Clear();
            foreach (var item in vm.Items)
                EigentuemerOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        System.Windows.MessageBox.Show(items, "Eigentuemer-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetEigentuemerOptions()
    {
        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEigentuemerOption(object? value)
        => AddOptionIfMissing(EigentuemerOptions, ExtractText(value));

    private void RemoveEigentuemerOption(object? value)
        => RemoveOptionFromList(EigentuemerOptions, ExtractText(value));

    private void EditPruefungsresultatOptions()
    {
        var vm = new OptionsEditorViewModel(PruefungsresultatOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            PruefungsresultatOptions.Clear();
            foreach (var item in vm.Items)
                PruefungsresultatOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewPruefungsresultatOptions()
    {
        var items = string.Join("\n", PruefungsresultatOptions);
        System.Windows.MessageBox.Show(items, "Pruefungsresultat-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetPruefungsresultatOptions()
    {
        PruefungsresultatOptions.Clear();
        foreach (var item in new[]
                 {
                     "Pruefung bestanden",
                     "Pruefung knapp nicht bestanden",
                     "Pruefung nicht bestanden (grob undicht)",
                     "Keine"
                 })
            PruefungsresultatOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddPruefungsresultatOption(object? value)
        => AddOptionIfMissing(PruefungsresultatOptions, ExtractText(value));

    private void RemovePruefungsresultatOption(object? value)
        => RemoveOptionFromList(PruefungsresultatOptions, ExtractText(value));

    private void EditReferenzpruefungOptions()
    {
        var vm = new OptionsEditorViewModel(ReferenzpruefungOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            ReferenzpruefungOptions.Clear();
            foreach (var item in vm.Items)
                ReferenzpruefungOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewReferenzpruefungOptions()
    {
        var items = string.Join("\n", ReferenzpruefungOptions);
        System.Windows.MessageBox.Show(items, "Referenzpruefung-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ResetReferenzpruefungOptions()
    {
        ReferenzpruefungOptions.Clear();
        foreach (var item in new[] { "Ja", "Nein" })
            ReferenzpruefungOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddReferenzpruefungOption(object? value)
        => AddOptionIfMissing(ReferenzpruefungOptions, ExtractText(value));

    private void RemoveReferenzpruefungOption(object? value)
        => RemoveOptionFromList(ReferenzpruefungOptions, ExtractText(value));

    private void EditEmpfohleneSanierungsmassnahmenOptions()
    {
        var vm = new OptionsEditorViewModel(EmpfohleneSanierungsmassnahmenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            EmpfohleneSanierungsmassnahmenOptions.Clear();
            foreach (var item in vm.Items)
                EmpfohleneSanierungsmassnahmenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEmpfohleneSanierungsmassnahmenOptions()
    {
        var items = string.Join("\n", EmpfohleneSanierungsmassnahmenOptions);
        System.Windows.MessageBox.Show(items, "Sanierungsmassnahmen-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ResetEmpfohleneSanierungsmassnahmenOptions()
    {
        EmpfohleneSanierungsmassnahmenOptions.Clear();
        foreach (var item in new[] { "" })
            EmpfohleneSanierungsmassnahmenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEmpfohleneSanierungsmassnahmenOption(object? value)
        => AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private void RemoveEmpfohleneSanierungsmassnahmenOption(object? value)
        => RemoveOptionFromList(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private static string ExtractText(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is System.Windows.Controls.ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        var existing = options.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;
        options.Remove(existing);
        SaveDropdownOptions();
    }

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        DropdownOptionsStore.SaveSanierenOptions(SanierenOptions);
        DropdownOptionsStore.SaveEigentuemerOptions(EigentuemerOptions);
        DropdownOptionsStore.SavePruefungsresultatOptions(PruefungsresultatOptions);
        DropdownOptionsStore.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
        DropdownOptionsStore.SaveEmpfohleneSanierungsmassnahmenOptions(EmpfohleneSanierungsmassnahmenOptions);
    }

    private void SyncDropdownOptionsFromRecords()
    {
        foreach (var record in Records)
        {
            AddOptionIfMissingCore(SanierenOptions, record.GetFieldValue("Sanieren_JaNein"));
            AddOptionIfMissingCore(PruefungsresultatOptions, record.GetFieldValue("Pruefungsresultat"));
            AddOptionIfMissingCore(ReferenzpruefungOptions, record.GetFieldValue("Referenzpruefung"));

            var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
            foreach (var entry in recommended)
                AddOptionIfMissingCore(EmpfohleneSanierungsmassnahmenOptions, entry);
        }
    }

    private void EnforceEigentuemerOptionsExact()
    {
        var same = EigentuemerOptions.Count == FixedEigentuemerOptions.Length;
        if (same)
        {
            for (var i = 0; i < FixedEigentuemerOptions.Length; i++)
            {
                if (!string.Equals(EigentuemerOptions[i], FixedEigentuemerOptions[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
            return;

        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
    }

    private static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeRecommendationEntry)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyCostsToRecord(HaltungRecord record, HoldingCost cost, bool learn = true, bool includeCosts = true)
    {
        if (includeCosts)
        {
            // Transfer net amount to table field "Kosten".
            var netTotal = ResolveNetTotal(cost);
            var totalText = netTotal.ToString("0.00", CultureInfo.InvariantCulture);
            record.SetFieldValue("Kosten", totalText, FieldSource.Manual, userEdited: true);
        }

        var massnahmenText = BuildMeasuresText(cost);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", massnahmenText, FieldSource.Manual, userEdited: true);

        var inlinerMeters = SumMeasureLengths(
            cost,
            "NADELFILZ",
            "GFK",
            "SCHLAUCHLINER_NADELFILZ",
            "SCHLAUCHLINER_NADELFILZ_OPENEND",
            "SCHLAUCHLINER_GFK");
        // Domain rule: if a liner is selected, count exactly 1 piece.
        var inlinerStk = HasSelectedLiner(cost) ? 1 : 0;
        var anschluesse = Math.Max(
            SumSelectedQty(cost, "ANSCHLUSS_EINBINDEN"),
            SumSelectedQty(cost, "ANSCHLUSS_AUFFRAESEN"));
        // LEM is not a repair manschette and must not fill Reparatur_Manschette.
        var manschette = SumSelectedQty(cost, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
        var lem = SumSelectedQty(cost, "LINERENDMANSCHETTE_LEM");
        var kurzliner = SumSelectedQty(cost, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

        record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(inlinerMeters), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(inlinerStk), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Anschluesse_verpressen", FormatNonNegativeInt(anschluesse), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Manschette", FormatInt(manschette), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Linerendmanschette_LEM", FormatInt(lem), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Kurzliner", FormatInt(kurzliner), FieldSource.Manual, userEdited: true);

        // Force a replace notification on the collection so dictionary-backed
        // grid cells refresh immediately without extra user clicks.
        RefreshRecordInGrid(record);

        if (learn)
        {
            var learnedNow = _measureRecommendationService.Learn(record);
            if (learnedNow)
                _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
            UpdateLearningInfo();
        }

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private static decimal ResolveNetTotal(HoldingCost cost)
    {
        if (cost.Total > 0m)
            return cost.Total;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
    }

    public void RefreshSelectedRecord()
    {
        if (Selected is not null)
            RefreshRecordInGrid(Selected);
    }

    private void RefreshRecordInGrid(HaltungRecord record)
    {
        var index = Records.IndexOf(record);
        if (index < 0)
            return;

        Records[index] = record;
        if (Selected?.Id == record.Id)
            Selected = record;
    }

    private static string BuildMeasuresText(HoldingCost cost)
    {
        // Automatically include selected rows that should always be written to recommendations.
        var autoIncludedPositions = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected && IsAutoIncludedRecommendationLine(l))
            .Select(l => FormatRecommendationBullet(l.Text))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Additionally include any transfer-marked rows (legacy/manual behavior).
        var markedPositions = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected && l.TransferMarked)
            .Select(l => FormatRecommendationBullet(l.Text))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var combined = autoIncludedPositions
            .Concat(markedPositions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (combined.Count > 0)
            return string.Join(Environment.NewLine, combined);

        return "";
    }

    private static bool IsAutoIncludedRecommendationLine(CostLine line)
    {
        if (line is null)
            return false;

        return IsHauptarbeitLine(line) || IsVerkehrsdienstLine(line);
    }

    private static bool IsHauptarbeitLine(CostLine line)
    {
        if (line is null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.Group) &&
            line.Group.Contains("hauptarbeit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsHauptarbeitIdentifier(line.ItemKey)
            || IsHauptarbeitIdentifier(line.Text);
    }

    private static bool IsHauptarbeitIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER")
            || MatchesIdentifier(value, "LINERENDMANSCHETTE")
            || MatchesIdentifier(value, "KURZLINER")
            || MatchesIdentifier(value, "MANSCHETTE")
            || MatchesIdentifier(value, "ANSCHLUSS_AUFFRAESEN")
            || MatchesIdentifier(value, "ANSCHLUSS_EINBINDEN")
            || MatchesIdentifier(value, "HAUPTARBEIT");
    }

    private static bool IsVerkehrsdienstLine(CostLine line)
    {
        if (line is null)
            return false;

        if (MatchesIdentifier(line.ItemKey, "VORARBEIT_VD"))
            return true;

        var text = line.Text ?? "";
        return text.Contains("verkehrsdienst", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRecommendationBullet(string? value)
    {
        var normalized = NormalizeRecommendationEntry(value);
        return normalized.Length == 0 ? string.Empty : "- " + normalized;
    }

    private static string NormalizeRecommendationEntry(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private static decimal SumMeasureLengths(HoldingCost cost, params string[] measureIds)
    {
        var sum = 0m;
        foreach (var measure in cost.Measures)
        {
            if (!measureIds.Any(id => MatchesIdentifier(measure.MeasureId, id)))
                continue;
            if (measure.LengthMeters is not null)
            {
                sum += measure.LengthMeters.Value;
                continue;
            }

            var fallback = measure.Lines
                .Where(l => l.Selected && string.Equals(l.Unit, "m", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Qty)
                .DefaultIfEmpty(0m)
                .Max();
            sum += fallback;
        }
        return sum;
    }

    private static bool HasSelectedLiner(HoldingCost cost)
    {
        foreach (var measure in cost.Measures)
        {
            var selectedLines = measure.Lines.Where(l => l.Selected).ToList();
            if (selectedLines.Count == 0)
                continue;

            if (selectedLines.Any(IsLinerLine))
                return true;

            // Fallback for legacy payloads where only measure id is reliable.
            if (IsLinerIdentifier(measure.MeasureId))
                return true;
        }

        return false;
    }

    private static bool IsLinerLine(CostLine line)
    {
        if (line is null)
            return false;

        if (IsLinerIdentifier(line.ItemKey))
            return true;

        var text = line.Text ?? "";
        return text.Contains("schlauchliner", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nadelfilz", StringComparison.OrdinalIgnoreCase)
            || text.Contains("gfk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLinerIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_OPENEND")
            || MatchesIdentifier(value, "SCHLAUCHLINER_GFK")
            || MatchesIdentifier(value, "NADELFILZ_LINER_BIS_5M")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_BIS_5M")
            || MatchesIdentifier(value, "NADELFILZ")
            || MatchesIdentifier(value, "GFK");
    }

    private static bool MatchesIdentifier(string? value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var candidate = value.Trim();
        var token = pattern.Trim();
        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy patterns like "NADELFILZ" or "GFK" should match newer IDs.
        if (token.IndexOf('_') >= 0 || token.IndexOf('-') >= 0)
            return false;

        return candidate.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static int SumSelectedQty(HoldingCost cost, params string[] itemKeys)
    {
        var total = 0m;
        foreach (var measure in cost.Measures)
        {
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;
                if (!itemKeys.Any(key => string.Equals(line.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                total += line.Qty;
            }
        }
        return (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);
    }

    private static string FormatDecimal(decimal value)
        => value <= 0m ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Filter predicate for the DataGrid's CollectionView. 
    /// Matches if the Haltungsname contains the search term (either side of the pair).
    /// </summary>
    public bool MatchesSearch(HaltungRecord record)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        var haltung = record.GetFieldValue("Haltungsname") ?? "";
        // Match against full haltungsname or individual shaft numbers
        if (haltung.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        // Also check Strasse
        var strasse = record.GetFieldValue("Strasse") ?? "";
        if (strasse.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Updates the search result info text.
    /// </summary>
    public void UpdateSearchResultInfo(int visibleCount)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            SearchResultInfo = string.Empty;
        else
            SearchResultInfo = $"{visibleCount} von {Records.Count} Haltungen";
    }

    private static string FormatInt(int value)
        => value <= 0 ? "" : value.ToString(CultureInfo.InvariantCulture);

    private static string FormatNonNegativeInt(int value)
        => Math.Max(0, value).ToString(CultureInfo.InvariantCulture);

    private void PersistDataPageBasicUiSettings()
    {
        var layout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.GridZoom = GridZoom;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _sp.Settings.DataPageLayout = layout;
        _sp.Settings.Save();
    }
}
