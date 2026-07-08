using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SchaechtePage : UserControl
{
    private SchaechtePageViewModel Vm => DataContext as SchaechtePageViewModel
        ?? throw new InvalidOperationException("SchaechtePage benoetigt SchaechtePageViewModel als DataContext.");
    private ServiceProvider Services => Vm.Services;

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

    private SchaechtePageViewModel? _vm;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _layoutSaveDebounceTimer;
    private readonly DataGridColumnLayoutController _columnLayoutController = new();
    private readonly DataGridColumnAlignmentToolbar _columnAlignmentToolbar;
    private readonly SchaechtePageSubscriptionController _subscriptionController;
    private bool _isRestoringLayout;

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
        _columnLayoutController.LayoutChanged += (_, __) => QueueLayoutSave();
        _columnAlignmentToolbar = new DataGridColumnAlignmentToolbar(
            Grid,
            _columnLayoutController,
            new DataGridColumnAlignmentButtons(
                AlignLeftButton,
                AlignCenterButton,
                AlignRightButton,
                AlignTopButton,
                AlignMiddleButton,
                AlignBottomButton));
        _subscriptionController = new SchaechtePageSubscriptionController(
            RebuildColumns,
            ApplySearchFilter,
            RecordPropertyChanged);

        SchachtansichtView.DetailBuilder = BuildRecordDetailsForAnsicht;
        SchachtansichtView.DamageLineBuilder = SchachtDamageLineBuilder.Build;
        SchachtansichtView.ActionRequested = RouteSchachtansichtAction;
        SchachtansichtToggle.IsChecked = true;
        SchachtansichtView.Visibility = Visibility.Visible;
        Grid.Visibility = Visibility.Collapsed;

        DataContextChanged += OnDataContextChanged;
        Grid.AddHandler(DataGridColumnHeader.ClickEvent, new RoutedEventHandler(Grid_ColumnHeaderClick), true);
        Grid.ColumnReordered += Grid_ColumnReordered;

        Loaded += (_, __) =>
        {
            _columnAlignmentToolbar.UpdateButtons();
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

        _vm = e.NewValue as SchaechtePageViewModel;
        if (_vm is null)
        {
            _subscriptionController.Detach();
            SchachtansichtView.Settings = null;
            return;
        }

        SchachtansichtView.Settings = _vm.Services.Settings;
        _subscriptionController.Switch(_vm.Columns, _vm.Records, () => _vm.Records);
    }

    private void RebuildColumns()
    {
        if (_vm is null)
            return;

        Grid.Columns.Clear();
        _columnLayoutController.Clear();
        _columnAlignmentToolbar.ClearActiveColumn();

        _isRestoringLayout = true;
        try
        {
            foreach (var col in _vm.Columns)
            {
                DataGridColumn column;
                if (IsCostColumn(col))
                {
                    column = DataGridCostColumnFactory.Create(col, col);
                }
                else if (IsZustandsklasseColumn(col))
                {
                    column = CreateZustandsklasseColumn(col);
                }
                else if (TryResolveDropdownColumnSpec(col, out var spec))
                {
                    column = DataGridComboColumnFactory.Create(
                        col,
                        col,
                        spec.ItemsSourcePath,
                        tag: new ComboBindingTag(col, spec.OptionField),
                        lostKeyboardFocus: ComboBox_LostKeyboardFocus,
                        selectionChanged: ComboBox_SelectionChanged,
                        allowFreeText: spec.AllowFreeText,
                        bindIsProjectReady: false,
                        menuCommands: spec.Managed
                            ? new DataGridComboColumnMenuCommands(
                                spec.EditCommand,
                                spec.PreviewCommand,
                                spec.ResetCommand,
                                spec.RemoveCommand,
                                spec.AddCommand)
                            : null,
                        useSelectedItemWhenNotFreeText: spec.Managed);
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
                _columnAlignmentToolbar.SetAlignment(column, defaultHorizontal, VerticalAlignment.Center);
            }
        }
        finally
        {
            _isRestoringLayout = false;
        }

        Grid.FrozenColumnCount = Math.Min(2, Grid.Columns.Count);
        RestoreLayoutFromSettings();
        _columnAlignmentToolbar.UpdateButtons();
        ApplySearchFilter();
    }

    private static void ApplyColorStyle(DataGridColumn column, string columnName)
    {
        var colorStyle = DataGridColorCellStyleFactory.CreateSchaechteStyle(columnName);
        if (colorStyle is not null)
            column.CellStyle = colorStyle;
    }

    private DataGridColumn CreateZustandsklasseColumn(string recordField)
    {
        return new DataGridComboBoxColumn
        {
            Header = GetDisplayHeader(recordField),
            ItemsSource = ZustandsklasseColorPalette.SelectionOptions,
            SelectedItemBinding = new Binding($"Fields[{recordField}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = DataGridLength.SizeToHeader,
            MinWidth = 90
        };
    }

    private void Grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _columnAlignmentToolbar.TrackSelectedCells();
    }

    private void Grid_CurrentCellChanged(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _columnAlignmentToolbar.TrackCurrentCell();
    }

    private void Grid_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _ = sender;

        if (e.OriginalSource is not DependencyObject dep)
            return;

        _columnAlignmentToolbar.TrackHeaderClick(dep);
    }

    private void Grid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        _ = sender;
        _ = e;
        QueueLayoutSave();
    }

    private void AlignLeftButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Left);
    }

    private void AlignCenterButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Center);
    }

    private void AlignRightButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyHorizontalAlignment(HorizontalAlignment.Right);
    }

    private void AlignTopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Top);
    }

    private void AlignMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Center);
    }

    private void AlignBottomButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _columnAlignmentToolbar.ApplyVerticalAlignment(VerticalAlignment.Bottom);
    }

    private void RestoreLayoutFromSettings()
    {
        var sp = Services;
        var layout = sp.Settings.SchaechtePageLayout;

        _isRestoringLayout = true;
        try
        {
            _columnLayoutController.Restore(
                Grid.Columns,
                layout,
                columns => DataGridColumnLayoutController.EnsureFieldBefore(columns, "Schachtnummer", "Funktion"));
        }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    private void QueueLayoutSave()
    {
        if (_isRestoringLayout || _columnLayoutController.IsRestoring)
            return;

        _layoutSaveDebounceTimer.Stop();
        _layoutSaveDebounceTimer.Start();
    }

    private void SaveLayoutToSettings()
    {
        // Beim Entladen der Seite (Unloaded-Handler) kann der DataContext bereits
        // null sein. Dann nichts speichern - kein Zugriff auf Vm/Services erzwingen.
        if (_isRestoringLayout || _columnLayoutController.IsRestoring ||
            Grid.Columns.Count == 0 || DataContext is not SchaechtePageViewModel)
            return;

        var sp = Services;
        var layout = sp.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        layout.Columns = _columnLayoutController.Capture(Grid.Columns).Columns;

        sp.Settings.SchaechtePageLayout = layout;
        sp.Settings.Save();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void MoveToPositionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        MoveToPosition_Click(sender, e);
    }

    private void MoveToPosition_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is not SchaechtePageViewModel vm)
            return;

        DataPageRowNavigationController.TryMoveToPosition(
            MoveToPositionBox.Text,
            vm.MoveToPosition,
            Services.Dialogs.Info);
    }

    private void GoToRowBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        GoToRow_Click(sender, e);
    }

    private void GoToRow_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is not SchaechtePageViewModel vm)
            return;

        if (DataPageRowNavigationController.TryResolveRowIndex(
            GoToRowBox.Text,
            vm.Records.Count,
            Services.Dialogs.Info,
            out var rowIndex))
        {
            vm.Selected = vm.Records[rowIndex];
            Grid.ScrollIntoView(vm.Selected);
        }
    }

    private void ApplySearchFilter()
    {
        if (DataContext is not SchaechtePageViewModel vm)
            return;

        DataGridSearchFilterController.Apply(
            CollectionViewSource.GetDefaultView(Grid.ItemsSource),
            vm.Records,
            vm.SearchText,
            vm.MatchesSearch,
            vm.UpdateSearchResultInfo,
            action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action));
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

        var value = DataGridEditedTextValueResolver.ResolveComboBoxValue(combo);
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

    private static bool TryResolveDropdownColumnSpec(string columnName, out GridDropdownFieldSpec spec)
    {
        var optionField = ResolveOptionField(columnName);
        if (optionField is not null && GridDropdownFieldPolicy.TryResolve(optionField, out spec))
            return true;

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

        if (!DataGridEditedTextValueResolver.TryResolve(e.EditingElement, out var value))
            return;

        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
        {
            var oldShaftNumber = record.GetFieldValue("Schachtnummer");
            if (!ApplySchachtNumberChange(record, oldShaftNumber, value))
                return;
        }
        else
        {
            record.SetFieldValue(recordField, value);
        }

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

    private void ShowRecordDetails(SchachtRecord record)
    {
        var schacht = GetSchachtNumber(record);
        var header = string.IsNullOrWhiteSpace(schacht)
            ? "Schachtdetails"
            : $"Schacht {schacht}";

        var subtitle = "Komplette Zeile in Spaltenreihenfolge der Schacht-Ansicht.";
        var groups = BuildRecordDetails(record);
        var window = new RecordDetailsWindow(
            title: string.IsNullOrWhiteSpace(schacht) ? "Schachtdetails" : $"Schachtdetails - {schacht}",
            header: header,
            subHeader: subtitle,
            groups: groups)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show();
    }

    private List<RecordDetailGroup> BuildRecordDetailsForAnsicht(SchachtRecord record)
        => BuildRecordDetails(record);

    private void SchachtansichtToggle_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var showAnsicht = SchachtansichtToggle.IsChecked == true;
        SchachtansichtView.Visibility = showAnsicht ? Visibility.Visible : Visibility.Collapsed;
        Grid.Visibility = showAnsicht ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RouteSchachtansichtAction(string actionKey, SchachtRecord record)
    {
        if (_vm is null)
            return;

        _vm.Selected = record;
        if (actionKey.StartsWith("zustandsklasse:", StringComparison.Ordinal))
        {
            var value = actionKey["zustandsklasse:".Length..];
            CommitSchachtDetailField(record, "Zustandsklasse", value);
            return;
        }

        var e = new RoutedEventArgs();
        switch (actionKey)
        {
            case "details":
                ShowRecordDetails(record);
                break;
            case "openpdf":
                ProtokollMenu_Click(this, e);
                break;
            case "openfolder":
                OpenContainingFolderMenu_Click(this, e);
                break;
            case "moveup":
                _vm.MoveUpCommand.Execute(null);
                break;
            case "movedown":
                _vm.MoveDownCommand.Execute(null);
                break;
            case "delete":
                _vm.RemoveCommand.Execute(null);
                break;
            case "sanierung":
                OpenSchachtMassnahmen(record);
                break;
            default:
                System.Diagnostics.Debug.Fail($"Unbekannter actionKey: {actionKey}");
                break;
        }
    }

    private List<RecordDetailGroup> BuildRecordDetails(SchachtRecord record)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = new(),
            ["Zustand und Inspektion"] = new(),
            ["Sanierung und Kosten"] = new(),
            ["Dokumente und Medien"] = new(),
            ["Weitere Angaben"] = new()
        };

        if (_vm is not null)
        {
            foreach (var column in _vm.Columns.Where(x => added.Add(x)))
            {
                var groupName = ResolveSchachtDetailGroup(column);
                buckets[groupName].Add(CreateSchachtDetailItem(column, record));
            }
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            buckets["Weitere Angaben"].Add(CreateSchachtDetailItem(extraField, record));
        }

        AddSchachtGroup(groups, buckets, "Stammdaten", "Identifikation und Lage des Schachts.");
        AddSchachtGroup(groups, buckets, "Zustand und Inspektion", "Bewertung, Schaeden und Pruefresultate.");
        AddSchachtGroup(groups, buckets, "Sanierung und Kosten", "Massnahmen, Kosten und Mengenangaben.");
        AddSchachtGroup(groups, buckets, "Dokumente und Medien", "Verknuepfte Dateien, PDFs und Links.");
        AddSchachtGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.");

        return groups;
    }

    private RecordDetailItem CreateSchachtDetailItem(string fieldName, SchachtRecord record)
    {
        var label = GetDisplayHeader(fieldName);
        var value = record.GetFieldValue(fieldName);

        if (_vm is not null && TryResolveDropdownColumnSpec(fieldName, out var spec))
        {
            var options = ResolveOptions(spec.ItemsSourcePath);
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitSchachtDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: spec.AllowFreeText,
                options: options,
                editOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.EditCommand) : null,
                previewOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.PreviewCommand) : null,
                resetOptionsCommand: spec.Managed ? ResolveViewModelCommand(spec.ResetCommand) : null,
                addOptionCommand: spec.Managed ? ResolveViewModelCommand(spec.AddCommand) : null,
                removeOptionCommand: spec.Managed ? ResolveViewModelCommand(spec.RemoveCommand) : null);
        }

        var normalized = Normalize(fieldName);
        var isMultiline = IsPrimaryDamagesColumn(fieldName)
                          || normalized.Contains("bemerk", StringComparison.Ordinal);
        if (IsZustandsklasseColumn(fieldName))
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitSchachtDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: false,
                options: ZustandsklasseColorPalette.SelectionOptions);
        }

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => CommitSchachtDetailField(record, fieldName, next),
            isMultiline: isMultiline);
    }

    private IEnumerable<string> ResolveOptions(string itemsSourcePath)
    {
        if (_vm is null)
            return Array.Empty<string>();

        return itemsSourcePath switch
        {
            "SanierenOptions" => _vm.SanierenOptions,
            "EigentuemerOptions" => _vm.EigentuemerOptions,
            "PruefungsresultatOptions" => _vm.PruefungsresultatOptions,
            "ReferenzpruefungOptions" => _vm.ReferenzpruefungOptions,
            "AusgefuehrtDurchOptions" => _vm.AusgefuehrtDurchOptions,
            _ => Array.Empty<string>()
        };
    }

    private ICommand? ResolveViewModelCommand(string propertyName)
    {
        if (_vm is null || string.IsNullOrWhiteSpace(propertyName))
            return null;

        return _vm.GetType().GetProperty(propertyName)?.GetValue(_vm) as ICommand;
    }

    private void CommitSchachtDetailField(SchachtRecord record, string recordField, string? value)
    {
        var next = value ?? string.Empty;
        if (string.Equals(recordField, "Schachtnummer", StringComparison.Ordinal))
        {
            var oldShaftNumber = record.GetFieldValue("Schachtnummer");
            if (!ApplySchachtNumberChange(record, oldShaftNumber, next))
                return;
        }
        else
        {
            record.SetFieldValue(recordField, next);
        }

        if (_vm is not null)
        {
            var optionField = ResolveOptionField(recordField);
            if (!string.IsNullOrWhiteSpace(optionField))
                _vm.EnsureOptionForField(optionField, next);
        }

        MarkProjectDirty();
        ApplySearchFilter();
    }

    private bool ApplySchachtNumberChange(SchachtRecord record, string? oldValue, string? newValue)
    {
        var oldNumber = oldValue ?? string.Empty;
        var newNumber = newValue ?? string.Empty;
        if (string.Equals(oldNumber, newNumber, StringComparison.OrdinalIgnoreCase))
            return true;

        var projectPath = Services.Settings.LastProjectPath;
        var renameResult = ShaftRenameService.Rename(record, oldNumber, newNumber, projectPath);
        if (!renameResult.Success)
        {
            DialogHost.Current.Error($"Umbenennen fehlgeschlagen:\n{renameResult.ErrorMessage}", "Umbenennen");
            return false;
        }

        record.SetFieldValue("Schachtnummer", newNumber);
        PdfCorrectionMetadata.RegisterShaftRename(GetCurrentProject(), oldNumber, newNumber);

        var pdfSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CollectPdf(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolved = ProjectPathResolver.ResolveFilePath(part.Trim(), projectPath);
                if (!string.IsNullOrWhiteSpace(resolved)
                    && resolved.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    pdfSet.Add(resolved);
                }
            }
        }

        CollectPdf(record.GetFieldValue(FieldKeys.PdfPath));
        CollectPdf(record.GetFieldValue(FieldKeys.PdfAll));
        CollectPdf(record.GetFieldValue(FieldKeys.PdfEigen));
        CollectPdf(record.GetFieldValue(FieldKeys.Link));

        if (pdfSet.Count > 0)
        {
            AuswertungPro.Next.Infrastructure.HoldingFolderDistributor.RewriteHoldingInPdfFiles(
                new List<string>(pdfSet),
                oldNumber,
                newNumber);
        }

        return true;
    }

    private static void AddSchachtGroup(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, List<RecordDetailItem>> buckets,
        string title,
        string description)
    {
        if (!buckets.TryGetValue(title, out var items) || items.Count == 0)
            return;

        groups.Add(new RecordDetailGroup(title, description, items));
    }

    private static string ResolveSchachtDetailGroup(string columnName)
    {
        var normalized = Normalize(columnName);

        if (normalized.Contains("kosten", StringComparison.Ordinal) ||
            normalized.Contains("sanier", StringComparison.Ordinal) ||
            normalized.Contains("renovierung", StringComparison.Ordinal) ||
            normalized.Contains("reparatur", StringComparison.Ordinal) ||
            normalized.Contains("erneuerung", StringComparison.Ordinal) ||
            normalized.Contains("anschluss", StringComparison.Ordinal))
            return "Sanierung und Kosten";

        if (normalized.Contains("pdf", StringComparison.Ordinal) ||
            normalized.Contains("link", StringComparison.Ordinal) ||
            normalized.Contains("video", StringComparison.Ordinal) ||
            normalized.Contains("film", StringComparison.Ordinal) ||
            normalized.Contains("datei", StringComparison.Ordinal))
            return "Dokumente und Medien";

        if (normalized.Contains("zustand", StringComparison.Ordinal) ||
            normalized.Contains("schaden", StringComparison.Ordinal) ||
            normalized.Contains("pruefung", StringComparison.Ordinal) ||
            normalized.Contains("dicht", StringComparison.Ordinal) ||
            normalized.Contains("referenz", StringComparison.Ordinal) ||
            normalized.Contains("gewaesser", StringComparison.Ordinal) ||
            normalized.Contains("grundwasser", StringComparison.Ordinal))
            return "Zustand und Inspektion";

        if (normalized.Contains("schacht", StringComparison.Ordinal) ||
            normalized.Contains("nummer", StringComparison.Ordinal) ||
            normalized.Contains("name", StringComparison.Ordinal) ||
            normalized.Contains("nr", StringComparison.Ordinal) ||
            normalized.Contains("funktion", StringComparison.Ordinal) ||
            normalized.Contains("strasse", StringComparison.Ordinal) ||
            normalized.Contains("lage", StringComparison.Ordinal) ||
            normalized.Contains("ort", StringComparison.Ordinal) ||
            normalized.Contains("material", StringComparison.Ordinal) ||
            normalized.Contains("dn", StringComparison.Ordinal) ||
            normalized.Contains("durchmesser", StringComparison.Ordinal) ||
            normalized.Contains("eigentuem", StringComparison.Ordinal) ||
            normalized.Contains("eigentum", StringComparison.Ordinal))
            return "Stammdaten";

        return "Weitere Angaben";
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
                return target;
            current = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.GetParentSafe(current);
        }

        return null;
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

        if (!DialogHost.Current.ConfirmWarn(
            $"Alle Werte in Spalte \"{displayName}\" löschen?",
            "Spalte leeren"))
        {
            return;
        }

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
            DialogHost.Current.Info("Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.", "Protokoll");
            return;
        }

        var pdfPath = ResolvePdfPath(record);
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            var schacht = GetSchachtNumber(record);
            DialogHost.Current.Info(
                string.IsNullOrWhiteSpace(schacht)
                    ? "Kein Schachtprotokoll-PDF verknüpft."
                    : $"Kein Schachtprotokoll-PDF verknüpft für Schacht {schacht}.",
                "Protokoll");
            return;
        }

        if (!AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(pdfPath, out var error))
        {
            DialogHost.Current.Error($"PDF konnte nicht geöffnet werden:\n{error}", "Protokoll");
        }
    }

    private void OpenContainingFolderMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_vm is null)
            return;

        var record = _vm.Selected;
        if (record is null)
        {
            DialogHost.Current.Info("Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.", "Ordner");
            return;
        }

        var target = ResolveExplorerTarget(record);
        if (string.IsNullOrWhiteSpace(target))
        {
            var schacht = GetSchachtNumber(record);
            DialogHost.Current.Info(
                string.IsNullOrWhiteSpace(schacht)
                    ? "Kein Datei- oder Ordnerpfad verknüpft."
                    : $"Kein Datei- oder Ordnerpfad verknüpft für Schacht {schacht}.",
                "Ordner");
            return;
        }

        if (!AuswertungPro.Next.UI.Services.ExplorerRevealService.TryReveal(target, out var error))
            DialogHost.Current.Error($"Ordner konnte nicht geöffnet werden:\n{error}", "Ordner");
    }

    private void DetailsMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;

        if (_vm is null)
            return;

        var record = _vm.Selected;
        if (record is null)
        {
            DialogHost.Current.Info("Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.", "Details");
            return;
        }

        ShowRecordDetails(record);
    }

    private void SanierungsmassnahmenMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_vm is null)
            return;

        var record = _vm.Selected;
        if (record is null)
        {
            DialogHost.Current.Info("Keine Zeile ausgewählt. Bitte direkt auf einen Schacht rechtsklicken.", "Sanierungsmassnahmen");
            return;
        }

        OpenSchachtMassnahmen(record);
    }

    /// <summary>
    /// Oeffnet das einfache Schacht-Sanierungsmassnahmen-Fenster (klickbare Liste, manuelle Preise,
    /// ohne NPK). Die Auswahl wird pro Schacht in schacht_empfehlungen.json abgelegt und als
    /// "Massnahmen"/"Kosten" in den Record geschrieben (-> Schaechte-Excel-Export).
    /// </summary>
    private void OpenSchachtMassnahmen(SchachtRecord record)
    {
        if (_vm is null)
            return;

        var schachtNummer = GetSchachtNumber(record);
        var projectPath = Services.Settings.LastProjectPath;

        var repo = new AuswertungPro.Next.Infrastructure.Costs.ProjectCostStoreRepository("schacht_empfehlungen.json");
        var store = repo.Load(projectPath, out var loadError);
        if (loadError is not null)
        {
            Services.Dialogs.Warn(
                $"Bestehende Schacht-Empfehlungen konnten nicht gelesen werden:\n{loadError}\n\nDu kannst neu erfassen; Speichern legt die Datei neu an.",
                "Sanierungsmassnahmen");
        }

        HoldingCost? bestehend = null;
        if (!string.IsNullOrWhiteSpace(schachtNummer))
            store.ByHolding.TryGetValue(schachtNummer, out bestehend);

        var katalog = Services.SchachtMassnahmenKatalog.Load();

        var vm = new SchachtMassnahmenViewModel(
            record,
            katalog,
            bestehend,
            onUebernehmen: cost => PersistSchachtEmpfehlung(repo, store, schachtNummer, cost, projectPath),
            onListeBearbeiten: EditSchachtMassnahmenListe);

        var win = new SchachtMassnahmenWindow(vm) { Owner = Window.GetWindow(this) };
        win.ShowDialog();
    }

    private void PersistSchachtEmpfehlung(
        AuswertungPro.Next.Infrastructure.Costs.ProjectCostStoreRepository repo,
        ProjectCostStore store,
        string schachtNummer,
        HoldingCost cost,
        string? projectPath)
    {
        if (!string.IsNullOrWhiteSpace(schachtNummer))
        {
            var hatAuswahl = cost.Measures.Any(m => m.Lines.Any(l => l.Selected));
            if (hatAuswahl)
                store.ByHolding[schachtNummer] = cost;
            else
                store.ByHolding.Remove(schachtNummer);

            if (string.IsNullOrWhiteSpace(projectPath))
                Services.Dialogs.Info("Projekt bitte zuerst speichern, damit die Auswahl dauerhaft abgelegt wird.", "Sanierungsmassnahmen");
            else if (!repo.Save(projectPath, store, out var error))
                Services.Dialogs.Error($"Speichern der Schacht-Empfehlungen fehlgeschlagen:\n{error}", "Sanierungsmassnahmen");
        }

        // Die Felder "Massnahmen"/"Kosten" hat das ViewModel bereits in den Record geschrieben.
        MarkProjectDirty();
        ApplySearchFilter();
    }

    private IReadOnlyList<SchachtMassnahmeKatalogEintrag>? EditSchachtMassnahmenListe()
    {
        var editorVm = new SchachtMassnahmenKatalogEditorViewModel(Services.SchachtMassnahmenKatalog.Load());
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Window.GetWindow(this);
        var win = new SchachtMassnahmenKatalogEditorWindow(editorVm) { Owner = owner };
        if (win.ShowDialog() == true)
        {
            Services.SchachtMassnahmenKatalog.Save(editorVm.Ergebnis);
            return editorVm.Ergebnis;
        }

        return null;
    }

    private string? ResolvePdfPath(SchachtRecord record)
        => SchachtFileTargetResolver.ResolvePdfPath(record, Services.Settings.LastProjectPath);

    private string? ResolveExplorerTarget(SchachtRecord record)
        => SchachtFileTargetResolver.ResolveExplorerTarget(record, Services.Settings.LastProjectPath);
}
