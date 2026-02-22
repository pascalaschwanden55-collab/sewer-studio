using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class BuilderPageViewModel : ObservableObject
{
    private const string AllFilterLabel = "Alle";
    private const string UnknownOwnerLabel = "Unbekannt";
    private static readonly string[] DefaultExecutedByValues =
    [
        "Kanalsanierer",
        "Baumeister",
        "Gartenbauer"
    ];
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly ProjectCostStoreRepository _costRepo = new();
    private readonly CostCatalogStore _catalogStore = new();
    private readonly DispatcherTimer _refreshDebounceTimer;

    private List<DruckcenterRowVm> _allRows = new();
    private ProjectCostStore _costStore = new();
    private decimal _vatRate = 0.081m;
    private ObservableCollection<HaltungRecord>? _attachedData;

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
    [ObservableProperty] private bool _isPdfExportInProgress;
    [ObservableProperty] private string _pdfExportProgress = "";
    [ObservableProperty] private string _lastExportedPdfPath = "";

    public string NetTotalText => $"{NetTotal:N2} CHF";
    public string StatsInlinerGfkText => $"{StatsInlinerGfk:0.00} m";
    public string StatsInlinerNadelfilzText => $"{StatsInlinerNadelfilz:0.00} m";
    public string StatsManschettenText => $"{StatsManschetten:0.##} stk";
    public string StatsLemText => $"{StatsLem:0.##} stk";

    public BuilderPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
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
        SelectedOwnerFilter = AllFilterLabel;
        SelectedExecutedByFilter = AllFilterLabel;
        SelectedSanierenFilter = AllFilterLabel;
        SelectedMaterialFilter = AllFilterLabel;
        SelectedStatusFilter = AllFilterLabel;
        SelectedYearFilter = AllFilterLabel;
        SearchText = "";
        OnlyWithCost = false;
        OnlyWithMeasures = false;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (IsPdfExportInProgress)
            return;

        RefreshData();
        var filteredRows = Rows.ToList();
        if (filteredRows.Count == 0)
        {
            MessageBox.Show(
                "Keine Daten fuer den aktuellen Filter gefunden.",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var safeProjectName = SanitizeFilePart(_shell.Project.Name);
        var defaultName = $"Druckcenter_{safeProjectName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Druckcenter PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        IsPdfExportInProgress = true;
        PdfExportProgress = "PDF wird vorbereitet...";

        try
        {
            await Task.Yield();
            var entries = BuildSummaryEntries(filteredRows);
            var dataLines = IncludeDataSection ? BuildHoldingDataLines(filteredRows) : null;

            var projectMeta = _shell.Project.Metadata;
            var projectCustomer = BuildProjectCustomerBlock(projectMeta);
            var objectBlock = BuildObjectBlock(projectMeta, filteredRows);
            var filterSummary = BuildFilterSummaryText();
            var qualityHint = RowsWithDetailedCosts == FilteredRowsCount
                ? "Alle gefilterten Haltungen haben Positionsdetails."
                : $"{FilteredRowsCount - RowsWithDetailedCosts} Haltung(en) ohne Positionsdetails (Pauschalwerte aus Tabelle).";

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri - Druckcenter",
                VariantTitle = $"Gefilterte Kostenzusammenstellung ({filteredRows.Count} Haltungen)",
                CustomerBlock = projectCustomer,
                ObjectBlock = objectBlock,
                FilterSummaryText = filterSummary,
                Currency = "CHF",
                OfferNo = "",
                TextBlocks = new List<string>
                {
                    qualityHint,
                    "Die Statistik fuer Inliner/Manschetten basiert auf vorhandenen Positionsdetails.",
                    "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen ist im Ausdruck enthalten."
                }
            };

            var model = OfferPdfModelFactory.CreateCostSummary(
                entries,
                ctx,
                DateTimeOffset.Now,
                includeOwnerSummary: IncludeOwnerSummarySection,
                includePositionSummary: IncludePositionSummarySection,
                holdingDataLines: dataLines);

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "cost_summary.sbnhtml");
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

            var renderer = new OfferHtmlToPdfRenderer();
            PdfExportProgress = "PDF wird gerendert...";
            await renderer.RenderAsync(model, templatePath, output, logoPath);

            LastExportedPdfPath = output;
            LastResult = $"PDF erstellt: {output}";
            _shell.SetStatus("Druckcenter PDF erstellt");
            PdfExportProgress = "PDF fertig.";
            MessageBox.Show(
                $"Druckcenter-PDF wurde erstellt:\n{output}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LastResult = $"Fehler: {ex.Message}";
            PdfExportProgress = "PDF-Erstellung fehlgeschlagen.";
            MessageBox.Show(
                $"PDF konnte nicht erstellt werden:\n{ex.Message}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsPdfExportInProgress = false;
        }
    }

    [RelayCommand]
    private void PrintPdf()
    {
        string? pdfPath = null;

        if (!string.IsNullOrWhiteSpace(LastExportedPdfPath) && File.Exists(LastExportedPdfPath))
            pdfPath = LastExportedPdfPath;
        else
            pdfPath = _sp.Dialogs.OpenFile("PDF zum Drucken waehlen", "PDF (*.pdf)|*.pdf");

        if (string.IsNullOrWhiteSpace(pdfPath))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pdfPath,
                Verb = "print",
                UseShellExecute = true
            };
            Process.Start(psi);
            LastResult = $"Druckauftrag gestartet: {pdfPath}";
            _shell.SetStatus("PDF-Druckauftrag gestartet");
        }
        catch (Exception ex)
        {
            LastResult = $"Fehler beim Drucken: {ex.Message}";
            MessageBox.Show(
                $"PDF konnte nicht gedruckt werden:\n{ex.Message}",
                "Druckcenter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    partial void OnSelectedOwnerFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedExecutedByFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedSanierenFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedMaterialFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedYearFilterChanged(string value) => ApplyFilters();
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnOnlyWithCostChanged(bool value) => ApplyFilters();
    partial void OnOnlyWithMeasuresChanged(bool value) => ApplyFilters();

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

    private void RecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            ScheduleRefreshData();
            return;
        }

        if (e.PropertyName == nameof(HaltungRecord.Fields) ||
            e.PropertyName == nameof(HaltungRecord.ModifiedAtUtc) ||
            e.PropertyName.StartsWith("Fields[", StringComparison.Ordinal))
        {
            ScheduleRefreshData();
        }
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
        var projectPath = _sp.Settings.LastProjectPath;
        _costStore = _costRepo.Load(projectPath);

        var catalog = _catalogStore.LoadMerged(projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : 0.081m;

        _allRows = BuildRows();
        RebuildFilterOptions();
        ApplyFilters();
    }

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
            var material = SafeText(record.GetFieldValue("Rohrmaterial"));
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
        IEnumerable<DruckcenterRowVm> query = _allRows;

        query = ApplyComboFilter(query, SelectedOwnerFilter, row => row.Owner);
        query = ApplyComboFilter(query, SelectedExecutedByFilter, row => row.ExecutedBy);
        query = ApplyComboFilter(query, SelectedSanierenFilter, row => row.Sanieren);
        query = ApplyComboFilter(query, SelectedMaterialFilter, row => row.Material);
        query = ApplyComboFilter(query, SelectedStatusFilter, row => row.Status);
        query = ApplyComboFilter(query, SelectedYearFilter, row => row.Year);

        var search = (SearchText ?? "").Trim();
        if (search.Length > 0)
        {
            query = query.Where(row =>
                row.Holding.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Owner.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.ExecutedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Street.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Material.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Sanieren.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Zustand.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.MeasuresPreview.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (OnlyWithCost)
            query = query.Where(row => row.NetCost > 0m);

        if (OnlyWithMeasures)
            query = query.Where(row => row.HasMeasures);

        var filtered = query
            .OrderBy(r => string.IsNullOrWhiteSpace(r.ExecutedBy) ? 1 : 0)
            .ThenBy(r => r.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Holding, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Rows.Clear();
        foreach (var row in filtered)
            Rows.Add(row);

        UpdateStatistics(filtered);
        ActiveFilterText = BuildFilterSummaryText();
    }

    private static IEnumerable<DruckcenterRowVm> ApplyComboFilter(
        IEnumerable<DruckcenterRowVm> query,
        string selected,
        Func<DruckcenterRowVm, string> selector)
    {
        if (string.IsNullOrWhiteSpace(selected) || selected.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
            return query;

        return query.Where(row => string.Equals(selector(row), selected, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateStatistics(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        TotalRows = _allRows.Count;
        FilteredRowsCount = filtered.Count;
        RowsWithDetailedCosts = filtered.Count(row => row.HasDetailedCost);
        RowsWithoutCosts = filtered.Count(row => row.NetCost <= 0m);
        RowsWithoutOwner = filtered.Count(row => row.Owner.Equals(UnknownOwnerLabel, StringComparison.OrdinalIgnoreCase));
        NetTotal = filtered.Sum(row => row.NetCost);

        ComputeSpecialStats(
            filtered,
            out var gfk,
            out var nadelfilz,
            out var manschetten,
            out var lem,
            out var positionStats);
        StatsInlinerGfk = gfk;
        StatsInlinerNadelfilz = nadelfilz;
        StatsManschetten = manschetten;
        StatsLem = lem;
        SpecialPositionStatsHint = positionStats.Count == 0
            ? "Keine spezialrelevanten Positionen in den gewaehlten Massnahmen gefunden."
            : $"Einzelpositionen aus Massnahmen: {positionStats.Count}";

        SpecialPositionStats.Clear();
        foreach (var item in positionStats)
            SpecialPositionStats.Add(item);

        SpecialStatsHint = RowsWithDetailedCosts == FilteredRowsCount
            ? "Spezialstatistik auf Basis aller gefilterten Haltungen."
            : $"Spezialstatistik basiert auf {RowsWithDetailedCosts} von {FilteredRowsCount} Haltungen mit Positionsdetails.";

        UpdateRehabilitationShareChart();
        UpdateCostByExecutorChart(filtered);
    }

    private void UpdateRehabilitationShareChart()
    {
        var total = _allRows.Count;
        var yesCount = _allRows.Count(row => IsSanierenYes(row.Sanieren));
        var noCount = _allRows.Count(row => IsSanierenNo(row.Sanieren));
        var openCount = Math.Max(0, total - yesCount - noCount);

        RehabilitationShareChart.Clear();
        RehabilitationShareChart.Add(new ChartBarVm("Sanierung noetig", yesCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Keine Sanierung", noCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Nicht bewertet", openCount, total));

        var yesPercent = total > 0 ? yesCount * 100.0 / total : 0.0;
        RehabilitationShareHint = total == 0
            ? "Keine Haltungen im Projekt."
            : $"{yesPercent:0.#}% von {total} Haltungen sind als 'Sanieren = Ja' markiert.";
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

    private static void ComputeSpecialStats(
        IEnumerable<DruckcenterRowVm> rows,
        out decimal gfk,
        out decimal nadelfilz,
        out decimal manschetten,
        out decimal lem,
        out List<SpecialPositionStatVm> positionStats)
    {
        gfk = 0m;
        nadelfilz = 0m;
        manschetten = 0m;
        lem = 0m;
        var buckets = new Dictionary<string, PositionStatBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.StoredCost is null)
                continue;

            foreach (var line in row.StoredCost.Measures.SelectMany(m => m.Lines).Where(l => l.Selected))
            {
                var key = SafeText(line.ItemKey);
                var text = SafeText(line.Text);
                var combined = key + " " + text;
                if (!TryResolveSpecialCategory(combined, out var category))
                    continue;

                switch (category)
                {
                    case SpecialStatsCategory.InlinerGfk:
                        gfk += line.Qty;
                        break;
                    case SpecialStatsCategory.InlinerNadelfilz:
                        nadelfilz += line.Qty;
                        break;
                    case SpecialStatsCategory.Manschette:
                        manschetten += line.Qty;
                        break;
                    case SpecialStatsCategory.Linerendmanschette:
                        lem += line.Qty;
                        break;
                }

                var categoryLabel = GetCategoryLabel(category);
                var positionLabel = BuildPositionLabel(key, text);
                var unit = NormalizeSpecialUnit(line.Unit, category);
                var bucketKey = $"{categoryLabel}|{positionLabel}|{unit}";

                if (!buckets.TryGetValue(bucketKey, out var bucket))
                {
                    bucket = new PositionStatBucket
                    {
                        Category = category,
                        CategoryLabel = categoryLabel,
                        Position = positionLabel,
                        Unit = unit
                    };
                    buckets[bucketKey] = bucket;
                }

                bucket.Qty += line.Qty;
                bucket.Holdings.Add(row.Holding);
            }
        }

        positionStats = buckets.Values
            .OrderBy(b => GetCategoryOrder(b.Category))
            .ThenByDescending(b => b.Qty)
            .ThenBy(b => b.Position, StringComparer.OrdinalIgnoreCase)
            .Select(b => new SpecialPositionStatVm
            {
                Category = b.CategoryLabel,
                Position = b.Position,
                Qty = b.Qty,
                Unit = b.Unit,
                HoldingCount = b.Holdings.Count
            })
            .ToList();
    }

    private List<CostSummaryEntry> BuildSummaryEntries(IReadOnlyList<DruckcenterRowVm> filteredRows)
    {
        var entries = new List<CostSummaryEntry>(filteredRows.Count);

        foreach (var row in filteredRows)
        {
            if (row.HasDetailedCost && row.StoredCost is not null)
            {
                entries.Add(new CostSummaryEntry
                {
                    Holding = row.Holding,
                    Owner = row.Owner,
                    ExecutedBy = row.ExecutedBy,
                    Cost = row.StoredCost
                });
                continue;
            }

            if (row.NetCost <= 0m)
                continue;

            entries.Add(new CostSummaryEntry
            {
                Holding = row.Holding,
                Owner = row.Owner,
                ExecutedBy = row.ExecutedBy,
                Cost = BuildFallbackHoldingCost(row)
            });
        }

        return entries;
    }

    private HoldingCost BuildFallbackHoldingCost(DruckcenterRowVm row)
    {
        var measureName = "Kostenpauschale";
        var lineText = "Kosten aus Tabelle (ohne Positionsdetails)";
        var vat = Math.Round(row.NetCost * _vatRate, 2, MidpointRounding.AwayFromZero);

        return new HoldingCost
        {
            Holding = row.Holding,
            Date = null,
            Total = row.NetCost,
            MwstRate = _vatRate,
            MwstAmount = vat,
            TotalInclMwst = Math.Round(row.NetCost + vat, 2, MidpointRounding.AwayFromZero),
            Measures = new List<MeasureCost>
            {
                new()
                {
                    MeasureId = "PAUSCHALE",
                    MeasureName = measureName,
                    Lines = new List<CostLine>
                    {
                        new()
                        {
                            Group = "Zusammenfassung",
                            ItemKey = "PAUSCHALE",
                            Text = lineText,
                            Unit = "pl",
                            Qty = 1m,
                            UnitPrice = row.NetCost,
                            Selected = true,
                            IsPriceOverridden = false,
                            IsQtyOverridden = false
                        }
                    },
                    Total = row.NetCost
                }
            }
        };
    }

    private List<OfferPdfHoldingDataLineModel> BuildHoldingDataLines(IReadOnlyList<DruckcenterRowVm> filteredRows)
    {
        return filteredRows
            .OrderBy(r => string.IsNullOrWhiteSpace(r.ExecutedBy) ? 1 : 0)
            .ThenBy(r => r.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Holding, StringComparer.OrdinalIgnoreCase)
            .Select(row => new OfferPdfHoldingDataLineModel
            {
                Holding = row.Holding,
                Street = row.Street,
                Owner = row.Owner,
                ExecutedBy = row.ExecutedBy,
                Sanieren = row.Sanieren,
                Material = row.Material,
                Zustand = row.Zustand,
                NetText = Money(row.NetCost),
                DetailText = row.CostSource,
                MeasuresText = row.MeasuresPreview
            })
            .ToList();
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

    private static void AddFilterPart(List<string> parts, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
            return;

        parts.Add($"{label}={value}");
    }

    private static string BuildProjectCustomerBlock(Dictionary<string, string> metadata)
    {
        var sb = new StringBuilder();

        AddLine(sb, metadata, "Auftraggeber");
        AddLine(sb, metadata, "FirmaName");
        AddLine(sb, metadata, "FirmaAdresse");
        AddLine(sb, metadata, "FirmaTelefon");
        AddLine(sb, metadata, "FirmaEmail");

        var result = sb.ToString().Trim();
        return result.Length == 0 ? "Nicht definiert" : result;
    }

    private static string BuildObjectBlock(Dictionary<string, string> metadata, IReadOnlyList<DruckcenterRowVm> filteredRows)
    {
        var lines = new List<string>();
        AddLine(lines, "Projekt", metadata.TryGetValue("Zone", out var zone) ? zone : "");
        AddLine(lines, "Gemeinde", metadata.TryGetValue("Gemeinde", out var gemeinde) ? gemeinde : "");
        AddLine(lines, "Auftrag-Nr.", metadata.TryGetValue("AuftragNr", out var auftragNr) ? auftragNr : "");
        AddLine(lines, "Bearbeiter", metadata.TryGetValue("Bearbeiter", out var bearbeiter) ? bearbeiter : "");
        AddLine(lines, "Inspektionsdatum", metadata.TryGetValue("InspektionsDatum", out var datum) ? datum : "");
        lines.Add($"Haltungen im Ausdruck: {filteredRows.Count}");
        return string.Join("\n", lines);
    }

    private static void AddLine(StringBuilder sb, Dictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
            return;

        value = SafeText(value);
        if (value.Length == 0)
            return;

        if (sb.Length > 0)
            sb.AppendLine();
        sb.Append(value);
    }

    private static void AddLine(List<string> lines, string label, string value)
    {
        value = SafeText(value);
        if (value.Length == 0)
            return;
        lines.Add($"{label}: {value}");
    }

    private static bool HasSelectedLines(HoldingCost cost)
        => cost.Measures.Any(m => m.Lines.Any(l => l.Selected));

    private static decimal ResolveNetTotal(HoldingCost cost)
    {
        if (cost.Total > 0m)
            return cost.Total;

        var selectedLineTotal = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected)
            .Sum(l => l.Qty * l.UnitPrice);

        if (selectedLineTotal > 0m)
            return selectedLineTotal;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
    }

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

    private static bool TryResolveSpecialCategory(string combinedText, out SpecialStatsCategory category)
    {
        category = SpecialStatsCategory.None;

        if (ContainsToken(combinedText, "LINERENDMANSCHETTE") ||
            ContainsToken(combinedText, "ENDMANSCHETTE") ||
            ContainsToken(combinedText, "LEM"))
        {
            category = SpecialStatsCategory.Linerendmanschette;
            return true;
        }

        if (ContainsToken(combinedText, "SCHLAUCHLINER_GFK") ||
            (ContainsToken(combinedText, "GFK") && ContainsToken(combinedText, "LINER")) ||
            (ContainsToken(combinedText, "GFK") && ContainsToken(combinedText, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerGfk;
            return true;
        }

        if (ContainsToken(combinedText, "SCHLAUCHLINER_NADELFILZ") ||
            ContainsToken(combinedText, "NADELFILZ_LINER") ||
            (ContainsToken(combinedText, "NADELFILZ") && ContainsToken(combinedText, "LINER")) ||
            (ContainsToken(combinedText, "NADELFILZ") && ContainsToken(combinedText, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerNadelfilz;
            return true;
        }

        if (ContainsToken(combinedText, "MANSCHETTE"))
        {
            category = SpecialStatsCategory.Manschette;
            return true;
        }

        return false;
    }

    private static string GetCategoryLabel(SpecialStatsCategory category)
        => category switch
        {
            SpecialStatsCategory.InlinerGfk => "Inliner GFK",
            SpecialStatsCategory.InlinerNadelfilz => "Inliner Nadelfilz",
            SpecialStatsCategory.Manschette => "Manschetten",
            SpecialStatsCategory.Linerendmanschette => "Linerendmanschetten (LEM)",
            _ => "Sonstiges"
        };

    private static int GetCategoryOrder(SpecialStatsCategory category)
        => category switch
        {
            SpecialStatsCategory.InlinerGfk => 0,
            SpecialStatsCategory.InlinerNadelfilz => 1,
            SpecialStatsCategory.Manschette => 2,
            SpecialStatsCategory.Linerendmanschette => 3,
            _ => 99
        };

    private static string BuildPositionLabel(string key, string text)
    {
        if (key.Length == 0 && text.Length == 0)
            return "(ohne Bezeichnung)";
        if (key.Length == 0)
            return text;
        if (text.Length == 0)
            return key;
        if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
            return text;
        return $"{key} - {text}";
    }

    private static string NormalizeSpecialUnit(string? unit, SpecialStatsCategory category)
    {
        var normalized = SafeText(unit).ToLowerInvariant();
        if (normalized.Length > 0)
            return normalized;

        return category switch
        {
            SpecialStatsCategory.InlinerGfk => "m",
            SpecialStatsCategory.InlinerNadelfilz => "m",
            SpecialStatsCategory.Manschette => "stk",
            SpecialStatsCategory.Linerendmanschette => "stk",
            _ => "stk"
        };
    }

    private static bool ContainsToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
            return false;
        return text.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFilePart(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "Projekt" : value.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private enum SpecialStatsCategory
    {
        None = 0,
        InlinerGfk = 1,
        InlinerNadelfilz = 2,
        Manschette = 3,
        Linerendmanschette = 4
    }

    private sealed class PositionStatBucket
    {
        public SpecialStatsCategory Category { get; set; } = SpecialStatsCategory.None;
        public string CategoryLabel { get; set; } = "";
        public string Position { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Qty { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static string Money(decimal value) => value.ToString("N2", Ch) + " CHF";

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

public sealed class DruckcenterRowVm
{
    public HaltungRecord Record { get; init; } = new();
    public string Holding { get; init; } = "";
    public string Street { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Sanieren { get; init; } = "";
    public string ExecutedBy { get; init; } = "";
    public string Material { get; init; } = "";
    public string Status { get; init; } = "";
    public string Year { get; init; } = "";
    public string Zustand { get; init; } = "";
    public decimal NetCost { get; init; }
    public HoldingCost? StoredCost { get; init; }
    public bool HasDetailedCost { get; init; }
    public bool HasMeasures { get; init; }
    public string CostSource { get; init; } = "";
    public string MeasuresRaw { get; init; } = "";
    public string MeasuresPreview { get; init; } = "";
    public string NetCostText => NetCost.ToString("N2", CultureInfo.GetCultureInfo("de-CH")) + " CHF";
}

public sealed class SpecialPositionStatVm
{
    public string Category { get; init; } = "";
    public string Position { get; init; } = "";
    public decimal Qty { get; init; }
    public string Unit { get; init; } = "";
    public int HoldingCount { get; init; }
    public string QtyText => $"{Qty:0.##} {Unit}";
    public string HoldingCountText => HoldingCount.ToString(CultureInfo.InvariantCulture);
}

public sealed class ChartBarVm
{
    public ChartBarVm(string label, int value, int total)
    {
        Label = label;
        var safeTotal = Math.Max(total, 0);
        var safeValue = Math.Max(value, 0);
        Percent = safeTotal > 0 ? (safeValue * 100.0) / safeTotal : 0.0;
        ValueText = $"{safeValue}/{safeTotal} ({Percent:0.#}%)";
    }

    public ChartBarVm(string label, decimal amount, decimal totalAmount)
    {
        Label = label;
        var safeAmount = amount < 0m ? 0m : amount;
        var safeTotal = totalAmount < 0m ? 0m : totalAmount;
        Percent = safeTotal > 0m ? (double)(safeAmount * 100m / safeTotal) : 0.0;
        ValueText = $"{safeAmount:N2} CHF ({Percent:0.#}%)";
    }

    public string Label { get; }
    public double Percent { get; }
    public string ValueText { get; }
}
