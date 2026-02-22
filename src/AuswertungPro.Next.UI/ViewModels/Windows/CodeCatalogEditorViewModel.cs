using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CodeCatalogEditorViewModel : ObservableObject
{
    private const string AllGroupsLabel = "Alle";

    private readonly AppProtocol.ICodeCatalogProvider _catalogProvider;
    private readonly Window _window;
    private readonly ICollectionView _codesView;
    private bool _hasChanges;

    public ObservableCollection<CodeDefinitionItem> Codes { get; }
    public ICollectionView CodesView => _codesView;
    public ObservableCollection<string> GroupOptions { get; } = new();
    public ObservableCollection<string> ValidationMessages { get; } = new();

    [ObservableProperty] private CodeDefinitionItem? _selectedCode;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedGroup = AllGroupsLabel;

    public IRelayCommand NewCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ValidateCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public CodeCatalogEditorViewModel(AppProtocol.ICodeCatalogProvider catalogProvider, Window window)
    {
        _catalogProvider = catalogProvider;
        _window = window;

        Codes = new ObservableCollection<CodeDefinitionItem>(
            catalogProvider.GetAll()
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .Select(ToItem));

        foreach (var item in Codes)
            item.PropertyChanged += OnCodeItemChanged;

        Codes.CollectionChanged += OnCodesCollectionChanged;

        _codesView = CollectionViewSource.GetDefaultView(Codes);
        _codesView.Filter = FilterCode;

        RefreshGroupOptions();
        if (GroupOptions.Count > 0)
            SelectedGroup = GroupOptions[0];

        if (catalogProvider is AppProtocol.JsonCodeCatalogProvider jsonProvider && jsonProvider.LastLoadErrors.Count > 0)
            SetValidationMessages(jsonProvider.LastLoadErrors);

        NewCommand = new RelayCommand(NewCode);
        DeleteCommand = new RelayCommand(DeleteSelectedCode, () => SelectedCode is not null);
        SaveCommand = new RelayCommand(Save);
        ValidateCommand = new RelayCommand(ValidateOnly);
        CancelCommand = new RelayCommand(Cancel);
    }

    partial void OnSelectedCodeChanged(CodeDefinitionItem? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value)
    {
        _codesView.Refresh();
    }

    partial void OnSelectedGroupChanged(string value)
    {
        _codesView.Refresh();
    }

    private bool FilterCode(object obj)
    {
        if (obj is not CodeDefinitionItem item)
            return false;

        if (!string.IsNullOrWhiteSpace(SelectedGroup) &&
            !string.Equals(SelectedGroup, AllGroupsLabel, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Group, SelectedGroup, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var q = SearchText.Trim();
        return item.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Group.Contains(q, StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void NewCode()
    {
        var item = new CodeDefinitionItem
        {
            Code = string.Empty,
            Title = string.Empty,
            Group = "Unbekannt",
            Description = string.Empty
        };

        item.PropertyChanged += OnCodeItemChanged;
        Codes.Add(item);
        SelectedCode = item;
        _codesView.Refresh();
        RefreshGroupOptions();
        _hasChanges = true;
    }

    private void DeleteSelectedCode()
    {
        if (SelectedCode is null)
            return;

        var label = string.IsNullOrWhiteSpace(SelectedCode.Code) ? SelectedCode.Title : SelectedCode.Code;
        var result = MessageBox.Show(
            $"Code '{label}' wirklich loeschen?",
            "Code loeschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var deleted = SelectedCode;
        Codes.Remove(deleted);
        deleted.PropertyChanged -= OnCodeItemChanged;
        SelectedCode = null;
        _codesView.Refresh();
        RefreshGroupOptions();
        _hasChanges = true;
    }

    private void Save()
    {
        var definitions = Codes.Select(ToDefinition).ToList();
        var errors = _catalogProvider.Validate(definitions);
        SetValidationMessages(errors);

        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "Code-Katalog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _catalogProvider.Save(definitions);
            _hasChanges = false;
            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Speichern fehlgeschlagen: {ex.Message}", "Code-Katalog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ValidateOnly()
    {
        var definitions = Codes.Select(ToDefinition).ToList();
        var errors = _catalogProvider.Validate(definitions);
        SetValidationMessages(errors);

        if (errors.Count == 0)
        {
            MessageBox.Show("Validierung erfolgreich. Keine Fehler gefunden.", "Code-Katalog", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(string.Join(Environment.NewLine, errors), "Code-Katalog", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Cancel()
    {
        if (_hasChanges)
        {
            var result = MessageBox.Show(
                "Aenderungen verwerfen?",
                "Code-Katalog",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _window.DialogResult = false;
        _window.Close();
    }

    private void OnCodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<CodeDefinitionItem>())
                item.PropertyChanged += OnCodeItemChanged;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<CodeDefinitionItem>())
                item.PropertyChanged -= OnCodeItemChanged;
        }

        RefreshGroupOptions();
        _codesView.Refresh();
        _hasChanges = true;
    }

    private void OnCodeItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        _codesView.Refresh();
        RefreshGroupOptions();
        _hasChanges = true;
    }

    private void RefreshGroupOptions()
    {
        var current = SelectedGroup;
        var groups = Codes
            .Select(x => string.IsNullOrWhiteSpace(x.Group) ? "Unbekannt" : x.Group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GroupOptions.Clear();
        GroupOptions.Add(AllGroupsLabel);
        foreach (var group in groups)
            GroupOptions.Add(group);

        if (string.IsNullOrWhiteSpace(current) || !GroupOptions.Contains(current))
            SelectedGroup = AllGroupsLabel;
        else
            SelectedGroup = current;
    }

    private void SetValidationMessages(IReadOnlyList<string> messages)
    {
        ValidationMessages.Clear();
        foreach (var message in messages)
            ValidationMessages.Add(message);
    }

    private static CodeDefinitionItem ToItem(AppProtocol.CodeDefinition code)
    {
        return new CodeDefinitionItem
        {
            Code = code.Code,
            Title = code.Title,
            Group = code.Group,
            Description = code.Description ?? string.Empty
        };
    }

    private static AppProtocol.CodeDefinition ToDefinition(CodeDefinitionItem item)
    {
        return new AppProtocol.CodeDefinition
        {
            Code = item.Code,
            Title = item.Title,
            Group = item.Group,
            Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
            Parameters = new List<AppProtocol.CodeParameter>(),
            Examples = new List<string>()
        };
    }
}

public sealed partial class CodeDefinitionItem : ObservableObject
{
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _group = "Unbekannt";
    [ObservableProperty] private string _description = string.Empty;
}
