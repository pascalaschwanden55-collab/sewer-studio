using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CostCatalogEditorViewModel : ObservableObject
{
    private readonly CostCatalogStore _store = new();
    private readonly CostCatalog _catalog;
    private readonly string? _projectPath;
    private readonly Window _window;
    private readonly IDialogService _dialogs;

    public ObservableCollection<CostCatalogItem> Items { get; }

    [ObservableProperty] private CostCatalogItem? _selectedItem;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CloseCommand { get; }

    public CostCatalogEditorViewModel(string? projectPath, Window window, IDialogService? dialogs = null)
    {
        _projectPath = projectPath;
        _window = window;
        _dialogs = dialogs ?? new DialogService();

        _catalog = _store.LoadMerged(projectPath);
        Items = new ObservableCollection<CostCatalogItem>(_catalog.Items);

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => SelectedItem is not null);
        SaveCommand = new RelayCommand(Save);
        CloseCommand = new RelayCommand(Close);
    }

    partial void OnSelectedItemChanged(CostCatalogItem? value)
    {
        RemoveCommand.NotifyCanExecuteChanged();
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

        if (!_store.SaveUserOverrides(_catalog, out var error))
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
}
