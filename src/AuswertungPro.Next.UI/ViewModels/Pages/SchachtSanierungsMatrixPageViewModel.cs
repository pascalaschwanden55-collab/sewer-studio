using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Schacht-Matrix: pro Schacht (aus <see cref="Project.SchaechteData"/>) eine Sanierungs-Massnahme
/// + Zusatz-Haekchen waehlen; die Kosten landen im eigenen Store (schacht_costs.json) und fliessen
/// als NPK-Kapitel 700 ins projektweite Leistungsverzeichnis. Bewusst als eigene, schlanke Seite
/// analog zur Haltungs-Sanierungs-Matrix — kein Umbau der Haltungs-Kette (Wiederverwendung von
/// Katalog/Templates/Factory). Schaechte haben keine Auto-Laenge/DN, darum ist die Menge immer
/// manuell (Stueck/Stunden/Meter).
/// </summary>
public sealed partial class SchachtSanierungsMatrixPageViewModel : ObservableObject, IConfirmLeave
{
    // Waehlbare Schacht-Hauptarbeiten (Ids der Templates in measure_templates.json).
    private static readonly (string Id, string Kategorie)[] SchachtMeasures =
    {
        ("SCHACHT_PAUSCHAL",      "Pauschal"),
        ("SCHACHT_LINER",         "Renovierung"),
        ("SCHACHT_BANKETT",       "Renovierung"),
        ("SCHACHT_STEIGEISEN",    "Reparatur"),
        ("SCHACHT_RAHMEN_DECKEL", "Reparatur"),
        ("SCHACHT_FUGEN",         "Reparatur"),
        ("SCHACHT_REGIE",         "Regie"),
    };

    // Zusatzoptionen -> Katalog-ItemKey.
    private const string KeyReinigung = "SCHACHT_REINIGUNG";
    private const string KeyVd = "VORARBEIT_VD";
    private const string KeyWasser = "VORARBEIT_WASSERHALTUNG";
    private const string KeyDoku = "QK_DOKUMENTATION";

    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;
    private readonly CostCatalogStore _catalogStore = new();
    private readonly MeasureTemplateStore _templateStore = new();
    private readonly ProjectCostStoreRepository _costRepo = new("schacht_costs.json");

    private Dictionary<string, MeasureTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CostCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private ProjectCostStore _store = new();
    private string? _storeLoadError;
    private decimal _vatRate = CostCalculatorLogicService.DefaultVatRate;
    private string _projectPath = "";
    private bool _hasUnsavedChanges;
    private bool _suppressSelectionGuard;
    private readonly HashSet<string> _touchedSchaechte = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<SchachtMatrixRowVm> Rows { get; } = new();
    public ObservableCollection<MeasureOption> MeasureOptions { get; } = new();

    [ObservableProperty] private string _pageTitle = "Schacht-Matrix";
    [ObservableProperty] private string _pageSubtitle = "Pro Schacht eine Massnahme waehlen — Menge (Stk/Std/m) selbst eingeben.";
    [ObservableProperty] private decimal _gesamtTotal;
    [ObservableProperty] private decimal _maxRowTotal;
    [ObservableProperty] private int _belegteSchaechte;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private SchachtMatrixRowVm? _selectedRow;

    public SchachtSanierungsMatrixPageViewModel(ShellViewModel shell, ServiceProvider services)
    {
        _shell = shell;
        _sp = services;
        Reload();
    }

    partial void OnSelectedRowChanged(SchachtMatrixRowVm? value)
    {
        if (_suppressSelectionGuard)
            return;
        // Schacht in der QGIS-Bridge melden (Klick -> QGIS zoomt), wie bei Haltungen.
        QgisBridge.QgisBridgeSelection.SetSchacht(value?.Schachtnummer);
    }

    [RelayCommand]
    private void Reload()
    {
        if (_hasUnsavedChanges &&
            !_sp.Dialogs.Confirm("Nicht gespeicherte Aenderungen gehen beim Neuladen verloren.\nTrotzdem neu laden?", PageTitle))
        {
            Status = "Neu laden abgebrochen (offene Aenderungen).";
            return;
        }

        _suppressSelectionGuard = true;
        try { ReloadCore(); }
        finally { _suppressSelectionGuard = false; }
    }

    private void ReloadCore()
    {
        _hasUnsavedChanges = false;
        _touchedSchaechte.Clear();
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

        MeasureOptions.Clear();
        foreach (var o in MatrixMeasureOptionBuilder.Build(SchachtMeasures, _templates, _catalog))
            MeasureOptions.Add(o);

        _store = _costRepo.Load(_projectPath, out _storeLoadError);

        Rows.Clear();
        foreach (var record in _shell.Project.SchaechteData)
        {
            var nummer = record.GetFieldValue("Schachtnummer").Trim();
            if (string.IsNullOrWhiteSpace(nummer))
                continue;

            var funktion = record.GetFieldValue("Funktion").Trim();
            var resultat = record.GetFieldValue("Pruefungsresultat").Trim();
            var row = new SchachtMatrixRowVm(record, nummer, funktion, resultat, RecomputeRow);
            InitRowFromStore(row, nummer);
            Rows.Add(row);
        }

        RecomputeGesamt();
        SelectedRow = Rows.FirstOrDefault();

        Status = Rows.Count == 0
            ? "Keine Schaechte geladen (Projekt mit importierten Schacht-Protokollen oeffnen)."
            : $"{Rows.Count} Schaechte geladen.";

        if (_storeLoadError is not null)
        {
            Status = $"WARNUNG: {_storeLoadError} — Speichern ist gesperrt, bestehende Kosten bleiben unangetastet.";
            _sp.Dialogs.Warn(
                $"Schacht-Kostendaten konnten nicht geladen werden:\n{_storeLoadError}\n\nSpeichern ist gesperrt, damit schacht_costs.json nicht mit einem leeren Stand ueberschrieben wird.",
                "Schacht-Matrix");
        }
    }

    private void InitRowFromStore(SchachtMatrixRowVm row, string nummer)
    {
        if (!_store.ByHolding.TryGetValue(nummer, out var existing) || existing.Measures.Count == 0)
        {
            row.SetStoredCost(null);
            row.InitFrom(MeasureOptions[0], 0m, 0m, false, false, false, false);
            return;
        }

        row.SetStoredCost(existing);
        var firstId = existing.Measures[0].MeasureId;
        var opt = MeasureOptions.FirstOrDefault(o => string.Equals(o.Id, firstId, StringComparison.OrdinalIgnoreCase));
        if (opt is null)
        {
            // Gespeicherte Massnahme ist keine bekannte Schacht-Option (alte Daten) -> als Ad-hoc zeigen.
            var name = string.IsNullOrWhiteSpace(existing.Measures[0].MeasureName) ? firstId : existing.Measures[0].MeasureName;
            opt = new MeasureOption(firstId, name + " (gespeichert)", "Übrige", true, firstId ?? "");
            MeasureOptions.Add(opt);
            row.InitFrom(opt, existing.Total, 0m, false, false, false, false);
            return;
        }

        var lines = existing.Measures[0].Lines;
        bool Sel(string key) => lines.Any(l => l.Selected && string.Equals(l.ItemKey, key, StringComparison.OrdinalIgnoreCase));
        var hauptLine = lines.FirstOrDefault(l => string.Equals(l.ItemKey, opt.HauptItemKey, StringComparison.OrdinalIgnoreCase));
        var menge = hauptLine?.Qty ?? 0m;

        row.InitFrom(opt, existing.Total, menge, Sel(KeyReinigung), Sel(KeyVd), Sel(KeyWasser), Sel(KeyDoku));
    }

    private void RecomputeRow(SchachtMatrixRowVm row)
    {
        var measureId = row.SelectedMeasure?.Id;
        if (string.IsNullOrWhiteSpace(measureId))
        {
            if (_store.ByHolding.Remove(row.Schachtnummer))
            {
                _touchedSchaechte.Add(row.Schachtnummer);
                _hasUnsavedChanges = true;
            }
            row.SetStoredCost(null);
            row.Total = 0m;
            row.Hinweis = "";
            RecomputeGesamt();
            return;
        }

        var extras = new List<string>();
        if (row.OptReinigung) extras.Add(KeyReinigung);
        if (row.OptVd) extras.Add(KeyVd);
        if (row.OptWasserhaltung) extras.Add(KeyWasser);
        if (row.OptDokumentation) extras.Add(KeyDoku);

        // Schaechte: Menge immer manuell. > 0 uebersteuert die Hauptarbeit; sonst Template-Default (1).
        decimal hauptMenge = row.Menge > 0m ? row.Menge : 1m;
        var hauptKey = row.SelectedMeasure?.HauptItemKey;
        if (SchachtAbdeckungStkAutoFill.TryApplyForMeasure(row.Record, measureId, row.SelectedMeasure?.Name))
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        var cost = SchachtMeasureFactory.Build(row.Schachtnummer, measureId,
            _templates, _catalog, _vatRate, extras, hauptMenge, hauptKey);

        if (cost is null)
        {
            if (_store.ByHolding.Remove(row.Schachtnummer))
            {
                _touchedSchaechte.Add(row.Schachtnummer);
                _hasUnsavedChanges = true;
            }
            row.SetStoredCost(null);
            row.Hinweis = "Massnahme nicht gefunden";
            row.Total = 0m;
        }
        else
        {
            _store.ByHolding[row.Schachtnummer] = cost;
            _touchedSchaechte.Add(row.Schachtnummer);
            _hasUnsavedChanges = true;
            row.SetStoredCost(cost);
            row.Total = cost.Total;
            row.Hinweis = cost.Measures.Count > 0 && cost.Measures[0].Lines.Count(l => l.Selected) > 1
                ? $"{cost.Measures[0].Lines.Count(l => l.Selected)} Positionen"
                : "";
        }

        RecomputeGesamt();
    }

    private void RecomputeGesamt()
    {
        GesamtTotal = Rows.Sum(r => r.Total);
        MaxRowTotal = Rows.Count == 0 ? 0m : Rows.Max(r => r.Total);
        BelegteSchaechte = Rows.Count(r => r.SelectedMeasure?.Id is not null);
    }

    [RelayCommand]
    private void Speichern()
    {
        if (_storeLoadError is not null)
        {
            _sp.Dialogs.Error($"Speichern gesperrt: {_storeLoadError}", PageTitle);
            return;
        }
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            _sp.Dialogs.Warn("Kein Projektpfad — bitte zuerst ein Projekt speichern.", PageTitle);
            return;
        }

        // Frisch laden und nur die in dieser Sitzung geaenderten Schaechte uebernehmen (Last-Write-Wins vermeiden).
        var fresh = _costRepo.Load(_projectPath, out var freshError);
        if (freshError is not null)
        {
            _sp.Dialogs.Error($"Speichern gesperrt: schacht_costs.json konnte nicht frisch gelesen werden.\n{freshError}", PageTitle);
            return;
        }
        foreach (var nummer in _touchedSchaechte)
        {
            if (_store.ByHolding.TryGetValue(nummer, out var ownCost))
                fresh.ByHolding[nummer] = ownCost;
            else
                fresh.ByHolding.Remove(nummer);
        }
        _store = fresh;

        if (!_costRepo.Save(_projectPath, _store, out var error))
        {
            _sp.Dialogs.Error($"Speichern fehlgeschlagen: {error}", PageTitle);
            return;
        }

        _touchedSchaechte.Clear();
        _hasUnsavedChanges = false;
        Status = $"Schacht-Kosten gespeichert ({_store.ByHolding.Count} Schacht/Schaechte).";
        _sp.DashboardRefresh.NotifyCostsChanged();
    }

    public bool ConfirmLeave()
    {
        if (!_hasUnsavedChanges)
            return true;
        return _sp.Dialogs.Confirm(
            "Es gibt nicht gespeicherte Schacht-Kosten.\nSeite trotzdem verlassen (Aenderungen gehen verloren)?",
            PageTitle);
    }
}

/// <summary>Eine Zeile der Schacht-Matrix (ein Schacht + gewaehlte Massnahme).</summary>
public sealed partial class SchachtMatrixRowVm : ObservableObject
{
    private readonly Action<SchachtMatrixRowVm>? _onChanged;
    private bool _suppress;

    public SchachtRecord Record { get; }
    public string Schachtnummer { get; }
    public string Funktion { get; }
    public string Resultat { get; }
    public HoldingCost? StoredCost { get; private set; }

    [ObservableProperty] private MeasureOption? _selectedMeasure;
    [ObservableProperty] private decimal _menge = 1m;
    [ObservableProperty] private bool _optReinigung;
    [ObservableProperty] private bool _optVd;
    [ObservableProperty] private bool _optWasserhaltung;
    [ObservableProperty] private bool _optDokumentation;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _hinweis = "";

    public SchachtMatrixRowVm(SchachtRecord record, string schachtnummer, string funktion, string resultat,
        Action<SchachtMatrixRowVm>? onChanged)
    {
        Record = record;
        Schachtnummer = schachtnummer;
        Funktion = funktion;
        Resultat = resultat;
        _onChanged = onChanged;
    }

    /// <summary>Vorbelegen aus gespeicherten Kosten ohne Neuberechnung.</summary>
    public void InitFrom(MeasureOption? option, decimal total, decimal menge,
        bool reinigung, bool vd, bool wasser, bool doku)
    {
        _suppress = true;
        SelectedMeasure = option;
        Menge = menge > 0m ? menge : 1m;
        OptReinigung = reinigung;
        OptVd = vd;
        OptWasserhaltung = wasser;
        OptDokumentation = doku;
        Total = total;
        _suppress = false;
    }

    public void SetStoredCost(HoldingCost? cost) => StoredCost = cost;

    partial void OnSelectedMeasureChanged(MeasureOption? value)
    {
        if (!_suppress && SchachtAbdeckungStkAutoFill.IsRahmenDeckelMeasure(value?.Id, value?.Name) && Menge <= 0m)
            Menge = 1m;

        Recalc();
    }
    partial void OnMengeChanged(decimal value) => Recalc();
    partial void OnOptReinigungChanged(bool value) => Recalc();
    partial void OnOptVdChanged(bool value) => Recalc();
    partial void OnOptWasserhaltungChanged(bool value) => Recalc();
    partial void OnOptDokumentationChanged(bool value) => Recalc();

    private void Recalc()
    {
        if (!_suppress)
            _onChanged?.Invoke(this);
    }
}
