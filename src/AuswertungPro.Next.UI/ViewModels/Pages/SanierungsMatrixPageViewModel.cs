using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Dialogs;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Eine waehlbare Hauptarbeit (Id=null = keine). Kategorie = Renovierung/Reparatur.
/// ManuelleMenge = Menge wird vom Anwender eingegeben (Stk oder Stunden), sonst = Haltungslaenge.
/// HauptItemKey = Katalog-Key der Hauptarbeit-Zeile (weicht bei Kanalroboter von Id ab).
/// </summary>
public sealed record MeasureOption(string? Id, string Name, string Kategorie, bool ManuelleMenge, string HauptItemKey)
{
    public override string ToString() => Name;
}

public static class SanierungsMatrixNavigationTarget
{
    public static string? FromRecord(HaltungRecord? record)
    {
        var holding = (record?.GetFieldValue("Haltungsname") ?? "").Trim();
        return string.IsNullOrWhiteSpace(holding) ? null : holding;
    }

    public static SanierungMatrixRowVm? FindRow(IEnumerable<SanierungMatrixRowVm> rows, string? holding, HaltungRecord? targetRecord = null)
    {
        if (targetRecord is not null)
        {
            var byRecord = rows.FirstOrDefault(r => ReferenceEquals(r.Record, targetRecord));
            if (byRecord is not null)
                return byRecord;
        }

        var target = (holding ?? "").Trim();
        if (target.Length == 0)
            return null;

        return rows.FirstOrDefault(r => string.Equals(r.Holding, target, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<SanierungMatrixRowVm> FilterRows(
        IEnumerable<SanierungMatrixRowVm> rows,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
    {
        var list = rows.ToList();
        if (!singleHoldingMode)
            return list;

        var row = FindRow(list, holding, targetRecord);
        return row is null ? Array.Empty<SanierungMatrixRowVm>() : new[] { row };
    }
}

public sealed partial class SanierungsMatrixDetailEditLineVm : ObservableObject
{
    private readonly Action _changed;
    // Erst nach dem Konstruktor duerfen Qty-/Preis-Aenderungen als Override zaehlen.
    private readonly bool _initialized;

    public string Group { get; }
    public string ItemKey { get; }
    public string Text { get; }
    public string Unit { get; }
    public string PriceHint { get; private set; }

    // Audit W5: Im Detail editierte Preise/Mengen muessen als Override markiert werden,
    // sonst setzt der naechste Katalog-Preis-Apply sie still auf Katalogwerte zurueck.
    public bool IsPriceOverridden { get; private set; }
    public bool IsQtyOverridden { get; private set; }

    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _transferMarked;
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitPrice;

    public decimal LineTotal => Selected ? Qty * UnitPrice : 0m;

    public SanierungsMatrixDetailEditLineVm(CostLine line, Action changed)
    {
        _changed = changed;
        Group = line.Group;
        ItemKey = line.ItemKey;
        Text = line.Text;
        Unit = line.Unit;
        PriceHint = line.PriceHint;
        Selected = line.Selected;
        TransferMarked = line.TransferMarked;
        Qty = line.Qty;
        UnitPrice = line.UnitPrice;
        IsPriceOverridden = line.IsPriceOverridden;
        IsQtyOverridden = line.IsQtyOverridden;
        _initialized = true;
    }

    public CostLine ToModel()
    {
        return new CostLine
        {
            Group = Group,
            ItemKey = ItemKey,
            Text = Text,
            Unit = Unit,
            Qty = Qty,
            UnitPrice = UnitPrice,
            Selected = Selected,
            TransferMarked = TransferMarked,
            IsPriceOverridden = IsPriceOverridden,
            IsQtyOverridden = IsQtyOverridden,
            PriceHint = PriceHint,
        };
    }

    partial void OnSelectedChanged(bool value) => NotifyChanged();
    partial void OnTransferMarkedChanged(bool value) => NotifyChanged();

    partial void OnQtyChanged(decimal value)
    {
        if (_initialized)
            IsQtyOverridden = true;
        NotifyChanged();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (_initialized)
        {
            IsPriceOverridden = true;
            PriceHint = "";
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(LineTotal));
        _changed();
    }
}

public sealed partial class SanierungsMatrixDetailEditMeasureVm : ObservableObject
{
    private readonly Action _changed;

    public string MeasureName { get; }
    public string MeasureId { get; }
    public int? Dn { get; }
    public decimal? LengthMeters { get; }
    public ObservableCollection<SanierungsMatrixDetailEditLineVm> Lines { get; } = new();

    [ObservableProperty] private decimal _total;

    public SanierungsMatrixDetailEditMeasureVm(MeasureCost measure, Action changed)
    {
        _changed = changed;
        MeasureName = string.IsNullOrWhiteSpace(measure.MeasureName) ? measure.MeasureId : measure.MeasureName;
        MeasureId = measure.MeasureId;
        Dn = measure.Dn;
        LengthMeters = measure.LengthMeters;

        foreach (var line in measure.Lines)
            Lines.Add(new SanierungsMatrixDetailEditLineVm(line, LineChanged));

        Recalculate(markDirty: false);
    }

    public MeasureCost ToModel()
    {
        return new MeasureCost
        {
            MeasureId = MeasureId,
            MeasureName = MeasureName,
            Dn = Dn,
            LengthMeters = LengthMeters,
            Lines = Lines.Select(l => l.ToModel()).ToList(),
            Total = Total,
        };
    }

    private void LineChanged()
    {
        Recalculate(markDirty: true);
    }

    private void Recalculate(bool markDirty)
    {
        Total = Lines.Sum(l => l.LineTotal);
        if (markDirty)
            _changed();
    }
}

public sealed partial class SanierungsMatrixDetailEditSession : ObservableObject
{
    private readonly decimal _vatRate;

    public ObservableCollection<SanierungsMatrixDetailEditMeasureVm> Measures { get; } = new();

    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _mwstAmount;
    [ObservableProperty] private decimal _totalInclMwst;
    [ObservableProperty] private bool _isDirty;

    private SanierungsMatrixDetailEditSession(decimal vatRate)
    {
        _vatRate = vatRate;
    }

    public static SanierungsMatrixDetailEditSession FromCost(HoldingCost? cost, decimal vatRate)
    {
        var session = new SanierungsMatrixDetailEditSession(vatRate);
        if (cost is not null)
        {
            foreach (var measure in cost.Measures)
                session.Measures.Add(new SanierungsMatrixDetailEditMeasureVm(CloneMeasure(measure), session.MeasureChanged));
        }

        session.Recalculate();
        session.MarkClean();
        return session;
    }

    public HoldingCost ToHoldingCost(string holding, DateTime? date, decimal vatRate)
    {
        var measures = Measures.Select(m => m.ToModel()).ToList();
        return CostCalculatorLogicService.BuildHoldingCost(holding, date, measures, vatRate);
    }

    public void MarkClean()
    {
        IsDirty = false;
    }

    private void MeasureChanged()
    {
        Recalculate();
        IsDirty = true;
    }

    private void Recalculate()
    {
        var totals = CostCalculatorLogicService.CalculateTotals(Measures.Sum(m => m.Total), _vatRate);
        Total = totals.Total;
        MwstAmount = totals.MwstAmount;
        TotalInclMwst = totals.TotalInclMwst;
    }

    private static MeasureCost CloneMeasure(MeasureCost measure)
    {
        return new MeasureCost
        {
            MeasureId = measure.MeasureId,
            MeasureName = measure.MeasureName,
            Dn = measure.Dn,
            LengthMeters = measure.LengthMeters,
            Total = measure.Total,
            Lines = measure.Lines.Select(CloneLine).ToList(),
        };
    }

    private static CostLine CloneLine(CostLine line)
    {
        return new CostLine
        {
            Group = line.Group,
            ItemKey = line.ItemKey,
            Text = line.Text,
            Unit = line.Unit,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            Selected = line.Selected,
            TransferMarked = line.TransferMarked,
            IsPriceOverridden = line.IsPriceOverridden,
            IsQtyOverridden = line.IsQtyOverridden,
            PriceHint = line.PriceHint,
        };
    }
}

/// <summary>Eine Haltungs-Zeile in der Sanierungs-Matrix.</summary>
public sealed partial class SanierungMatrixRowVm : ObservableObject
{
    private readonly Action<SanierungMatrixRowVm>? _onChanged;
    private bool _suppress;

    public HaltungRecord Record { get; }
    public string Holding { get; }
    public string Dn { get; }
    public string Laenge { get; }
    public int Anschluesse { get; }
    public HoldingCost? StoredCost { get; private set; }

    [ObservableProperty] private MeasureOption? _selectedMeasure;
    [ObservableProperty] private decimal _menge;
    [ObservableProperty] private bool _isMengeEditierbar;

    /// <summary>Mengen-Zelle nur bei Stk-Hauptarbeit (Reparatur) editierbar; bei m = Länge gesperrt.</summary>
    public bool IsMengeReadOnly => !IsMengeEditierbar;
    [ObservableProperty] private bool _optVerkehrsdienst;
    [ObservableProperty] private bool _optWasserhaltung;
    [ObservableProperty] private bool _optFraesen;
    [ObservableProperty] private bool _optDichtheit;
    [ObservableProperty] private bool _optDokumentation;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _hinweis = "";
    [ObservableProperty] private bool _hasMultipleStoredMeasures;
    [ObservableProperty] private string _measuresSummary = SanierungsMatrixMeasureSummaryFormatter.EmptySummary;

    public bool IsMatrixEditable => !HasMultipleStoredMeasures;

    public SanierungMatrixRowVm(HaltungRecord record, string holding, string dn, string laenge,
        int anschluesse, Action<SanierungMatrixRowVm>? onChanged)
    {
        Record = record;
        Holding = holding;
        Dn = dn;
        Laenge = laenge;
        Anschluesse = anschluesse;
        _onChanged = onChanged;
    }

    /// <summary>Vorbelegen aus gespeicherten Kosten ohne Neuberechnung.</summary>
    public void InitFrom(MeasureOption? option, decimal total, decimal menge,
        bool vd, bool wasser, bool fraesen, bool dichtheit, bool doku)
    {
        _suppress = true;
        SelectedMeasure = option;
        IsMengeEditierbar = option?.ManuelleMenge == true;
        Menge = menge;
        OptVerkehrsdienst = vd;
        OptWasserhaltung = wasser;
        OptFraesen = fraesen;
        OptDichtheit = dichtheit;
        OptDokumentation = doku;
        Total = total;
        _suppress = false;
    }

    public void SetStoredCost(HoldingCost? cost)
    {
        StoredCost = cost;
        MeasuresSummary = SanierungsMatrixMeasureSummaryFormatter.FormatSummary(cost);
    }

    public void MarkMultipleStoredMeasures()
    {
        HasMultipleStoredMeasures = true;
        Hinweis = "Mehrfach-Massnahme: im Detail bearbeiten";
    }

    partial void OnSelectedMeasureChanged(MeasureOption? value)
    {
        if (_suppress)
            return;

        // Menge passend vorbelegen: manuelle Menge (Stk/h) -> 1 (editierbar), sonst Laenge.
        _suppress = true;
        IsMengeEditierbar = value?.ManuelleMenge == true;
        if (value?.ManuelleMenge == true)
        {
            if (Menge <= 0m) Menge = 1m;
        }
        else
        {
            // Kulturunabhaengig parsen: "45.30" darf auf Komma-Locales nicht zu 4530 werden.
            Menge = decimal.TryParse(
                Laenge?.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : 0m;
        }
        _suppress = false;

        _onChanged?.Invoke(this);
    }

    partial void OnIsMengeEditierbarChanged(bool value) => OnPropertyChanged(nameof(IsMengeReadOnly));
    partial void OnHasMultipleStoredMeasuresChanged(bool value) => OnPropertyChanged(nameof(IsMatrixEditable));
    partial void OnMengeChanged(decimal value) => Recalc();
    partial void OnOptVerkehrsdienstChanged(bool value) => Recalc();
    partial void OnOptWasserhaltungChanged(bool value) => Recalc();
    partial void OnOptFraesenChanged(bool value) => Recalc();
    partial void OnOptDichtheitChanged(bool value) => Recalc();
    partial void OnOptDokumentationChanged(bool value) => Recalc();

    private void Recalc()
    {
        if (!_suppress)
            _onChanged?.Invoke(this);
    }
}

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
    };

    // Zusatzoptionen -> Katalog-ItemKey.
    private const string KeyVd = "VORARBEIT_VD";
    private const string KeyWasser = "VORARBEIT_WASSERHALTUNG";
    private const string KeyFraesen = "VORARBEIT_FRAESEN";
    private const string KeyDichtheit = "QK_DICHTHEITSPRUEFUNG";
    private const string KeyDoku = "QK_DOKUMENTATION";

    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;
    private readonly CostCatalogStore _catalogStore = new();
    private readonly MeasureTemplateStore _templateStore = new();
    private readonly ProjectCostStoreRepository _costRepo = new();

    private Dictionary<string, MeasureTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CostCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private ProjectCostStore _store = new();
    // != null wenn costs.json beim Laden nicht lesbar war -> Speichern gesperrt (Audit K3).
    private string? _storeLoadError;
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

    [ObservableProperty] private bool _isSingleHoldingMode;
    [ObservableProperty] private string _pageTitle = "Sanierungs-Matrix";
    [ObservableProperty] private string _pageSubtitle = "Pro Haltung eine Hauptarbeit waehlen - Meter, DN und Anschluesse kommen automatisch.";
    [ObservableProperty] private decimal _gesamtTotal;
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
    {
        _shell = shell;
        _sp = services;
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

    [RelayCommand]
    private void Reload()
    {
        // Audit W1: Rows.Clear() drueckt via TwoWay-SelectedItem synchron SelectedRow=null —
        // ohne Schutz feuert der Dirty-Guard mitten im Neuaufbau (Doppel-Dialoge, Store/UI-Drift).
        // Darum: offene Aenderungen EINMAL vorab klaeren, dann Guard unterdruecken.
        if (IsDetailDirty || _hasUnsavedChanges)
        {
            if (!_sp.Dialogs.Confirm(
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
        _projectPath = _sp.Settings.LastProjectPath ?? "";

        var catalog = _catalogStore.LoadMerged(_projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
        _catalog = catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var tplCatalog = _templateStore.LoadMerged(_projectPath);
        _templates = tplCatalog.Measures
            .Where(m => !m.Disabled && !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(m => m.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        BuildMeasureOptions();

        _store = _costRepo.Load(_projectPath, out _storeLoadError);

        var loadedRows = new List<SanierungMatrixRowVm>();
        foreach (var record in _shell.Project.Data)
        {
            var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
            if (string.IsNullOrWhiteSpace(holding))
                continue;

            var dn = (record.GetFieldValue("DN_mm") ?? "").Trim();
            var laenge = (record.GetFieldValue("Haltungslaenge_m") ?? "").Trim();
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

        if (_storeLoadError is not null)
        {
            Status = $"WARNUNG: {_storeLoadError} — Speichern ist gesperrt, bestehende Kosten bleiben unangetastet.";
            _sp.Dialogs.Warn(
                $"Kostendaten konnten nicht geladen werden:\n{_storeLoadError}\n\nSpeichern ist gesperrt, damit costs.json nicht mit einem leeren Stand ueberschrieben wird.\nBitte Datei pruefen (costs\\costs.json bzw. .bak) und danach 'Neu laden'.",
                "Sanierungs-Matrix");
        }
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
        MeasureOptions.Add(new MeasureOption(null, "— keine —", "", false, ""));

        var options = new List<MeasureOption>();
        foreach (var (id, kategorie) in MatrixMeasures)
        {
            if (!_templates.TryGetValue(id, out var tpl))
                continue;

            // Hauptarbeit-Zeile bestimmen (ItemKey + Einheit). Bei Kanalroboter weicht der
            // Hauptarbeit-ItemKey von der Massnahmen-Id ab (HAUPTARBEIT_HINDERNISSE_ROBOTER).
            var hauptLine = tpl.Lines.FirstOrDefault(l =>
                string.Equals(l.Group, "Hauptarbeit", StringComparison.OrdinalIgnoreCase));
            var hauptKey = string.IsNullOrWhiteSpace(hauptLine?.ItemKey) ? id : hauptLine!.ItemKey.Trim();
            _catalog.TryGetValue(hauptKey, out var hauptItem);
            var unit = hauptItem?.Unit ?? "";
            // Manuelle Menge bei Stk (Reparatur) ODER h (Roboter-Stunden); m -> Haltungslaenge.
            var manuelleMenge = string.Equals(unit, "Stk", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(unit, "h", StringComparison.OrdinalIgnoreCase);
            var baseName = string.IsNullOrWhiteSpace(tpl.Name) ? id : tpl.Name;
            // Name ohne Praefix - die Kategorie zeigt der ComboBox-Gruppen-Header.
            options.Add(new MeasureOption(id, baseName, kategorie, manuelleMenge, hauptKey));
        }

        foreach (var o in options
                     .OrderBy(o => o.Kategorie == "Renovierung" ? 0 : o.Kategorie == "Reparatur" ? 1 : 2)
                     .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            MeasureOptions.Add(o);
        }
    }

    private void InitRowFromStore(SanierungMatrixRowVm row, string holding)
    {
        if (!_store.ByHolding.TryGetValue(holding, out var existing) || existing.Measures.Count == 0)
        {
            row.SetStoredCost(null);
            row.InitFrom(MeasureOptions[0], 0m, 0m, false, false, false, false, false);
            return;
        }

        row.SetStoredCost(existing);
        var hasMultipleMeasures = existing.Measures.Count > 1;
        var firstId = existing.Measures[0].MeasureId;
        var opt = MeasureOptions.FirstOrDefault(o => string.Equals(o.Id, firstId, StringComparison.OrdinalIgnoreCase));
        if (opt is null)
        {
            // Gespeicherte Massnahme ist (mehr) keine Matrix-Hauptarbeit (alte Daten oder aus dem
            // Einzelfenster). Als Ad-hoc-Option anzeigen statt "keine" -> kein Datenverlust, UI bleibt
            // konsistent zum Store. Bei Aenderung baut RecomputeRow neu (oder zeigt "nicht gefunden").
            var name = string.IsNullOrWhiteSpace(existing.Measures[0].MeasureName)
                ? firstId : existing.Measures[0].MeasureName;
            var adhoc = MeasureOptions.FirstOrDefault(o => string.Equals(o.Id, firstId, StringComparison.OrdinalIgnoreCase))
                        ?? new MeasureOption(firstId, name + " (gespeichert)", "Übrige", false, firstId);
            if (!MeasureOptions.Contains(adhoc))
                MeasureOptions.Add(adhoc);
            row.InitFrom(adhoc, existing.Total, 0m, false, false, false, false, false);
            if (hasMultipleMeasures)
                row.MarkMultipleStoredMeasures();
            return;
        }

        var lines = existing.Measures[0].Lines;
        bool Sel(string key) => lines.Any(l => l.Selected &&
            string.Equals(l.ItemKey, key, StringComparison.OrdinalIgnoreCase));
        // Hauptmenge ueber den HauptItemKey der Option (bei Kanalroboter != MeasureId).
        var hauptLine = lines.FirstOrDefault(l => string.Equals(l.ItemKey, opt.HauptItemKey, StringComparison.OrdinalIgnoreCase));
        var menge = hauptLine?.Qty ?? 0m;

        row.InitFrom(opt, existing.Total, menge,
            Sel(KeyVd), Sel(KeyWasser), Sel(KeyFraesen), Sel(KeyDichtheit), Sel(KeyDoku));
        if (hasMultipleMeasures)
            row.MarkMultipleStoredMeasures();
    }

    private void RecomputeRow(SanierungMatrixRowVm row)
    {
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

        _clearedHoldings.Remove(row.Holding); // wieder belegt

        var extras = new List<string>();
        if (row.OptVerkehrsdienst) extras.Add(KeyVd);
        if (row.OptWasserhaltung) extras.Add(KeyWasser);
        if (row.OptFraesen) extras.Add(KeyFraesen);
        if (row.OptDichtheit) extras.Add(KeyDichtheit);
        if (row.OptDokumentation) extras.Add(KeyDoku);

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

    private void RecomputeGesamt()
    {
        GesamtTotal = Rows.Sum(r => r.Total);
        BelegteHaltungen = Rows.Count(r => r.SelectedMeasure?.Id is not null);
    }

    /// <summary>
    /// Record zu einer Haltung — zuerst ueber die sichtbaren Zeilen, sonst im Projekt
    /// (im Einzelmodus kann der Preis-Apply auch unsichtbare Haltungen aendern).
    /// </summary>
    private HaltungRecord? FindRecordForHolding(string holding)
        => Rows.FirstOrDefault(r => string.Equals(r.Holding, holding, StringComparison.OrdinalIgnoreCase))?.Record
           ?? _shell.Project.Data.FirstOrDefault(rec =>
               string.Equals((rec.GetFieldValue("Haltungsname") ?? "").Trim(), holding, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Hinweis-Text der Zeile: Anschluss-Zahl plus Warnung bei fehlenden Katalogpreisen
    /// (Audit W9: 0-CHF-Totals waren vorher unsichtbar).
    /// </summary>
    private static string BuildRowHinweis(SanierungMatrixRowVm row, HoldingCost cost)
    {
        var hints = new List<string>();
        if (row.Anschluesse > 0)
            hints.Add($"{row.Anschluesse} Anschluss(e)");
        if (cost.Measures.SelectMany(m => m.Lines).Any(l => l.Selected && l.Qty > 0m && l.UnitPrice <= 0m))
            hints.Add("Preis fehlt im Katalog");
        return string.Join(" | ", hints);
    }

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

        var decision = _sp.Dialogs.ConfirmCancel(
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

        var decision = _sp.Dialogs.ConfirmCancel(
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

        NotifyDetailCommands();
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

        var dialog = new CostCatalogEditorDialog(string.IsNullOrWhiteSpace(_projectPath) ? null : _projectPath);
        dialog.ShowDialog();
        ReloadCatalogAndApplyPrices();
    }

    private void ReloadCatalogAndApplyPrices()
    {
        var catalog = _catalogStore.LoadMerged(_projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
        _catalog = catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var updatedHoldings = ApplyCatalogPricesToStoredCosts();

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

    /// <summary>
    /// Wendet die aktuellen Katalogpreise auf alle gespeicherten Positionen an (auch
    /// Mehrfach-Buendel — reine Preisaktualisierung, kein Rebuild). Zeilen mit
    /// IsPriceOverridden und Positionen ohne eindeutigen Katalogpreis bleiben unveraendert.
    /// </summary>
    private int ApplyCatalogPricesToStoredCosts()
    {
        var updated = 0;
        foreach (var (holding, cost) in _store.ByHolding)
        {
            var changed = false;
            foreach (var measure in cost.Measures)
            {
                var measureChanged = false;
                foreach (var line in measure.Lines)
                {
                    if (line.IsPriceOverridden || string.IsNullOrWhiteSpace(line.ItemKey))
                        continue;
                    if (!_catalog.TryGetValue(line.ItemKey.Trim(), out var item) || !item.Active)
                        continue;

                    var price = ResolveExactCatalogPrice(item, measure.Dn, line.Qty);
                    if (price is decimal p)
                    {
                        if (p != line.UnitPrice)
                        {
                            line.UnitPrice = p;
                            measureChanged = true;
                        }

                        if (!string.IsNullOrWhiteSpace(line.PriceHint))
                        {
                            line.PriceHint = "";
                            measureChanged = true;
                        }
                    }
                }

                if (measureChanged)
                {
                    measure.Total = measure.Lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);
                    changed = true;
                }
            }

            if (changed)
            {
                var totals = CostCalculatorLogicService.CalculateTotals(cost.Measures.Sum(m => m.Total), _vatRate);
                cost.Total = totals.Total;
                cost.MwstRate = _vatRate;
                cost.MwstAmount = totals.MwstAmount;
                cost.TotalInclMwst = totals.TotalInclMwst;
                _touchedHoldings.Add(holding);
                updated++;
            }
        }

        return updated;
    }

    /// <summary>
    /// Exakter Katalogpreis: Fixed-Positionen direkt, ByDN nur bei passendem DN-/Mengen-Bereich.
    /// Bewusst KEIN Naechster-DN-Fallback — lieber Preis stehen lassen als still falsch ersetzen.
    /// </summary>
    private static decimal? ResolveExactCatalogPrice(CostCatalogItem item, int? dn, decimal qty)
    {
        if (item.DnPrices is { Count: > 0 })
        {
            if (dn is not int d)
                return null;
            var bucket = item.DnPrices.FirstOrDefault(b =>
                d >= b.DnFrom && d <= b.DnTo
                && (!b.QtyFrom.HasValue || qty >= b.QtyFrom.Value)
                && (!b.QtyTo.HasValue || qty <= b.QtyTo.Value));
            return bucket?.Price;
        }

        return item.Price;
    }

    [RelayCommand]
    private void Speichern()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            _sp.Dialogs.Info("Projekt bitte zuerst speichern, um Kosten abzulegen.", "Sanierungs-Matrix");
            return;
        }

        // Verlustschutz (Audit K3): costs.json war beim Laden nicht lesbar -> _store ist leer.
        // Ein Save wuerde alle Kostendaten endgueltig ueberschreiben (.bak waere danach auch defekt).
        if (_storeLoadError is not null)
        {
            _sp.Dialogs.Error(
                $"Speichern gesperrt: costs.json konnte beim Laden nicht gelesen werden.\n{_storeLoadError}\n\nBitte Datei pruefen (costs\\costs.json bzw. .bak), dann 'Neu laden'.",
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
            _sp.Dialogs.Error(
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
            _sp.Dialogs.Error($"Speichern fehlgeschlagen: {error}", "Sanierungs-Matrix");
            return;
        }

        _clearedHoldings.Clear();
        _touchedHoldings.Clear();

        _hasUnsavedChanges = false;
        if (_detailSession is not null && !_detailSession.IsDirty)
            DetailEditStatus = "";
        _shell.Project.Dirty = true;
        Status = $"Gespeichert: {BelegteHaltungen} Haltungen, Total {GesamtTotal:N2} CHF.";
        _sp.Dialogs.Info(
            $"Sanierungs-Matrix gespeichert.\n{BelegteHaltungen} Haltungen mit Massnahme, Total {GesamtTotal:N2} CHF (exkl. MwSt.).\n\nDas NPK-Leistungsverzeichnis exportierst du im Druckcenter.",
            "Sanierungs-Matrix");
    }
}
