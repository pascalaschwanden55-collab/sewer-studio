using System;
using System.IO;
using AuswertungPro.Next.Application.Export;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// ViewModel einer einzelnen Ziel-Ablage-Karte (Haltungen / Schaechte / Dichtheit / Excel):
/// bindet Ziel-Wurzel und die drei Namens-/Ordner-Muster zweiseitig, aktualisiert eine
/// Live-Vorschau des fertigen Pfads (ueber <see cref="IDistributionPatternResolver"/>) und
/// meldet jede Aenderung ueber <c>onChanged</c> nach oben zum Speichern.
/// </summary>
public sealed partial class DistributionTargetConfigViewModel : ObservableObject
{
    private readonly DistributionTargetConfig _config;
    private readonly IDistributionPatternResolver _resolver;
    private readonly DistributionPatternContext _sampleContext;
    private readonly string _extension;
    private readonly Action _onChanged;
    private readonly Func<string?> _browseFolder;

    public string Titel { get; }
    public string Untertitel { get; }

    /// <summary>Excel-Export hat keine Ordner-Ebenen: dann nur Ziel-Wurzel + Datei anzeigen.</summary>
    public bool ShowFolderLevels { get; }
    public string PlatzhalterHinweis { get; }

    [ObservableProperty] private string? _root;
    [ObservableProperty] private string _ordnerPattern;
    [ObservableProperty] private string _unterordnerPattern;
    [ObservableProperty] private string _dateiPattern;
    [ObservableProperty] private string _vorschau = string.Empty;

    public IRelayCommand BrowseRootCommand { get; }

    public DistributionTargetConfigViewModel(
        string titel,
        string untertitel,
        DistributionTargetConfig config,
        IDistributionPatternResolver resolver,
        DistributionPatternContext sampleContext,
        string extension,
        bool showFolderLevels,
        string platzhalterHinweis,
        Action onChanged,
        Func<string?> browseFolder)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sampleContext = sampleContext ?? throw new ArgumentNullException(nameof(sampleContext));
        _extension = extension;
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _browseFolder = browseFolder ?? throw new ArgumentNullException(nameof(browseFolder));
        Titel = titel;
        Untertitel = untertitel;
        ShowFolderLevels = showFolderLevels;
        PlatzhalterHinweis = platzhalterHinweis;

        // Backing-Felder direkt aus der Konfiguration setzen -> loest KEINE OnChanged-Callbacks
        // und damit kein vorzeitiges Speichern beim Aufbau aus.
        _root = config.Root;
        _ordnerPattern = config.OrdnerPattern;
        _unterordnerPattern = config.UnterordnerPattern;
        _dateiPattern = config.DateiPattern;

        BrowseRootCommand = new RelayCommand(BrowseRoot);
        UpdateVorschau();
    }

    private void BrowseRoot()
    {
        var gewaehlt = _browseFolder();
        if (!string.IsNullOrWhiteSpace(gewaehlt))
            Root = gewaehlt;
    }

    partial void OnRootChanged(string? value)
    {
        _config.Root = value;
        UpdateVorschau();
        _onChanged();
    }

    partial void OnOrdnerPatternChanged(string value)
    {
        _config.OrdnerPattern = value ?? string.Empty;
        UpdateVorschau();
        _onChanged();
    }

    partial void OnUnterordnerPatternChanged(string value)
    {
        _config.UnterordnerPattern = value ?? string.Empty;
        UpdateVorschau();
        _onChanged();
    }

    partial void OnDateiPatternChanged(string value)
    {
        _config.DateiPattern = value ?? string.Empty;
        UpdateVorschau();
        _onChanged();
    }

    /// <summary>Baut die Live-Vorschau des fertigen Zielpfads aus den aktuellen Mustern.</summary>
    private void UpdateVorschau()
    {
        // Excel-Karten haben keine Ordner-Ebenen -> nur Ziel-Wurzel + Datei.
        var relativ = _resolver.ResolveRelativePath(
            ShowFolderLevels ? OrdnerPattern : null,
            ShowFolderLevels ? UnterordnerPattern : null,
            DateiPattern,
            _sampleContext,
            _extension);

        // Ohne gesetzte Ziel-Wurzel einen sichtbaren Platzhalter statt eines echten Pfads zeigen.
        var wurzel = string.IsNullOrWhiteSpace(Root) ? "<Ziel-Wurzel>" : Root!;
        Vorschau = Path.Combine(wurzel, relativ);
    }
}
