using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SchaechtePage : UserControl
{
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

    private sealed class ComboBindingTag
    {
        public ComboBindingTag(string recordField, string optionField)
        {
            RecordField = recordField;
            OptionField = optionField;
        }

        public string RecordField { get; }
        public string OptionField { get; }
    }

    private sealed class DropdownColumnSpec
    {
        public DropdownColumnSpec(
            string optionField,
            string itemsSourcePath,
            bool allowFreeText,
            bool managed,
            string editCommand = "",
            string previewCommand = "",
            string resetCommand = "",
            string removeCommand = "",
            string addCommand = "")
        {
            OptionField = optionField;
            ItemsSourcePath = itemsSourcePath;
            AllowFreeText = allowFreeText;
            Managed = managed;
            EditCommand = editCommand;
            PreviewCommand = previewCommand;
            ResetCommand = resetCommand;
            RemoveCommand = removeCommand;
            AddCommand = addCommand;
        }

        public string OptionField { get; }
        public string ItemsSourcePath { get; }
        public bool AllowFreeText { get; }
        public bool Managed { get; }
        public string EditCommand { get; }
        public string PreviewCommand { get; }
        public string ResetCommand { get; }
        public string RemoveCommand { get; }
        public string AddCommand { get; }
    }

    private sealed class HorizontalAlignmentToTextAlignmentValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is HorizontalAlignment horizontal)
            {
                return horizontal switch
                {
                    HorizontalAlignment.Center => TextAlignment.Center,
                    HorizontalAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left
                };
            }

            return TextAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
    }

    private static readonly IValueConverter CostDisplayConverter = new ChfAccountingDisplayConverter();
    private static readonly IValueConverter HorizontalAlignmentToTextAlignmentConverter = new HorizontalAlignmentToTextAlignmentValueConverter();
    private static readonly Regex NonNumericRegex = new("[^0-9]", RegexOptions.Compiled);

    private SchaechtePageViewModel? _vm;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _layoutSaveDebounceTimer;
    private readonly Dictionary<DataGridColumn, HorizontalAlignment> _columnHorizontalAlignments = new();
    private readonly Dictionary<DataGridColumn, VerticalAlignment> _columnVerticalAlignments = new();
    private readonly Dictionary<DataGridColumn, Style?> _baseCellStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextElementStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextEditingStyles = new();
    private bool _updatingAlignmentButtons;
    private bool _isRestoringLayout;
    private DataGridColumn? _activeColumn;

    public SchaechtePage()
    {
        InitializeComponent();

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchDebounceTimer.Tick += (_, __) =>
        {
            _searchDebounceTimer.Stop();
            ApplySearchFilter();
        };

        _layoutSaveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _layoutSaveDebounceTimer.Tick += (_, __) =>
        {
            _layoutSaveDebounceTimer.Stop();
            SaveLayoutToSettings();
        };

        DataContextChanged += OnDataContextChanged;
        Grid.AddHandler(DataGridColumnHeader.ClickEvent, new RoutedEventHandler(Grid_ColumnHeaderClick), true);
        Grid.ColumnReordered += Grid_ColumnReordered;

        Loaded += (_, __) =>
        {
            UpdateAlignmentButtonsForCurrentColumn();
            ApplySearchFilter();
        };
        Unloaded += (_, __) =>
        {
            _layoutSaveDebounceTimer.Stop();
            SaveLayoutToSettings();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;

        if (_vm is not null)
        {
            _vm.Columns.CollectionChanged -= ColumnsChanged;
            _vm.Records.CollectionChanged -= RecordsChanged;
            foreach (var record in _vm.Records)
                record.PropertyChanged -= RecordPropertyChanged;
        }

        _vm = e.NewValue as SchaechtePageViewModel;
        if (_vm is null)
            return;

        _vm.Columns.CollectionChanged += ColumnsChanged;
        _vm.Records.CollectionChanged += RecordsChanged;
        foreach (var record in _vm.Records)
            record.PropertyChanged += RecordPropertyChanged;

        RebuildColumns();
        ApplySearchFilter();
    }

    private void ColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RebuildColumns();
    }

    private void RecordsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (_vm is null)
            return;

        if (e.OldItems is not null)
        {
            foreach (var record in e.OldItems.OfType<SchachtRecord>())
                record.PropertyChanged -= RecordPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var record in e.NewItems.OfType<SchachtRecord>())
                record.PropertyChanged += RecordPropertyChanged;
        }

        ApplySearchFilter();
    }

    private void RebuildColumns()
    {
        if (_vm is null)
            return;

        Grid.Columns.Clear();
        _columnHorizontalAlignments.Clear();
        _columnVerticalAlignments.Clear();
        _baseCellStyles.Clear();
        _baseTextElementStyles.Clear();
        _baseTextEditingStyles.Clear();
        _activeColumn = null;

        _isRestoringLayout = true;
        try
        {
            foreach (var col in _vm.Columns)
            {
                DataGridColumn column;
                if (IsCostColumn(col))
                {
                    column = CreateCostColumn(col);
                }
                else if (IsZustandsklasseColumn(col))
                {
                    column = CreateZustandsklasseColumn(col);
                }
                else if (TryResolveDropdownColumnSpec(col, out var spec))
                {
                    column = spec.Managed
                        ? CreateManagedComboColumn(
                            col,
                            spec.OptionField,
                            spec.ItemsSourcePath,
                            spec.EditCommand,
                            spec.PreviewCommand,
                            spec.ResetCommand,
                            spec.RemoveCommand,
                            spec.AddCommand,
                            spec.AllowFreeText)
                        : CreateSimpleComboColumn(
                            col,
                            spec.OptionField,
                            spec.ItemsSourcePath,
                            spec.AllowFreeText);
                }
                else
                {
                    column = new DataGridTextColumn
                    {
                        Header = GetDisplayHeader(col),
                        Binding = new Binding($"Fields[{col}]")
                        {
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                        },
                        Width = DataGridLength.SizeToHeader,
                        MinWidth = 90
                    };
                }

                column.Header = GetDisplayHeader(col);
                column.SetValue(FrameworkElement.TagProperty, col);
                ApplyColorStyle(column, col);
                column.MinWidth = 90;
                Grid.Columns.Add(column);

                var defaultHorizontal = IsCostColumn(col)
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
                _columnHorizontalAlignments[column] = defaultHorizontal;
                _columnVerticalAlignments[column] = VerticalAlignment.Center;
                ApplyColumnAlignment(column, defaultHorizontal, VerticalAlignment.Center);
                AttachColumnLayoutChangeHandlers(column);
            }
        }
        finally
        {
            _isRestoringLayout = false;
        }

        Grid.FrozenColumnCount = Math.Min(2, Grid.Columns.Count);
        RestoreLayoutFromSettings();
        UpdateAlignmentButtonsForCurrentColumn();
        ApplySearchFilter();
    }


    private void Grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (Grid.SelectedCells.Count > 0)
            _activeColumn = Grid.SelectedCells[0].Column;

        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_CurrentCellChanged(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (Grid.CurrentCell.Column is not null)
            _activeColumn = Grid.CurrentCell.Column;

        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _ = sender;

        if (e.OriginalSource is not DependencyObject dep)
            return;

        var header = FindAncestor<DataGridColumnHeader>(dep);
        if (header?.Column is null)
            return;

        _activeColumn = header.Column;
        TrySetCurrentCellForColumn(_activeColumn);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void Grid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        _ = sender;
        _ = e;
        QueueLayoutSave();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void ApplySearchFilter()
    {
        if (DataContext is not SchaechtePageViewModel vm)
            return;

        var view = CollectionViewSource.GetDefaultView(Grid.ItemsSource);
        if (view is null)
            return;

        if (view is IEditableCollectionView editableView && (editableView.IsAddingNew || editableView.IsEditingItem))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplySearchFilter));
            return;
        }

        if (string.IsNullOrWhiteSpace(vm.SearchText))
        {
            using (view.DeferRefresh())
                view.Filter = null;
            vm.UpdateSearchResultInfo(vm.Records.Count);
        }
        else
        {
            using (view.DeferRefresh())
                view.Filter = obj => obj is SchachtRecord rec && vm.MatchesSearch(rec);
            var count = view.Cast<object>().Count();
            vm.UpdateSearchResultInfo(count);
        }
    }

    private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (DataContext is not SchaechtePageViewModel vm)
            return;

        const double step = 0.05d;
        var delta = e.Delta > 0 ? step : -step;
        var next = Math.Clamp(vm.GridZoom + delta, 0.5d, 2.0d);
        if (Math.Abs(next - vm.GridZoom) < 0.001d)
            return;

        vm.GridZoom = next;
        e.Handled = true;
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = e;
        CommitComboBoxValue(sender as ComboBox);
    }

    private void ComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = e;
        CommitComboBoxValue(sender as ComboBox);
    }

    private void CommitComboBoxValue(ComboBox? combo)
    {
        if (combo?.Tag is not ComboBindingTag tag)
            return;

        if (DataContext is not SchaechtePageViewModel vm)
            return;

        var record = ResolveRecordFromComboBox(combo);
        if (record is null)
            return;

        var value = ResolveComboBoxValue(combo);
        if (string.IsNullOrWhiteSpace(value))
            return;

        record.SetFieldValue(tag.RecordField, value);
        vm.EnsureOptionForField(tag.OptionField, value);
        MarkProjectDirty();
        ApplySearchFilter();
    }

    private SchachtRecord? ResolveRecordFromComboBox(ComboBox combo)
    {
        if (combo.DataContext is SchachtRecord direct)
            return direct;

        var row = FindAncestor<DataGridRow>(combo);
        if (row?.Item is SchachtRecord fromRow)
            return fromRow;

        return Grid.CurrentItem as SchachtRecord;
    }

    private static string ResolveComboBoxValue(ComboBox combo)
    {
        if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            return selected;

        return combo.Text ?? string.Empty;
    }

    private static bool TryResolveDropdownColumnSpec(string columnName, out DropdownColumnSpec spec)
    {
        var optionField = ResolveOptionField(columnName);
        if (optionField is null)
        {
            spec = null!;
            return false;
        }

        if (optionField == "Sanieren_JaNein")
        {
            spec = new DropdownColumnSpec(
                optionField,
                "SanierenOptions",
                allowFreeText: true,
                managed: true,
                "EditSanierenOptionsCommand",
                "PreviewSanierenOptionsCommand",
                "ResetSanierenOptionsCommand",
                "RemoveSanierenOptionCommand",
                "AddSanierenOptionCommand");
            return true;
        }

        if (optionField == "Eigentuemer")
        {
            spec = new DropdownColumnSpec(
                optionField,
                "EigentuemerOptions",
                allowFreeText: false,
                managed: true,
                "EditEigentuemerOptionsCommand",
                "PreviewEigentuemerOptionsCommand",
                "ResetEigentuemerOptionsCommand",
                "RemoveEigentuemerOptionCommand",
                "AddEigentuemerOptionCommand");
            return true;
        }

        if (optionField == "Pruefungsresultat")
        {
            spec = new DropdownColumnSpec(
                optionField,
                "PruefungsresultatOptions",
                allowFreeText: true,
                managed: true,
                "EditPruefungsresultatOptionsCommand",
                "PreviewPruefungsresultatOptionsCommand",
                "ResetPruefungsresultatOptionsCommand",
                "RemovePruefungsresultatOptionCommand",
                "AddPruefungsresultatOptionCommand");
            return true;
        }

        if (optionField == "Referenzpruefung")
        {
            spec = new DropdownColumnSpec(
                optionField,
                "ReferenzpruefungOptions",
                allowFreeText: true,
                managed: true,
                "EditReferenzpruefungOptionsCommand",
                "PreviewReferenzpruefungOptionsCommand",
                "ResetReferenzpruefungOptionsCommand",
                "RemoveReferenzpruefungOptionCommand",
                "AddReferenzpruefungOptionCommand");
            return true;
        }

        if (optionField == "Ausgefuehrt_durch")
        {
            spec = new DropdownColumnSpec(
                optionField,
                "AusgefuehrtDurchOptions",
                allowFreeText: true,
                managed: false);
            return true;
        }

        spec = null!;
        return false;
    }

    private static string? ResolveOptionField(string columnName)
    {
        var normalized = Normalize(columnName);

        if ((normalized.Contains("ausgefuehrt", StringComparison.Ordinal) || normalized.Contains("ausgefuhrt", StringComparison.Ordinal)) &&
            normalized.Contains("durch", StringComparison.Ordinal))
            return "Ausgefuehrt_durch";

        if (normalized.Contains("eigentuemer", StringComparison.Ordinal) ||
            normalized.Contains("eigentumer", StringComparison.Ordinal) ||
            normalized.Contains("eigentum", StringComparison.Ordinal))
            return "Eigentuemer";

        if (normalized.Contains("referenz", StringComparison.Ordinal) && normalized.Contains("pruefung", StringComparison.Ordinal))
            return "Referenzpruefung";

        var compact = normalized
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Trim();
        while (compact.Contains("  ", StringComparison.Ordinal))
            compact = compact.Replace("  ", " ", StringComparison.Ordinal);
        if (compact.Equals("ja nein", StringComparison.Ordinal))
            return "Sanieren_JaNein";

        if (normalized.Contains("sanieren", StringComparison.Ordinal) ||
            (normalized.Contains("sanierung", StringComparison.Ordinal) && normalized.Contains("ja", StringComparison.Ordinal)))
            return "Sanieren_JaNein";

        if (normalized.Contains("pruefung", StringComparison.Ordinal) ||
            normalized.Contains("dichtheit", StringComparison.Ordinal) ||
            normalized.Contains("dichtigkeit", StringComparison.Ordinal))
            return "Pruefungsresultat";

        return null;
    }

    private static string GetDisplayHeader(string columnName)
    {
        var optionField = ResolveOptionField(columnName);
        return string.Equals(optionField, "Sanieren_JaNein", StringComparison.Ordinal)
            ? "Sanieren Ja/Nein"
            : columnName;
    }

    private static bool IsCostColumn(string columnName)
        => Normalize(columnName).Contains("kosten", StringComparison.Ordinal);

    private static bool IsZustandsklasseColumn(string columnName)
        => Normalize(columnName).Contains("zustandsklasse", StringComparison.Ordinal);

    private void ZustandsklasseTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _ = sender;
        e.Handled = NonNumericRegex.IsMatch(e.Text ?? string.Empty);
    }

    private void ZustandsklasseTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        _ = sender;
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (NonNumericRegex.IsMatch(text))
            e.CancelCommand();
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        _ = sender;

        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Row?.Item is not SchachtRecord record)
            return;
        if (e.Column.GetValue(FrameworkElement.TagProperty) is not string recordField)
            return;

        if (IsCostColumn(recordField))
        {
            MarkProjectDirty();
            ApplySearchFilter();
            return;
        }

        if (!TryGetEditedTextValue(e.EditingElement, out var value))
            return;

        string? oldShaftNumber = null;
        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
            oldShaftNumber = record.GetFieldValue("Schachtnummer");

        record.SetFieldValue(recordField, value);

        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
            PdfCorrectionMetadata.RegisterShaftRename(GetCurrentProject(), oldShaftNumber, value);

        if (_vm is not null)
        {
            var optionField = ResolveOptionField(recordField);
            if (!string.IsNullOrWhiteSpace(optionField))
                _vm.EnsureOptionForField(optionField, value);
        }

        MarkProjectDirty();
        ApplySearchFilter();
    }

    private void Grid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        if (e.OriginalSource is not DependencyObject source)
            return;

        var cell = FindAncestor<DataGridCell>(source);
        if (cell is null)
            return;

        if (cell.Column?.GetValue(FrameworkElement.TagProperty) is not string fieldName)
            return;

        var row = FindAncestor<DataGridRow>(cell);
        if (row?.Item is not SchachtRecord record)
            return;

        if (IsDetailsNameColumn(fieldName))
        {
            ShowRecordDetails(record);
            e.Handled = true;
            return;
        }

        if (!IsPrimaryDamagesColumn(fieldName))
            return;

        var content = record.GetFieldValue(fieldName);
        if (string.IsNullOrWhiteSpace(content))
            return;

        var schacht = GetSchachtNumber(record);
        var title = string.IsNullOrWhiteSpace(schacht)
            ? "Primaere Schaeden"
            : $"Primaere Schaeden - Schacht {schacht}";

        ShowTextPreview(title, content);
        e.Handled = true;
    }

    private void RecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (string.IsNullOrWhiteSpace(e.PropertyName) || e.PropertyName.StartsWith("Fields[", StringComparison.Ordinal))
            MarkProjectDirty();
    }

    private void MarkProjectDirty()
    {
        if (_vm is null)
            return;

        var project = GetCurrentProject();
        if (project is null)
            return;

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
    }

    private static Project? GetCurrentProject()
        => ((ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;

    private static bool IsPrimaryDamagesColumn(string header)
    {
        var n = Normalize(header);
        return n.Contains("primaere", StringComparison.Ordinal) && n.Contains("schaeden", StringComparison.Ordinal);
    }

    private static bool IsDetailsNameColumn(string header)
    {
        var normalized = Normalize(header);
        return normalized.Contains("schacht", StringComparison.Ordinal)
               && (normalized.Contains("name", StringComparison.Ordinal)
                   || normalized.Contains("nummer", StringComparison.Ordinal));
    }

    private static string GetSchachtNumber(SchachtRecord record)
    {
        var byName = record.GetFieldValue("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(byName))
            return byName.Trim();

        var byNr = record.GetFieldValue("Nr.");
        if (!string.IsNullOrWhiteSpace(byNr))
            return byNr.Trim();

        var byNR = record.GetFieldValue("NR.");
        return byNR?.Trim() ?? "";
    }

    private static string Normalize(string value)
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

    private void ShowTextPreview(string title, string content)
    {
        var owner = Window.GetWindow(this);
        var win = new TextPreviewWindow(title, content)
        {
            Owner = owner
        };
        win.Show();
    }


    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ClearColumnModeButton.IsChecked == true)
        {
            var header = FindAncestor<DataGridColumnHeader>((DependencyObject)e.OriginalSource);
            if (header?.Column is not null)
            {
                var fieldName = header.Column.GetValue(FrameworkElement.TagProperty) as string;
                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    var displayName = header.Column.Header?.ToString() ?? fieldName;
                    ClearColumn(fieldName, displayName);
                    e.Handled = true;
                    return;
                }
            }
        }

        var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row is not null)
            Grid.SelectedItem = row.Item;
    }

    private void ClearColumn(string fieldName, string displayName)
    {
        if (_vm is null)
            return;

        var result = _dialogs.ShowMessage(
            $"Alle Werte in Spalte \"{displayName}\" loeschen?",
            "Spalte leeren",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        foreach (var record in _vm.Records)
            record.SetFieldValue(fieldName, string.Empty);

        MarkProjectDirty();
    }

    private void ProtokollMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var record = _vm.Selected;
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile ausgewaehlt. Bitte direkt auf eine Zeile rechtsklicken.", "Protokoll",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pdfPath = ResolvePdfPath(record);
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            var schacht = GetSchachtNumber(record);
            _dialogs.ShowMessage(
                string.IsNullOrWhiteSpace(schacht)
                    ? "Kein Schachtprotokoll-PDF verknuepft."
                    : $"Kein Schachtprotokoll-PDF verknuepft fuer Schacht {schacht}.",
                "Protokoll", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!AuswertungPro.Next.Application.Common.ProcessRunner.TryOpenWithDefaultProgram(pdfPath, out var openErr))
        {
            _dialogs.ShowMessage($"PDF konnte nicht geoeffnet werden:\n{openErr}", "Protokoll",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DetailsMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;

        if (_vm is null)
            return;

        var record = _vm.Selected;
        if (record is null)
        {
            _dialogs.ShowMessage("Keine Zeile ausgewaehlt. Bitte direkt auf eine Zeile rechtsklicken.", "Details",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowRecordDetails(record);
    }

    private static string? ResolvePdfPath(SchachtRecord record)
    {
        var pdfField = record.GetFieldValue("PDF_Path");
        if (!string.IsNullOrWhiteSpace(pdfField))
        {
            if (System.IO.File.Exists(pdfField))
                return pdfField;

            // Phase 5.1.B Etappe 3.F: via DI-Container.
            AppSettings? settings = null;
            try { settings = App.Resolve<AppSettings>(); } catch { }
            var resolved = TryResolveRelativePath(pdfField, settings?.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        var link = record.GetFieldValue("Link");
        if (!string.IsNullOrWhiteSpace(link) &&
            link.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (System.IO.File.Exists(link))
                return link;

            // Phase 5.1.B Etappe 3.F: via DI-Container.
            AppSettings? linkSettings = null;
            try { linkSettings = App.Resolve<AppSettings>(); } catch { }
            var resolved = TryResolveRelativePath(link, linkSettings?.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return null;
    }

    private static string? TryResolveRelativePath(string? raw, string? lastProjectPath)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (System.IO.File.Exists(path))
            return path;
        if (!System.IO.Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(lastProjectPath))
        {
            var baseDir = System.IO.Path.GetDirectoryName(lastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, path));
                if (System.IO.File.Exists(combined))
                    return combined;
            }
        }
        return null;
    }
}
