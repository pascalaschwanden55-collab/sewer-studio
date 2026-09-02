using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Massen-Ansicht: alle Haltungen einer Zone als Tabelle, pro Zeile EINE Hauptarbeit
/// plus ankreuzbare Zusatzoptionen (Verkehrsdienst,
/// Wasserhaltung, Fraesen, Dichtheitspruefung, Dokumentation). Mengen (DN, Laenge,
/// Anschluesse-Dedup) kommen automatisch; bei Stk-Reparaturen gibt der Anwender die
/// Stueckzahl selbst ein. Speichern legt alles in costs.json ab; das aggregierte
/// NPK-Leistungsverzeichnis wird im Druckcenter exportiert.
/// </summary>
public sealed partial class SanierungsMatrixPageViewModel : ObservableObject, IConfirmLeave
{
    // Waehlbare Hauptarbeiten mit fachlicher Kategorie. Kanalroboter (nur Ablagerungen
    // fraesen) und Anschluss einbinden zaehlen zur REPARATUR, auch wenn ihre Katalog-
    // Position ein anderes Kapitel traegt.
    // UNTERHALT sind Haltungen ohne Sanierung: nur reinigen und/oder TV-kontrollieren
    // (NPK 135, Abschnitte 111/112, 211, 222, 234). Sie rechnen in Gruppenstunden ab,
    // deshalb kommt ihre Menge nicht aus der Haltungslaenge.
    private static readonly (string Id, string Kategorie)[] MatrixMeasures =
    {
        ("SCHLAUCHLINER_NADELFILZ", "Renovierung"),
        ("SCHLAUCHLINER_NADELFILZ_OPENEND", "Renovierung"),
        ("SCHLAUCHLINER_GFK", "Renovierung"),
        ("KURZLINER_PARTLINER", "Reparatur"),
        ("MANSCHETTE_EDELSTAHL", "Reparatur"),
        ("KANALROBOTER", "Reparatur"),
        ("ANSCHLUSS_DICHTEN", "Reparatur"),
        ("ANSCHLUSS_VERSCHLIESSEN", "Reparatur"),
        ("KANALREINIGUNG_TV", "Unterhalt"),
        ("KANALREINIGUNG", "Unterhalt"),
        ("TV_KONTROLLE", "Unterhalt"),
    };

    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly IDerivedCostFieldSynchronizer _costFieldSync;
    private readonly DashboardRefreshNotifier _dashboardRefresh;
    private readonly ICostCatalogStore _catalogStore;
    private readonly IMeasureTemplateStore _templateStore;
    private readonly IProjectCostStoreRepository _costRepo;

    private Dictionary<string, MeasureTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CostCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private ProjectCostStore _store = new();
    // != null wenn costs.json beim Laden nicht lesbar war -> Speichern gesperrt (Audit K3).
    private string? _storeLoadError;
    // Defekte Berechnungsgrundlagen duerfen nicht als leere Kataloge/Vorlagen
    // weiterlaufen, weil das bestehende Kosten still entfernen oder auf CHF 0 setzen kann.
    private string? _catalogLoadError;
    private string? _templateLoadError;
    // Nichtleere unlesbare Tabellenkosten duerfen weder als CHF 0 gelten noch
    // beim Speichern unbemerkt durch abgeleitete Werte ersetzt werden.
    private string? _tableCostParseError;
    // Nichtleere unlesbare oder negative Haltungslaengen duerfen nicht auf die
    // Default-Menge einer Meterposition zurueckfallen.
    private string? _lengthParseError;
    private decimal _vatRate = CostCalculatorLogicService.DefaultVatRate;
    private string _projectPath = "";
    private readonly string? _singleHoldingTarget;
    private readonly HaltungRecord? _singleHoldingTargetRecord;
    private SanierungMatrixRowVm? _detailRow;
    private SanierungsMatrixDetailEditSession? _detailSession;
    private bool _suppressSelectionGuard;
    // In-Memory-Store weicht von costs.json ab (RecomputeRow/DetailUebernehmen/Preis-Apply)
    // -> Leave-Guard fragt nach, Speichern setzt zurueck (Audit K1/W2).
    private bool _hasUnsavedChanges;

    // Haltungen, die in dieser Sitzung auf "keine" gesetzt wurden -> beim Speichern
    // muessen ihre Tabellenfelder (Kosten, Massnahmen, Mengen) geleert werden.
    private readonly HashSet<string> _clearedHoldings = new(StringComparer.OrdinalIgnoreCase);

    // Haltungen, die in dieser Sitzung wirklich geaendert wurden — nur deren Tabellen-
    // felder werden beim Speichern gestempelt (Audit W6: vorher bekam JEDE Haltung
    // userEdited/Manual, auch unberuehrte) und nur sie werden in den frischen Store
    // gemergt (Audit W8: Last-Write-Wins gegen das Kostenfenster vermeiden).
    private readonly HashSet<string> _touchedHoldings = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<SanierungMatrixRowVm> Rows { get; } = new();
    public ObservableCollection<MeasureOption> MeasureOptions { get; } = new();
    public string? ProjectRootPath => ProjectFileLocator.ProjectRootFromFile(_settings.LastProjectPath);

    [ObservableProperty] private bool _isSingleHoldingMode;
    [ObservableProperty] private string _pageTitle = "Sanierungs-Matrix";
    [ObservableProperty] private string _pageSubtitle = "Pro Haltung eine Hauptarbeit waehlen - Meter, DN und Anschluesse kommen automatisch.";
    [ObservableProperty] private decimal _gesamtTotal;
    [ObservableProperty] private decimal _pauschalenTotal;
    [ObservableProperty] private int _pauschalenHaltungen;
    [ObservableProperty] private bool _hasPauschalen;
    [ObservableProperty] private string _pauschalenText = "";

    /// <summary>Teuerstes Zeilen-Total — Bezugsgroesse fuer die Kostenbalken-Spalte.</summary>
    [ObservableProperty] private decimal _maxRowTotal;

    [ObservableProperty] private int _belegteHaltungen;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private SanierungMatrixRowVm? _selectedRow;
    [ObservableProperty] private string _detailTitle = "Keine Haltung gewaehlt";
    [ObservableProperty] private string _detailSubtitle = "Links eine Haltung waehlen.";
    [ObservableProperty] private string _detailTotal = "";
    [ObservableProperty] private string _detailEditStatus = "";
    [ObservableProperty] private bool _isDetailDirty;

    public ObservableCollection<SanierungsMatrixDetailEditMeasureVm> SelectedDetailMeasures { get; private set; } = new();

    public SanierungsMatrixPageViewModel(ShellViewModel shell, ServiceProvider services)
        : this(shell, services, null, singleHoldingMode: false)
    {
    }

    public SanierungsMatrixPageViewModel(
        ShellViewModel shell,
        ServiceProvider services,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
        : this(
            shell,
            settings: services.Settings,
            dialogs: services.Dialogs,
            costFieldSync: services.CostFieldSync,
            dashboardRefresh: services.DashboardRefresh,
            costStores: services.CostStores.CreateCalculationStores(),
            holding: holding,
            singleHoldingMode: singleHoldingMode,
            targetRecord: targetRecord)
    {
    }

    public SanierungsMatrixPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IDerivedCostFieldSynchronizer costFieldSync,
        DashboardRefreshNotifier dashboardRefresh,
        ICostCatalogStore catalogStore,
        IMeasureTemplateStore templateStore,
        IProjectCostStoreRepository costRepo,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _costFieldSync = costFieldSync ?? throw new ArgumentNullException(nameof(costFieldSync));
        _dashboardRefresh = dashboardRefresh ?? throw new ArgumentNullException(nameof(dashboardRefresh));
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _costRepo = costRepo ?? throw new ArgumentNullException(nameof(costRepo));
        _singleHoldingTarget = string.IsNullOrWhiteSpace(holding) ? null : holding.Trim();
        _singleHoldingTargetRecord = targetRecord;
        IsSingleHoldingMode = singleHoldingMode;
        UpdatePageTexts();
        Reload();
    }

    public bool SelectHolding(string? holding)
    {
        var row = SanierungsMatrixNavigationTarget.FindRow(Rows, holding);
        if (row is null)
        {
            if (!string.IsNullOrWhiteSpace(holding))
                Status = $"Haltung in Sanierungs-Matrix nicht gefunden: {holding.Trim()}";
            return false;
        }

        SelectedRow = row;
        Status = $"Sanierungs-Matrix: {row.Holding} gewaehlt.";
        return true;
    }

    /// <summary>
    /// Speichert die Positionen der gewaehlten Massnahme als (User-)Vorlage — z.B. "Schlauchliner GFK".
    /// Kuenftige Projekte laden diese Positionen/Mengen als Standard fuer dieselbe Massnahme.
    /// Spiegelt bewusst den Vorlage-Speichern-Pfad des Kosten-Fensters (gleicher MeasureTemplateStore).
    /// </summary>
    [RelayCommand]
    private void SaveTemplate(SanierungsMatrixDetailEditMeasureVm? measure)
    {
        if (measure is null)
            return;

        if (_templateLoadError is not null)
        {
            _dialogs.Error(
                $"Vorlage kann nicht gespeichert werden, weil die Massnahmenvorlagen nicht sauber geladen wurden:\n{_templateLoadError}",
                "Vorlage");
            return;
        }

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

        _dialogs.Info($"Vorlage \"{template.Name}\" gespeichert. Gilt fuer neue Projekte.", "Vorlage");
    }

    [RelayCommand]
    private void Reload()
    {
        // Audit W1: Rows.Clear() drueckt via TwoWay-SelectedItem synchron SelectedRow=null —
        // ohne Schutz feuert der Dirty-Guard mitten im Neuaufbau (Doppel-Dialoge, Store/UI-Drift).
        // Darum: offene Aenderungen EINMAL vorab klaeren, dann Guard unterdruecken.
        if (IsDetailDirty || _hasUnsavedChanges)
        {
            if (!_dialogs.Confirm(
                    "Nicht gespeicherte Aenderungen gehen beim Neuladen verloren.\nTrotzdem neu laden?",
                    PageTitle))
            {
                Status = "Neu laden abgebrochen (offene Aenderungen).";
                return;
            }
        }

        _suppressSelectionGuard = true;
        try
        {
            ReloadCore();
        }
        finally
        {
            _suppressSelectionGuard = false;
        }

        // Detailbereich explizit nachziehen (der unterdrueckte Selection-Handler tat es nicht).
        LoadDetailForRow(SelectedRow);
    }

    private void ReloadCore()
    {
        _hasUnsavedChanges = false;
        _clearedHoldings.Clear();
        _touchedHoldings.Clear();
        _projectPath = _settings.LastProjectPath ?? "";

        var catalog = _catalogStore.LoadMerged(_projectPath, out _catalogLoadError);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
        _catalog = catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var tplCatalog = _templateStore.LoadMerged(_projectPath, out _templateLoadError);
        _templates = tplCatalog.Measures
            .Where(m => !m.Disabled && !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(m => m.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        BuildMeasureOptions();

        _store = _costRepo.Load(_projectPath, out _storeLoadError);
        _tableCostParseError = FindTableCostParseError(_shell.Project.Data);
        _lengthParseError = FindLengthParseError(_shell.Project.Data);

        var loadedRows = new List<SanierungMatrixRowVm>();
        foreach (var record in _shell.Project.Data)
        {
            var holding = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (string.IsNullOrWhiteSpace(holding))
                continue;

            var dn = (record.GetFieldValue(FieldKeys.NominalDiameterMm) ?? "").Trim();
            var laenge = (record.GetFieldValue(FieldKeys.HoldingLengthMeters) ?? "").Trim();
            var anschluesse = ConnectionCountEstimator.EstimateFromRecord(record) ?? 0;

            var row = new SanierungMatrixRowVm(record, holding, dn, laenge, anschluesse, RecomputeRow);
            InitRowFromStore(row, holding);
            loadedRows.Add(row);
        }

        Rows.Clear();
        foreach (var row in SanierungsMatrixNavigationTarget.FilterRows(
                     loadedRows,
                     _singleHoldingTarget,
                     IsSingleHoldingMode,
                     _singleHoldingTargetRecord))
        {
            Rows.Add(row);
        }

        RecomputeGesamt();
        SelectedRow = Rows.FirstOrDefault();
        if (IsSingleHoldingMode && Rows.Count == 0)
        {
            Status = string.IsNullOrWhiteSpace(_singleHoldingTarget)
                ? "Keine Haltung fuer Einzelansicht angegeben."
                : $"Haltung in Sanierungsmassnahme nicht gefunden: {_singleHoldingTarget}.";
            return;
        }

        Status = Rows.Count == 0
            ? "Keine Haltungen geladen (Projekt mit Haltungen oeffnen)."
            : IsSingleHoldingMode
                ? $"Sanierungsmassnahme geladen: {SelectedRow?.Holding}"
                : $"{Rows.Count} Haltungen geladen.";

        var calculationLoadError = BuildCalculationLoadError();
        if (_storeLoadError is not null)
        {
            Status = $"WARNUNG: {_storeLoadError} - Speichern ist gesperrt, bestehende Kosten bleiben unangetastet.";
            _dialogs.Warn(
                $"Kostendaten konnten nicht geladen werden:\n{_storeLoadError}\n\nSpeichern ist gesperrt, damit costs.json nicht mit einem leeren Stand ueberschrieben wird.\nBitte Datei pruefen (costs\\costs.json bzw. .bak) und danach 'Neu laden'.",
                "Sanierungs-Matrix");
        }
        else if (calculationLoadError is not null)
        {
            Status = $"FEHLER: {calculationLoadError} - Berechnungen und Speichern sind gesperrt.";
            _dialogs.Error(
                $"{calculationLoadError}\n\n" +
                "Berechnungen und Speichern sind gesperrt, damit bestehende Kosten nicht mit leeren Ersatzdaten veraendert werden. " +
                "Bitte die Dateien pruefen und danach 'Neu laden'.",
                "Sanierungs-Matrix");
        }
        else if (_tableCostParseError is not null)
        {
            Status = $"FEHLER: {_tableCostParseError} - Speichern ist gesperrt.";
            _dialogs.Error(
                $"{_tableCostParseError}\n\n" +
                "Nichtleere ungueltige Kosten werden nicht als CHF 0 behandelt. " +
                "Bitte die Kostenfelder korrigieren und danach 'Neu laden'.",
                "Sanierungs-Matrix");
        }
    }

    private string? BuildCalculationLoadError()
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(_catalogLoadError))
            errors.Add($"Kostenkatalog konnte nicht geladen werden: {_catalogLoadError}");
        if (!string.IsNullOrWhiteSpace(_templateLoadError))
            errors.Add($"Massnahmenvorlagen konnten nicht geladen werden: {_templateLoadError}");
        if (!string.IsNullOrWhiteSpace(_lengthParseError))
            errors.Add(_lengthParseError);
        return errors.Count == 0 ? null : string.Join("\n", errors);
    }

    private static string? FindTableCostParseError(IEnumerable<HaltungRecord> records)
    {
        var invalidHoldings = records
            .Where(record =>
            {
                var raw = record.GetFieldValue(FieldKeys.Cost);
                return !string.IsNullOrWhiteSpace(raw)
                       && !TablePauschaleCostHelper.TryParseTableNetCost(raw, out _);
            })
            .Select(record =>
            {
                var holding = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
                return string.IsNullOrWhiteSpace(holding) ? "(ohne Haltungsname)" : holding;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return invalidHoldings.Count == 0
            ? null
            : $"Tabellenkosten sind nicht lesbar bei: {string.Join(", ", invalidHoldings)}";
    }

    private static string? FindLengthParseError(IEnumerable<HaltungRecord> records)
    {
        var invalidHoldings = records
            .Where(record =>
            {
                var raw = record.GetFieldValue(FieldKeys.HoldingLengthMeters);
                return !string.IsNullOrWhiteSpace(raw)
                       && (!FachzahlParser.TryParseMeasurement(raw, out var length)
                           || length <= 0m);
            })
            .Select(record =>
            {
                var holding = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
                return string.IsNullOrWhiteSpace(holding) ? "(ohne Haltungsname)" : holding;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return invalidHoldings.Count == 0
            ? null
            : $"Haltungslaenge ist nicht lesbar bei: {string.Join(", ", invalidHoldings)}";
    }

    private void UpdatePageTexts()
    {
        if (IsSingleHoldingMode)
        {
            PageTitle = "Sanierungsmassnahme";
            PageSubtitle = "Einzelhaltung bearbeiten - Positionen im Detail pruefen, anpassen und uebernehmen.";
            return;
        }

        PageTitle = "Sanierungs-Matrix";
        PageSubtitle = "Pro Haltung eine Hauptarbeit waehlen - Meter, DN und Anschluesse kommen automatisch.";
    }

    private void BuildMeasureOptions()
    {
        MeasureOptions.Clear();
        foreach (var o in MatrixMeasureOptionBuilder.Build(MatrixMeasures, _templates, _catalog))
            MeasureOptions.Add(o);
    }

    private void InitRowFromStore(SanierungMatrixRowVm row, string holding)
    {
        var state = SanierungsMatrixStoredRowProjection.Project(_store, holding, MeasureOptions);
        if (state.AdditionalOption is not null && !MeasureOptions.Contains(state.AdditionalOption))
            MeasureOptions.Add(state.AdditionalOption);

        row.SetStoredCost(state.StoredCost);
        row.InitFrom(
            state.SelectedMeasure,
            state.Total,
            state.Menge,
            state.Verkehrsdienst,
            state.Wasserhaltung,
            state.Fraesen,
            state.Dichtheitspruefung,
            state.Dokumentation);
        if (state.HasMultipleMeasures)
            row.MarkMultipleStoredMeasures();
    }

    private void RecomputeRow(SanierungMatrixRowVm row)
    {
        var calculationLoadError = BuildCalculationLoadError();
        if (calculationLoadError is not null)
        {
            row.Hinweis = "Berechnung gesperrt";
            Status = $"FEHLER: {calculationLoadError}";
            return;
        }

        if (row.HasMultipleStoredMeasures)
        {
            row.Hinweis = "Mehrfach-Massnahme geschuetzt";
            return;
        }

        var measureId = row.SelectedMeasure?.Id;
        if (string.IsNullOrWhiteSpace(measureId))
        {
            if (_store.ByHolding.Remove(row.Holding))
            {
                _clearedHoldings.Add(row.Holding); // beim Speichern Tabellenfelder leeren
                _touchedHoldings.Add(row.Holding);
                _hasUnsavedChanges = true;
            }
            row.SetStoredCost(null);
            row.Total = 0m;
            row.Hinweis = "";
            RefreshSelectedDetailIfNeeded(row);
            RecomputeGesamt();
            return;
        }

        if (row.SelectedMeasure?.ManuelleMenge != true
            && (!FachzahlParser.TryParseMeasurement(row.Laenge, out var length)
                || length <= 0m))
        {
            row.Hinweis = "Laenge fehlt oder ist ungueltig - Berechnung gesperrt";
            Status =
                $"FEHLER: Haltungslaenge fuer {row.Holding} fehlt oder ist ungueltig. " +
                "Bestehende Kosten bleiben unveraendert.";
            return;
        }

        var preservedDetailCost = ResolveDirtyDetailForRowRecompute(row);
        _clearedHoldings.Remove(row.Holding); // wieder belegt

        var extras = new List<string>();
        if (row.OptVerkehrsdienst) extras.Add(SanierungsMatrixOptionKeys.Verkehrsdienst);
        if (row.OptWasserhaltung) extras.Add(SanierungsMatrixOptionKeys.Wasserhaltung);
        if (row.OptFraesen) extras.Add(SanierungsMatrixOptionKeys.Fraesen);
        if (row.OptDichtheit) extras.Add(SanierungsMatrixOptionKeys.Dichtheitspruefung);
        if (row.OptDokumentation) extras.Add(SanierungsMatrixOptionKeys.Dokumentation);

        // Bei manueller Menge (Reparatur-Stk oder Roboter-Stunden) den Wert uebersteuern.
        decimal? hauptMenge = row.SelectedMeasure?.ManuelleMenge == true && row.Menge > 0m ? row.Menge : null;
        var hauptKey = row.SelectedMeasure?.HauptItemKey;

        var cost = HoldingMeasureFactory.Build(row.Holding, row.Record, measureId,
            _templates, _catalog, _vatRate, extras, hauptMenge, hauptKey);

        if (cost is null)
        {
            // Massnahme nicht (mehr) baubar (Template fehlt) -> Store-Eintrag nicht stehen lassen,
            // sonst zeigt die UI "nicht gefunden", waehrend beim Speichern der alte Wert bliebe.
            if (_store.ByHolding.Remove(row.Holding))
            {
                _clearedHoldings.Add(row.Holding);
                _touchedHoldings.Add(row.Holding);
                _hasUnsavedChanges = true;
            }
            row.SetStoredCost(null);
            row.Hinweis = "Massnahme nicht gefunden";
            row.Total = 0m;
        }
        else
        {
            SanierungsMatrixDetailOverrideMerger.ApplyManualOverrides(cost, preservedDetailCost);
            _store.ByHolding[row.Holding] = cost;
            _touchedHoldings.Add(row.Holding);
            _hasUnsavedChanges = true;
            row.SetStoredCost(cost);
            row.Total = cost.Total;
            row.Hinweis = BuildRowHinweis(row, cost);
        }

        RefreshSelectedDetailIfNeeded(row);
        RecomputeGesamt();
    }

    private HoldingCost? ResolveDirtyDetailForRowRecompute(SanierungMatrixRowVm row)
    {
        if (!ReferenceEquals(_detailRow, row) || _detailSession is null || !IsDetailDirty)
            return null;

        var keep = _dialogs.Confirm(
            "Ungespeicherte Detail-Aenderungen an dieser Haltung gefunden.\n\n" +
            "Ja = Detail-Aenderungen uebernehmen und neu berechnen.\n" +
            "Nein = Detail-Aenderungen verwerfen und neu berechnen.",
            PageTitle);

        if (!keep)
            return null;

        var updated = _detailSession.ToHoldingCost(row.Holding, row.StoredCost?.Date, _vatRate);
        _detailSession.MarkClean();
        IsDetailDirty = false;
        return updated;
    }

    private void RecomputeGesamt()
    {
        GesamtTotal = Rows.Sum(r => r.Total);
        MaxRowTotal = Rows.Count == 0 ? 0m : Rows.Max(r => r.Total);
        BelegteHaltungen = Rows.Count(r => r.SelectedMeasure?.Id is not null);
        var pauschalen = TablePauschaleCostHelper.SummarizeRows(Rows.Select(r =>
        {
            var tableCost = TablePauschaleCostHelper.ParseTableNetCost(
                r.Record.GetFieldValue(FieldKeys.Cost));
            return (r.StoredCost, tableCost);
        }));
        PauschalenTotal = pauschalen.Total;
        PauschalenHaltungen = pauschalen.HoldingCount;
        HasPauschalen = PauschalenTotal > 0m;
        PauschalenText = HasPauschalen
            ? $"+ Pauschalen aus Tabelle: {PauschalenTotal:N2} CHF ({PauschalenHaltungen} Haltungen)"
            : "";
    }

    /// <summary>
    /// Record zu einer Haltung — zuerst ueber die sichtbaren Zeilen, sonst im Projekt
    /// (im Einzelmodus kann der Preis-Apply auch unsichtbare Haltungen aendern).
    /// </summary>
    private HaltungRecord? FindRecordForHolding(string holding)
        => Rows.FirstOrDefault(r => string.Equals(r.Holding, holding, StringComparison.OrdinalIgnoreCase))?.Record
           ?? _shell.Project.Data.FirstOrDefault(rec =>
               string.Equals(
                   (rec.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim(),
                   holding,
                   StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Hinweis-Text der Zeile: Anschluss-Zahl plus Warnung bei fehlenden Katalogpreisen.
    /// Delegiert an RowStoreProjection (Audit W9).
    /// </summary>
    private static string BuildRowHinweis(SanierungMatrixRowVm row, HoldingCost cost)
        => RowStoreProjection.BuildRowHinweis(row.Anschluesse, cost);

    partial void OnSelectedRowChanged(SanierungMatrixRowVm? value)
    {
        if (_suppressSelectionGuard)
            return;

        if (_detailRow is not null && !ReferenceEquals(value, _detailRow) && !ResolveDirtyDetail())
        {
            // Abbrechen -> auf der aktuellen Haltung bleiben.
            _suppressSelectionGuard = true;
            SelectedRow = _detailRow;
            _suppressSelectionGuard = false;
            return;
        }

        LoadDetailForRow(value);
    }

    /// <summary>
    /// Klaert eine offene (dirty) Detail-Session per Ja/Nein/Abbrechen-Dialog.
    /// true = geklaert (uebernommen oder verworfen) bzw. nichts offen; false = Abbrechen.
    /// </summary>
    private bool ResolveDirtyDetail()
    {
        if (_detailRow is null || _detailSession is null || !IsDetailDirty)
            return true;

        var decision = _dialogs.ConfirmCancel(
            "Es gibt nicht uebernommene Aenderungen im Detailbereich.\n\nJa = uebernehmen, Nein = verwerfen, Abbrechen = abbrechen.",
            PageTitle);

        if (decision == DialogConfirm.Cancel)
            return false;

        if (decision == DialogConfirm.Yes)
            DetailUebernehmen();
        else
            DetailVerwerfen();
        return true;
    }

    /// <summary>
    /// Leave-Guard (Audit K1/W2): Beim Verlassen der Seite (Nav-Klick, Projektwechsel,
    /// App-Schliessen) offene Detail-Edits und nicht gespeicherte Matrix-Aenderungen klaeren —
    /// vorher gingen sie kommentarlos verloren.
    /// </summary>
    public bool ConfirmLeave()
    {
        if (!ResolveDirtyDetail())
            return false;

        if (!_hasUnsavedChanges)
            return true;

        var decision = _dialogs.ConfirmCancel(
            $"{PageTitle}: Es gibt nicht gespeicherte Aenderungen (costs.json).\n\nJa = speichern, Nein = verwerfen, Abbrechen = auf der Seite bleiben.",
            PageTitle);

        if (decision == DialogConfirm.Cancel)
            return false;

        if (decision == DialogConfirm.Yes)
        {
            Speichern();
            return !_hasUnsavedChanges; // Speichern kann verweigern (z.B. Load-Fehler) -> bleiben
        }

        return true; // Nein = bewusst verwerfen
    }

    private void RefreshSelectedDetailIfNeeded(SanierungMatrixRowVm row)
    {
        if (ReferenceEquals(_detailRow, row))
            LoadDetailForRow(row);
    }

    private void LoadDetailForRow(SanierungMatrixRowVm? row)
    {
        if (_detailSession is not null)
            _detailSession.PropertyChanged -= DetailSession_PropertyChanged;

        _detailRow = row;
        if (row is null)
        {
            _detailSession = null;
            SelectedDetailMeasures = new ObservableCollection<SanierungsMatrixDetailEditMeasureVm>();
            DetailTitle = "Keine Haltung gewaehlt";
            DetailSubtitle = "Links eine Haltung waehlen.";
            DetailTotal = "";
            DetailEditStatus = "";
            IsDetailDirty = false;
            OnPropertyChanged(nameof(SelectedDetailMeasures));
            NotifyDetailCommands();
            return;
        }

        _detailSession = SanierungsMatrixDetailEditSession.FromCost(row.StoredCost, _vatRate);
        _detailSession.PropertyChanged += DetailSession_PropertyChanged;
        SelectedDetailMeasures = _detailSession.Measures;
        DetailTitle = $"Haltung {row.Holding}";
        DetailSubtitle = row.MeasuresSummary;
        UpdateDetailStateFromSession();
        OnPropertyChanged(nameof(SelectedDetailMeasures));
    }

    private void DetailSession_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SanierungsMatrixDetailEditSession.Total)
            or nameof(SanierungsMatrixDetailEditSession.MwstAmount)
            or nameof(SanierungsMatrixDetailEditSession.TotalInclMwst)
            or nameof(SanierungsMatrixDetailEditSession.IsDirty))
        {
            UpdateDetailStateFromSession();
        }
    }

    private void UpdateDetailStateFromSession()
    {
        if (_detailSession is null)
        {
            DetailTotal = "";
            DetailEditStatus = "";
            IsDetailDirty = false;
        }
        else
        {
            DetailTotal = $"Total: {_detailSession.Total:N2} CHF";
            DetailEditStatus = _detailSession.IsDirty ? "Aenderungen offen" : "";
            IsDetailDirty = _detailSession.IsDirty;
        }

        // Obere Tabelle mit der Detailliste synchron halten: die Zusatz-Haekchen (VD/Wasser/
        // Fraesen/Dichtheit/Doku) der Zeile aus den ausgewaehlten Detail-Positionen ableiten.
        SyncDetailRowOptions();
        NotifyDetailCommands();
    }

    // Spiegelt die im Detail ausgewaehlten Zusatz-Positionen auf die Haekchen der Matrix-Zeile.
    private void SyncDetailRowOptions()
    {
        if (_detailRow is null || _detailSession is null)
            return;

        var lines = _detailSession.Measures
            .SelectMany(m => m.Lines)
            .Select(l => ((string?)l.ItemKey, l.Selected));
        var flags = SanierungMatrixOptionDeriver.Derive(
            lines,
            SanierungsMatrixOptionKeys.Verkehrsdienst,
            SanierungsMatrixOptionKeys.Wasserhaltung,
            SanierungsMatrixOptionKeys.Fraesen,
            SanierungsMatrixOptionKeys.Dichtheitspruefung,
            SanierungsMatrixOptionKeys.Dokumentation);
        _detailRow.SetOptionFlags(flags.Vd, flags.Wasser, flags.Fraesen, flags.Dichtheit, flags.Doku);
    }

    [RelayCommand(CanExecute = nameof(CanApplyDetailChanges))]
    private void DetailUebernehmen()
    {
        if (_detailRow is null || _detailSession is null)
            return;

        var updated = _detailSession.ToHoldingCost(_detailRow.Holding, _detailRow.StoredCost?.Date, _vatRate);
        _store.ByHolding[_detailRow.Holding] = updated;
        _clearedHoldings.Remove(_detailRow.Holding);
        _touchedHoldings.Add(_detailRow.Holding);
        _hasUnsavedChanges = true; // erst "Speichern" schreibt costs.json (Audit K1)

        _detailRow.SetStoredCost(updated);
        _detailRow.Total = updated.Total;
        _detailRow.Hinweis = updated.Measures.Count > 1
            ? "Mehrfach-Massnahme: im Detail bearbeiten"
            : BuildRowHinweis(_detailRow, updated);

        _detailSession.MarkClean();
        DetailSubtitle = _detailRow.MeasuresSummary;
        UpdateDetailStateFromSession();
        DetailEditStatus = "Uebernommen - noch nicht gespeichert";
        RecomputeGesamt();
        Status = $"Detail uebernommen: {_detailRow.Holding}, Total {_detailRow.Total:N2} CHF - 'Speichern' schreibt costs.json.";
    }

    [RelayCommand(CanExecute = nameof(CanApplyDetailChanges))]
    private void DetailVerwerfen()
    {
        if (_detailRow is null)
            return;

        LoadDetailForRow(_detailRow);
        Status = $"Detail verworfen: {_detailRow.Holding}.";
    }

    private bool CanApplyDetailChanges() => IsDetailDirty && _detailRow is not null && _detailSession is not null;

    partial void OnIsDetailDirtyChanged(bool value)
    {
        NotifyDetailCommands();
    }

    private void NotifyDetailCommands()
    {
        DetailUebernehmenCommand.NotifyCanExecuteChanged();
        DetailVerwerfenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Öffnet den (einen) Preis-Katalog. Nach dem Schliessen werden NUR die Katalogpreise
    /// auf die bestehenden gespeicherten Positionen angewendet — KEIN Template-Rebuild mehr
    /// (Audit K2: der Rebuild verwarf Detail-/Fenster-Anpassungen an Einzel-Massnahmen).
    /// Overrides (IsPriceOverridden) bleiben unangetastet.
    /// </summary>
    [RelayCommand]
    private void KatalogBearbeiten()
    {
        // Offene Detail-Edits zuerst klaeren, sonst ersetzt der Refresh die Session still (Audit W4).
        if (!ResolveDirtyDetail())
            return;

        var dialog = new CostCatalogEditorDialog(
            string.IsNullOrWhiteSpace(_projectPath) ? null : _projectPath,
            _catalogStore);
        dialog.ShowDialog();
        ReloadCatalogAndApplyPrices();
    }

    private void ReloadCatalogAndApplyPrices()
    {
        var catalog = _catalogStore.LoadMerged(_projectPath, out var loadError);
        if (!string.IsNullOrWhiteSpace(loadError))
        {
            _catalogLoadError = loadError;
            Status = $"FEHLER: Kostenkatalog konnte nicht geladen werden: {loadError}";
            _dialogs.Error(
                $"Katalogpreise wurden nicht angewendet, weil der Kostenkatalog nicht sauber geladen werden konnte:\n{loadError}",
                "Sanierungs-Matrix");
            return;
        }

        _catalogLoadError = null;
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
        _catalog = catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var changedHoldings = CatalogPriceApplier.ApplyCatalogPricesToStoredCosts(_store, _catalog, _vatRate);
        foreach (var h in changedHoldings)
            _touchedHoldings.Add(h);
        var updatedHoldings = changedHoldings.Count;

        // Zeilen-Anzeige nachziehen (Totals/Zusammenfassung) — ohne Rebuild.
        foreach (var row in Rows)
        {
            if (_store.ByHolding.TryGetValue(row.Holding, out var cost))
            {
                row.SetStoredCost(cost);
                row.Total = cost.Total;
                if (!row.HasMultipleStoredMeasures)
                    row.Hinweis = BuildRowHinweis(row, cost);
            }
        }

        RecomputeGesamt();
        if (_detailRow is not null)
            LoadDetailForRow(_detailRow); // Session wurde vorab geklaert (ResolveDirtyDetail)

        if (updatedHoldings > 0)
            _hasUnsavedChanges = true;
        Status = updatedHoldings == 0
            ? "Katalog neu geladen - keine Preisaenderungen."
            : $"Katalogpreise auf {updatedHoldings} Haltung(en) angewendet (manuelle Overrides unangetastet) - 'Speichern' schreibt costs.json.";
    }

    [RelayCommand]
    private void Speichern()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            _dialogs.Info("Projekt bitte zuerst speichern, um Kosten abzulegen.", "Sanierungs-Matrix");
            return;
        }

        // Verlustschutz (Audit K3): costs.json war beim Laden nicht lesbar -> _store ist leer.
        // Ein Save wuerde alle Kostendaten endgueltig ueberschreiben (.bak waere danach auch defekt).
        if (_storeLoadError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: costs.json konnte beim Laden nicht gelesen werden.\n{_storeLoadError}\n\nBitte Datei pruefen (costs\\costs.json bzw. .bak), dann 'Neu laden'.",
                "Sanierungs-Matrix");
            return;
        }

        var calculationLoadError = BuildCalculationLoadError();
        if (calculationLoadError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: {calculationLoadError}\n\n" +
                "Bitte Kostenkatalog/Massnahmenvorlagen pruefen und danach 'Neu laden'.",
                "Sanierungs-Matrix");
            return;
        }

        if (_tableCostParseError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: {_tableCostParseError}\n\n" +
                "Bitte die Tabellenkosten korrigieren und danach 'Neu laden'.",
                "Sanierungs-Matrix");
            return;
        }

        // Audit K1: Offene Detail-Aenderungen gehoeren zum Speichern dazu — vorher zeigte
        // der Erfolgs-Dialog "Gespeichert", waehrend die sichtbaren Edits fehlten.
        if (IsDetailDirty && _detailRow is not null && _detailSession is not null)
            DetailUebernehmen();

        // Audit W8: Frisch von Platte laden und NUR die eigenen Aenderungen hineinmergen —
        // sonst ueberschreibt der Seiten-Snapshot Aenderungen anderer Schreiber (Kostenfenster).
        var fresh = _costRepo.Load(_projectPath, out var freshError);
        if (freshError is not null)
        {
            _dialogs.Error(
                $"Speichern gesperrt: costs.json konnte nicht frisch gelesen werden.\n{freshError}",
                "Sanierungs-Matrix");
            return;
        }
        foreach (var holding in _touchedHoldings)
        {
            if (_store.ByHolding.TryGetValue(holding, out var ownCost))
                fresh.ByHolding[holding] = ownCost;
            else
                fresh.ByHolding.Remove(holding);
        }
        _store = fresh;

        // Audit W6: Nur in dieser Sitzung geaenderte Haltungen stempeln (userEdited/Manual) —
        // vorher wurden ALLE Haltungen mit Store-Eintrag bei jedem Speichern ueberschrieben.
        foreach (var holding in _touchedHoldings)
        {
            var record = FindRecordForHolding(holding);
            if (record is null)
                continue;
            if (_store.ByHolding.TryGetValue(holding, out var cost))
                DataPageSanierungCostMapper.ApplyCosts(record, cost);
            else if (_clearedHoldings.Contains(holding))
                // Massnahme wurde auf "keine" gesetzt -> alte Kosten/Massnahmen-Felder echt leeren.
                DataPageSanierungCostMapper.ClearCosts(record);
        }

        if (!_costRepo.Save(_projectPath, _store, out var error))
        {
            _dialogs.Error($"Speichern fehlgeschlagen: {error}", "Sanierungs-Matrix");
            return;
        }

        // Abgeleitete Kostenfelder aller Haltungen auf den frisch gespeicherten Store nachziehen
        // (Sanieren-Regel: nur Ja zaehlt; Nein/leer -> Felder leer).
        _costFieldSync.Sync(_shell.Project, _store);

        _clearedHoldings.Clear();
        _touchedHoldings.Clear();

        _hasUnsavedChanges = false;
        if (_detailSession is not null && !_detailSession.IsDirty)
            DetailEditStatus = "";
        _shell.Project.Dirty = true;
        Status = $"Gespeichert: {BelegteHaltungen} Haltungen, Total {GesamtTotal:N2} CHF.";
        _dashboardRefresh.NotifyCostsChanged();
        _dialogs.Info(
            $"Sanierungs-Matrix gespeichert.\n{BelegteHaltungen} Haltungen mit Massnahme, Total {GesamtTotal:N2} CHF (exkl. MwSt.).\n\nDas NPK-Leistungsverzeichnis exportierst du im Druckcenter.",
            "Sanierungs-Matrix");
    }
}
