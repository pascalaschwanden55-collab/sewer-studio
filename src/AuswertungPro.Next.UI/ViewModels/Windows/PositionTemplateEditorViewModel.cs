using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class PositionTemplateEditorViewModel : ObservableObject
{
    private readonly IPositionTemplateStore _store;
    private readonly ICostCatalogStore _catalogStore;
    private readonly string? _projectPath;
    private readonly Window _window;
    private readonly IDialogService _dialogs;
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

    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen die Vorlagen-Speicher injizieren.")]
    public PositionTemplateEditorViewModel(
        string? projectPath,
        Window window,
        IDialogService? dialogs = null,
        PositionTemplateStore? store = null,
        CostCatalogStore? catalogStore = null)
        : this(
            projectPath,
            window,
            store ?? CostStoreCompatibility.Factory.CreatePositionTemplateStore(),
            catalogStore ?? CostStoreCompatibility.Factory.CreateCostCatalogStore(),
            dialogs)
    {
    }

    public PositionTemplateEditorViewModel(
        string? projectPath,
        Window window,
        IPositionTemplateStore store,
        ICostCatalogStore catalogStore,
        IDialogService? dialogs = null)
    {
        _projectPath = projectPath;
        _window = window;
        _dialogs = dialogs ?? new DialogService();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));

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

        // Setup groups (Tief-Kopie fuer Bearbeitung via PositionTemplateCopier)
        Groups = new ObservableCollection<PositionGroup>(
            PositionTemplateCopier.DeepCopyAll(_originalCatalog.Groups));

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
            _dialogs.Error($"Fehler beim Speichern: {error}", "Fehler");
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
        var confirmed = _dialogs.Confirm(
            "Möchten Sie wirklich alle Änderungen verwerfen und die Standard-Einstellungen wiederherstellen?",
            "Standard wiederherstellen");

        if (confirmed)
        {
            var defaultCatalog = _store.Load(_projectPath);
            Groups.Clear();
            foreach (var group in PositionTemplateCopier.DeepCopyAll(defaultCatalog.Groups))
                Groups.Add(group);

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

        var confirmed = _dialogs.Confirm(
            $"Möchten Sie die Gruppe '{SelectedGroup.Name}' wirklich löschen?",
            "Gruppe löschen");

        if (confirmed)
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

        // Standard-Position ueber PositionListEditor anlegen
        var newPosition = PositionListEditor.CreateDefault();
        SelectedGroup.Positions.Add(newPosition);
        SelectedPosition = newPosition;
    }

    private void RemovePosition()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        // Entfernen und Folge-Index via PositionListEditor berechnen
        var nextIndex = PositionListEditor.RemoveAndGetNextIndex(SelectedGroup.Positions, index);
        SelectedPosition = nextIndex >= 0 ? SelectedGroup.Positions[nextIndex] : null;
    }

    private void MoveUp()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        if (PositionListEditor.MoveUp(SelectedGroup.Positions, index))
        {
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
    }

    private void MoveDown()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        if (PositionListEditor.MoveDown(SelectedGroup.Positions, index))
        {
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanMoveUp()
    {
        if (SelectedPosition is null || SelectedGroup is null) return false;
        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        return PositionListEditor.CanMoveUp(SelectedGroup.Positions, index);
    }

    private bool CanMoveDown()
    {
        if (SelectedPosition is null || SelectedGroup is null) return false;
        var index = SelectedGroup.Positions.IndexOf(SelectedPosition);
        return PositionListEditor.CanMoveDown(SelectedGroup.Positions, index);
    }

    private void MoveToStorage()
    {
        if (SelectedPosition is null || SelectedGroup is null) return;

        // Kopiere Position in Wartebox (Tief-Kopie via PositionTemplateCopier)
        StorageBox.Add(PositionTemplateCopier.DeepCopy(SelectedPosition));

        // Entferne aus Gruppe
        SelectedGroup.Positions.Remove(SelectedPosition);
        SelectedPosition = null;
    }

    private void RestoreFromStorage()
    {
        if (SelectedStoragePosition is null || SelectedGroup is null) return;

        // Kopiere Position zurueck zur Gruppe (Tief-Kopie via PositionTemplateCopier)
        SelectedGroup.Positions.Add(PositionTemplateCopier.DeepCopy(SelectedStoragePosition));

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
