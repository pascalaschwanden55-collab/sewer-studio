using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Export;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// ViewModel einer Ziel-Ablage-Karte auf der Export-Seite. Zwei Auspraegungen:
///  - <b>Verteilung</b> (Haltungen/Schaechte/Dichtheit): Ziel-Wurzel und zwei sichere
///    Ueberordner sind einstellbar. Der letzte Objektordner und die Datei-Benennung bleiben fest,
///    weil Video-Zuordnung und Projektpfade darauf angewiesen sind.
///    <see cref="ShowFilePattern"/> = false.
///  - <b>Excel-Export</b>: zusaetzlich ein freies Datei-Muster mit Live-Vorschau des fertigen
///    Namens (ueber <see cref="IDistributionPatternResolver"/>). <see cref="ShowFilePattern"/> = true.
/// Jede Aenderung wird ueber <c>onChanged</c> nach oben zum Speichern gemeldet.
/// </summary>
public sealed partial class DistributionTargetConfigViewModel : ObservableObject
{
    private readonly DistributionTargetConfig _config;
    private readonly IDistributionPatternResolver _resolver;
    private readonly IDistributionDirectoryTreeResolver _directoryTreeResolver;
    private readonly DistributionPatternContext _sampleContext;
    private readonly string _extension;
    private readonly string? _fixedPattern;
    private readonly string? _fixedObjectFolderPattern;
    private readonly Action _onChanged;
    private readonly Func<string?> _browseFolder;
    private bool _suppressRootSave;
    private bool _suppressFilePatternSave;

    public string Titel { get; }
    public string Untertitel { get; }

    /// <summary>true = Excel-Karte mit freiem Datei-Muster; false = Verteil-Karte (nur Ziel-Wurzel).</summary>
    public bool ShowFilePattern { get; }

    /// <summary>true = diese Karte besitzt ein eigenes Ziel-Wurzel-Feld.</summary>
    public bool ShowRootEditor { get; }

    /// <summary>true = diese Karte zeigt einmalig den gemeinsamen Excel-Zielordner.</summary>
    public bool ShowSharedExcelRoot { get; }

    /// <summary>true = Verteilkarte mit zwei konfigurierbaren Ueberordnern.</summary>
    public bool ShowDirectoryTree => !ShowFilePattern;

    /// <summary>Fuer Verteil-Karten wird das feste, sichere Benennungsschema nur angezeigt.</summary>
    public bool ShowFixedPattern => !ShowFilePattern;

    /// <summary>Erklaerender Hinweis (Excel: Platzhalter-Legende; Verteilung: festes Benennungsschema).</summary>
    public string Hinweis { get; }

    /// <summary>Der feste letzte Ordner: Haltung oder Schachtnummer.</summary>
    public string FixedObjectFolderPattern => _fixedObjectFolderPattern ?? string.Empty;

    public IReadOnlyList<DistributionPatternPart> ObjectFolderPatternParts { get; }

    /// <summary>Anklickbare Bausteine fuer den Excel-Dateinamen.</summary>
    public IReadOnlyList<DistributionPatternBlock> AvailablePatternBlocks { get; } =
        DistributionPatternBlockComposer.AvailableExcelBlocks;

    /// <summary>Anklickbare Bausteine fuer die beiden optionalen Verteil-Ordner.</summary>
    public IReadOnlyList<DistributionPatternBlock> AvailableDirectoryBlocks { get; } =
        DistributionPatternBlockComposer.AvailableDirectoryBlocks;

    [ObservableProperty] private string? _root;
    [ObservableProperty] private string _ordnerPattern;
    [ObservableProperty] private string _unterordnerPattern;
    [ObservableProperty] private IReadOnlyList<DistributionPatternPart> _ordnerPatternParts =
        Array.Empty<DistributionPatternPart>();
    [ObservableProperty] private IReadOnlyList<DistributionPatternPart> _unterordnerPatternParts =
        Array.Empty<DistributionPatternPart>();
    [ObservableProperty] private string _dateiPattern;
    [ObservableProperty] private IReadOnlyList<DistributionPatternPart> _dateiPatternParts =
        Array.Empty<DistributionPatternPart>();
    [ObservableProperty] private string _vorschau = string.Empty;

    /// <summary>true = Haltung/Schacht (mit Sanierungs-Variante); false = DP/Excel.</summary>
    public bool SupportsSanierung { get; }

    /// <summary>Aktuell in der Vorschau gezeigte Variante (schaltet nur die Anzeige um).</summary>
    [ObservableProperty] private DistributionVariant _previewVariant = DistributionVariant.Normal;

    /// <summary>true = der Baustein-Baukasten ("Erweitert") ist aufgeklappt.</summary>
    [ObservableProperty] private bool _isAdvancedExpanded;

    /// <summary>Grafische Ordnerbaum-Vorschau der aktuellen Variante.</summary>
    [ObservableProperty] private IReadOnlyList<DistributionTreeNode> _treeNodes =
        Array.Empty<DistributionTreeNode>();

    public IRelayCommand BrowseRootCommand { get; }
    public IRelayCommand<DistributionPatternBlock> AddPatternBlockCommand { get; }
    public IRelayCommand RemoveLastPatternBlockCommand { get; }
    public IRelayCommand ClearPatternCommand { get; }
    public IRelayCommand<DistributionPatternBlock> AddOrdnerPatternBlockCommand { get; }
    public IRelayCommand RemoveLastOrdnerPatternBlockCommand { get; }
    public IRelayCommand ClearOrdnerPatternCommand { get; }
    public IRelayCommand<DistributionPatternBlock> AddUnterordnerPatternBlockCommand { get; }
    public IRelayCommand RemoveLastUnterordnerPatternBlockCommand { get; }
    public IRelayCommand ClearUnterordnerPatternCommand { get; }

    public DistributionTargetConfigViewModel(
        string titel,
        string untertitel,
        DistributionTargetConfig config,
        IDistributionPatternResolver resolver,
        DistributionPatternContext sampleContext,
        string extension,
        bool showFilePattern,
        string hinweis,
        Action onChanged,
        Func<string?> browseFolder,
        string? fixedPattern = null,
        bool showRootEditor = true,
        bool showSharedExcelRoot = false,
        string? fixedObjectFolderPattern = null,
        IDistributionDirectoryTreeResolver? directoryTreeResolver = null,
        bool supportsSanierung = false)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _directoryTreeResolver = directoryTreeResolver ?? new DistributionDirectoryTreeResolver(_resolver);
        _sampleContext = sampleContext ?? throw new ArgumentNullException(nameof(sampleContext));
        _extension = extension;
        _fixedPattern = fixedPattern;
        _fixedObjectFolderPattern = fixedObjectFolderPattern;
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _browseFolder = browseFolder ?? throw new ArgumentNullException(nameof(browseFolder));
        Titel = titel;
        Untertitel = untertitel;
        ShowFilePattern = showFilePattern;
        ShowRootEditor = showRootEditor;
        ShowSharedExcelRoot = showSharedExcelRoot;
        Hinweis = hinweis;
        SupportsSanierung = supportsSanierung;

        // Backing-Felder direkt aus der Konfiguration setzen -> loest KEINE OnChanged-Callbacks
        // und damit kein vorzeitiges Speichern beim Aufbau aus.
        _root = config.Root;
        _ordnerPattern = config.OrdnerPattern ?? string.Empty;
        _unterordnerPattern = config.UnterordnerPattern ?? string.Empty;
        _dateiPattern = config.DateiPattern;
        ObjectFolderPatternParts = DistributionPatternBlockComposer.Parse(FixedObjectFolderPattern);

        BrowseRootCommand = new RelayCommand(BrowseRoot);
        AddPatternBlockCommand = new RelayCommand<DistributionPatternBlock>(AddPatternBlock);
        RemoveLastPatternBlockCommand = new RelayCommand(RemoveLastPatternBlock);
        ClearPatternCommand = new RelayCommand(ClearPattern);
        AddOrdnerPatternBlockCommand = new RelayCommand<DistributionPatternBlock>(AddOrdnerPatternBlock);
        RemoveLastOrdnerPatternBlockCommand = new RelayCommand(RemoveLastOrdnerPatternBlock);
        ClearOrdnerPatternCommand = new RelayCommand(ClearOrdnerPattern);
        AddUnterordnerPatternBlockCommand = new RelayCommand<DistributionPatternBlock>(AddUnterordnerPatternBlock);
        RemoveLastUnterordnerPatternBlockCommand = new RelayCommand(RemoveLastUnterordnerPatternBlock);
        ClearUnterordnerPatternCommand = new RelayCommand(ClearUnterordnerPattern);
        UpdateDirectoryPatternParts();
        UpdateDateiPatternParts();
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
        if (!_suppressRootSave)
            _onChanged();
    }

    partial void OnOrdnerPatternChanged(string value)
    {
        _config.OrdnerPattern = value ?? string.Empty;
        UpdateDirectoryPatternParts();
        UpdateVorschau();
        _onChanged();
    }

    partial void OnUnterordnerPatternChanged(string value)
    {
        _config.UnterordnerPattern = value ?? string.Empty;
        UpdateDirectoryPatternParts();
        UpdateVorschau();
        _onChanged();
    }

    partial void OnDateiPatternChanged(string value)
    {
        _config.DateiPattern = value ?? string.Empty;
        UpdateDateiPatternParts();
        UpdateVorschau();
        if (!_suppressFilePatternSave)
            _onChanged();
    }

    partial void OnPreviewVariantChanged(DistributionVariant value)
    {
        _ = value;
        // Umschalter aendert nur die Anzeige (Vorschau + Ordnerbaum), keine Persistenz.
        UpdateVorschau();
    }

    /// <summary>
    /// Aktualisiert den gemeinsamen Excel-Zielordner ohne einen zweiten Speichervorgang auszuloesen.
    /// Der Besitzer der gemeinsamen Einstellung speichert anschliessend genau einmal.
    /// </summary>
    internal void ApplySharedRoot(string? value)
    {
        if (string.Equals(Root, value, StringComparison.Ordinal))
        {
            _config.Root = value;
            UpdateVorschau();
            return;
        }

        _suppressRootSave = true;
        try
        {
            Root = value;
        }
        finally
        {
            _suppressRootSave = false;
        }
    }

    /// <summary>Uebernimmt einen sicher korrigierten Excel-Dateinamen ohne Callback-Schleife.</summary>
    internal void ApplyFilePattern(string value)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(DateiPattern, normalized, StringComparison.Ordinal))
        {
            _config.DateiPattern = normalized;
            return;
        }

        _suppressFilePatternSave = true;
        try
        {
            DateiPattern = normalized;
        }
        finally
        {
            _suppressFilePatternSave = false;
        }
    }

    private void AddPatternBlock(DistributionPatternBlock? block)
    {
        if (!ShowFilePattern || block is null)
            return;

        DateiPattern = DistributionPatternBlockComposer.Append(DateiPattern, block);
    }

    private void RemoveLastPatternBlock()
    {
        if (ShowFilePattern)
            DateiPattern = DistributionPatternBlockComposer.RemoveLast(DateiPattern);
    }

    private void ClearPattern()
    {
        if (ShowFilePattern)
            DateiPattern = string.Empty;
    }

    private void AddOrdnerPatternBlock(DistributionPatternBlock? block)
    {
        if (ShowDirectoryTree && block is not null)
            OrdnerPattern = DistributionPatternBlockComposer.Append(OrdnerPattern, block);
    }

    private void RemoveLastOrdnerPatternBlock()
    {
        if (ShowDirectoryTree)
            OrdnerPattern = DistributionPatternBlockComposer.RemoveLast(OrdnerPattern);
    }

    private void ClearOrdnerPattern()
    {
        if (ShowDirectoryTree)
            OrdnerPattern = string.Empty;
    }

    private void AddUnterordnerPatternBlock(DistributionPatternBlock? block)
    {
        if (ShowDirectoryTree && block is not null)
            UnterordnerPattern = DistributionPatternBlockComposer.Append(UnterordnerPattern, block);
    }

    private void RemoveLastUnterordnerPatternBlock()
    {
        if (ShowDirectoryTree)
            UnterordnerPattern = DistributionPatternBlockComposer.RemoveLast(UnterordnerPattern);
    }

    private void ClearUnterordnerPattern()
    {
        if (ShowDirectoryTree)
            UnterordnerPattern = string.Empty;
    }

    private void UpdateDirectoryPatternParts()
    {
        OrdnerPatternParts = DistributionPatternBlockComposer.Parse(OrdnerPattern);
        UnterordnerPatternParts = DistributionPatternBlockComposer.Parse(UnterordnerPattern);
    }

    private void UpdateDateiPatternParts()
    {
        var shownPattern = ShowFilePattern
            ? DateiPattern
            : _fixedPattern ?? DateiPattern;
        DateiPatternParts = DistributionPatternBlockComposer.Parse(shownPattern);
    }

    /// <summary>
    /// Baut die Live-Vorschau. Excel: Ziel-Wurzel + aufgeloestes Datei-Muster.
    /// Verteilung: kompletter Beispielpfad mit optionalen Ueberordnern, festem
    /// Objektordner und festem Dateinamen.
    /// </summary>
    private void UpdateVorschau()
    {
        var result = DistributionTargetPreviewBuilder.Build(
            new DistributionTargetPreviewRequest(
                Root,
                OrdnerPattern,
                UnterordnerPattern,
                DateiPattern,
                _fixedPattern,
                _fixedObjectFolderPattern,
                _extension,
                ShowFilePattern,
                SupportsSanierung,
                PreviewVariant,
                _sampleContext),
            _resolver,
            _directoryTreeResolver);

        Vorschau = result.Vorschau;
        TreeNodes = result.TreeNodes;
    }
}
