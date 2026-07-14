using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly ISchachtProtocolImportService _schachtProtocolImport;
    private readonly ISchachtStammdatenErgaenzungsService _schachtStammdatenErgaenzung;
    private readonly ISchachtMassnahmenKatalogStore _schachtMassnahmenKatalog;
    private readonly IDropdownOptionsStore _dropdownOptions;
    private readonly ShellViewModel _shell;
    private readonly DropdownOptionGroupController _sanierenDropdownOptions;
    private readonly DropdownOptionGroupController _eigentuemerDropdownOptions;
    private readonly DropdownOptionGroupController _pruefungsresultatDropdownOptions;
    private readonly DropdownOptionGroupController _referenzpruefungDropdownOptions;
    private readonly DropdownCommandGroup _sanierenDropdownCommands;
    private readonly DropdownCommandGroup _eigentuemerDropdownCommands;
    private readonly DropdownCommandGroup _pruefungsresultatDropdownCommands;
    private readonly DropdownCommandGroup _referenzpruefungDropdownCommands;
    private bool _suppressRequiredFieldWarning;

    internal AppSettings Settings => _settings;
    internal IDialogService Dialogs => _dialogs;
    internal ISchachtMassnahmenKatalogStore SchachtMassnahmenKatalog => _schachtMassnahmenKatalog;

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

    public IRelayCommand EditSanierenOptionsCommand => _sanierenDropdownCommands.Edit;
    public IRelayCommand PreviewSanierenOptionsCommand => _sanierenDropdownCommands.Preview;
    public IRelayCommand ResetSanierenOptionsCommand => _sanierenDropdownCommands.Reset;
    public IRelayCommand<object?> AddSanierenOptionCommand => _sanierenDropdownCommands.Add;
    public IRelayCommand<object?> RemoveSanierenOptionCommand => _sanierenDropdownCommands.Remove;

    public IRelayCommand EditEigentuemerOptionsCommand => _eigentuemerDropdownCommands.Edit;
    public IRelayCommand PreviewEigentuemerOptionsCommand => _eigentuemerDropdownCommands.Preview;
    public IRelayCommand ResetEigentuemerOptionsCommand => _eigentuemerDropdownCommands.Reset;
    public IRelayCommand<object?> AddEigentuemerOptionCommand => _eigentuemerDropdownCommands.Add;
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand => _eigentuemerDropdownCommands.Remove;

    public IRelayCommand EditPruefungsresultatOptionsCommand => _pruefungsresultatDropdownCommands.Edit;
    public IRelayCommand PreviewPruefungsresultatOptionsCommand => _pruefungsresultatDropdownCommands.Preview;
    public IRelayCommand ResetPruefungsresultatOptionsCommand => _pruefungsresultatDropdownCommands.Reset;
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand => _pruefungsresultatDropdownCommands.Add;
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand => _pruefungsresultatDropdownCommands.Remove;

    public IRelayCommand EditReferenzpruefungOptionsCommand => _referenzpruefungDropdownCommands.Edit;
    public IRelayCommand PreviewReferenzpruefungOptionsCommand => _referenzpruefungDropdownCommands.Preview;
    public IRelayCommand ResetReferenzpruefungOptionsCommand => _referenzpruefungDropdownCommands.Reset;
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand => _referenzpruefungDropdownCommands.Add;
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand => _referenzpruefungDropdownCommands.Remove;

    public SchaechtePageViewModel(ShellViewModel shell, ServiceProvider services)
        : this(
            shell,
            settings: services.Settings,
            dialogs: services.Dialogs,
            schachtProtocolImport: services.SchachtProtocolImport,
            schachtStammdatenErgaenzung: services.SchachtStammdatenErgaenzung,
            schachtMassnahmenKatalog: services.SchachtMassnahmenKatalog,
            dropdownOptions: services.DropdownOptions)
    {
    }

    public SchaechtePageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        ISchachtProtocolImportService schachtProtocolImport,
        ISchachtStammdatenErgaenzungsService schachtStammdatenErgaenzung,
        ISchachtMassnahmenKatalogStore schachtMassnahmenKatalog,
        IDropdownOptionsStore? dropdownOptions = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _schachtProtocolImport = schachtProtocolImport ?? throw new ArgumentNullException(nameof(schachtProtocolImport));
        _schachtStammdatenErgaenzung = schachtStammdatenErgaenzung ?? throw new ArgumentNullException(nameof(schachtStammdatenErgaenzung));
        _schachtMassnahmenKatalog = schachtMassnahmenKatalog ?? throw new ArgumentNullException(nameof(schachtMassnahmenKatalog));
        _dropdownOptions = dropdownOptions ?? new FileDropdownOptionsStore();

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

        _sanierenDropdownOptions = CreateDropdownOptionGroup(
            SanierenOptions,
            "Sanieren-Liste",
            new[] { "Nein", "Ja" });
        _eigentuemerDropdownOptions = CreateDropdownOptionGroup(
            EigentuemerOptions,
            "Eigentuemer-Liste",
            _dropdownOptions.FixedEigentuemerOptions,
            lockedToResetItems: true);
        _pruefungsresultatDropdownOptions = CreateDropdownOptionGroup(
            PruefungsresultatOptions,
            "Pruefungsresultat-Liste",
            new[]
            {
                "Pruefung bestanden",
                "Pruefung knapp nicht bestanden",
                "Pruefung nicht bestanden (grob undicht)",
                "Keine"
            });
        _referenzpruefungDropdownOptions = CreateDropdownOptionGroup(
            ReferenzpruefungOptions,
            "Referenzpruefung-Liste",
            new[] { "Ja", "Nein" });

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        SaveCommand = new RelayCommand(Save);
        RefreshProtocolCommand = new RelayCommand(RefreshProtocol, CanRefreshProtocol);
        ImportProtocolCommand = new RelayCommand(ImportProtocol);
        ErgaenzeStammdatenAusPdfsCommand = new AsyncRelayCommand(
            ErgaenzeStammdatenAusPdfsAsync,
            CanErgaenzeStammdatenAusPdfs);
        CancelStammdatenErgaenzungCommand = new RelayCommand(
            CancelStammdatenErgaenzung,
            () => IsStammdatenErgaenzungInProgress);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        _sanierenDropdownCommands = DropdownCommandFactory.Create(new DropdownCommandActions(
            _sanierenDropdownOptions.Edit,
            _sanierenDropdownOptions.Preview,
            _sanierenDropdownOptions.Reset,
            _sanierenDropdownOptions.Add,
            _sanierenDropdownOptions.Remove));
        _eigentuemerDropdownCommands = DropdownCommandFactory.Create(new DropdownCommandActions(
            _eigentuemerDropdownOptions.Edit,
            _eigentuemerDropdownOptions.Preview,
            _eigentuemerDropdownOptions.Reset,
            _eigentuemerDropdownOptions.Add,
            _eigentuemerDropdownOptions.Remove));
        _pruefungsresultatDropdownCommands = DropdownCommandFactory.Create(new DropdownCommandActions(
            _pruefungsresultatDropdownOptions.Edit,
            _pruefungsresultatDropdownOptions.Preview,
            _pruefungsresultatDropdownOptions.Reset,
            _pruefungsresultatDropdownOptions.Add,
            _pruefungsresultatDropdownOptions.Remove));
        _referenzpruefungDropdownCommands = DropdownCommandFactory.Create(new DropdownCommandActions(
            _referenzpruefungDropdownOptions.Edit,
            _referenzpruefungDropdownOptions.Preview,
            _referenzpruefungDropdownOptions.Reset,
            _referenzpruefungDropdownOptions.Add,
            _referenzpruefungDropdownOptions.Remove));

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
        (RefreshProtocolCommand as RelayCommand)?.NotifyCanExecuteChanged();
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

    private DropdownOptionGroupController CreateDropdownOptionGroup(
        ObservableCollection<string> options,
        string previewTitle,
        IReadOnlyList<string> resetItems,
        bool lockedToResetItems = false)
        => new(
            options,
            new DropdownOptionGroupSettings(previewTitle, resetItems, lockedToResetItems),
            new DropdownOptionGroupActions(
                OptionsEditorDialogService.Show,
                _dialogs.Info,
                SaveDropdownOptions));

    private void LoadColumnsFromTemplate()
    {
        Columns.Clear();

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(AppContext.BaseDirectory);
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

    private void Add()
    {
        var rec = new SchachtRecord();
        foreach (var col in Columns)
            rec.Fields[col] = "";

        var nrCol = SchaechteFieldLogic.ResolveNrColumnName(Columns, Records);

        if (!string.IsNullOrWhiteSpace(nrCol))
            rec.Fields[nrCol] = (Records.Count + 1).ToString();

        // WPF-Sync-Vertrag: SchaechteData nutzt EnableCollectionSynchronization —
        // JEDE Mutation (auch vom UI-Thread) muss den gemeinsamen Lock halten.
        lock (_shell.CollectionLock)
        {
            Records.Add(rec);
        }
        SetSelectedWithoutRequiredFieldWarning(rec);
        UpdateSearchResultInfo(Records.Count);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private void Remove()
    {
        if (Selected is null)
            return;

        SchachtRecord? neueAuswahl;
        lock (_shell.CollectionLock)
        {
            var idx = Records.IndexOf(Selected);
            if (idx < 0)
                return;

            Records.RemoveAt(idx);
            neueAuswahl = idx < Records.Count ? Records[idx] : Records.LastOrDefault();
        }

        SetSelectedWithoutRequiredFieldWarning(neueAuswahl);
        UpdateNr();
        UpdateSearchResultInfo(Records.Count);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private bool CanMoveUp()
    {
        if (Selected is null)
            return false;

        var idx = Records.IndexOf(Selected);
        return idx > 0;
    }

    private bool CanMoveDown()
    {
        if (Selected is null)
            return false;

        var idx = Records.IndexOf(Selected);
        return idx >= 0 && idx < Records.Count - 1;
    }

    private void MoveUp()
    {
        if (Selected is null)
            return;

        lock (_shell.CollectionLock)
        {
            var idx = Records.IndexOf(Selected);
            if (idx <= 0)
                return;

            Records.Move(idx, idx - 1);
        }
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void MoveDown()
    {
        if (Selected is null)
            return;

        lock (_shell.CollectionLock)
        {
            var idx = Records.IndexOf(Selected);
            if (idx < 0 || idx >= Records.Count - 1)
                return;

            Records.Move(idx, idx + 1);
        }
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Verschiebt den ausgewaehlten Schacht auf die angegebene 1-basierte Position
    /// (analog zur Haltungsansicht). Liefert false, wenn nichts ausgewaehlt ist oder
    /// der Zug ins Leere laeuft. Renummeriert danach ueber <see cref="UpdateNr"/>.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        if (Selected is null)
            return false;

        lock (_shell.CollectionLock)
        {
            var oldIndex = Records.IndexOf(Selected);
            if (!RecordMovePositionCalculator.TryResolveTargetIndex(
                    oldIndex, Records.Count, targetPosition, out var targetIndex))
                return false;

            Records.Move(oldIndex, targetIndex);
        }
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
        return true;
    }

    private void Save()
    {
        var ok = _shell.TrySaveProject();
        LastResult = ok ? "Schaechte gespeichert." : "Speichern fehlgeschlagen.";
    }

    // "Aktualisieren": verknuepftes Protokoll neu einlesen -> Schacht komplett neu aufbauen (mit Warnung).
    private bool CanRefreshProtocol()
        => Selected is not null && !string.IsNullOrWhiteSpace(Selected.GetFieldValue("PDF_Path"));

    private void RefreshProtocol()
    {
        var schacht = Selected;
        if (schacht is null)
            return;

        var relPath = schacht.GetFieldValue("PDF_Path");
        if (string.IsNullOrWhiteSpace(relPath))
            return;

        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", "Aktualisieren");
            return;
        }

        if (!_dialogs.ConfirmWarn(
                "Der Schacht wird komplett aus dem Protokoll neu aufgebaut. Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                "Aktualisieren"))
            return;

        var absPath = ProjectPathResolver.ResolveFilePathFromProjectFolder(relPath, projektOrdner);
        if (absPath is null)
        {
            _dialogs.Warn("Die verknuepfte Protokoll-Datei wurde nicht gefunden.", "Aktualisieren");
            return;
        }

        var ergebnis = _schachtProtocolImport.Parse(absPath);
        if (!ergebnis.IstSchachtprotokoll || string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _dialogs.Warn("Das verknuepfte PDF ist kein lesbares Schachtprotokoll.", "Aktualisieren");
            return;
        }

        // Relativen Pfad behalten (Datei liegt bereits im Projekt).
        _schachtProtocolImport.Apply(schacht, ergebnis, relPath);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Schacht {ergebnis.Schachtnummer} aktualisiert ({ergebnis.Schaeden.Count} Beobachtungen).";
    }

    // "Protokoll importieren": einzelnes PDF waehlen -> bei Kollision nachfragen -> verteilen + anwenden.
    private void ImportProtocol()
    {
        var projektOrdner = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projektOrdner))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", "Protokoll importieren");
            return;
        }

        var pdfPfad = _dialogs.OpenFile("Protokoll importieren", "PDF (*.pdf)|*.pdf");
        if (string.IsNullOrWhiteSpace(pdfPfad))
            return;

        var ergebnis = _schachtProtocolImport.Parse(pdfPfad);
        if (!ergebnis.IstSchachtprotokoll)
        {
            _dialogs.Warn("Das gewaehlte PDF ist kein Schachtprotokoll.", "Protokoll importieren");
            return;
        }
        if (string.IsNullOrWhiteSpace(ergebnis.Schachtnummer))
        {
            _dialogs.Warn("Im Protokoll wurde keine Schachtnummer gefunden.", "Protokoll importieren");
            return;
        }

        var vorhanden = _schachtProtocolImport.FindSchacht(_shell.Project, ergebnis.Schachtnummer);
        SchachtRecord ziel;
        if (vorhanden is not null)
        {
            var wahl = _dialogs.ConfirmCancel(
                $"Schacht {ergebnis.Schachtnummer} ist bereits vorhanden.\n\n" +
                "Ja = Ueberschreiben\nNein = Als neuen Schacht anlegen\nAbbrechen = Nichts tun",
                "Protokoll importieren");

            if (wahl == DialogConfirm.Cancel)
                return;
            if (wahl == DialogConfirm.Yes)
            {
                ziel = vorhanden;
            }
            else
            {
                ziel = new SchachtRecord();
                lock (_shell.CollectionLock)
                {
                    Records.Add(ziel);
                }
            }
        }
        else
        {
            ziel = new SchachtRecord();
            lock (_shell.CollectionLock)
            {
                Records.Add(ziel);
            }
        }

        var relPath = _schachtProtocolImport.DistributePdf(projektOrdner, ergebnis.Schachtnummer, pdfPfad);
        _schachtProtocolImport.Apply(ziel, ergebnis, relPath);
        Selected = ziel;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        _shell.TrySaveProject();
        LastResult = $"Protokoll importiert: Schacht {ergebnis.Schachtnummer} ({ergebnis.Schaeden.Count} Beobachtungen).";
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

    private void UpdateNr()
    {
        var nrField = SchaechteFieldLogic.ResolveNrColumnName(Columns, Records);
        if (string.IsNullOrWhiteSpace(nrField))
            return;

        for (var i = 0; i < Records.Count; i++)
            Records[i].SetFieldValue(nrField, (i + 1).ToString());
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

    private void SetSelectedWithoutRequiredFieldWarning(SchachtRecord? record)
    {
        _suppressRequiredFieldWarning = true;
        try
        {
            Selected = record;
        }
        finally
        {
            _suppressRequiredFieldWarning = false;
        }
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
