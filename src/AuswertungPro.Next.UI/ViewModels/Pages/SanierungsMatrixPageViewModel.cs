using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Eine waehlbare Hauptarbeit (Id=null = keine). Kategorie = Renovierung/Reparatur.</summary>
public sealed record MeasureOption(string? Id, string Name, string Kategorie, bool IsStk)
{
    public override string ToString() => Name;
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
        IsMengeEditierbar = option?.IsStk == true;
        Menge = menge;
        OptVerkehrsdienst = vd;
        OptWasserhaltung = wasser;
        OptFraesen = fraesen;
        OptDichtheit = dichtheit;
        OptDokumentation = doku;
        Total = total;
        _suppress = false;
    }

    partial void OnSelectedMeasureChanged(MeasureOption? value)
    {
        if (_suppress)
            return;

        // Menge passend vorbelegen: Stk-Hauptarbeit -> 1 (editierbar), m-Hauptarbeit -> Laenge.
        _suppress = true;
        IsMengeEditierbar = value?.IsStk == true;
        if (value?.IsStk == true)
        {
            if (Menge <= 0m) Menge = 1m;
        }
        else
        {
            Menge = decimal.TryParse(Laenge, out var l) ? l : 0m;
        }
        _suppress = false;

        _onChanged?.Invoke(this);
    }

    partial void OnIsMengeEditierbarChanged(bool value) => OnPropertyChanged(nameof(IsMengeReadOnly));
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
/// (gruppiert Renovierung/Reparatur) plus ankreuzbare Zusatzoptionen (Verkehrsdienst,
/// Wasserhaltung, Fraesen, Dichtheitspruefung, Dokumentation). Mengen (DN, Laenge,
/// Anschluesse-Dedup) kommen automatisch; bei Stk-Reparaturen gibt der Anwender die
/// Stueckzahl selbst ein. Speichern legt alles in costs.json ab; das aggregierte
/// NPK-Leistungsverzeichnis wird im Druckcenter exportiert.
/// </summary>
public sealed partial class SanierungsMatrixPageViewModel : ObservableObject
{
    // Hauptarbeiten, die in der Matrix waehlbar sind (Renovierung + Reparatur).
    private static readonly string[] MatrixMeasureIds =
    {
        "SCHLAUCHLINER_NADELFILZ", "SCHLAUCHLINER_NADELFILZ_OPENEND", "SCHLAUCHLINER_GFK",
        "KURZLINER_PARTLINER", "MANSCHETTE_EDELSTAHL"
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

    public ObservableCollection<SanierungMatrixRowVm> Rows { get; } = new();
    public ObservableCollection<MeasureOption> MeasureOptions { get; } = new();

    [ObservableProperty] private decimal _gesamtTotal;
    [ObservableProperty] private int _belegteHaltungen;
    [ObservableProperty] private string _status = "";

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
        Status = Rows.Count == 0
            ? "Keine Haltungen geladen (Projekt mit Haltungen oeffnen)."
            : $"{Rows.Count} Haltungen geladen.";
    }

    private void BuildMeasureOptions()
    {
        MeasureOptions.Clear();
        MeasureOptions.Add(new MeasureOption(null, "— keine —", "", false));

        var options = new List<MeasureOption>();
        foreach (var id in MatrixMeasureIds)
        {
            if (!_templates.TryGetValue(id, out var tpl))
                continue;

            _catalog.TryGetValue(id, out var item);
            var chapter = item?.Chapter ?? "";
            var kategorie = chapter == "600" ? "Renovierung" : chapter == "500" ? "Reparatur" : "Weitere";
            var isStk = string.Equals(item?.Unit, "Stk", StringComparison.OrdinalIgnoreCase);
            var baseName = string.IsNullOrWhiteSpace(tpl.Name) ? id : tpl.Name;
            options.Add(new MeasureOption(id, $"{kategorie} · {baseName}", kategorie, isStk));
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
            row.InitFrom(MeasureOptions[0], 0m, 0m, false, false, false, false, false);
            return;
        }

        var firstId = existing.Measures[0].MeasureId;
        var opt = MeasureOptions.FirstOrDefault(o => string.Equals(o.Id, firstId, StringComparison.OrdinalIgnoreCase));
        if (opt is null)
        {
            // Gespeicherte Massnahme ist keine Matrix-Hauptarbeit -> nur Total zeigen.
            row.InitFrom(MeasureOptions[0], existing.Total, 0m, false, false, false, false, false);
            return;
        }

        var lines = existing.Measures[0].Lines;
        bool Sel(string key) => lines.Any(l => l.Selected &&
            string.Equals(l.ItemKey, key, StringComparison.OrdinalIgnoreCase));
        var hauptLine = lines.FirstOrDefault(l => string.Equals(l.ItemKey, firstId, StringComparison.OrdinalIgnoreCase));
        var menge = hauptLine?.Qty ?? 0m;

        row.InitFrom(opt, existing.Total, menge,
            Sel(KeyVd), Sel(KeyWasser), Sel(KeyFraesen), Sel(KeyDichtheit), Sel(KeyDoku));
    }

    private void RecomputeRow(SanierungMatrixRowVm row)
    {
        var measureId = row.SelectedMeasure?.Id;
        if (string.IsNullOrWhiteSpace(measureId))
        {
            _store.ByHolding.Remove(row.Holding);
            row.Total = 0m;
            row.Hinweis = "";
            RecomputeGesamt();
            return;
        }

        var extras = new List<string>();
        if (row.OptVerkehrsdienst) extras.Add(KeyVd);
        if (row.OptWasserhaltung) extras.Add(KeyWasser);
        if (row.OptFraesen) extras.Add(KeyFraesen);
        if (row.OptDichtheit) extras.Add(KeyDichtheit);
        if (row.OptDokumentation) extras.Add(KeyDoku);

        // Bei Stk-Hauptarbeit (Reparatur) die manuell eingegebene Menge uebersteuern.
        decimal? hauptMenge = row.SelectedMeasure?.IsStk == true && row.Menge > 0m ? row.Menge : null;

        var cost = HoldingMeasureFactory.Build(row.Holding, row.Record, measureId,
            _templates, _catalog, _vatRate, extras, hauptMenge);

        if (cost is null)
        {
            row.Hinweis = "Massnahme nicht gefunden";
            row.Total = 0m;
        }
        else
        {
            _store.ByHolding[row.Holding] = cost;
            row.Total = cost.Total;
            row.Hinweis = row.Anschluesse > 0 ? $"{row.Anschluesse} Anschluss(e)" : "";
        }

        RecomputeGesamt();
    }

    private void RecomputeGesamt()
    {
        GesamtTotal = Rows.Sum(r => r.Total);
        BelegteHaltungen = Rows.Count(r => r.SelectedMeasure?.Id is not null);
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
        }

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
