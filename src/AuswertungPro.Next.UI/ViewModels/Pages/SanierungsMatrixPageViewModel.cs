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

/// <summary>Eine Auswahl-Option (Hauptarbeit) fuer die Massnahmen-Spalte. Id=null = keine.</summary>
public sealed record MeasureOption(string? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine Haltungs-Zeile in der Sanierungs-Matrix.</summary>
public sealed partial class SanierungMatrixRowVm : ObservableObject
{
    private readonly Action<SanierungMatrixRowVm>? _onMeasureChanged;
    private bool _suppress;

    public HaltungRecord Record { get; }
    public string Holding { get; }
    public string Dn { get; }
    public string Laenge { get; }
    public int Anschluesse { get; }

    [ObservableProperty] private MeasureOption? _selectedMeasure;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _hinweis = "";

    public SanierungMatrixRowVm(HaltungRecord record, string holding, string dn, string laenge,
        int anschluesse, Action<SanierungMatrixRowVm>? onMeasureChanged)
    {
        Record = record;
        Holding = holding;
        Dn = dn;
        Laenge = laenge;
        Anschluesse = anschluesse;
        _onMeasureChanged = onMeasureChanged;
    }

    /// <summary>Vorbelegen ohne Neuberechnung (beim Laden aus gespeicherten Kosten).</summary>
    public void InitSelection(MeasureOption? option, decimal total)
    {
        _suppress = true;
        SelectedMeasure = option;
        Total = total;
        _suppress = false;
    }

    partial void OnSelectedMeasureChanged(MeasureOption? value)
    {
        if (!_suppress)
            _onMeasureChanged?.Invoke(this);
    }
}

/// <summary>
/// Massen-Ansicht: alle Haltungen einer Zone als Tabelle, pro Zeile EINE Hauptarbeit
/// anklickbar. Mengen (DN, Laenge, Anschluesse-Dedup) kommen automatisch; beim Waehlen
/// baut <see cref="HoldingMeasureFactory"/> dasselbe Buendel wie das Einzelfenster und
/// rechnet den Haltungs-Total. Speichern legt alles in costs.json ab und aktualisiert
/// die Tabellenfelder. Das NPK-Leistungsverzeichnis wird im Druckcenter exportiert.
/// </summary>
public sealed partial class SanierungsMatrixPageViewModel : ObservableObject
{
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

        MeasureOptions.Clear();
        MeasureOptions.Add(new MeasureOption(null, "— keine —"));
        foreach (var m in _templates.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            MeasureOptions.Add(new MeasureOption(m.Id, string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name));

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

            var row = new SanierungMatrixRowVm(record, holding, dn, laenge, anschluesse, OnRowMeasureChanged);

            if (_store.ByHolding.TryGetValue(holding, out var existing) && existing.Measures.Count > 0)
            {
                var firstId = existing.Measures[0].MeasureId;
                var opt = MeasureOptions.FirstOrDefault(o => string.Equals(o.Id, firstId, StringComparison.OrdinalIgnoreCase))
                          ?? MeasureOptions[0];
                row.InitSelection(opt, existing.Total);
            }
            else
            {
                row.InitSelection(MeasureOptions[0], 0m);
            }

            Rows.Add(row);
        }

        RecomputeGesamt();
        Status = Rows.Count == 0
            ? "Keine Haltungen geladen (Projekt mit Haltungen oeffnen)."
            : $"{Rows.Count} Haltungen geladen.";
    }

    private void OnRowMeasureChanged(SanierungMatrixRowVm row)
    {
        var measureId = row.SelectedMeasure?.Id;
        if (string.IsNullOrWhiteSpace(measureId))
        {
            _store.ByHolding.Remove(row.Holding);
            row.Total = 0m;
            row.Hinweis = "";
        }
        else
        {
            var cost = HoldingMeasureFactory.Build(row.Holding, row.Record, measureId, _templates, _catalog, _vatRate);
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

        // Tabellenfelder pro Haltung aktualisieren (Kosten, Mengen, Massnahmen-Text).
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
