using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class BuilderPageViewModel : ObservableObject, IDisposable
{
    private const string AllFilterLabel = BuilderPageRowFilter.AllFilterLabel;
    private const string UnknownOwnerLabel = "Unbekannt";
    private static readonly string[] DefaultExecutedByValues =
    [
        "Kanalsanierer",
        "Baumeister",
        "Gartenbauer"
    ];
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly IProtocolPdfExporter _protocolPdfExporter;
    private readonly IDerivedCostFieldSynchronizer _costFieldSync;
    private readonly IDossierPhotoAvailabilityService _dossierPhotoAvailability;
    private readonly IInspectionProtocolFileLocator _inspectionProtocolFiles;
    private readonly IPdfMergeService _pdfMerge;
    private readonly ProjectCostStoreRepository _costRepo = new();
    private readonly CostCatalogStore _catalogStore = new();
    private readonly DispatcherTimer _refreshDebounceTimer;

    private List<DruckcenterRowVm> _allRows = new();
    private ProjectCostStore _costStore = new();
    private decimal _vatRate = CostCalculatorLogicService.DefaultVatRate;
    private ObservableCollection<HaltungRecord>? _attachedData;
    private bool _suspendFilterRefresh;
    private string _lastExportProjectPath = "";

    public ObservableCollection<DruckcenterRowVm> Rows { get; } = new();
    public ObservableCollection<SpecialPositionStatVm> SpecialPositionStats { get; } = new();
    public ObservableCollection<ChartBarVm> RehabilitationShareChart { get; } = new();
    public ObservableCollection<ChartBarVm> CostByExecutorChart { get; } = new();

    public ObservableCollection<string> OwnerFilterOptions { get; } = new();
    public ObservableCollection<string> ExecutedByFilterOptions { get; } = new();
    public ObservableCollection<string> SanierenFilterOptions { get; } = new();
    public ObservableCollection<string> MaterialFilterOptions { get; } = new();
    public ObservableCollection<string> StatusFilterOptions { get; } = new();
    public ObservableCollection<string> YearFilterOptions { get; } = new();

    [ObservableProperty] private string _selectedOwnerFilter = AllFilterLabel;
    [ObservableProperty] private string _selectedExecutedByFilter = AllFilterLabel;
    [ObservableProperty] private string _selectedSanierenFilter = AllFilterLabel;
    [ObservableProperty] private string _selectedMaterialFilter = AllFilterLabel;
    [ObservableProperty] private string _selectedStatusFilter = AllFilterLabel;
    [ObservableProperty] private string _selectedYearFilter = AllFilterLabel;
    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private DruckcenterRowVm? _selectedRow;

    [ObservableProperty] private bool _onlyWithCost;
    [ObservableProperty] private bool _onlyWithMeasures;

    [ObservableProperty] private bool _includeDataSection = true;
    [ObservableProperty] private bool _includeOwnerSummarySection = true;
    [ObservableProperty] private bool _includePositionSummarySection = true;

    [ObservableProperty] private int _totalRows;
    [ObservableProperty] private int _filteredRowsCount;
    [ObservableProperty] private int _rowsWithDetailedCosts;
    [ObservableProperty] private int _rowsWithoutCosts;
    [ObservableProperty] private int _rowsWithoutOwner;
    [ObservableProperty] private decimal _netTotal;

    [ObservableProperty] private decimal _statsInlinerGfk;
    [ObservableProperty] private decimal _statsInlinerNadelfilz;
    [ObservableProperty] private decimal _statsManschetten;
    [ObservableProperty] private decimal _statsLem;

    [ObservableProperty] private string _activeFilterText = "";
    [ObservableProperty] private string _specialStatsHint = "";
    [ObservableProperty] private string _specialPositionStatsHint = "";
    [ObservableProperty] private string _rehabilitationShareHint = "";
    [ObservableProperty] private string _costByExecutorHint = "";
    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportStateText))]
    [NotifyCanExecuteChangedFor(nameof(OpenLastExportedPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintPdfCommand))]
    private bool _isPdfExportInProgress;
    [ObservableProperty] private string _pdfExportProgress = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportStateText))]
    [NotifyCanExecuteChangedFor(nameof(OpenLastExportedPdfCommand))]
    private string _lastExportedPdfPath = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportStateText))]
    private bool _isLastExportCurrent;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportStateText))]
    private string _lastExportScopeSummary = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportStateText))]
    private DateTimeOffset? _lastExportedAt;

    public string NetTotalText => ChfFormat.Money(NetTotal);
    public string StatsInlinerGfkText => $"{StatsInlinerGfk:0.00} m";
    public string StatsInlinerNadelfilzText => $"{StatsInlinerNadelfilz:0.00} m";
    public string StatsManschettenText => $"{StatsManschetten:0.##} stk";
    public string StatsLemText => $"{StatsLem:0.##} stk";
    public string ExportStateText => BuildExportStateText();

    public BuilderPageViewModel(ShellViewModel shell, ServiceProvider services)
        : this(
            shell,
            settings: services.Settings,
            dialogs: services.Dialogs,
            protocolPdfExporter: services.ProtocolPdfExporter,
            costFieldSync: services.CostFieldSync,
            dossierPhotoAvailability: services.DossierPhotoAvailability,
            inspectionProtocolFiles: services.InspectionProtocolFiles)
    {
        _pdfMerge = services.PdfMerge;
    }

    public BuilderPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        IDerivedCostFieldSynchronizer costFieldSync,
        IDossierPhotoAvailabilityService? dossierPhotoAvailability = null,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _protocolPdfExporter = protocolPdfExporter ?? throw new ArgumentNullException(nameof(protocolPdfExporter));
        _costFieldSync = costFieldSync ?? throw new ArgumentNullException(nameof(costFieldSync));
        _dossierPhotoAvailability = dossierPhotoAvailability
            ?? DataPage.DataPageDossierAvailability.CompatibilityService;
        _inspectionProtocolFiles = inspectionProtocolFiles
            ?? DataPage.DataPageProtocolPathResolver.CompatibilityService;
        _pdfMerge = PdfMergeHelper.Current;
        _shell.PropertyChanged += ShellPropertyChanged;
        _refreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _refreshDebounceTimer.Tick += RefreshDebounceTimerTick;

        InitializeOptionCollections();
        AttachProjectData();
        RefreshData();
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshData();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        _suspendFilterRefresh = true;
        try
        {
            SelectedOwnerFilter = AllFilterLabel;
            SelectedExecutedByFilter = AllFilterLabel;
            SelectedSanierenFilter = AllFilterLabel;
            SelectedMaterialFilter = AllFilterLabel;
            SelectedStatusFilter = AllFilterLabel;
            SelectedYearFilter = AllFilterLabel;
            SearchText = "";
            OnlyWithCost = false;
            OnlyWithMeasures = false;
        }
        finally
        {
            _suspendFilterRefresh = false;
        }

        ApplyFilters();
    }
    partial void OnSelectedOwnerFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedExecutedByFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedSanierenFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedMaterialFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSelectedYearFilterChanged(string value) => ApplyFiltersIfReady();
    partial void OnSearchTextChanged(string value) => ApplyFiltersIfReady();
    partial void OnOnlyWithCostChanged(bool value) => ApplyFiltersIfReady();
    partial void OnOnlyWithMeasuresChanged(bool value) => ApplyFiltersIfReady();
    partial void OnIncludeDataSectionChanged(bool value) => MarkExportAsStale();
    partial void OnIncludeOwnerSummarySectionChanged(bool value) => MarkExportAsStale();
    partial void OnIncludePositionSummarySectionChanged(bool value) => MarkExportAsStale();

    partial void OnNetTotalChanged(decimal value) => OnPropertyChanged(nameof(NetTotalText));
    partial void OnStatsInlinerGfkChanged(decimal value) => OnPropertyChanged(nameof(StatsInlinerGfkText));
    partial void OnStatsInlinerNadelfilzChanged(decimal value) => OnPropertyChanged(nameof(StatsInlinerNadelfilzText));
    partial void OnStatsManschettenChanged(decimal value) => OnPropertyChanged(nameof(StatsManschettenText));
    partial void OnStatsLemChanged(decimal value) => OnPropertyChanged(nameof(StatsLemText));

    private void ShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Project))
        {
            AttachProjectData();
            RefreshData();
        }
    }

    private void AttachProjectData()
    {
        if (_attachedData is not null)
        {
            _attachedData.CollectionChanged -= ProjectDataCollectionChanged;
            DetachRecordHandlers(_attachedData);
        }

        _attachedData = _shell.Project.Data;
        _attachedData.CollectionChanged += ProjectDataCollectionChanged;
        AttachRecordHandlers(_attachedData);
    }

    private void ProjectDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var record in e.OldItems.OfType<HaltungRecord>())
                record.PropertyChanged -= RecordPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var record in e.NewItems.OfType<HaltungRecord>())
                record.PropertyChanged += RecordPropertyChanged;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset && _attachedData is not null)
        {
            DetachRecordHandlers(_attachedData);
            AttachRecordHandlers(_attachedData);
        }

        ScheduleRefreshData();
    }

    private void AttachRecordHandlers(IEnumerable<HaltungRecord> records)
    {
        foreach (var record in records)
            record.PropertyChanged += RecordPropertyChanged;
    }

    private void DetachRecordHandlers(IEnumerable<HaltungRecord> records)
    {
        foreach (var record in records)
            record.PropertyChanged -= RecordPropertyChanged;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shell.PropertyChanged -= ShellPropertyChanged;
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Tick -= RefreshDebounceTimerTick;
        if (_attachedData is not null)
        {
            _attachedData.CollectionChanged -= ProjectDataCollectionChanged;
            DetachRecordHandlers(_attachedData);
        }
    }

    private void RecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            ScheduleRefreshDataOnUiThread();
            return;
        }

        if (e.PropertyName == nameof(HaltungRecord.Fields) ||
            e.PropertyName == nameof(HaltungRecord.ModifiedAtUtc) ||
            e.PropertyName.StartsWith("Fields[", StringComparison.Ordinal))
        {
            ScheduleRefreshDataOnUiThread();
        }
    }

    private void ScheduleRefreshDataOnUiThread()
    {
        if (_refreshDebounceTimer.Dispatcher.CheckAccess())
        {
            ScheduleRefreshData();
            return;
        }

        _refreshDebounceTimer.Dispatcher.BeginInvoke((Action)ScheduleRefreshData);
    }

    private void ScheduleRefreshData()
    {
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void RefreshDebounceTimerTick(object? sender, EventArgs e)
    {
        _refreshDebounceTimer.Stop();
        RefreshData();
    }

    private void InitializeOptionCollections()
    {
        OwnerFilterOptions.Clear();
        ExecutedByFilterOptions.Clear();
        SanierenFilterOptions.Clear();
        MaterialFilterOptions.Clear();
        StatusFilterOptions.Clear();
        YearFilterOptions.Clear();

        OwnerFilterOptions.Add(AllFilterLabel);
        ExecutedByFilterOptions.Add(AllFilterLabel);
        SanierenFilterOptions.Add(AllFilterLabel);
        MaterialFilterOptions.Add(AllFilterLabel);
        StatusFilterOptions.Add(AllFilterLabel);
        YearFilterOptions.Add(AllFilterLabel);
    }

    private void RefreshData()
    {
        var projectPath = _settings.LastProjectPath ?? "";
        if (!string.Equals(_lastExportProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
            ClearLastExport();

        _costStore = _costRepo.Load(projectPath);

        var catalog = _catalogStore.LoadMerged(projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;

        _suspendFilterRefresh = true;
        try
        {
            _allRows = BuildRows();
            RebuildFilterOptions();
        }
        finally
        {
            _suspendFilterRefresh = false;
        }

        ApplyFilters();
    }

    private bool OfferRecomputeCostsForCurrentCatalog(IReadOnlyList<DruckcenterRowVm> filteredRows)
    {
        var oldRates = FindMismatchedVatRates(filteredRows, _vatRate);
        if (oldRates.Count == 0)
            return true;

        var decision = _dialogs.ConfirmCancel(
            BuildVatRecomputePrompt(oldRates, _vatRate),
            "Druckcenter");
        if (decision == DialogConfirm.Cancel)
            return false;
        if (decision == DialogConfirm.No)
            return true;

        return RecomputeStoredCostsWithCurrentCatalog();
    }

    private bool RecomputeStoredCostsWithCurrentCatalog()
    {
        var projectPath = _settings.LastProjectPath ?? "";
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _dialogs.Info("Projekt bitte zuerst speichern, um Kosten neu zu berechnen.", "Druckcenter");
            return false;
        }

        var store = _costRepo.Load(projectPath, out var loadError);
        if (!string.IsNullOrWhiteSpace(loadError))
        {
            _dialogs.Error(
                $"Kosten konnten nicht neu berechnet werden, weil costs.json nicht sauber geladen werden konnte:\n{loadError}",
                "Druckcenter");
            return false;
        }

        var catalog = _catalogStore.LoadMerged(projectPath);
        var vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
        var changedHoldings = CatalogPriceApplier.ApplyCatalogPricesToStoredCosts(
            store,
            BuildCatalogMap(catalog),
            vatRate);

        if (changedHoldings.Count > 0 && !_costRepo.Save(projectPath, store, out var saveError))
        {
            _dialogs.Error($"Kosten konnten nicht gespeichert werden:\n{saveError}", "Druckcenter");
            return false;
        }

        _vatRate = vatRate;
        _costStore = store;
        // Abgeleitete Kostenfelder aller Haltungen nach der Sanieren-Regel nachziehen.
        _costFieldSync.Sync(_shell.Project, store);
        RefreshData();

        LastResult = changedHoldings.Count == 0
            ? "Kosten waren bereits aktuell."
            : $"Kosten neu berechnet: {changedHoldings.Count} Haltung(en).";
        _shell.SetStatus(LastResult);
        return true;
    }

    private static Dictionary<string, CostCatalogItem> BuildCatalogMap(CostCatalog catalog)
        => catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private List<DruckcenterRowVm> BuildRows()
    {
        var rows = new List<DruckcenterRowVm>(_shell.Project.Data.Count);
        foreach (var record in _shell.Project.Data)
        {
            var holding = SafeText(record.GetFieldValue("Haltungsname"));
            if (holding.Length == 0)
                holding = "(ohne Haltungsname)";

            var owner = SafeText(record.GetFieldValue("Eigentuemer"));
            if (owner.Length == 0 && _shell.Project.Metadata.TryGetValue("Eigentuemer", out var ownerMeta))
                owner = SafeText(ownerMeta);
            if (owner.Length == 0)
                owner = UnknownOwnerLabel;

            var sanieren = SafeText(record.GetFieldValue("Sanieren_JaNein"));
            var executedBy = SafeText(record.GetFieldValue("Ausgefuehrt_durch"));
            if (executedBy.Length == 0) executedBy = "(unbekannt)";
            var material = SafeText(record.GetFieldValue("Rohrmaterial"));
            if (material.Length == 0) material = "(unbekannt)";
            var status = SafeText(record.GetFieldValue("Offen_abgeschlossen"));
            var year = NormalizeYear(record.GetFieldValue("Datum_Jahr"));
            var street = SafeText(record.GetFieldValue("Strasse"));
            var zustand = SafeText(record.GetFieldValue("Zustandsklasse"));

            var recommendedRaw = record.GetFieldValue("Empfohlene_Sanierungsmassnahmen");
            var recommendedPreview = BuildMeasurePreview(recommendedRaw);

            var recordCost = ParseDecimal(record.GetFieldValue("Kosten")) ?? 0m;
            var storedCost = TryGetCostByHolding(holding);
            var hasDetailedCost = storedCost is not null && HasSelectedLines(storedCost);
            var netCost = storedCost is null ? recordCost : ResolveNetTotal(storedCost);
            // Store-Eintrag mit Total 0 (z.B. alles abgewaehlt) darf den manuell gepflegten
            // Tabellenwert "Kosten" nicht verdraengen (Audit W11) — sonst fehlt die Haltung
            // still in Netto-Summe und Druck.
            if (netCost <= 0m && recordCost > 0m)
                netCost = recordCost;

            if (netCost < 0m)
                netCost = 0m;

            rows.Add(new DruckcenterRowVm
            {
                Record = record,
                Holding = holding,
                Street = street,
                Owner = owner,
                Sanieren = sanieren,
                ExecutedBy = executedBy,
                Material = material,
                Status = status,
                Year = year,
                Zustand = zustand,
                NetCost = netCost,
                StoredCost = storedCost,
                HasDetailedCost = hasDetailedCost,
                HasMeasures = hasDetailedCost || !string.IsNullOrWhiteSpace(recommendedPreview),
                CostSource = hasDetailedCost
                    ? "Positionsdetails"
                    : netCost > 0m
                        ? (storedCost is null ? "Tabellenwert" : "Kostenstore")
                        : "Keine Kosten",
                MeasuresRaw = recommendedRaw ?? "",
                MeasuresPreview = recommendedPreview
            });
        }

        return rows
            .OrderBy(r => string.IsNullOrWhiteSpace(r.ExecutedBy) ? 1 : 0)
            .ThenBy(r => r.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Holding, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private HoldingCost? TryGetCostByHolding(string holding)
    {
        if (string.IsNullOrWhiteSpace(holding))
            return null;

        if (_costStore.ByHolding.TryGetValue(holding, out var direct))
            return direct;

        foreach (var kvp in _costStore.ByHolding)
        {
            if (string.Equals(kvp.Key, holding, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    private void RebuildFilterOptions()
    {
        RebuildOptionCollection(
            OwnerFilterOptions,
            _allRows.Select(r => r.Owner).Where(v => v.Length > 0),
            SelectedOwnerFilter,
            value => SelectedOwnerFilter = value);

        var executedByValues = _allRows
            .Select(r => r.ExecutedBy)
            .Where(v => v.Length > 0)
            .Concat(DefaultExecutedByValues);

        if (!string.IsNullOrWhiteSpace(SelectedExecutedByFilter) &&
            !SelectedExecutedByFilter.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
        {
            executedByValues = executedByValues.Concat(new[] { SelectedExecutedByFilter.Trim() });
        }

        RebuildOptionCollection(
            ExecutedByFilterOptions,
            executedByValues,
            SelectedExecutedByFilter,
            value => SelectedExecutedByFilter = value);

        RebuildOptionCollection(
            SanierenFilterOptions,
            _allRows.Select(r => r.Sanieren).Where(v => v.Length > 0),
            SelectedSanierenFilter,
            value => SelectedSanierenFilter = value);

        RebuildOptionCollection(
            MaterialFilterOptions,
            _allRows.Select(r => r.Material).Where(v => v.Length > 0),
            SelectedMaterialFilter,
            value => SelectedMaterialFilter = value);

        RebuildOptionCollection(
            StatusFilterOptions,
            _allRows.Select(r => r.Status).Where(v => v.Length > 0),
            SelectedStatusFilter,
            value => SelectedStatusFilter = value);

        RebuildOptionCollection(
            YearFilterOptions,
            _allRows.Select(r => r.Year).Where(v => v.Length > 0),
            SelectedYearFilter,
            value => SelectedYearFilter = value);
    }

    private static void RebuildOptionCollection(
        ObservableCollection<string> target,
        IEnumerable<string> values,
        string selected,
        Action<string> setSelected)
    {
        var allValues = values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        target.Clear();
        target.Add(AllFilterLabel);
        foreach (var value in allValues)
            target.Add(value);

        if (target.Contains(selected))
            setSelected(selected);
        else
            setSelected(AllFilterLabel);
    }

    private void ApplyFilters()
    {
        var filtered = BuilderPageRowFilter.Apply(
            _allRows,
            new BuilderPageFilterCriteria(
                SelectedOwnerFilter,
                SelectedExecutedByFilter,
                SelectedSanierenFilter,
                SelectedMaterialFilter,
                SelectedStatusFilter,
                SelectedYearFilter,
                SearchText,
                OnlyWithCost,
                OnlyWithMeasures));

        Rows.Clear();
        foreach (var row in filtered)
            Rows.Add(row);

        UpdateStatistics(filtered);
        ActiveFilterText = BuildFilterSummaryText();
        MarkExportAsStale();
    }

    private void ApplyFiltersIfReady()
    {
        if (_suspendFilterRefresh)
            return;

        ApplyFilters();
    }

    private void UpdateStatistics(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        TotalRows = _allRows.Count;
        FilteredRowsCount = filtered.Count;
        RowsWithDetailedCosts = filtered.Count(row => row.HasDetailedCost);
        RowsWithoutCosts = filtered.Count(row => row.NetCost <= 0m);
        RowsWithoutOwner = filtered.Count(row => row.Owner.Equals(UnknownOwnerLabel, StringComparison.OrdinalIgnoreCase));
        NetTotal = filtered.Sum(row => row.NetCost);

        var specialStats = BuilderPageSpecialStatsCalculator.Compute(filtered);
        StatsInlinerGfk = specialStats.InlinerGfk;
        StatsInlinerNadelfilz = specialStats.InlinerNadelfilz;
        StatsManschetten = specialStats.Manschetten;
        StatsLem = specialStats.Linerendmanschetten;
        var positionStats = specialStats.PositionStats;
        SpecialPositionStatsHint = positionStats.Count == 0
            ? "Keine spezialrelevanten Positionen in den gewaehlten Massnahmen gefunden."
            : $"Einzelpositionen aus Massnahmen: {positionStats.Count}";

        SpecialPositionStats.Clear();
        foreach (var item in positionStats)
            SpecialPositionStats.Add(item);

        SpecialStatsHint = RowsWithDetailedCosts == FilteredRowsCount
            ? "Spezialstatistik auf Basis aller gefilterten Haltungen."
            : $"Spezialstatistik basiert auf {RowsWithDetailedCosts} von {FilteredRowsCount} Haltungen mit Positionsdetails.";

        UpdateRehabilitationShareChart(filtered);
        UpdateCostByExecutorChart(filtered);
    }

    private void UpdateRehabilitationShareChart(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        var total = filtered.Count;
        var yesCount = filtered.Count(row => IsSanierenYes(row.Sanieren));
        var noCount = filtered.Count(row => IsSanierenNo(row.Sanieren));
        var openCount = Math.Max(0, total - yesCount - noCount);

        RehabilitationShareChart.Clear();
        RehabilitationShareChart.Add(new ChartBarVm("Sanierung noetig", yesCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Keine Sanierung", noCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Nicht bewertet", openCount, total));

        var yesPercent = total > 0 ? yesCount * 100.0 / total : 0.0;
        var basis = filtered.Count == _allRows.Count ? "Haltungen" : "gefilterten Haltungen";
        RehabilitationShareHint = total == 0
            ? "Keine Haltungen im Projekt."
            : $"{yesPercent:0.#}% von {total} {basis} sind als 'Sanieren = Ja' markiert.";
    }

    private void UpdateCostByExecutorChart(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        CostByExecutorChart.Clear();
        var groups = filtered
            .GroupBy(
                row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? "Unbekannt" : row.ExecutedBy.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Role = g.Key, Total = g.Sum(x => x.NetCost) })
            .Where(x => x.Total > 0m)
            .OrderByDescending(x => x.Total)
            .ToList();

        var totalCost = groups.Sum(x => x.Total);
        foreach (var group in groups)
            CostByExecutorChart.Add(new ChartBarVm(group.Role, group.Total, totalCost));

        CostByExecutorHint = totalCost <= 0m
            ? "Keine Kosten in der aktuellen Filterauswahl."
            : $"Kostenverteilung nach 'Ausgefuehrt durch' (Basis: {filtered.Count} gefilterte Haltungen).";
    }

    private string BuildFilterSummaryText()
    {
        var parts = new List<string>();

        AddFilterPart(parts, "Eigentuemer", SelectedOwnerFilter);
        AddFilterPart(parts, "Ausgefuehrt durch", SelectedExecutedByFilter);
        AddFilterPart(parts, "Sanieren", SelectedSanierenFilter);
        AddFilterPart(parts, "Material", SelectedMaterialFilter);
        AddFilterPart(parts, "Status", SelectedStatusFilter);
        AddFilterPart(parts, "Jahr", SelectedYearFilter);

        if (OnlyWithCost)
            parts.Add("nur mit Kosten");
        if (OnlyWithMeasures)
            parts.Add("nur mit Massnahmen");

        var search = (SearchText ?? "").Trim();
        if (search.Length > 0)
            parts.Add($"Suche='{search}'");

        parts.Add($"Treffer={FilteredRowsCount}/{TotalRows}");
        return string.Join(" | ", parts);
    }

    private string BuildExportStateText()
    {
        if (!HasAnyExportPath())
            return "Noch kein Druckcenter-PDF exportiert.";

        if (!HasLastExportedPdf())
            return "Letztes Export-PDF wurde nicht gefunden.";

        var fileName = Path.GetFileName(LastExportedPdfPath);
        var timestamp = LastExportedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Ch) ?? "unbekannt";
        var summary = string.IsNullOrWhiteSpace(LastExportScopeSummary)
            ? fileName
            : $"{fileName} | {LastExportScopeSummary}";

        return IsLastExportCurrent
            ? $"Aktueller Export: {summary} | {timestamp}"
            : $"Letzter Export veraltet: {summary} | {timestamp}";
    }

    private string BuildExportScopeSummary(IReadOnlyList<DruckcenterRowVm> filteredRows)
        => $"{filteredRows.Count} Haltungen | Netto {Money(filteredRows.Sum(r => r.NetCost))}";

    private void MarkExportAsStale()
    {
        if (IsPdfExportInProgress || !HasAnyExportPath())
            return;

        if (!HasLastExportedPdf())
        {
            ClearLastExport("Die zuletzt exportierte PDF-Datei wurde nicht gefunden.");
            return;
        }

        IsLastExportCurrent = false;
    }

    private void ClearLastExport(string? resultText = null)
    {
        LastExportedPdfPath = "";
        LastExportScopeSummary = "";
        LastExportedAt = null;
        IsLastExportCurrent = false;
        _lastExportProjectPath = "";

        if (!string.IsNullOrWhiteSpace(resultText))
            LastResult = resultText;
    }

    private bool HasAnyExportPath()
        => !string.IsNullOrWhiteSpace(LastExportedPdfPath);

    private bool HasLastExportedPdf()
        => HasAnyExportPath() && File.Exists(LastExportedPdfPath);

    private bool CanOpenLastExportedPdf()
        => !IsPdfExportInProgress && HasLastExportedPdf();

    private bool CanPrintPdf()
        => !IsPdfExportInProgress;

    private static void AddFilterPart(List<string> parts, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
            return;

        parts.Add($"{label}={value}");
    }

    private static bool HasSelectedLines(HoldingCost cost)
        => TablePauschaleCostHelper.HasDetailedCost(cost);

    private static decimal ResolveNetTotal(HoldingCost cost)
        => TablePauschaleCostHelper.ResolveNetTotal(cost);

    private static string SafeText(string? value)
        => (value ?? "").Trim();

    private static string NormalizeYear(string? value)
    {
        var text = SafeText(value);
        if (text.Length >= 4 && int.TryParse(text[..4], out var year) && year >= 1900 && year <= 2200)
            return year.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    private static decimal? ParseDecimal(string? value)
    {
        var text = SafeText(value);
        if (text.Length == 0)
            return null;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var current))
            return current;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        var normalized = text.Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var normalizedValue))
            return normalizedValue;

        return null;
    }

    private static string BuildMeasurePreview(string? raw)
    {
        var entries = ParseMeasureEntries(raw);
        if (entries.Count == 0)
            return "";
        if (entries.Count == 1)
            return entries[0];
        if (entries.Count == 2)
            return $"{entries[0]}; {entries[1]}";
        return $"{entries[0]}; {entries[1]} (+{entries.Count - 2} weitere)";
    }

    private static List<string> ParseMeasureEntries(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeMeasureEntry)
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeMeasureEntry(string? value)
    {
        var text = SafeText(value);
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private static string SanitizeFilePart(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "Projekt" : value.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string Money(decimal value) => ChfFormat.Money(value);

    private static string BuildVatMismatchHint(IReadOnlyList<CostSummaryEntry> entries, decimal currentVatRate)
    {
        var oldRates = entries
            .Select(e => e.Cost.MwstRate)
            .Where(rate => rate > 0m && rate != currentVatRate)
            .Distinct()
            .OrderBy(rate => rate)
            .Select(FormatVatRate)
            .ToList();
        if (oldRates.Count == 0)
            return "";

        return "Hinweis: Diese Ausgabe enthaelt gespeicherte MwSt-Saetze "
            + string.Join(", ", oldRates)
            + $"; aktueller Katalogsatz ist {FormatVatRate(currentVatRate)}. "
            + "Kosten neu berechnen, wenn der aktuelle Satz gelten soll.";
    }

    private static IReadOnlyList<decimal> FindMismatchedVatRates(IEnumerable<DruckcenterRowVm> rows, decimal currentVatRate)
        => rows
            .Select(row => row.StoredCost?.MwstRate ?? 0m)
            .Where(rate => rate > 0m && rate != currentVatRate)
            .Distinct()
            .OrderBy(rate => rate)
            .ToList();

    private static string BuildVatRecomputePrompt(IReadOnlyList<decimal> oldRates, decimal currentVatRate)
        => "Die gefilterte Ausgabe enthaelt gespeicherte Kosten mit altem MwSt-Satz "
           + string.Join(", ", oldRates.Select(FormatVatRate))
           + $".\nAktueller Katalogsatz ist {FormatVatRate(currentVatRate)}.\n\n"
           + "Ja = alle gespeicherten Sanierungskosten jetzt mit aktuellem Katalog/MwSt neu berechnen\n"
           + "Nein = Ausgabe mit gespeicherten Saetzen fortsetzen\n"
           + "Abbrechen = Export abbrechen\n\n"
           + "Manuelle Preis-Overrides bleiben unangetastet.";

    private static string FormatVatRate(decimal rate)
        => $"{rate * 100m:0.0}%";

    private static string BuildReferenceBlock(IReadOnlyDictionary<string, string> metadata)
    {
        var lines = new List<string>();
        AddReferenceLine(lines, "Ihre Referenz", metadata, "Referenz", "IhreReferenz", "AuftraggeberReferenz");
        AddReferenceLine(lines, "Unsere Referenz", metadata, "Bearbeiter", "UnsereReferenz");
        AddReferenceLine(lines, "Auftrag-Nr.", metadata, "AuftragNr");
        return string.Join("\n", lines);
    }

    private static void AddReferenceLine(
        List<string> lines,
        string label,
        IReadOnlyDictionary<string, string> metadata,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            lines.Add($"{label}: {value.Trim()}");
            return;
        }
    }

    private static bool IsSanierenYes(string value)
    {
        var normalized = SafeText(value);
        return normalized.Equals("ja", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSanierenNo(string value)
    {
        var normalized = SafeText(value);
        return normalized.Equals("nein", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("0", StringComparison.OrdinalIgnoreCase);
    }
}
