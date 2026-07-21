using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly ISchachtProtocolImportService _schachtProtocolImport;
    private readonly SchachtProtocolRefreshController _schachtProtocolRefreshController;
    private readonly SchachtProtocolSingleImportController _schachtProtocolSingleImportController;
    private readonly ISchachtStammdatenErgaenzungsService _schachtStammdatenErgaenzung;
    private readonly ISchachtMassnahmenKatalogStore _schachtMassnahmenKatalog;
    private readonly IProjectCostStoreRepository _schachtRecommendationCosts;
    private readonly IDropdownOptionsStore _dropdownOptions;
    private readonly IShaftRenameService _shaftRename;
    private readonly IPdfTextLayerRewriter _pdfTextLayerRewrite;
    private readonly IExplorerRevealService _explorerReveal;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly ISchaechteTemplateColumnReader _templateColumnReader;
    private readonly ISchachtFileTargetResolver _schachtFileTargets;
    private readonly ShellViewModel _shell;
    private readonly SchaechteDropdownCommands _dropdownCommands;
    private bool _suppressRequiredFieldWarning;

    internal AppSettings Settings => _settings;
    internal IDialogService Dialogs => _dialogs;
    internal ISchachtMassnahmenKatalogStore SchachtMassnahmenKatalog => _schachtMassnahmenKatalog;
    internal IProjectCostStoreRepository SchachtRecommendationCosts => _schachtRecommendationCosts;
    internal IShaftRenameService ShaftRename => _shaftRename;
    internal IPdfTextLayerRewriter PdfTextLayerRewrite => _pdfTextLayerRewrite;
    internal IExplorerRevealService ExplorerReveal => _explorerReveal;
    internal ISafeShellOpenService ShellOpen => _shellOpen;
    internal ISchachtFileTargetResolver SchachtFileTargets => _schachtFileTargets;

    public ObservableCollection<SchachtRecord> Records => _shell.Project.SchaechteData;
    public ObservableCollection<string> Columns { get; } = new();

    public ObservableCollection<string> SanierenOptions { get; }
    public ObservableCollection<string> EigentuemerOptions { get; }
    public ObservableCollection<string> PruefungsresultatOptions { get; }
    public ObservableCollection<string> ReferenzpruefungOptions { get; }
    public ObservableCollection<string> AusgefuehrtDurchOptions { get; }
    public ObservableCollection<string> SchachtformOptions { get; }

    [ObservableProperty] private SchachtRecord? _selected;
    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _searchResultInfo = string.Empty;
    [ObservableProperty] private double _gridMinRowHeight = 38d;
    [ObservableProperty] private double _gridZoom = 1.0d;
    [ObservableProperty] private bool _isColumnReorderEnabled;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand RefreshProtocolCommand { get; }
    public IRelayCommand ImportProtocolCommand { get; }
    public IRelayCommand ClearSearchCommand { get; }

    public IRelayCommand EditSanierenOptionsCommand => _dropdownCommands.Sanieren.Edit;
    public IRelayCommand PreviewSanierenOptionsCommand => _dropdownCommands.Sanieren.Preview;
    public IRelayCommand ResetSanierenOptionsCommand => _dropdownCommands.Sanieren.Reset;
    public IRelayCommand<object?> AddSanierenOptionCommand => _dropdownCommands.Sanieren.Add;
    public IRelayCommand<object?> RemoveSanierenOptionCommand => _dropdownCommands.Sanieren.Remove;

    public IRelayCommand EditEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Edit;
    public IRelayCommand PreviewEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Preview;
    public IRelayCommand ResetEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Reset;
    public IRelayCommand<object?> AddEigentuemerOptionCommand => _dropdownCommands.Eigentuemer.Add;
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand => _dropdownCommands.Eigentuemer.Remove;

    public IRelayCommand EditPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Edit;
    public IRelayCommand PreviewPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Preview;
    public IRelayCommand ResetPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Reset;
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand => _dropdownCommands.Pruefungsresultat.Add;
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand => _dropdownCommands.Pruefungsresultat.Remove;

    public IRelayCommand EditReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Edit;
    public IRelayCommand PreviewReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Preview;
    public IRelayCommand ResetReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Reset;
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand => _dropdownCommands.Referenzpruefung.Add;
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand => _dropdownCommands.Referenzpruefung.Remove;

    public SchaechtePageViewModel(ShellViewModel shell, ServiceProvider services)
        : this(
            shell,
            settings: services.Settings,
            dialogs: services.Dialogs,
            schachtProtocolImport: services.SchachtProtocolImport,
            schachtStammdatenErgaenzung: services.SchachtStammdatenErgaenzung,
            schachtMassnahmenKatalog: services.SchachtMassnahmenKatalog,
            schachtRecommendationCosts: services.CostStores.CreateProjectCostStore("schacht_empfehlungen.json"),
            dropdownOptions: services.DropdownOptions,
            shaftRename: services.ShaftRename,
            pdfTextLayerRewrite: services.PdfTextLayerRewrite,
            shellOpen: services.ShellOpen,
            explorerReveal: services.ExplorerReveal,
            templateColumnReader: services.SchaechteTemplateColumns,
            schachtFileTargets: services.SchachtFileTargets)
    {
    }

    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen die Kosten-Speicher injizieren.")]
    public SchaechtePageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        ISchachtProtocolImportService schachtProtocolImport,
        ISchachtStammdatenErgaenzungsService schachtStammdatenErgaenzung,
        ISchachtMassnahmenKatalogStore schachtMassnahmenKatalog,
        IDropdownOptionsStore? dropdownOptions = null,
        IShaftRenameService? shaftRename = null,
        IExplorerRevealService? explorerReveal = null,
        ISchaechteTemplateColumnReader? templateColumnReader = null,
        ISchachtFileTargetResolver? schachtFileTargets = null)
        : this(
            shell,
            settings,
            dialogs,
            schachtProtocolImport,
            schachtStammdatenErgaenzung,
            schachtMassnahmenKatalog,
            CostStoreCompatibility.Factory.CreateProjectCostStore("schacht_empfehlungen.json"),
            dropdownOptions ?? DropdownOptionsCompatibility.Default,
            PdfTextLayerRewriter.Current,
            SafeShellOpen.CompatibilityService,
            shaftRename,
            explorerReveal,
            templateColumnReader,
            schachtFileTargets)
    {
    }

    public SchaechtePageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        ISchachtProtocolImportService schachtProtocolImport,
        ISchachtStammdatenErgaenzungsService schachtStammdatenErgaenzung,
        ISchachtMassnahmenKatalogStore schachtMassnahmenKatalog,
        IProjectCostStoreRepository schachtRecommendationCosts,
        IDropdownOptionsStore dropdownOptions,
        IPdfTextLayerRewriter pdfTextLayerRewrite,
        ISafeShellOpenService shellOpen,
        IShaftRenameService? shaftRename = null,
        IExplorerRevealService? explorerReveal = null,
        ISchaechteTemplateColumnReader? templateColumnReader = null,
        ISchachtFileTargetResolver? schachtFileTargets = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _schachtProtocolImport = schachtProtocolImport ?? throw new ArgumentNullException(nameof(schachtProtocolImport));
        _schachtStammdatenErgaenzung = schachtStammdatenErgaenzung ?? throw new ArgumentNullException(nameof(schachtStammdatenErgaenzung));
        _schachtMassnahmenKatalog = schachtMassnahmenKatalog ?? throw new ArgumentNullException(nameof(schachtMassnahmenKatalog));
        _schachtRecommendationCosts = schachtRecommendationCosts ?? throw new ArgumentNullException(nameof(schachtRecommendationCosts));
        _dropdownOptions = dropdownOptions ?? throw new ArgumentNullException(nameof(dropdownOptions));
        _pdfTextLayerRewrite = pdfTextLayerRewrite ?? throw new ArgumentNullException(nameof(pdfTextLayerRewrite));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
        _shaftRename = shaftRename ?? new ShaftRenameFileService();
        _explorerReveal = explorerReveal ?? ExplorerRevealService.DefaultService;
        _templateColumnReader = templateColumnReader ?? SchaechteTemplateColumnReader.DefaultReader;
        _schachtFileTargets = schachtFileTargets ?? SchachtFileTargetResolver.CompatibilityService;
        _schachtProtocolRefreshController = new SchachtProtocolRefreshController(
            _dialogs,
            new SchachtProtocolRefreshActions(
                GetProjectFolder: _shell.GetProjectFolder,
                CaptureProject: () => new ProjectOperationContext(
                    _shell.Project,
                    _settings.LastProjectPath),
                ResolveLinkedFile: ProjectPathResolver.ResolveFilePathFromProjectFolder,
                ReadProtocolAsync: ReadProtocolAsync,
                ProjectIsStillOpen: ProjectIsStillOpen,
                Apply: _schachtProtocolImport.Apply,
                SaveProject: _shell.TrySaveProject,
                SetLastResult: value => LastResult = value));
        _schachtProtocolSingleImportController = new SchachtProtocolSingleImportController(
            _dialogs,
            _schachtProtocolImport,
            new SchachtProtocolSingleImportActions(
                ReadProtocolAsync: ReadProtocolAsync,
                ProjectIsStillOpen: ProjectIsStillOpen,
                CollectionLock: _shell.CollectionLock,
                SaveProject: _shell.TrySaveProject,
                SetSelected: record => Selected = record,
                ClearSelectedIfSame: ClearSelectedIfSame,
                SetLastResult: value => LastResult = value));

        var uiLayout = _settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 240d
            ? uiLayout.GridMinRowHeight
            : 38d;
        GridZoom = uiLayout.GridZoom is >= 0.5d and <= 2.0d
            ? uiLayout.GridZoom
            : 1.0d;
        IsColumnReorderEnabled = uiLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(_dropdownOptions.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(_dropdownOptions.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(_dropdownOptions.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(_dropdownOptions.LoadReferenzpruefungOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));
        SchachtformOptions = new ObservableCollection<string>(
            new[] { "Rund", "Oval", "Quadratisch", "Rechteckig" });
        EnforceEigentuemerOptionsExact();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        SaveCommand = new RelayCommand(Save);
        RefreshProtocolCommand = new AsyncRelayCommand(RefreshProtocolAsync, CanRefreshProtocol);
        ImportProtocolCommand = new AsyncRelayCommand(ImportProtocolAsync);
        ErgaenzeStammdatenAusPdfsCommand = new AsyncRelayCommand(
            ErgaenzeStammdatenAusPdfsAsync,
            CanErgaenzeStammdatenAusPdfs);
        CancelStammdatenErgaenzungCommand = new RelayCommand(
            CancelStammdatenErgaenzung,
            () => IsStammdatenErgaenzungInProgress);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        _dropdownCommands = SchaechteDropdownCommandFactory.Create(
            new SchaechteDropdownOptionCollections(
                SanierenOptions,
                EigentuemerOptions,
                PruefungsresultatOptions,
                ReferenzpruefungOptions),
            _dropdownOptions.FixedEigentuemerOptions,
            new DropdownOptionGroupActions(
                OptionsEditorDialogService.Show,
                _dialogs.Info,
                SaveDropdownOptions));

        LoadColumnsFromTemplate();
        EnsureRecordColumns();
        UpdateNr();
        UpdateSearchResultInfo(Records.Count);
    }

    partial void OnSelectedChanged(SchachtRecord? value)
    {
        // Gewaehlten Schacht der QGIS-Bridge melden -> QGIS zoomt auf den Punkt (analog Haltungen).
        QgisBridge.QgisBridgeSelection.SetSchacht(value?.GetFieldValue("Schachtnummer"));
        (RemoveCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RefreshProtocolCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedChanging(SchachtRecord? oldValue, SchachtRecord? newValue)
    {
        if (_suppressRequiredFieldWarning)
            return;
        if (oldValue is null || newValue is null)
            return;
        if (ReferenceEquals(oldValue, newValue) || oldValue.Id == newValue.Id)
            return;

        var missing = SchachtSanierungPflichtfeldValidator.MissingFields(oldValue);
        if (missing.Count == 0)
            return;

        _dialogs.Warn(
            $"Beim Schacht {ResolveSchachtNummer(oldValue)} fehlen:\n- {string.Join("\n- ", missing)}",
            "Schacht-Felder fehlen");
    }

    partial void OnGridMinRowHeightChanged(double value)
    {
        var clamped = Math.Clamp(value, 24d, 240d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
            return;
        }

        PersistSchaechtePageBasicUiSettings();
    }

    partial void OnGridZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.5d, 2.0d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridZoom = clamped;
            return;
        }

        PersistSchaechtePageBasicUiSettings();
    }

    partial void OnIsColumnReorderEnabledChanged(bool value)
    {
        _ = value;
        PersistSchaechtePageBasicUiSettings();
    }

    public void EnsureOptionForField(string optionField, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (optionField == "Sanieren_JaNein")
            AddOptionIfMissing(SanierenOptions, text);
        else if (optionField == "Eigentuemer")
            return;
        else if (optionField == "Pruefungsresultat")
            AddOptionIfMissing(PruefungsresultatOptions, text);
        else if (optionField == "Referenzpruefung")
            AddOptionIfMissing(ReferenzpruefungOptions, text);
        else if (optionField == "Ausgefuehrt_durch")
            AddOptionIfMissing(AusgefuehrtDurchOptions, text);
    }

    private void LoadColumnsFromTemplate()
    {
        Columns.Clear();

        var result = _templateColumnReader.LoadFromExportDirectory(AppContext.BaseDirectory);
        if (!result.TemplateFound)
        {
            LastResult = "Schaechte-Vorlage nicht gefunden.";
            return;
        }

        foreach (var column in result.Columns)
            Columns.Add(column);

        // Schaechte kennen in der Vorlage kein "Ausgefuehrt durch" — fuer die kategorisierte
        // QGIS-Einfaerbung + Auswertung ergaenzen wir es als editierbare Dropdown-Spalte. Die
        // Optionen (Baumeister/Sanierer/Gaertner) stehen ueber AusgefuehrtDurchOptions bereit.
        if (!Columns.Any(c => c.IndexOf("usgef", StringComparison.OrdinalIgnoreCase) >= 0
                           && c.IndexOf("durch", StringComparison.OrdinalIgnoreCase) >= 0))
            Columns.Add("Ausgefuehrt durch");

        EnsureRecordColumns();
        UpdateNr();
        LastResult = $"Spalten geladen: {Columns.Count}";
    }

    private void EnsureRecordColumns()
    {
        foreach (var rec in Records)
        {
            foreach (var col in Columns)
            {
                if (!rec.Fields.ContainsKey(col))
                    rec.Fields[col] = "";
            }
        }
    }

    private void Save()
    {
        var ok = _shell.TrySaveProject();
        LastResult = ok ? "Schaechte gespeichert." : "Speichern fehlgeschlagen.";
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!DropdownOptionList.AddIfMissing(options, value))
            return;
        SaveDropdownOptions();
    }

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SchaechteDropdownOptionSynchronizer.SyncFromRecords(
            Records,
            new SchaechteDropdownOptionSets(
                SanierenOptions,
                PruefungsresultatOptions,
                ReferenzpruefungOptions,
                AusgefuehrtDurchOptions));
        _dropdownOptions.SaveSanierenOptions(SanierenOptions);
        _dropdownOptions.SaveEigentuemerOptions(EigentuemerOptions);
        _dropdownOptions.SavePruefungsresultatOptions(PruefungsresultatOptions);
        _dropdownOptions.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
    }

    public bool MatchesSearch(SchachtRecord record)
        => SchaechteFieldLogic.MatchesSearch(record, SearchText ?? "");

    public void UpdateSearchResultInfo(int visibleCount)
        => SearchResultInfo = SchaechteFieldLogic.BuildSearchResultInfo(visibleCount, Records.Count, SearchText ?? "");

    private void PersistSchaechtePageBasicUiSettings()
    {
        var layout = _settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.GridZoom = GridZoom;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _settings.SchaechtePageLayout = layout;
        _settings.Save();
    }

    private void EnforceEigentuemerOptionsExact()
    {
        DropdownOptionList.EnsureExact(EigentuemerOptions, _dropdownOptions.FixedEigentuemerOptions);
    }

    private static string ResolveSchachtNummer(SchachtRecord record)
    {
        var nummer = record.GetFieldValue("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(nummer))
            return nummer.Trim();

        var nr = record.GetFieldValue("Nr.");
        if (!string.IsNullOrWhiteSpace(nr))
            return nr.Trim();

        nr = record.GetFieldValue("NR.");
        return string.IsNullOrWhiteSpace(nr) ? "(ohne Nummer)" : nr.Trim();
    }
}
