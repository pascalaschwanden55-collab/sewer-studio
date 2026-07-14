using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Export;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// ViewModel einer Ziel-Ablage-Karte auf der Export-Seite. Zwei Auspraegungen:
///  - <b>Verteilung</b> (Haltungen/Schaechte/Dichtheit): nur die Ziel-Wurzel ist einstellbar;
///    die Datei-Benennung bleibt fest (sie ist im Verteiler mit der Video-Zuordnung verwoben).
///    <see cref="ShowFilePattern"/> = false.
///  - <b>Excel-Export</b>: zusaetzlich ein freies Datei-Muster mit Live-Vorschau des fertigen
///    Namens (ueber <see cref="IDistributionPatternResolver"/>). <see cref="ShowFilePattern"/> = true.
/// Jede Aenderung wird ueber <c>onChanged</c> nach oben zum Speichern gemeldet.
/// </summary>
public sealed partial class DistributionTargetConfigViewModel : ObservableObject
{
    private readonly DistributionTargetConfig _config;
    private readonly IDistributionPatternResolver _resolver;
    private readonly DistributionPatternContext _sampleContext;
    private readonly string _extension;
    private readonly string? _fixedPattern;
    private readonly Action _onChanged;
    private readonly Func<string?> _browseFolder;

    public string Titel { get; }
    public string Untertitel { get; }

    /// <summary>true = Excel-Karte mit freiem Datei-Muster; false = Verteil-Karte (nur Ziel-Wurzel).</summary>
    public bool ShowFilePattern { get; }

    /// <summary>Fuer Verteil-Karten wird das feste, sichere Benennungsschema nur angezeigt.</summary>
    public bool ShowFixedPattern => !ShowFilePattern;

    /// <summary>Erklaerender Hinweis (Excel: Platzhalter-Legende; Verteilung: festes Benennungsschema).</summary>
    public string Hinweis { get; }

    /// <summary>Anklickbare Bausteine fuer den Excel-Dateinamen.</summary>
    public IReadOnlyList<DistributionPatternBlock> AvailablePatternBlocks { get; } =
        DistributionPatternBlockComposer.AvailableExcelBlocks;

    [ObservableProperty] private string? _root;
    [ObservableProperty] private string _dateiPattern;
    [ObservableProperty] private IReadOnlyList<DistributionPatternPart> _dateiPatternParts =
        Array.Empty<DistributionPatternPart>();
    [ObservableProperty] private string _vorschau = string.Empty;

    public IRelayCommand BrowseRootCommand { get; }
    public IRelayCommand<DistributionPatternBlock> AddPatternBlockCommand { get; }
    public IRelayCommand RemoveLastPatternBlockCommand { get; }
    public IRelayCommand ClearPatternCommand { get; }

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
        string? fixedPattern = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sampleContext = sampleContext ?? throw new ArgumentNullException(nameof(sampleContext));
        _extension = extension;
        _fixedPattern = fixedPattern;
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _browseFolder = browseFolder ?? throw new ArgumentNullException(nameof(browseFolder));
        Titel = titel;
        Untertitel = untertitel;
        ShowFilePattern = showFilePattern;
        Hinweis = hinweis;

        // Backing-Felder direkt aus der Konfiguration setzen -> loest KEINE OnChanged-Callbacks
        // und damit kein vorzeitiges Speichern beim Aufbau aus.
        _root = config.Root;
        _dateiPattern = config.DateiPattern;

        BrowseRootCommand = new RelayCommand(BrowseRoot);
        AddPatternBlockCommand = new RelayCommand<DistributionPatternBlock>(AddPatternBlock);
        RemoveLastPatternBlockCommand = new RelayCommand(RemoveLastPatternBlock);
        ClearPatternCommand = new RelayCommand(ClearPattern);
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
        _onChanged();
    }

    partial void OnDateiPatternChanged(string value)
    {
        _config.DateiPattern = value ?? string.Empty;
        UpdateDateiPatternParts();
        UpdateVorschau();
        _onChanged();
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

    private void UpdateDateiPatternParts()
    {
        var shownPattern = ShowFilePattern
            ? DateiPattern
            : _fixedPattern ?? DateiPattern;
        DateiPatternParts = DistributionPatternBlockComposer.Parse(shownPattern);
    }

    /// <summary>
    /// Baut die Live-Vorschau. Excel: Ziel-Wurzel + aufgeloestes Datei-Muster.
    /// Verteilung: die Ziel-Wurzel selbst (die Benennung darunter ist fest).
    /// </summary>
    private void UpdateVorschau()
    {
        var wurzel = string.IsNullOrWhiteSpace(Root) ? "<Ziel-Wurzel>" : Root!;
        if (ShowFilePattern)
        {
            var relativ = _resolver.ResolveRelativePath(
                ordnerPattern: null,
                unterordnerPattern: null,
                dateiPattern: DateiPattern,
                context: _sampleContext,
                extension: _extension);
            Vorschau = Path.Combine(wurzel, relativ);
        }
        else
        {
            Vorschau = wurzel;
        }
    }
}
