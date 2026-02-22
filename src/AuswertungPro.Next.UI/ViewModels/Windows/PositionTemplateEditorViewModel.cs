using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class PositionTemplateEditorViewModel : ObservableObject
{
    private readonly PositionTemplateStore _store = new();
    private readonly CostCatalogStore _catalogStore = new();
    private readonly string? _projectPath;
    private readonly Window _window;
    private readonly PositionTemplateCatalog _originalCatalog;

    [ObservableProperty] private PositionGroup? _selectedGroup;
    [ObservableProperty] private PositionTemplate? _selectedPosition;
    [ObservableProperty] private PositionTemplate? _selectedStoragePosition;

    public ObservableCollection<PositionGroup> Groups { get; }
    public ObservableCollection<PositionTemplate> StorageBox { get; } = new();
    public List<CatalogItemViewModel> AvailableItems { get; }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ResetToDefaultCommand { get; }
    public IRelayCommand AddGroupCommand { get; }
    public IRelayCommand RemoveGroupCommand { get; }
    public IRelayCommand AddPositionCommand { get; }
    public IRelayCommand RemovePositionCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand MoveToStorageCommand { get; }
    public IRelayCommand RestoreFromStorageCommand { get; }

    public PositionTemplateEditorViewModel(string? projectPath, Window window)
    {
        _projectPath = projectPath;
        _window = window;

        // Load data
        _originalCatalog = _store.LoadMerged(projectPath);
        var costCatalog = _catalogStore.LoadMerged(projectPath);

        // Setup available items for ComboBox
        AvailableItems = costCatalog.Items
            .OrderBy(item => item.Name)
            .Select(item => new CatalogItemViewModel
            {
                Key = item.Key,
                DisplayName = $"{item.Name} ({item.Unit})"
            })
            .ToList();

        // Setup commands FIRST before setting SelectedGroup
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        AddGroupCommand = new RelayCommand(AddGroup);
        RemoveGroupCommand = new RelayCommand(RemoveGroup, () => SelectedGroup is not null);
        AddPositionCommand = new RelayCommand(AddPosition, () => SelectedGroup is not null && AvailableItems.Count > 0);
        RemovePositionCommand = new RelayCommand(RemovePosition, () => SelectedPosition is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        MoveToStorageCommand = new RelayCommand(MoveToStorage, () => SelectedPosition is not null);
        RestoreFromStorageCommand = new RelayCommand(RestoreFromStorage, () => SelectedStoragePosition is not null);

        // Setup groups (deep copy for editing)
        Groups = new ObservableCollection<PositionGroup>(
            _originalCatalog.Groups.Select(g => new PositionGroup
            {
                Name = g.Name,
                Positions = new List<PositionTemplate>(g.Positions.Select(p => new PositionTemplate
                {
                    ItemKey = p.ItemKey,
                    Enabled = p.Enabled,
                    DefaultQty = p.DefaultQty,
                    Name = p.Name,
                    Unit = p.Unit,
                    Price = p.Price,
                    IsCustom = p.IsCustom
                }))
            }));

        // Select first group by default - NOW commands are initialized
        if (Groups.Count > 0)
            SelectedGroup = Groups[0];
    }

    partial void OnSelectedGroupChanged(PositionGroup? value)
    {
        SelectedPosition = null;
        AddPositionCommand.NotifyCanExecuteChanged();
        RemoveGroupCommand.NotifyCanExecuteChanged();
    }

    // Falls sich AvailableItems ändern könnten, müsste hier ggf. auch AddPositionCommand.NotifyCanExecuteChanged() aufgerufen werden.

    partial void OnSelectedPositionChanged(PositionTemplate? value)
    {
        RemovePositionCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        MoveToStorageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedStoragePositionChanged(PositionTemplate? value)
    {
        RestoreFromStorageCommand.NotifyCanExecuteChanged();
    }

    private void Save()
    {
        var catalog = new PositionTemplateCatalog
        {
            Version = _originalCatalog.Version,
            Groups = Groups.ToList()
        };

        if (!_store.SaveUserOverride(catalog, out var error))
        {
            MessageBox.Show($"Fehler beim Speichern: {error}", "Fehler", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _window.DialogResult = true;
        _window.Close();
    }

    private void Cancel()
    {
        _window.DialogResult = false;
        _window.Close();
    }

    private void ResetToDefault()
    {
        var result = MessageBox.Show(
            "Möchten Sie wirklich alle Änderungen verwerfen und die Standard-Einstellungen wiederherstellen?",
            "Standard wiederherstellen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var defaultCatalog = _store.Load(_projectPath);
            Groups.Clear();
            foreach (var group in defaultCatalog.Groups)
            {
                Groups.Add(new PositionGroup
                {
                    Name = group.Name,
                    Positions = new List<PositionTemplate>(group.Positions.Select(p => new PositionTemplate
                    {
                        ItemKey = p.ItemKey,
                        Enabled = p.Enabled,
                        DefaultQty = p.DefaultQty,
                        Name = p.Name,
                        Unit = p.Unit,
                        Price = p.Price,
                        IsCustom = p.IsCustom
                    }))
                });
            }

            SelectedGroup = Groups.FirstOrDefault();
        }
    }

    private void AddGroup()
    {
        var groupName = Microsoft.VisualBasic.Interaction.InputBox(
            "Name der neuen Massnahmen-Gruppe:",
            "Neue Gruppe erstellen",
            "Neue Massnahme");

        if (string.IsNullOrWhiteSpace(groupName))
            return;

        var newGroup = new PositionGroup
        {
            Name = groupName.Trim(),
            Positions = new List<PositionTemplate>()
        };

        Groups.Add(newGroup);
        SelectedGroup = newGroup;
    }

    private void RemoveGroup()
    {
        if (SelectedGroup is null) return;

        var result = MessageBox.Show(
            $"Möchten Sie die Gruppe '{SelectedGroup.Name}' wirklich löschen?",
            "Gruppe löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var index = Groups.IndexOf(SelectedGroup);
            Groups.Remove(SelectedGroup);

            if (Groups.Count > 0)
            {
                var newIndex = Math.Min(index, Groups.Count - 1);
                SelectedGroup = Groups[newIndex];
            }
            else
            {
                // Automatisch neue leere Gruppe erstellen
                var newGroup = new PositionGroup
                {
                    Name = "Neue Gruppe",
                    Positions = new List<PositionTemplate>()
                };
                Groups.Add(newGroup);
                SelectedGroup = newGroup;
            }
        }
    }

    private void AddPosition()
    {
        if (SelectedGroup is null) return;

        var newPosition = new PositionTemplate
        {
            Enabled = true,
            DefaultQty = 1,
            Name = "Neue Position",
            Unit = "Stk",
            Price = 0,
            IsCustom = true
        };

        SelectedGroup.Positions.Add(newPosition);
        SelectedPosition = newPosition;
    }

    private void RemovePosition()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        SelectedGroup.Positions.Remove(SelectedPosition);

        // Select next position or previous if this was the last
        if (SelectedGroup.Positions.Count > 0)
        {
            var newIndex = Math.Min(index, SelectedGroup.Positions.Count - 1);
            SelectedPosition = SelectedGroup.Positions[newIndex];
        }
        else
        {
            SelectedPosition = null;
        }
    }

    private void MoveUp()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        if (index > 0)
        {
            var temp = SelectedGroup.Positions[index];
            SelectedGroup.Positions[index] = SelectedGroup.Positions[index - 1];
            SelectedGroup.Positions[index - 1] = temp;
            
            // Refresh commands
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
    }

    private void MoveDown()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        if (index < SelectedGroup.Positions.Count - 1)
        {
            var temp = SelectedGroup.Positions[index];
            SelectedGroup.Positions[index] = SelectedGroup.Positions[index + 1];
            SelectedGroup.Positions[index + 1] = temp;
            
            // Refresh commands
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanMoveUp()
    {
        if (SelectedPosition is null || SelectedGroup is null) return false;
        return SelectedGroup.Positions.IndexOf(SelectedPosition) > 0;
    }

    private bool CanMoveDown()
    {
        if (SelectedPosition is null || SelectedGroup is null) return false;
        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        return index >= 0 && index < SelectedGroup.Positions.Count - 1;
    }

    private void MoveToStorage()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        // Kopiere Position in Wartebox
        var positionCopy = new PositionTemplate
        {
            ItemKey = SelectedPosition.ItemKey,
            Enabled = SelectedPosition.Enabled,
            DefaultQty = SelectedPosition.DefaultQty,
            Name = SelectedPosition.Name,
            Unit = SelectedPosition.Unit,
            Price = SelectedPosition.Price,
            IsCustom = SelectedPosition.IsCustom
        };

        StorageBox.Add(positionCopy);

        // Entferne aus Gruppe
        SelectedGroup.Positions.Remove(SelectedPosition);
        SelectedPosition = null;
    }

    private void RestoreFromStorage()
    {
        if (SelectedStoragePosition is null || SelectedGroup is null) return;

        // Kopiere Position zurück zur Gruppe
        var positionCopy = new PositionTemplate
        {
            ItemKey = SelectedStoragePosition.ItemKey,
            Enabled = SelectedStoragePosition.Enabled,
            DefaultQty = SelectedStoragePosition.DefaultQty,
            Name = SelectedStoragePosition.Name,
            Unit = SelectedStoragePosition.Unit,
            Price = SelectedStoragePosition.Price,
            IsCustom = SelectedStoragePosition.IsCustom
        };

        SelectedGroup.Positions.Add(positionCopy);

        // Entferne aus Wartebox
        StorageBox.Remove(SelectedStoragePosition);
        SelectedStoragePosition = null;
    }
}

public sealed class CatalogItemViewModel
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
}