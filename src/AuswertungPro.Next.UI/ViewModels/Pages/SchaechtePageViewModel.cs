using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel : ObservableObject
{
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly ShellViewModel _shell;

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
    [ObservableProperty] private bool _isColumnReorderEnabled;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ClearSearchCommand { get; }

    public IRelayCommand EditSanierenOptionsCommand { get; }
    public IRelayCommand PreviewSanierenOptionsCommand { get; }
    public IRelayCommand ResetSanierenOptionsCommand { get; }
    public IRelayCommand<object?> AddSanierenOptionCommand { get; }
    public IRelayCommand<object?> RemoveSanierenOptionCommand { get; }

    public IRelayCommand EditEigentuemerOptionsCommand { get; }
    public IRelayCommand PreviewEigentuemerOptionsCommand { get; }
    public IRelayCommand ResetEigentuemerOptionsCommand { get; }
    public IRelayCommand<object?> AddEigentuemerOptionCommand { get; }
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand { get; }

    public IRelayCommand EditPruefungsresultatOptionsCommand { get; }
    public IRelayCommand PreviewPruefungsresultatOptionsCommand { get; }
    public IRelayCommand ResetPruefungsresultatOptionsCommand { get; }
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand { get; }
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand { get; }

    public IRelayCommand EditReferenzpruefungOptionsCommand { get; }
    public IRelayCommand PreviewReferenzpruefungOptionsCommand { get; }
    public IRelayCommand ResetReferenzpruefungOptionsCommand { get; }
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand { get; }
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand { get; }

    public SchaechtePageViewModel(ShellViewModel shell)
    {
        _shell = shell;

        var uiLayout = _sp.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 120d
            ? uiLayout.GridMinRowHeight
            : 38d;
        IsColumnReorderEnabled = uiLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadReferenzpruefungOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));
        EnforceEigentuemerOptionsExact();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        SaveCommand = new RelayCommand(Save);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        EditSanierenOptionsCommand = new RelayCommand(EditSanierenOptions);
        PreviewSanierenOptionsCommand = new RelayCommand(PreviewSanierenOptions);
        ResetSanierenOptionsCommand = new RelayCommand(ResetSanierenOptions);
        AddSanierenOptionCommand = new RelayCommand<object?>(AddSanierenOption);
        RemoveSanierenOptionCommand = new RelayCommand<object?>(RemoveSanierenOption);

        EditEigentuemerOptionsCommand = new RelayCommand(EditEigentuemerOptions);
        PreviewEigentuemerOptionsCommand = new RelayCommand(PreviewEigentuemerOptions);
        ResetEigentuemerOptionsCommand = new RelayCommand(ResetEigentuemerOptions);
        AddEigentuemerOptionCommand = new RelayCommand<object?>(AddEigentuemerOption);
        RemoveEigentuemerOptionCommand = new RelayCommand<object?>(RemoveEigentuemerOption);

        EditPruefungsresultatOptionsCommand = new RelayCommand(EditPruefungsresultatOptions);
        PreviewPruefungsresultatOptionsCommand = new RelayCommand(PreviewPruefungsresultatOptions);
        ResetPruefungsresultatOptionsCommand = new RelayCommand(ResetPruefungsresultatOptions);
        AddPruefungsresultatOptionCommand = new RelayCommand<object?>(AddPruefungsresultatOption);
        RemovePruefungsresultatOptionCommand = new RelayCommand<object?>(RemovePruefungsresultatOption);

        EditReferenzpruefungOptionsCommand = new RelayCommand(EditReferenzpruefungOptions);
        PreviewReferenzpruefungOptionsCommand = new RelayCommand(PreviewReferenzpruefungOptions);
        ResetReferenzpruefungOptionsCommand = new RelayCommand(ResetReferenzpruefungOptions);
        AddReferenzpruefungOptionCommand = new RelayCommand<object?>(AddReferenzpruefungOption);
        RemoveReferenzpruefungOptionCommand = new RelayCommand<object?>(RemoveReferenzpruefungOption);

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
        var clamped = Math.Clamp(value, 24d, 120d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
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

    private void EditSanierenOptions()
    {
        var vm = new OptionsEditorViewModel(SanierenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            SanierenOptions.Clear();
            foreach (var item in vm.Items)
                SanierenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        System.Windows.MessageBox.Show(items, "Sanieren-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetSanierenOptions()
    {
        SanierenOptions.Clear();
        foreach (var item in new[] { "Nein", "Ja" })
            SanierenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddSanierenOption(object? value) => AddOptionIfMissing(SanierenOptions, ExtractText(value));
    private void RemoveSanierenOption(object? value) => RemoveOptionFromList(SanierenOptions, ExtractText(value));

    private void EditEigentuemerOptions()
    {
        var vm = new OptionsEditorViewModel(EigentuemerOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            EigentuemerOptions.Clear();
            foreach (var item in vm.Items)
                EigentuemerOptions.Add(item);
            EnforceEigentuemerOptionsExact();
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        System.Windows.MessageBox.Show(items, "Eigentuemer-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetEigentuemerOptions()
    {
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private void AddEigentuemerOption(object? value)
    {
        _ = value;
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private void RemoveEigentuemerOption(object? value)
    {
        _ = value;
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private void EditPruefungsresultatOptions()
    {
        var vm = new OptionsEditorViewModel(PruefungsresultatOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            PruefungsresultatOptions.Clear();
            foreach (var item in vm.Items)
                PruefungsresultatOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewPruefungsresultatOptions()
    {
        var items = string.Join("\n", PruefungsresultatOptions);
        System.Windows.MessageBox.Show(items, "Pruefungsresultat-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetPruefungsresultatOptions()
    {
        PruefungsresultatOptions.Clear();
        foreach (var item in new[]
                 {
                     "Pruefung bestanden",
                     "Pruefung knapp nicht bestanden",
                     "Pruefung nicht bestanden (grob undicht)",
                     "Keine"
                 })
            PruefungsresultatOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddPruefungsresultatOption(object? value) => AddOptionIfMissing(PruefungsresultatOptions, ExtractText(value));
    private void RemovePruefungsresultatOption(object? value) => RemoveOptionFromList(PruefungsresultatOptions, ExtractText(value));

    private void EditReferenzpruefungOptions()
    {
        var vm = new OptionsEditorViewModel(ReferenzpruefungOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            ReferenzpruefungOptions.Clear();
            foreach (var item in vm.Items)
                ReferenzpruefungOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewReferenzpruefungOptions()
    {
        var items = string.Join("\n", ReferenzpruefungOptions);
        System.Windows.MessageBox.Show(items, "Referenzpruefung-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetReferenzpruefungOptions()
    {
        ReferenzpruefungOptions.Clear();
        foreach (var item in new[] { "Ja", "Nein" })
            ReferenzpruefungOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddReferenzpruefungOption(object? value) => AddOptionIfMissing(ReferenzpruefungOptions, ExtractText(value));
    private void RemoveReferenzpruefungOption(object? value) => RemoveOptionFromList(ReferenzpruefungOptions, ExtractText(value));

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!AddOptionIfMissingCore(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return false;
        if (options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return false;
        options.Insert(0, text);
        return true;
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        var existing = options.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;
        options.Remove(existing);
        SaveDropdownOptions();
    }

    private static string ExtractText(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is System.Windows.Controls.ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

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
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _sp.Settings.SchaechtePageLayout = layout;
        _sp.Settings.Save();
    }

    private void EnforceEigentuemerOptionsExact()
    {
        var same = EigentuemerOptions.Count == FixedEigentuemerOptions.Length;
        if (same)
        {
            for (var i = 0; i < FixedEigentuemerOptions.Length; i++)
            {
                if (!string.Equals(EigentuemerOptions[i], FixedEigentuemerOptions[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
            return;

        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
    }
}
