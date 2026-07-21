using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Collections;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;
using static AuswertungPro.Next.Infrastructure.Costs.CostCalculatorLogicService;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CostCalculatorViewModel : ObservableObject
{
    private readonly ICostCatalogStore _catalogStore;
    private readonly IMeasureTemplateStore _templateStore;
    private readonly IProjectCostStoreRepository _costRepo;
    private readonly Action<HoldingCost>? _applyTotal;
    private readonly IDialogService _dialogs;
    // Nur ueber die Factory (ServiceProvider) gesetzt; Alt-/Test-Konstruktoren lassen ihn
    // null, der PDF-Export wacht dann mit klarer Meldung statt selbst einen Renderer zu newen.
    private readonly AuswertungPro.Next.Application.Output.IOfferPdfExportService? _offerPdfExport;
    private readonly string? _projectPath;
    private readonly Dictionary<string, CostCatalogItem> _catalogItems;
    private readonly Dictionary<string, MeasureTemplate> _templateItems;
    private readonly Dictionary<string, string> _ownerByHolding = new(StringComparer.OrdinalIgnoreCase);
    private readonly CostCalculatorMeasureSelectionController _measureSelection = new();
    private readonly CostCalculatorCatalogFilterController _catalogFilter = new();
    private readonly CostCalculatorImportDefaultsController _importDefaults = new();
    private readonly CostCalculatorWarningSuppressionController _warningSuppression = new();
    private ProjectCostStore _store = new();
    // != null wenn costs.json beim Laden nicht lesbar war -> Speichern gesperrt (Audit K3).
    private string? _storeLoadError;
    private readonly decimal _vatRate;
    private readonly CostConsistencyCheckService _consistencyChecker = new();
    private System.Windows.Threading.DispatcherTimer? _checkDebounceTimer;

    public string Holding { get; }
    public DateTime? Date { get; }
    public string Header => string.IsNullOrWhiteSpace(Holding) ? "Kostenberechnung" : $"Kostenberechnung - {Holding}";

    public ObservableCollection<MeasureTemplateListItem> Measures { get; }
    public ObservableCollection<MeasureBlockVm> SelectedMeasures { get; } = new();
    public IReadOnlyList<string> InitialMeasureIds { get; }

    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _mwstAmount;
    [ObservableProperty] private decimal _totalInclMwst;
    [ObservableProperty] private string _catalogSearchText = "";

    // Consistency checking
    [ObservableProperty] private ObservableCollection<ConsistencyWarning> _consistencyWarnings = new();
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;
    [ObservableProperty] private bool _hasWarnings;
    public string MwstLabel => $"MWST {_vatRate * 100:0.0}%:";

    /// <summary>All active catalog items for the drag-source panel.</summary>
    public IReadOnlyList<CatalogItemOption> AllCatalogItems => _catalogFilter.AllCatalogItems;
    public ObservableCollection<CatalogItemOption> FilteredCatalogItems => _catalogFilter.FilteredCatalogItems;

    public IRelayCommand ApplyMeasuresCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ApplyTotalCommand { get; }
    public IRelayCommand EditPositionTemplatesCommand { get; }
    public IRelayCommand<MeasureBlockVm> RemoveMeasureCommand { get; }
    public IRelayCommand<MeasureBlockVm> MoveMeasureUpCommand { get; }
    public IRelayCommand<MeasureBlockVm> MoveMeasureDownCommand { get; }
    public IRelayCommand SortMeasuresCommand { get; }
    public IRelayCommand<MeasureBlockVm> SaveTemplateCommand { get; }
    public IAsyncRelayCommand<Window?> ExportPdfCommand { get; }

    public event Action? Saved;

    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen die Kosten-Speicher injizieren.")]
    public CostCalculatorViewModel(
        string holding,
        DateTime? date,
        IReadOnlyList<string> recommendedTokens,
        string? projectPath,
        Action<HoldingCost>? applyTotal = null,
        HaltungRecord? haltungRecord = null,
        IReadOnlyList<HaltungRecord>? projectRecords = null,
        IDialogService? dialogs = null,
        AuswertungPro.Next.Application.Output.IOfferPdfExportService? pdfExport = null)
        : this(
            holding,
            date,
            recommendedTokens,
            projectPath,
            CostStoreCompatibility.CreateCalculationStores(),
            applyTotal,
            haltungRecord,
            projectRecords,
            dialogs,
            pdfExport)
    {
    }

    public CostCalculatorViewModel(
        string holding,
        DateTime? date,
        IReadOnlyList<string> recommendedTokens,
        string? projectPath,
        CostCalculationStores stores,
        Action<HoldingCost>? applyTotal = null,
        HaltungRecord? haltungRecord = null,
        IReadOnlyList<HaltungRecord>? projectRecords = null,
        IDialogService? dialogs = null,
        AuswertungPro.Next.Application.Output.IOfferPdfExportService? pdfExport = null)
        : this(
            holding,
            date,
            recommendedTokens,
            projectPath,
            stores?.Catalog ?? throw new ArgumentNullException(nameof(stores)),
            stores.Templates,
            stores.ProjectCosts,
            applyTotal,
            haltungRecord,
            projectRecords,
            dialogs,
            pdfExport)
    {
    }

    public CostCalculatorViewModel(
        string holding,
        DateTime? date,
        IReadOnlyList<string> recommendedTokens,
        string? projectPath,
        ICostCatalogStore catalogStore,
        IMeasureTemplateStore templateStore,
        IProjectCostStoreRepository costRepo,
        Action<HoldingCost>? applyTotal = null,
        HaltungRecord? haltungRecord = null,
        IReadOnlyList<HaltungRecord>? projectRecords = null,
        IDialogService? dialogs = null,
        AuswertungPro.Next.Application.Output.IOfferPdfExportService? pdfExport = null)
    {
        Holding = holding;
        Date = date;
        _projectPath = projectPath;
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _costRepo = costRepo ?? throw new ArgumentNullException(nameof(costRepo));
        _applyTotal = applyTotal;
        _dialogs = dialogs ?? new DialogService();
        _offerPdfExport = pdfExport;

        var catalog = _catalogStore.LoadMerged(projectPath);
        _catalogItems = catalog.Items.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        _vatRate = catalog.VatRate > 0 ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;

        var templates = _templateStore.LoadMerged(projectPath);
        _templateItems = templates.Measures.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _measureSelection.ReplaceMeasureOrder(templates.Measures.Select(t => t.Id));
        Measures = new ObservableCollection<MeasureTemplateListItem>(
            templates.Measures.Select(t => new MeasureTemplateListItem(t)));
        _catalogFilter.ReplaceItems(_catalogItems.Values, templates.Measures, CatalogSearchText);

        _store = _costRepo.Load(projectPath, out _storeLoadError);
        if (_storeLoadError is not null)
            _dialogs.Warn(
                $"Kostendaten konnten nicht geladen werden:\n{_storeLoadError}\n\nSpeichern ist gesperrt, damit vorhandene Kosten nicht ueberschrieben werden.",
                "Kosten");
        InitializeOwnerLookup(projectRecords, haltungRecord);

        var existing = GetExistingCost();
        var recommendedIds = CostCalculatorLogicService.ResolveMeasureIds(recommendedTokens, templates.Measures, _catalogItems);
        var initialIds = existing is null ? recommendedIds : existing.Measures.Select(m => m.MeasureId).ToList();
        InitialMeasureIds = initialIds;

        // Initialize DN and Length from HaltungRecord if provided
        if (haltungRecord != null)
        {
            _importDefaults.InitializeFromHaltungRecord(haltungRecord, SelectedMeasures);
        }

        if (existing is not null)
        {
            LoadExisting(existing);
        }
        else if (initialIds.Count > 0)
        {
            foreach (var id in initialIds)
                TryAddMeasure(id, applyPrices: true);
        }

        ApplyMeasuresCommand = new RelayCommand(ApplySelectedMeasures);
        SaveCommand = new RelayCommand(Save);
        ApplyTotalCommand = new RelayCommand(ApplyTotal);
        EditPositionTemplatesCommand = new RelayCommand(EditPositionTemplates);
        RemoveMeasureCommand = new RelayCommand<MeasureBlockVm>(RemoveMeasure);
        MoveMeasureUpCommand = new RelayCommand<MeasureBlockVm>(MoveMeasureUp);
        MoveMeasureDownCommand = new RelayCommand<MeasureBlockVm>(MoveMeasureDown);
        SortMeasuresCommand = new RelayCommand(SortMeasures);
        SaveTemplateCommand = new RelayCommand<MeasureBlockVm>(SaveTemplate);
        ExportPdfCommand = new AsyncRelayCommand<Window?>(ExportPdfAsync);

        _checkDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _checkDebounceTimer.Tick += (_, _) =>
        {
            _checkDebounceTimer.Stop();
            RunConsistencyCheck();
        };

        UpdateTotal();
        RunConsistencyCheck();
    }

    public void SetSelectedMeasures(IEnumerable<MeasureTemplateListItem> measures)
    {
        _measureSelection.SetSelectedMeasures(measures);
    }

    private void ApplySelectedMeasures()
    {
        if (_measureSelection.SelectedMeasureIds.Count == 0)
            return;

        foreach (var id in _measureSelection.SelectedMeasureIds)
            TryAddMeasure(id, applyPrices: true);

        UpdateTotal();
    }

    private bool TryAddMeasure(string id, bool applyPrices)
    {
        if (SelectedMeasures.Any(m => string.Equals(m.MeasureId, id, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!_templateItems.TryGetValue(id, out var template))
            return false;
        if (template.Disabled)
            return false;

        var block = new MeasureBlockVm(template, _catalogItems);
        block.BlockChanged += UpdateTotal;
        SelectedMeasures.Add(block);

        _importDefaults.ApplyTo(block);

        if (applyPrices)
            block.ApplyCatalogPrices();

        return true;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            _dialogs.Info("Projekt bitte speichern, um Kosten abzulegen.", "Kosten");
            return;
        }

        // Verlustschutz (Audit K3): Wenn costs.json nicht lesbar war, ist _store leer —
        // ein Save wuerde alle Kostendaten des Projekts endgueltig ueberschreiben.
        if (_storeLoadError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: Die bestehende costs.json konnte beim Oeffnen nicht gelesen werden.\n{_storeLoadError}\n\nBitte Datei pruefen (costs\\costs.json bzw. .bak) und das Fenster neu oeffnen.",
                "Kosten");
            return;
        }

        var key = Holding?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            _dialogs.Warn("Haltungsname fehlt.", "Kosten");
            return;
        }

        var holdingCost = BuildHoldingCost(key);

        // Audit W8: Frisch laden und nur die EIGENE Haltung mergen — der Fenster-Snapshot
        // vom Oeffnen wuerde sonst zwischenzeitliche Aenderungen anderer Schreiber
        // (Sanierungs-Matrix) per Last-Write-Wins ueberschreiben.
        var fresh = _costRepo.Load(_projectPath, out var freshError);
        if (freshError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: costs.json konnte nicht frisch gelesen werden.\n{freshError}",
                "Kosten");
            return;
        }
        fresh.ByHolding[key] = holdingCost;
        _store = fresh;

        if (!_costRepo.Save(_projectPath, _store, out var error))
        {
            _dialogs.Error($"Speichern fehlgeschlagen: {error}", "Kosten");
            return;
        }

        // Keep record fields in sync when user saves from the calculator window.
        _applyTotal?.Invoke(BuildHoldingCost(key));
        Saved?.Invoke();
    }

    private void ApplyTotal()
    {
        if (_applyTotal is null)
        {
            _dialogs.Info("Kosten/Massnahmen koennen hier nicht in die Zeile uebernommen werden.", "Kosten/Massnahmen");
            return;
        }

        _applyTotal(BuildHoldingCost(Holding));
    }

    private async Task ExportPdfAsync(Window? owner)
    {
        if (SelectedMeasures.Count == 0)
        {
            _dialogs.Info("Bitte zuerst Massnahmen hinzufuegen.", "PDF-Export");
            return;
        }

        var safeName = SanitizeFilePart(Holding);
        var defaultName = $"Kostenzusammenstellung_{safeName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Kostenzusammenstellung als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            owner ??= System.Windows.Application.Current?.MainWindow;
            if (owner is not null) owner.Cursor = System.Windows.Input.Cursors.Wait;

            var holdingCost = BuildHoldingCost(Holding);
            var pdfExport = CostCalculatorPdfExportModelBuilder.Build(
                Holding,
                Date,
                holdingCost,
                SelectedMeasures,
                _ownerByHolding,
                DateTimeOffset.Now);
            if (pdfExport is null)
            {
                _dialogs.Info(
                    "Keine passenden Kostenpositionen gefunden.",
                    "PDF-Export");
                return;
            }

            var exporter = _offerPdfExport
                ?? throw new InvalidOperationException("PDF-Export ist ohne ServiceProvider nicht verfuegbar.");
            await exporter.ExportAsync(pdfExport.Model, output);

            _dialogs.Info($"PDF-Kostenzusammenstellung wurde erstellt:\n{output}", "PDF-Export");
        }
        catch (Exception ex)
        {
            _dialogs.Error(
                $"PDF konnte nicht erstellt werden:\n{UserError.DescribeAndReport(ex, "Kosten-PDF erstellen")}",
                "PDF-Export");
        }
        finally
        {
            if (owner is not null) owner.Cursor = null;
        }
    }

    private static string SanitizeFilePart(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unbekannt";

        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
        return clean.Trim();
    }

    private void InitializeOwnerLookup(
        IReadOnlyList<HaltungRecord>? projectRecords,
        HaltungRecord? haltungRecord)
    {
        _ownerByHolding.Clear();
        foreach (var pair in CostCalculatorSummaryEntryBuilder.BuildOwnerLookup(projectRecords, haltungRecord))
            _ownerByHolding[pair.Key] = pair.Value;
    }

    private HoldingCost BuildHoldingCost(string holding)
    {
        var measures = SelectedMeasures.Select(m => m.ToModel()).ToList();
        return CostCalculatorLogicService.BuildHoldingCost(holding, Date, measures, _vatRate);
    }

    private void LoadExisting(HoldingCost cost)
    {
        SelectedMeasures.Clear();
        foreach (var measure in cost.Measures)
        {
            _templateItems.TryGetValue(measure.MeasureId, out var template);

            var block = new MeasureBlockVm(template, _catalogItems);
            block.LoadFrom(measure);
            block.BlockChanged += UpdateTotal;
            SelectedMeasures.Add(block);
        }
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var totals = CostCalculatorLogicService.CalculateTotals(SelectedMeasures.Sum(m => m.Total), _vatRate);
        Total = totals.Total;
        MwstAmount = totals.MwstAmount;
        TotalInclMwst = totals.TotalInclMwst;

        // Debounce consistency check (300ms after last edit)
        _checkDebounceTimer?.Stop();
        _checkDebounceTimer?.Start();
    }

    private void RunConsistencyCheck()
    {
        var allResults = _consistencyChecker.CheckAll(
            SelectedMeasures.ToList(),
            _catalogItems,
            _templateItems,
            _store,
            Holding);

        var results = _warningSuppression.FilterVisibleWarnings(allResults);

        ConsistencyWarnings = new ObservableCollection<ConsistencyWarning>(results);
        ErrorCount = results.Count(w => w.Severity == ConsistencyWarningSeverity.Error);
        WarningCount = results.Count(w => w.Severity == ConsistencyWarningSeverity.Warning);
        InfoCount = results.Count(w => w.Severity == ConsistencyWarningSeverity.Info);
        HasWarnings = results.Count > 0;
    }

    /// <summary>
    /// Marks a warning as "acknowledged / in order" so it no longer appears.
    /// </summary>
    public void SuppressWarning(ConsistencyWarning warning)
    {
        _warningSuppression.SuppressWarning(warning);
        RunConsistencyCheck();
    }

    /// <summary>
    /// Resets all suppressions so every warning is shown again.
    /// </summary>
    public void ResetSuppressedWarnings()
    {
        _warningSuppression.ResetSuppressedWarnings();
        RunConsistencyCheck();
    }

    partial void OnCatalogSearchTextChanged(string value)
    {
        _catalogFilter.ApplyFilter(value);
    }

    internal static string DeriveGroupFromKey(string key)
        => CatalogItemGrouping.DeriveGroupFromKey(key);

    private void RemoveMeasure(MeasureBlockVm? measure)
    {
        if (measure == null)
            return;

        measure.BlockChanged -= UpdateTotal;
        SelectedMeasures.Remove(measure);
        UpdateTotal();
    }

    private void MoveMeasureUp(MeasureBlockVm? measure)
        => ObservableCollectionOrderController.TryMoveByOffset(SelectedMeasures, measure, -1);

    private void MoveMeasureDown(MeasureBlockVm? measure)
        => ObservableCollectionOrderController.TryMoveByOffset(SelectedMeasures, measure, 1);

    private void SortMeasures()
    {
        if (SelectedMeasures.Count == 0)
            return;

        foreach (var measure in SelectedMeasures)
            measure.SortLines();

        var ordered = _measureSelection.OrderMeasures(SelectedMeasures);

        ObservableCollectionOrderController.Reorder(SelectedMeasures, ordered);
    }

    private void SaveTemplate(MeasureBlockVm? measure)
    {
        if (measure is null)
            return;

        if (string.IsNullOrWhiteSpace(measure.MeasureId))
        {
            _dialogs.Warn("Vorlagen-ID fehlt.", "Vorlage");
            return;
        }

        var template = new MeasureTemplate
        {
            Id = measure.MeasureId,
            Name = string.IsNullOrWhiteSpace(measure.MeasureName) ? measure.MeasureId : measure.MeasureName,
            Lines = measure.Lines.Select(l => new MeasureLineTemplate
            {
                Group = l.Group ?? "",
                ItemKey = l.ItemKey ?? "",
                Enabled = l.Selected,
                DefaultQty = l.Qty
            }).ToList()
        };

        if (!_templateStore.UpsertUserTemplate(template, out var error))
        {
            _dialogs.Error($"Speichern fehlgeschlagen: {error}", "Vorlage");
            return;
        }

        _dialogs.Info("Vorlage gespeichert. Gilt fuer neue Projekte.", "Vorlage");
    }

    private HoldingCost? GetExistingCost()
    {
        if (string.IsNullOrWhiteSpace(Holding))
            return null;

        foreach (var kvp in _store.ByHolding)
        {
            if (string.Equals(kvp.Key, Holding, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Oeffentlicher Zugang zur Score-basierten Massnahmen-Aufloesung.
    /// Wird von SanierungsmassnahmenViewModel.SelectMeasuresInCalc benutzt.
    /// </summary>
    public HashSet<string> ResolveMatchingMeasureIds(IReadOnlyList<string> tokens)
    {
        var templates = Measures
            .Where(m => !m.Disabled)
            .Select(m => m.Template)
            .ToList();
        var ids = CostCalculatorLogicService.ResolveMeasureIds(tokens, templates, _catalogItems);
        return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    private void EditPositionTemplates()
    {
        var dialog = new CostCatalogEditorDialog(_projectPath, _catalogStore);
        dialog.ShowDialog();
        // Always reload â€“ user may have saved changes
        ReloadCatalog();
    }

    private void RefreshMeasures()
    {
        var templates = _templateStore.LoadMerged(_projectPath);
        _templateItems.Clear();
        var orderIds = new List<string?>();
        foreach (var template in templates.Measures)
        {
            _templateItems[template.Id] = template;
            orderIds.Add(template.Id);
        }
        _measureSelection.ReplaceMeasureOrder(orderIds);

        Measures.Clear();
        foreach (var template in templates.Measures.Select(t => new MeasureTemplateListItem(t)))
        {
            Measures.Add(template);
        }
    }

    private void ReloadCatalog()
    {
        var catalog = _catalogStore.LoadMerged(_projectPath);
        _catalogItems.Clear();
        foreach (var item in catalog.Items)
            _catalogItems[item.Key] = item;

        _catalogFilter.ReplaceItems(_catalogItems.Values, _templateItems.Values, CatalogSearchText);

        // Update each measure block
        foreach (var block in SelectedMeasures)
        {
            block.RefreshCatalog(_catalogItems);
            block.ApplyCatalogPrices();
        }
    }
}
