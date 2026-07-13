using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CostCatalogEditorViewModel : ObservableObject
{
    private readonly CostCatalogStore _store;
    private readonly CostCatalog _catalog;
    private readonly string? _projectPath;
    private readonly Window _window;
    private readonly IDialogService _dialogs;

    public ObservableCollection<CostCatalogItem> Items { get; }

    [ObservableProperty] private CostCatalogItem? _selectedItem;

    // DN-Preise der aktuell gewaehlten ByDN-Position (Liner/Manschette). Spiegelt SelectedItem.DnPrices;
    // dieselben DnPrice-Objekte, damit direkte Zell-Aenderungen im Grid ins Modell durchschlagen.
    public ObservableCollection<DnPrice> SelectedDnPrices { get; } = new();
    [ObservableProperty] private DnPrice? _selectedDnPrice;

    /// <summary>true, wenn die gewaehlte Position nach Durchmesser bepreist wird (Typ "ByDN").</summary>
    public bool SelectedIsByDn => string.Equals(SelectedItem?.Type, "ByDN", StringComparison.OrdinalIgnoreCase);
    public Visibility DnEditorVisibility => SelectedIsByDn ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DnHintVisibility => SelectedIsByDn ? Visibility.Collapsed : Visibility.Visible;
    public string SelectedDnHeader => SelectedItem is null
        ? "Preise nach Durchmesser"
        : $"Preise nach Durchmesser — {SelectedItem.Name} ({SelectedItem.Unit})";

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand AddDnCommand { get; }
    public IRelayCommand RemoveDnCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CloseCommand { get; }

    public CostCatalogEditorViewModel(
        string? projectPath,
        Window window,
        IDialogService? dialogs = null,
        CostCatalogStore? store = null)
    {
        _projectPath = projectPath;
        _window = window;
        _dialogs = dialogs ?? new DialogService();
        _store = store ?? new CostCatalogStore();

        _catalog = _store.LoadMerged(projectPath);
        Items = new ObservableCollection<CostCatalogItem>(_catalog.Items);
        WarnDuplicateNpkCodes();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => SelectedItem is not null);
        AddDnCommand = new RelayCommand(AddDn, () => SelectedIsByDn);
        RemoveDnCommand = new RelayCommand(RemoveDn, () => SelectedDnPrice is not null);
        SaveCommand = new RelayCommand(Save);
        CloseCommand = new RelayCommand(Close);
    }

    partial void OnSelectedItemChanged(CostCatalogItem? value)
    {
        RemoveCommand.NotifyCanExecuteChanged();
        RebuildSelectedDnPrices();
        NotifyDnState();
    }

    partial void OnSelectedDnPriceChanged(DnPrice? value)
    {
        RemoveDnCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Wird vom Dialog aufgerufen, wenn im Grid die Typ-Spalte geaendert wurde
    /// (Fixed &lt;-&gt; ByDN), damit das DN-Panel sofort ein-/ausblendet.</summary>
    public void NotifyTypeChanged() => NotifyDnState();

    private void NotifyDnState()
    {
        OnPropertyChanged(nameof(SelectedIsByDn));
        OnPropertyChanged(nameof(DnEditorVisibility));
        OnPropertyChanged(nameof(DnHintVisibility));
        OnPropertyChanged(nameof(SelectedDnHeader));
        AddDnCommand.NotifyCanExecuteChanged();
    }

    private void RebuildSelectedDnPrices()
    {
        SelectedDnPrices.Clear();
        SelectedDnPrice = null;
        if (SelectedItem?.DnPrices is { } list)
            foreach (var p in list)
                SelectedDnPrices.Add(p);
    }

    private void AddDn()
    {
        if (SelectedItem is null || !SelectedIsByDn)
            return;

        SelectedItem.DnPrices ??= new System.Collections.Generic.List<DnPrice>();
        var row = CostCatalogDnPriceEditor.CreateNextRow(SelectedItem.DnPrices);
        SelectedItem.DnPrices.Add(row);   // Modell
        SelectedDnPrices.Add(row);        // Anzeige (gleiches Objekt)
        SelectedDnPrice = row;
    }

    private void RemoveDn()
    {
        if (SelectedItem is null || SelectedDnPrice is null)
            return;

        SelectedItem.DnPrices?.Remove(SelectedDnPrice);
        SelectedDnPrices.Remove(SelectedDnPrice);
        SelectedDnPrice = null;
    }

    private void Add()
    {
        var newItem = new CostCatalogItem
        {
            Key = CreateNewKey(),
            Name = "Neue Position",
            Unit = "St",
            Type = "Fixed",
            Price = 0m,
            Active = true
        };

        Items.Add(newItem);
        SelectedItem = newItem;
    }

    private void Remove()
    {
        if (SelectedItem is null)
            return;

        var label = string.IsNullOrWhiteSpace(SelectedItem.Name) ? SelectedItem.Key : SelectedItem.Name;
        var confirmed = _dialogs.Confirm($"Position '{label}' wirklich löschen?", "Position löschen");

        if (!confirmed)
            return;

        Items.Remove(SelectedItem);
        SelectedItem = null;
    }

    private void Save()
    {
        _catalog.Items = Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .Select(i => i)
            .ToList();
        WarnDuplicateNpkCodes();

        // Audit W18: projectPath mitgeben, damit unveraenderte NPK-Metadaten nicht im
        // Override eingefroren werden (Default-Korrekturen sollen weiter durchschlagen).
        if (!_store.SaveUserOverrides(_catalog, _projectPath, out var error))
        {
            _dialogs.Error($"Speichern fehlgeschlagen: {error}", "Positionen");
            return;
        }

        _window.DialogResult = true;
        _window.Close();
    }

    private void Close()
    {
        _window.DialogResult = false;
        _window.Close();
    }

    private string CreateNewKey()
    {
        var index = Items.Count + 1;
        while (true)
        {
            var candidate = $"POS_NEU_{index}";
            if (Items.All(i => !string.Equals(i.Key, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }

    private void WarnDuplicateNpkCodes()
    {
        var warnings = CostCatalogStore.FindDuplicateNpkCodesWithDifferentUnits(_catalog);
        if (warnings.Count == 0)
            return;

        var lines = warnings
            .Select(w => $"{w.NpkCode}: Einheiten {string.Join(", ", w.Units)} ({string.Join(", ", w.ItemKeys)})");
        _dialogs.Warn(
            "Der Katalog enthaelt gleiche NPK-Nummern mit unterschiedlichen Einheiten:\n\n" +
            string.Join("\n", lines) +
            "\n\nBitte fachlich pruefen; Speichern wird nicht blockiert.",
            "NPK-Katalog");
    }
}
