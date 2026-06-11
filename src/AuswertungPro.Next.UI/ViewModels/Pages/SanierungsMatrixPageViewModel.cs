using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.ViewModels.Windows;

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

public sealed record SanierungMatrixDetailLineVm(
    string Group,
    string Text,
    string Unit,
    decimal Qty,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SanierungMatrixDetailMeasureVm(
    string MeasureName,
    string MeasureId,
    decimal Total,
    IReadOnlyList<SanierungMatrixDetailLineVm> Lines);

public static class SanierungsMatrixMeasureSummaryFormatter
{
    public const string EmptySummary = "- keine -";

    public static string FormatSummary(HoldingCost? cost)
    {
        var names = MeasureNames(cost).ToList();
        return names.Count switch
        {
            0 => EmptySummary,
            1 => names[0],
            2 => $"{names[0]} + {names[1]}",
            _ => $"{names[0]} + {names[1]} + {names.Count - 2} weitere",
        };
    }

    public static IReadOnlyList<SanierungMatrixDetailMeasureVm> BuildDetailMeasures(HoldingCost? cost)
    {
        if (cost?.Measures is null || cost.Measures.Count == 0)
            return Array.Empty<SanierungMatrixDetailMeasureVm>();

        return cost.Measures
            .Select(m => new SanierungMatrixDetailMeasureVm(
                CleanMeasureName(m),
                m.MeasureId,
                m.Total,
                m.Lines
                    .Where(l => l.Selected)
                    .Select(l => new SanierungMatrixDetailLineVm(
                        l.Group,
                        l.Text,
                        l.Unit,
                        l.Qty,
                        l.UnitPrice,
                        l.Qty * l.UnitPrice))
                    .ToList()))
            .ToList();
    }

    private static IEnumerable<string> MeasureNames(HoldingCost? cost)
    {
        if (cost?.Measures is null)
            yield break;

        foreach (var measure in cost.Measures)
            yield return CleanMeasureName(measure);
    }

    private static string CleanMeasureName(MeasureCost measure)
    {
        if (!string.IsNullOrWhiteSpace(measure.MeasureName))
            return measure.MeasureName.Trim();

        if (!string.IsNullOrWhiteSpace(measure.MeasureId))
            return measure.MeasureId.Trim();

        return "Massnahme";
    }
}

public sealed partial class SanierungsMatrixDetailEditLineVm : ObservableObject
{
    private readonly Action _changed;

    public string Group { get; }
    public string ItemKey { get; }
    public string Text { get; }
    public string Unit { get; }
    public bool IsPriceOverridden { get; }
    public bool IsQtyOverridden { get; }

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
        Selected = line.Selected;
        TransferMarked = line.TransferMarked;
        Qty = line.Qty;
        UnitPrice = line.UnitPrice;
        IsPriceOverridden = line.IsPriceOverridden;
        IsQtyOverridden = line.IsQtyOverridden;
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
        };
    }

    partial void OnSelectedChanged(bool value) => NotifyChanged();
    partial void OnTransferMarkedChanged(bool value) => NotifyChanged();
    partial void OnQtyChanged(decimal value) => NotifyChanged();
    partial void OnUnitPriceChanged(decimal value) => NotifyChanged();

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
public sealed partial class SanierungsMatrixPageViewModel : ObservableObject
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
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly CostCatalogStore _catalogStore = new();
    private readonly MeasureTemplateStore _templateStore = new();
    private readonly ProjectCostStoreRepository _costRepo = new();

    private Dictionary<string, MeasureTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CostCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private ProjectCostStore _store = new();
    private decimal _vatRate = 0.081m;
    private string _projectPath = "";
    private SanierungMatrixRowVm? _detailRow;
    private SanierungsMatrixDetailEditSession? _detailSession;
    private bool _suppressSelectionGuard;

    // Haltungen, die in dieser Sitzung auf "keine" gesetzt wurden -> beim Speichern
    // muessen ihre Tabellenfelder (Kosten, Massnahmen, Mengen) geleert werden.
    private readonly HashSet<string> _clearedHoldings = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<SanierungMatrixRowVm> Rows { get; } = new();
    public ObservableCollection<MeasureOption> MeasureOptions { get; } = new();

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

    public SanierungsMatrixPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Reload();
    }

    [RelayCommand]
    private void Reload()
    {
        _projectPath = _sp.Settings.LastProjectPath ?? "";

        var catalog = _catalogStore.LoadMerged(_projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : 0.081m;
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

        _store = _costRepo.Load(_projectPath);

        Rows.Clear();
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
            Rows.Add(row);
        }

        RecomputeGesamt();
        SelectedRow = Rows.FirstOrDefault();
        Status = Rows.Count == 0
            ? "Keine Haltungen geladen (Projekt mit Haltungen oeffnen)."
            : $"{Rows.Count} Haltungen geladen.";
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
                _clearedHoldings.Add(row.Holding); // beim Speichern Tabellenfelder leeren
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
                _clearedHoldings.Add(row.Holding);
            row.SetStoredCost(null);
            row.Hinweis = "Massnahme nicht gefunden";
            row.Total = 0m;
        }
        else
        {
            _store.ByHolding[row.Holding] = cost;
            row.SetStoredCost(cost);
            row.Total = cost.Total;
            row.Hinweis = row.Anschluesse > 0 ? $"{row.Anschluesse} Anschluss(e)" : "";
        }

        RefreshSelectedDetailIfNeeded(row);
        RecomputeGesamt();
    }

    private void RecomputeGesamt()
    {
        GesamtTotal = Rows.Sum(r => r.Total);
        BelegteHaltungen = Rows.Count(r => r.SelectedMeasure?.Id is not null);
    }

    partial void OnSelectedRowChanged(SanierungMatrixRowVm? value)
    {
        if (_suppressSelectionGuard)
            return;

        if (_detailRow is not null && !ReferenceEquals(value, _detailRow) && IsDetailDirty)
        {
            var decision = _sp.Dialogs.ConfirmCancel(
                "Es gibt nicht uebernommene Aenderungen im Detailbereich.\n\nJa = uebernehmen, Nein = verwerfen, Abbrechen = auf der aktuellen Haltung bleiben.",
                "Sanierungs-Matrix");

            if (decision == DialogConfirm.Cancel)
            {
                _suppressSelectionGuard = true;
                SelectedRow = _detailRow;
                _suppressSelectionGuard = false;
                return;
            }

            if (decision == DialogConfirm.Yes)
                DetailUebernehmen();
            else
                DetailVerwerfen();
        }

        LoadDetailForRow(value);
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

        _detailRow.SetStoredCost(updated);
        _detailRow.Total = updated.Total;
        _detailRow.Hinweis = updated.Measures.Count > 1
            ? "Mehrfach-Massnahme: im Detail bearbeiten"
            : _detailRow.Anschluesse > 0 ? $"{_detailRow.Anschluesse} Anschluss(e)" : "";

        _detailSession.MarkClean();
        DetailSubtitle = _detailRow.MeasuresSummary;
        UpdateDetailStateFromSession();
        RecomputeGesamt();
        Status = $"Detail uebernommen: {_detailRow.Holding}, Total {_detailRow.Total:N2} CHF.";
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
    /// Öffnet den (einen) Preis-Katalog. Nach dem Schliessen werden die geänderten
    /// Preise sofort auf alle Zeilen mit Massnahme angewendet (Totals neu gerechnet).
    /// </summary>
    [RelayCommand]
    private void KatalogBearbeiten()
    {
        var dialog = new CostCatalogEditorDialog(string.IsNullOrWhiteSpace(_projectPath) ? null : _projectPath);
        dialog.ShowDialog();
        ReloadCatalogAndApplyPrices();
    }

    private void ReloadCatalogAndApplyPrices()
    {
        var catalog = _catalogStore.LoadMerged(_projectPath);
        _vatRate = catalog.VatRate > 0m ? catalog.VatRate : 0.081m;
        _catalog = catalog.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .GroupBy(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var protectedRows = 0;
        foreach (var row in Rows.Where(r => r.SelectedMeasure?.Id is not null))
        {
            if (row.HasMultipleStoredMeasures)
            {
                protectedRows++;
                continue;
            }

            RecomputeRow(row);
        }

        RecomputeGesamt();
        Status = protectedRows == 0
            ? "Preise aus Katalog angewendet."
            : $"Preise angewendet; {protectedRows} Mehrfach-Massnahme(n) geschuetzt.";
    }

    [RelayCommand]
    private void Speichern()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            _sp.Dialogs.Info("Projekt bitte zuerst speichern, um Kosten abzulegen.", "Sanierungs-Matrix");
            return;
        }

        foreach (var row in Rows)
        {
            if (_store.ByHolding.TryGetValue(row.Holding, out var cost))
                DataPageSanierungCostMapper.ApplyCosts(row.Record, cost);
            else if (_clearedHoldings.Contains(row.Holding))
                // Massnahme wurde auf "keine" gesetzt -> alte Kosten/Massnahmen-Felder echt leeren.
                DataPageSanierungCostMapper.ClearCosts(row.Record);
        }
        _clearedHoldings.Clear();

        if (!_costRepo.Save(_projectPath, _store, out var error))
        {
            _sp.Dialogs.Error($"Speichern fehlgeschlagen: {error}", "Sanierungs-Matrix");
            return;
        }

        _shell.Project.Dirty = true;
        Status = $"Gespeichert: {BelegteHaltungen} Haltungen, Total {GesamtTotal:N2} CHF.";
        _sp.Dialogs.Info(
            $"Sanierungs-Matrix gespeichert.\n{BelegteHaltungen} Haltungen mit Massnahme, Total {GesamtTotal:N2} CHF (exkl. MwSt.).\n\nDas NPK-Leistungsverzeichnis exportierst du im Druckcenter.",
            "Sanierungs-Matrix");
    }
}
