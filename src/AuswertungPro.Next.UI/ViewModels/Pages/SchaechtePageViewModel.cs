using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel : ObservableObject
{
    private readonly ServiceProvider _sp;
    private readonly ShellViewModel _shell;
    private readonly DropdownOptionGroupController _sanierenDropdownOptions;
    private readonly DropdownOptionGroupController _eigentuemerDropdownOptions;
    private readonly DropdownOptionGroupController _pruefungsresultatDropdownOptions;
    private readonly DropdownOptionGroupController _referenzpruefungDropdownOptions;
    private readonly DropdownCommandGroup _sanierenDropdownCommands;
    private readonly DropdownCommandGroup _eigentuemerDropdownCommands;
    private readonly DropdownCommandGroup _pruefungsresultatDropdownCommands;
    private readonly DropdownCommandGroup _referenzpruefungDropdownCommands;

    internal ServiceProvider Services => _sp;

    public ObservableCollection<SchachtRecord> Records => _shell.Project.SchaechteData;
    public ObservableCollection<string> Columns { get; } = new();

    public ObservableCollection<string> SanierenOptions { get; }
    public ObservableCollection<string> EigentuemerOptions { get; }
    public ObservableCollection<string> PruefungsresultatOptions { get; }
    public ObservableCollection<string> ReferenzpruefungOptions { get; }
    public ObservableCollection<string> AusgefuehrtDurchOptions { get; }

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
    {
        _shell = shell;
        _sp = services;

        var uiLayout = _sp.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 240d
            ? uiLayout.GridMinRowHeight
            : 38d;
        GridZoom = uiLayout.GridZoom is >= 0.5d and <= 2.0d
            ? uiLayout.GridZoom
            : 1.0d;
        IsColumnReorderEnabled = uiLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadReferenzpruefungOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));
        EnforceEigentuemerOptionsExact();

        _sanierenDropdownOptions = CreateDropdownOptionGroup(
            SanierenOptions,
            "Sanieren-Liste",
            new[] { "Nein", "Ja" });
        _eigentuemerDropdownOptions = CreateDropdownOptionGroup(
            EigentuemerOptions,
            "Eigentuemer-Liste",
            DropdownOptionsStore.FixedEigentuemerOptions,
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
        _ = value;
        (RemoveCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
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
                _sp.Dialogs.Info,
                SaveDropdownOptions));

    private void LoadColumnsFromTemplate()
    {
        Columns.Clear();

        var templatePath = ResolveTemplatePath();
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            LastResult = "Schaechte-Vorlage nicht gefunden.";
            return;
        }

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Schaechte", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheet(1);

        const int headerRow = 12;
        var lastHeaderCell = ws.Row(headerRow).LastCellUsed();
        var lastCol = lastHeaderCell?.Address.ColumnNumber ?? 1;

        for (var c = 1; c <= lastCol; c++)
        {
            var header = ws.Cell(headerRow, c).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(header) && !Columns.Contains(header))
                Columns.Add(header);
        }

        SwapColumnOrder("Funktion", "Schachtnummer");
        EnsureRecordColumns();
        UpdateNr();
        LastResult = $"Spalten geladen: {Columns.Count}";
    }

    private void SwapColumnOrder(string firstColumnName, string secondColumnName)
    {
        if (Columns.Count == 0)
            return;

        var first = Columns.FirstOrDefault(x => x.Equals(firstColumnName, StringComparison.OrdinalIgnoreCase));
        var second = Columns.FirstOrDefault(x => x.Equals(secondColumnName, StringComparison.OrdinalIgnoreCase));
        if (first is null || second is null)
            return;

        var firstIndex = Columns.IndexOf(first);
        var secondIndex = Columns.IndexOf(second);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            return;

        Columns[firstIndex] = second;
        Columns[secondIndex] = first;
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

        var nrCol = ResolveNrColumnName();

        if (!string.IsNullOrWhiteSpace(nrCol))
            rec.Fields[nrCol] = (Records.Count + 1).ToString();

        Records.Add(rec);
        Selected = rec;
        UpdateSearchResultInfo(Records.Count);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private void Remove()
    {
        if (Selected is null)
            return;

        var idx = Records.IndexOf(Selected);
        if (idx < 0)
            return;

        Records.RemoveAt(idx);
        Selected = idx < Records.Count ? Records[idx] : Records.LastOrDefault();
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

        var idx = Records.IndexOf(Selected);
        if (idx <= 0)
            return;

        Records.Move(idx, idx - 1);
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

        var idx = Records.IndexOf(Selected);
        if (idx < 0 || idx >= Records.Count - 1)
            return;

        Records.Move(idx, idx + 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void Save()
    {
        var ok = _shell.TrySaveProject();
        LastResult = ok ? "Schaechte gespeichert." : "Speichern fehlgeschlagen.";
    }

    private static string ResolveTemplatePath()
    {
        var exportDir = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage");
        if (!Directory.Exists(exportDir))
            return string.Empty;

        var exact = Path.Combine(exportDir, "Schaechte.xlsx");
        if (File.Exists(exact))
            return exact;

        var fallback = Directory
            .GetFiles(exportDir, "*.xlsx")
            .FirstOrDefault(f => Path.GetFileName(f).Contains("ch", StringComparison.OrdinalIgnoreCase) &&
                                 Path.GetFileName(f).Contains("te", StringComparison.OrdinalIgnoreCase));

        return fallback ?? string.Empty;
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!DropdownOptionList.AddIfMissing(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
        => DropdownOptionList.AddIfMissing(options, value);

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        DropdownOptionsStore.SaveSanierenOptions(SanierenOptions);
        DropdownOptionsStore.SaveEigentuemerOptions(EigentuemerOptions);
        DropdownOptionsStore.SavePruefungsresultatOptions(PruefungsresultatOptions);
        DropdownOptionsStore.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
    }

    private void SyncDropdownOptionsFromRecords()
    {
        foreach (var record in Records)
        {
            AddOptionIfMissingCore(SanierenOptions, ResolveFieldValue(record, "sanieren"));
            AddOptionIfMissingCore(PruefungsresultatOptions, ResolveFieldValue(record, "pruefungsresultat"));
            AddOptionIfMissingCore(ReferenzpruefungOptions, ResolveFieldValue(record, "referenzpruefung"));
            AddOptionIfMissingCore(AusgefuehrtDurchOptions, ResolveFieldValue(record, "ausgefuehrt_durch"));
        }
    }

    private static string ResolveFieldValue(SchachtRecord record, string logicalField)
    {
        foreach (var kvp in record.Fields)
        {
            var n = NormalizeKey(kvp.Key);
            if (logicalField == "sanieren" && n.Contains("sanieren", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "pruefungsresultat" &&
                (n.Contains("pruefung", StringComparison.Ordinal) || n.Contains("dichtheit", StringComparison.Ordinal) || n.Contains("dichtigkeit", StringComparison.Ordinal)))
                return kvp.Value ?? "";
            if (logicalField == "referenzpruefung" && n.Contains("referenz", StringComparison.Ordinal) && n.Contains("pruefung", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "ausgefuehrt_durch" &&
                (n.Contains("ausgefuehrt", StringComparison.Ordinal) || n.Contains("ausgefuhrt", StringComparison.Ordinal)) && n.Contains("durch", StringComparison.Ordinal))
                return kvp.Value ?? "";
        }

        return "";
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("Ã¤", "ae", StringComparison.Ordinal)
            .Replace("Ã¶", "oe", StringComparison.Ordinal)
            .Replace("Ã¼", "ue", StringComparison.Ordinal)
            .Replace("ÃŸ", "ss", StringComparison.Ordinal)
            .Replace("ÃƒÂ¤", "ae", StringComparison.Ordinal)
            .Replace("ÃƒÂ¶", "oe", StringComparison.Ordinal)
            .Replace("ÃƒÂ¼", "ue", StringComparison.Ordinal)
            .Replace("ÃƒÅ¸", "ss", StringComparison.Ordinal);
    }

    private string? ResolveNrColumnName()
    {
        var fromColumns = Columns.FirstOrDefault(c =>
            c.Contains("NR", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Nr", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(fromColumns))
            return fromColumns;

        var fromRecord = Records
            .SelectMany(r => r.Fields.Keys)
            .FirstOrDefault(c =>
                c.Contains("NR", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Nr", StringComparison.OrdinalIgnoreCase));
        return fromRecord;
    }

    private void UpdateNr()
    {
        var nrField = ResolveNrColumnName();
        if (string.IsNullOrWhiteSpace(nrField))
            return;

        for (var i = 0; i < Records.Count; i++)
            Records[i].SetFieldValue(nrField, (i + 1).ToString());
    }

    public bool MatchesSearch(SchachtRecord record)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        if (term.Length == 0)
            return true;

        return record.Fields.Any(kvp =>
            (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Key.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(kvp.Value) && kvp.Value.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    public void UpdateSearchResultInfo(int visibleCount)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            SearchResultInfo = string.Empty;
        else
            SearchResultInfo = $"{visibleCount} von {Records.Count} Schaechten";
    }

    private void PersistSchaechtePageBasicUiSettings()
    {
        var layout = _sp.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.GridZoom = GridZoom;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _sp.Settings.SchaechtePageLayout = layout;
        _sp.Settings.Save();
    }

    private void EnforceEigentuemerOptionsExact()
    {
        DropdownOptionList.EnsureExact(EigentuemerOptions, DropdownOptionsStore.FixedEigentuemerOptions);
    }
}
