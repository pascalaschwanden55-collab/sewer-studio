using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CatalogSelectorViewModel : ObservableObject
{
    private const string AllFilter = "Alle";

    private readonly Window _window;
    private readonly WinCanCatalogDiscoveryService _discovery = new();
    private readonly List<WinCanCatalogInfo> _allCatalogs = new();

    public ObservableCollection<WinCanCatalogInfo> FilteredCatalogs { get; } = new();
    public ObservableCollection<string> CountryOptions { get; } = new() { AllFilter };
    public ObservableCollection<string> StandardOptions { get; } = new() { AllFilter };
    public ObservableCollection<string> ObjectTypeOptions { get; } = new() { AllFilter, "SEC", "NOD" };

    [ObservableProperty] private string _catalogDirectory = "";
    [ObservableProperty] private string _filterCountry = AllFilter;
    [ObservableProperty] private string _filterStandard = AllFilter;
    [ObservableProperty] private string _filterObjectType = AllFilter;
    [ObservableProperty] private WinCanCatalogInfo? _selectedCatalog;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _currentCatalogInfo = "";

    /// <summary>
    /// The file path of the selected catalog when the user clicks "Uebernehmen".
    /// Null if canceled.
    /// </summary>
    public string? ResultPath { get; private set; }

    public IRelayCommand BrowseDirectoryCommand { get; }
    public IRelayCommand ScanCommand { get; }
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public CatalogSelectorViewModel(Window window, string? currentCatalogPath, string? winCanCatalogDir, string? lastProjectPath)
    {
        _window = window;

        BrowseDirectoryCommand = new RelayCommand(BrowseDirectory);
        ScanCommand = new RelayCommand(ScanCatalogs);
        ApplyCommand = new RelayCommand(Apply, () => SelectedCatalog is not null);
        CancelCommand = new RelayCommand(Cancel);

        if (!string.IsNullOrWhiteSpace(currentCatalogPath))
            CurrentCatalogInfo = $"Aktuell: {Path.GetFileName(currentCatalogPath)}";

        // Auto-discover directories
        var defaultDirs = WinCanCatalogDiscoveryService.GetDefaultSearchDirectories(winCanCatalogDir, lastProjectPath);
        if (defaultDirs.Count > 0)
        {
            CatalogDirectory = defaultDirs[0];
            ScanDirectories(defaultDirs);
        }
        else if (!string.IsNullOrWhiteSpace(winCanCatalogDir))
        {
            CatalogDirectory = winCanCatalogDir;
        }
    }

    partial void OnFilterCountryChanged(string value) => ApplyFilters();
    partial void OnFilterStandardChanged(string value) => ApplyFilters();
    partial void OnFilterObjectTypeChanged(string value) => ApplyFilters();
    partial void OnSelectedCatalogChanged(WinCanCatalogInfo? value) => ((RelayCommand)ApplyCommand).NotifyCanExecuteChanged();

    private void BrowseDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "WinCan-Katalog Verzeichnis waehlen",
            Filter = "XML-Katalog (*.xml)|*.xml|Alle Dateien (*.*)|*.*",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Verzeichnis waehlen"
        };

        if (!string.IsNullOrWhiteSpace(CatalogDirectory) && Directory.Exists(CatalogDirectory))
            dialog.InitialDirectory = CatalogDirectory;

        if (dialog.ShowDialog(_window) == true)
        {
            var selected = dialog.FileName;
            var dir = File.Exists(selected) ? Path.GetDirectoryName(selected) : selected;
            if (!string.IsNullOrWhiteSpace(dir))
            {
                CatalogDirectory = dir;
                ScanCatalogs();
            }
        }
    }

    private void ScanCatalogs()
    {
        if (string.IsNullOrWhiteSpace(CatalogDirectory))
            return;

        ScanDirectories(new[] { CatalogDirectory });
    }

    private void ScanDirectories(IEnumerable<string> directories)
    {
        _allCatalogs.Clear();
        FilteredCatalogs.Clear();
        CountryOptions.Clear();
        StandardOptions.Clear();
        CountryOptions.Add(AllFilter);
        StandardOptions.Add(AllFilter);

        var catalogs = _discovery.DiscoverCatalogs(directories);
        _allCatalogs.AddRange(catalogs);

        // Build filter options
        var countries = catalogs.Select(c => c.Country).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c);
        foreach (var c in countries)
            CountryOptions.Add(c);

        var standards = catalogs.Select(c => c.Standard).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s);
        foreach (var s in standards)
            StandardOptions.Add(s);

        FilterCountry = AllFilter;
        FilterStandard = AllFilter;
        FilterObjectType = AllFilter;

        ApplyFilters();

        StatusText = $"{catalogs.Count} Katalog(e) gefunden";
    }

    private void ApplyFilters()
    {
        FilteredCatalogs.Clear();

        foreach (var cat in _allCatalogs)
        {
            if (FilterCountry != AllFilter && !string.Equals(cat.Country, FilterCountry, StringComparison.OrdinalIgnoreCase))
                continue;
            if (FilterStandard != AllFilter && !string.Equals(cat.Standard, FilterStandard, StringComparison.OrdinalIgnoreCase))
                continue;
            if (FilterObjectType != AllFilter && !string.Equals(cat.ObjectType, FilterObjectType, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredCatalogs.Add(cat);
        }

        StatusText = $"{FilteredCatalogs.Count} von {_allCatalogs.Count} Katalog(en) angezeigt";
    }

    private void Apply()
    {
        if (SelectedCatalog is null)
            return;

        ResultPath = SelectedCatalog.FilePath;
        _window.DialogResult = true;
        _window.Close();
    }

    private void Cancel()
    {
        ResultPath = null;
        _window.DialogResult = false;
        _window.Close();
    }
}
