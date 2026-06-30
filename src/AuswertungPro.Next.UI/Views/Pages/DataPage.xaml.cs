using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.UI.ViewModels.Pages;
using System.IO;
using AuswertungPro.Next.Domain.Protocol;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage : System.Windows.Controls.UserControl
{
    private DataPageViewModel Vm => DataContext as DataPageViewModel
        ?? throw new InvalidOperationException("DataPage benoetigt DataPageViewModel als DataContext.");
    private ServiceProvider Services => Vm.Services;
    private IDialogService Dialogs => Services.Dialogs;
    private bool _columnsBuilt;
    private System.Windows.Point _dragStartPoint;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DataGridColumnLayoutController _columnLayoutController = new();
    private readonly DispatcherTimer _layoutSaveDebounceTimer;
    private bool _updatingAlignmentButtons;
    private bool _isUndocking;
    private DataGridColumn? _activeColumn;

    public DataPage()
    {
        InitializeComponent();

        // Haltungsansicht teilt sich Selected/Records mit der Tabelle; Detail-Aufbau wie im Detailfenster,
        // aber ohne Primaere_Schaeden (steht dort schon als Schadensliste unten).
        HaltungsansichtView.DetailBuilder = BuildHaltungRecordDetailsForAnsicht;
        HaltungsansichtView.ActionRequested = RouteHaltungsansichtAction;

        // Standardansicht beim Oeffnen der Haltungen-Seite: Haltungsansicht (Liste + Detail)
        // statt der Tabelle. IsChecked loest HaltungsansichtToggle_Changed; die Sichtbarkeiten
        // werden zusaetzlich explizit gesetzt (robust gegen Event-Timing).
        HaltungsansichtToggle.IsChecked = true;
        HaltungsansichtView.Visibility = Visibility.Visible;
        Grid.Visibility = Visibility.Collapsed;

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

        Grid.AddHandler(DataGridColumnHeader.ClickEvent, new RoutedEventHandler(Grid_ColumnHeaderClick), true);
        Grid.ColumnReordered += Grid_ColumnReordered;
        Loaded += (_, __) =>
        {
            ApplyHaltungsansichtSettings();
            EnsureColumns();
            UpdateAlignmentButtonsForCurrentColumn();
        };
        Unloaded += (_, __) =>
        {
            _layoutSaveDebounceTimer.Stop();
            SaveLayoutToSettings();
            // Wenn die Seite gewechselt wird, Grid zurueck docken
            // NICHT waehrend des Abdock-Vorgangs ausfuehren!
            if (_floatingGridWindow is not null && !_isUndocking)
                DockGridBack();
        };
        DataContextChanged += DataPage_DataContextChanged;
    }

    private void DataPage_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DataPageViewModel oldVm)
        {
            oldVm.RecordsOrderChanged -= ResetSort;
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }
        if (e.NewValue is DataPageViewModel newVm)
        {
            newVm.RecordsOrderChanged += ResetSort;
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            ApplyHaltungsansichtSettings(newVm);
        }
    }

    private void ApplyHaltungsansichtSettings()
    {
        if (DataContext is DataPageViewModel vm)
            ApplyHaltungsansichtSettings(vm);
    }

    private void ApplyHaltungsansichtSettings(DataPageViewModel vm)
        => HaltungsansichtView.Settings = vm.Services.Settings;

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private void EnsureColumns()
    {
        if (_columnsBuilt)
            return;

        _columnsBuilt = true;
        _columnLayoutController.Clear();
        _activeColumn = null;

        foreach (var field in FieldCatalog.ColumnOrder)
        {
            var def = FieldCatalog.Get(field);
            DataGridColumn col;

            if (GridDropdownFieldPolicy.TryResolve(field, out var comboSpec))
            {
                col = comboSpec.Managed
                    ? CreateComboColumn(
                        field,
                        def.Label,
                        comboSpec.ItemsSourcePath,
                        comboSpec.EditCommand,
                        comboSpec.PreviewCommand,
                        comboSpec.ResetCommand,
                        comboSpec.RemoveCommand,
                        comboSpec.AddCommand,
                        comboSpec.AllowFreeText)
                    : CreateSimpleComboColumn(field, def.Label, comboSpec.ItemsSourcePath);
            }
            else if (field == "Empfohlene_Sanierungsmassnahmen")
            {
                var displayStyle = new Style(typeof(TextBlock));
                displayStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                displayStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                displayStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

                var editStyle = new Style(typeof(TextBox));
                editStyle.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
                editStyle.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
                editStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top));
                editStyle.Setters.Add(new Setter(TextBox.MinHeightProperty, 60d));

                col = new DataGridTextColumn
                {
                    Header = def.Label,
                    Binding = new Binding($"Fields[{field}]")
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    ElementStyle = displayStyle,
                    EditingElementStyle = editStyle,
                    Width = DataGridLength.SizeToHeader
                };
            }
            else if (field == "Kosten")
            {
                col = DataGridCostColumnFactory.Create(field, def.Label);
            }
            else
            {
                col = new DataGridTextColumn
                {
                    Header = def.Label,
                    Binding = new Binding($"Fields[{field}]")
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = DataGridLength.SizeToHeader
                };
            }

            col.SetValue(FrameworkElement.TagProperty, field);
            if (string.Equals(field, "Zustandsklasse", StringComparison.Ordinal))
                col.CellStyle = ZustandsklasseCellStyleFactory.CreateHaltungenStyle(field);
            else if (string.Equals(field, "Eigentuemer", StringComparison.Ordinal))
                col.CellStyle = ZustandsklasseCellStyleFactory.CreateEigentuemerStyle(field);
            else if (string.Equals(field, "Pruefungsresultat", StringComparison.Ordinal))
                col.CellStyle = ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(field);
            else if (string.Equals(field, "Referenzpruefung", StringComparison.Ordinal))
                col.CellStyle = ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(field);
            else if (string.Equals(field, "Ausgefuehrt_durch", StringComparison.Ordinal))
                col.CellStyle = ZustandsklasseCellStyleFactory.CreateAusgefuehrtDurchStyle(field);

            ApplyFieldMetaTooltip(col, field);
            col.CanUserResize = true;
            col.MinWidth = field == "NR" ? 56 : 72;
            Grid.Columns.Add(col);

            var defaultHorizontalAlignment = string.Equals(field, "Kosten", StringComparison.Ordinal)
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            ApplyColumnAlignment(col, defaultHorizontalAlignment, VerticalAlignment.Center);
        }

        Grid.FrozenColumnCount = 2;
        RestoreLayoutFromSettings();
        ResetSort();
    }

    private DataGridTemplateColumn CreateComboColumn(
        string fieldName,
        string header,
        string itemsSourcePath,
        string editCommand,
        string previewCommand,
        string resetCommand,
        string removeCommand,
        string addCommand,
        bool allowFreeText = true)
        => DataGridComboColumnFactory.Create(
            fieldName,
            header,
            itemsSourcePath,
            tag: fieldName,
            lostKeyboardFocus: ComboBox_LostKeyboardFocus,
            selectionChanged: ComboBox_SelectionChanged,
            allowFreeText,
            bindIsProjectReady: true,
            menuCommands: new DataGridComboColumnMenuCommands(
                editCommand,
                previewCommand,
                resetCommand,
                removeCommand,
                addCommand));

    private DataGridTemplateColumn CreateSimpleComboColumn(
        string fieldName,
        string header,
        string itemsSourcePath)
        => DataGridComboColumnFactory.Create(
            fieldName,
            header,
            itemsSourcePath,
            tag: fieldName,
            lostKeyboardFocus: ComboBox_LostKeyboardFocus,
            selectionChanged: ComboBox_SelectionChanged,
            allowFreeText: true,
            bindIsProjectReady: true);

    private void Grid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Don't capture drag start when clicking inside an editing TextBox
        if (e.OriginalSource is DependencyObject dep && FindAncestor<TextBox>(dep) is not null)
            return;

        _dragStartPoint = e.GetPosition(null);
    }

    private void Grid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            tb.SelectAll();
            tb.Focus();
        }
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ClearColumnMenuItem.IsChecked)
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
        if (DataContext is not DataPageViewModel vm)
            return;

        var betroffen = vm.Records.Count(r =>
            !string.IsNullOrEmpty(r.GetFieldValue(fieldName)));
        if (betroffen == 0)
        {
            Dialogs.Info($"Spalte \"{displayName}\" ist bereits leer.", "Spalte leeren");
            return;
        }

        if (!Dialogs.ConfirmWarn(
            $"ACHTUNG: Alle Werte in Spalte \"{displayName}\" werden geloescht.\n\n" +
            $"Betroffen: {betroffen} von {vm.Records.Count} Haltungen.\n" +
            "Auch manuell bearbeitete Werte gehen verloren und koennen nicht rueckgaengig gemacht werden.\n\n" +
            "Wirklich loeschen?",
            "Spalte leeren"))
            return;

        foreach (var record in vm.Records)
        {
            // userEdited: true um die Guard-Clause zu umgehen (sonst wird das Leeren blockiert)
            record.SetFieldValue(fieldName, string.Empty, FieldSource.Manual, userEdited: true);
            // Danach UserEdited zuruecksetzen, damit Importe das Feld wieder fuellen koennen
            if (record.FieldMeta.TryGetValue(fieldName, out var meta))
                meta.UserEdited = false;
        }

        vm.ScheduleAutoSave();
    }

    private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (DataContext is not DataPageViewModel vm)
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
        => CommitComboBoxValue(sender as ComboBox);

    private void ComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => CommitComboBoxValue(sender as ComboBox);

    private void CommitComboBoxValue(ComboBox? combo)
    {
        if (combo is null)
            return;
        if (combo.Tag is not string fieldName)
            return;
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!vm.IsProjectReady)
            return;

        var record = ResolveRecordFromComboBox(combo);
        if (record is not null)
        {
            var value = ResolveComboBoxValue(combo);
            if (string.IsNullOrWhiteSpace(value))
                return;
            record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: true);
        }

        vm.EnsureOptionForField(fieldName, ResolveComboBoxValue(combo));
        vm.ScheduleAutoSave();
    }

    private HaltungRecord? ResolveRecordFromComboBox(ComboBox combo)
    {
        if (combo.DataContext is HaltungRecord direct)
            return direct;

        var row = FindAncestor<DataGridRow>(combo);
        if (row?.Item is HaltungRecord fromRow)
            return fromRow;

        return Grid.CurrentItem as HaltungRecord;
    }

    private void Grid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DataContext is DataPageViewModel vm && !vm.IsProjectReady)
            return;

        // Don't start row drag when user is selecting text inside an editing TextBox
        if (e.OriginalSource is DependencyObject dep && FindAncestor<TextBox>(dep) is not null)
            return;

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            System.Windows.Point mousePos = e.GetPosition(null);
            System.Windows.Vector diff = _dragStartPoint - mousePos;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
                if (row == null) return;
                var record = row.Item as HaltungRecord;
                if (record == null) return;
                DragDrop.DoDragDrop(row, record, DragDropEffects.Move);
            }
        }
    }

    private void Grid_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!vm.IsProjectReady)
            return;

        if (e.Data.GetDataPresent(typeof(HaltungRecord)))
        {
            var droppedData = e.Data.GetData(typeof(HaltungRecord)) as HaltungRecord;
            var target = GetDataGridRowItem(e.OriginalSource);
            if (droppedData == null || target == null || droppedData == target) return;

            var list = vm.Records;
            int oldIndex = list.IndexOf(droppedData);
            int newIndex = list.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
            list.Move(oldIndex, newIndex);
            ResetSort();
            var updateNr = vm.GetType().GetMethod("UpdateNr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateNr?.Invoke(vm, null);
        }
    }

    private void Grid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        var cell = FindAncestor<DataGridCell>(source);
        if (cell is null)
            return;

        if (cell.Column?.GetValue(FrameworkElement.TagProperty) is not string fieldName)
            return;

        var row = FindAncestor<DataGridRow>(cell);
        if (row?.Item is not HaltungRecord record)
            return;

        if (fieldName == "Primaere_Schaeden")
        {
            var holding = record.GetFieldValue("Haltungsname");
            var title = string.IsNullOrWhiteSpace(holding)
                ? "Primäre Schäden"
                : $"Primäre Schäden - {holding}";
            var preview = BuildPrimaryDamagePreviewContent(record);
            ShowTextPreview(title, preview);
            e.Handled = true;
            return;
        }

        if (fieldName == "Zustandsklasse")
        {
            ShowZustandsklasseExplanation(record);
            e.Handled = true;
            return;
        }

        if (fieldName == "Haltungsname")
        {
            ShowHaltungRecordDetails(record);
            e.Handled = true;
        }
    }

    // ── Mehrfach-Loeschen (Delete-Taste / Kontextmenue) ────────────────

    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Grid.SelectedItems.Count > 0)
        {
            DeleteSelectedRows();
            e.Handled = true;
        }
    }

    private void DeleteSelectedRows_Click(object sender, RoutedEventArgs e)
        => DeleteSelectedRows();

    private void DeleteSelectedRows()
    {
        if (DataContext is not DataPageViewModel vm) return;

        var items = Grid.SelectedItems.OfType<HaltungRecord>().ToList();
        if (items.Count == 0) return;

        if (!Dialogs.Confirm($"{items.Count} Haltung(en) wirklich loeschen?", "Loeschen")) return;

        foreach (var item in items)
            vm.Project.RemoveRecord(item.Id);

        vm.Selected = vm.Records.FirstOrDefault();
        vm.ScheduleAutoSave();
    }

    // ── Haltung Record Details ──────────────────────────────────────────

    private void RouteHaltungsansichtAction(string actionKey, HaltungRecord record)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        // Sender ist hier die DataPage (kein HaltungRecord-DataContext, kein offenes
        // ContextMenu) -> GetContextMenuRecord(this) liefert null -> ResolveActionRecord
        // faellt auf das gerade gesetzte vm.Selected zurueck. Darum reicht 'this' als Sender.
        vm.Selected = record;
        var e = new RoutedEventArgs();
        switch (actionKey)
        {
            case "codieren": vm.OpenProtocolCommand.Execute(record); break;
            case "play": PlayMenu_Click(this, e); break;
            case "beobachtungen": BeobachtungenMenu_Click(this, e); break;
            case "printawu": PrintAwuHaltungsprotokollMenu_Click(this, e); break;
            case "openpdf": OpenOriginalPdfMenu_Click(this, e); break;
            case "costs": CostsMenu_Click(this, e); break;
            case "moveup": MoveRecordUpMenu_Click(this, e); break;
            case "movedown": MoveRecordDownMenu_Click(this, e); break;
            case "delete": DeleteSelectedRows(); break;
            default: System.Diagnostics.Debug.Fail($"Unbekannter actionKey: {actionKey}"); break;
        }
    }

    // Umschalter Tabelle <-> Haltungsansicht: beide Sichten teilen Selected/Records
    private void HaltungsansichtToggle_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var showAnsicht = HaltungsansichtToggle.IsChecked == true;
        HaltungsansichtView.Visibility = showAnsicht ? Visibility.Visible : Visibility.Collapsed;
        Grid.Visibility = showAnsicht ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowHaltungRecordDetails(HaltungRecord record)
    {
        var holding = record.GetFieldValue("Haltungsname");
        var header = string.IsNullOrWhiteSpace(holding)
            ? "Haltungsdetails"
            : $"Haltung {holding}";

        var subtitle = "Komplette Zeile in Spaltenreihenfolge der Haltungs-Ansicht.";
        var groups = BuildHaltungRecordDetails(record);

        ICommand? suggestCmd = null;
        if (DataContext is DataPageViewModel vm)
        {
            suggestCmd = new RelayCommand(() => vm.OpenCostsCommand.Execute(record));
        }

        var window = new RecordDetailsWindow(
            title: string.IsNullOrWhiteSpace(holding) ? "Haltungsdetails" : $"Haltungsdetails - {holding}",
            header: header,
            subHeader: subtitle,
            groups: groups,
            suggestMeasuresCommand: suggestCmd)
        {
            Owner = Window.GetWindow(this)
        };
        // S5: Modal oeffnen. Das Detailfenster baut beim Oeffnen einen Snapshot der Feldwerte
        // und hoert nicht auf spaetere Aenderungen des Records. Nicht-modal konnte daher eine
        // spaetere Detail-Eingabe parallele Tabellen-/KI-Aenderungen still ueberschreiben.
        // Modal verhindert Parallelbearbeitung waehrend das Detail offen ist.
        window.ShowDialog();
    }

    private List<RecordDetailGroup> BuildHaltungRecordDetails(HaltungRecord record)
        => DataPageRecordDetailsBuilder.Build(record, fieldName => CreateHaltungDetailItem(fieldName, record));

    /// <summary>In der eingebetteten Haltungsansicht ausgeblendete Formularfelder, weil sie dort
    /// bereits anders dargestellt sind. Primaere_Schaeden = die Schadensliste unten (SchadenList).
    /// Das Datenfeld selbst bleibt erhalten (Export/VSA/Massnahmen), nur die doppelte Anzeige entfaellt.</summary>
    private static readonly IReadOnlySet<string> HaltungsansichtHiddenFields =
        new HashSet<string>(StringComparer.Ordinal) { "Primaere_Schaeden" };

    private List<RecordDetailGroup> BuildHaltungRecordDetailsForAnsicht(HaltungRecord record)
        => DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => CreateHaltungDetailItem(fieldName, record),
            HaltungsansichtHiddenFields);

    private RecordDetailItem CreateHaltungDetailItem(string fieldName, HaltungRecord record)
    {
        var def = FieldCatalog.Get(fieldName);
        var label = def.Label;
        var value = record.GetFieldValue(fieldName);

        // Managed combo fields (ViewModel-driven dropdowns)
        var managedCombo = ResolveManagedComboSpec(fieldName);
        if (managedCombo is not null)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitHaltungDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: managedCombo.Value.AllowFreeText,
                options: managedCombo.Value.Options,
                editOptionsCommand: managedCombo.Value.EditCmd,
                previewOptionsCommand: managedCombo.Value.PreviewCmd,
                resetOptionsCommand: managedCombo.Value.ResetCmd,
                addOptionCommand: managedCombo.Value.AddCmd,
                removeOptionCommand: managedCombo.Value.RemoveCmd);
        }

        // Catalog combo fields
        var catalogItems = FieldCatalog.GetComboItems(fieldName);
        if (catalogItems.Count > 0)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => CommitHaltungDetailField(record, fieldName, next),
                isCombo: true,
                allowFreeText: false,
                options: catalogItems);
        }

        var isMultiline = fieldName is "Primaere_Schaeden" or "Bemerkungen" or "Empfohlene_Sanierungsmassnahmen";
        var digitsOnly = def.Type == FieldType.Int;

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => CommitHaltungDetailField(record, fieldName, next),
            isMultiline: isMultiline,
            digitsOnly: digitsOnly);
    }

    private (IEnumerable<string> Options, bool AllowFreeText,
        ICommand? EditCmd, ICommand? PreviewCmd, ICommand? ResetCmd,
        ICommand? AddCmd, ICommand? RemoveCmd)? ResolveManagedComboSpec(string fieldName)
    {
        if (DataContext is not DataPageViewModel vm)
            return null;

        if (!GridDropdownFieldPolicy.TryResolve(fieldName, out var spec) || !spec.Managed)
            return null;

        return spec.OptionField switch
        {
            "Sanieren_JaNein" => (vm.SanierenOptions, spec.AllowFreeText,
                vm.EditSanierenOptionsCommand, vm.PreviewSanierenOptionsCommand,
                vm.ResetSanierenOptionsCommand, null, null),
            "Eigentuemer" => (vm.EigentuemerOptions, spec.AllowFreeText,
                vm.EditEigentuemerOptionsCommand, vm.PreviewEigentuemerOptionsCommand,
                vm.ResetEigentuemerOptionsCommand, null, null),
            "Pruefungsresultat" => (vm.PruefungsresultatOptions, spec.AllowFreeText,
                vm.EditPruefungsresultatOptionsCommand, vm.PreviewPruefungsresultatOptionsCommand,
                vm.ResetPruefungsresultatOptionsCommand, null, null),
            "Referenzpruefung" => (vm.ReferenzpruefungOptions, spec.AllowFreeText,
                vm.EditReferenzpruefungOptionsCommand, vm.PreviewReferenzpruefungOptionsCommand,
                vm.ResetReferenzpruefungOptionsCommand, null, null),
            _ => null
        };
    }

    private void CommitHaltungDetailField(HaltungRecord record, string fieldName, string? value)
    {
        var next = value ?? string.Empty;
        record.SetFieldValue(fieldName, next, FieldSource.Manual, userEdited: true);

        if (DataContext is DataPageViewModel vm)
        {
            vm.EnsureOptionForField(fieldName, next);
            vm.ScheduleAutoSave();
        }
    }

    private static readonly SolidColorBrush TrainedRowBrush = new(Color.FromArgb(60, 220, 40, 40));

    private string BuildPrimaryDamagePreviewContent(HaltungRecord record)
        => DataPagePrimaryDamagePreviewBuilder.Build(record, ResolvePrimaryDamageCodeTitle);

    private string? ResolvePrimaryDamageCodeTitle(string code)
    {
        var sp = Services;
        if (sp?.CodeCatalog is null || string.IsNullOrWhiteSpace(code))
            return null;
        if (!sp.CodeCatalog.TryGet(code, out var def))
            return null;
        return string.IsNullOrWhiteSpace(def.Title) ? null : def.Title.Trim();
    }

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not HaltungRecord record)
            return;

        if (DataContext is DataPageViewModel vm && vm.IsTrainedCase(record.GetFieldValue("Haltungsname")))
        {
            e.Row.Background = TrainedRowBrush;
        }
        else
        {
            e.Row.ClearValue(DataGridRow.BackgroundProperty);
        }
    }

    private void OpenPhotoLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;

        var rawPath = fe.Tag as string;
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        var sp = Services;
        var resolved = AuswertungPro.Next.Application.Common.ProjectPathResolver.ResolveFilePath(rawPath, sp?.Settings.LastProjectPath) ?? rawPath;
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            Dialogs.Info($"Foto nicht gefunden:\n{rawPath}", "Foto");
            return;
        }

        if (!AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(resolved, out var error))
        {
            Dialogs.Error($"Foto konnte nicht geoeffnet werden:\n{error}", "Foto");
        }
    }

    private void OpenFilmLink_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = vm.Selected;
        if (record is null)
        {
            Dialogs.Info("Bitte zuerst eine Haltung waehlen.", "Video");
            return;
        }

        var entry = ResolveProtocolEntry(sender);
        if (entry is null)
        {
            Dialogs.Info("Keine Beobachtung erkannt.", "Video");
            return;
        }

        var targetTime = entry.Zeit ?? ParseMpegTime(entry.Mpeg);
        vm.PlayVideoCommand.Execute(record);

        if (targetTime is null)
            return;

        var overlayText = BuildOverlayText(entry);
        SeekVideoWithRetry(targetTime.Value, overlayText);
    }

    private static ProtocolEntry? ResolveProtocolEntry(object sender)
    {
        if (sender is not FrameworkElement fe)
            return null;

        return fe.Tag as ProtocolEntry ?? fe.DataContext as ProtocolEntry;
    }

    private void SeekVideoWithRetry(TimeSpan time, string? overlayText)
    {
        if (PlayerWindow.TrySeekTo(time))
        {
            if (!string.IsNullOrWhiteSpace(overlayText))
                PlayerWindow.TryShowOverlayOnLast(overlayText!, TimeSpan.FromSeconds(6));
            return;
        }

        var attempts = 0;
        var pendingOverlay = overlayText;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, __) =>
        {
            attempts++;
            var seeked = PlayerWindow.TrySeekTo(time);
            if (!string.IsNullOrWhiteSpace(pendingOverlay))
            {
                if (PlayerWindow.TryShowOverlayOnLast(pendingOverlay!, TimeSpan.FromSeconds(6)))
                    pendingOverlay = null;
            }

            if (seeked || attempts >= 8)
                timer.Stop();
        };
        timer.Start();
    }

    private static string BuildOverlayText(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Code))
            parts.Add(entry.Code.Trim());
        if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
            parts.Add(entry.Beschreibung.Trim());
        if (entry.MeterStart.HasValue || entry.MeterEnd.HasValue)
        {
            var m1 = entry.MeterStart?.ToString("0.00") ?? "-";
            var m2 = entry.MeterEnd?.ToString("0.00") ?? "-";
            parts.Add(entry.IsStreckenschaden ? $"Strecke {m1} - {m2} m" : $"Meter {m1} - {m2}");
        }

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Column.GetValue(FrameworkElement.TagProperty) is not string fieldName)
            return;

        if (DataContext is not DataPageViewModel vm)
            return;

        // Merkt sich, ob ein Sonderfeld-Block die Spalte bereits behandelt hat, damit der
        // generische S3-Zweig sie nicht erneut setzt (keine zweite, synchron zu haltende Namensliste).
        bool handled = false;

        if (fieldName == "Sanieren_JaNein" || fieldName == "Eigentuemer" ||
            fieldName == "Pruefungsresultat" || fieldName == "Referenzpruefung")
        {
            handled = true;
            var value = GetEditedTextValue(e.EditingElement);
            if (!string.IsNullOrWhiteSpace(value) && e.Row?.Item is HaltungRecord editedRecord)
                editedRecord.SetFieldValue(fieldName, value ?? string.Empty, FieldSource.Manual, userEdited: true);
            vm.EnsureOptionForField(fieldName, value);
        }

        if (fieldName == "Zustandsklasse" && e.Row?.Item is HaltungRecord record)
        {
            handled = true;
            var value = GetEditedTextValue(e.EditingElement) ?? record.GetFieldValue(fieldName);
            record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: true);
        }

        if (fieldName == "Haltungsname" && e.Row?.Item is HaltungRecord hRecord)
        {
            handled = true;
            var oldValue = hRecord.GetFieldValue("Haltungsname");
            var newValue = GetEditedTextValue(e.EditingElement) ?? oldValue;
            if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                var sp = Services;
                var projectPath = sp?.Settings.LastProjectPath;

                // Erst Ordner + Pfade umbenennen, DANN erst den Namen setzen
                var renameResult = AuswertungPro.Next.Application.Common.HoldingRenameService.Rename(
                    hRecord, oldValue, newValue, projectPath);

                if (!renameResult.Success)
                {
                    Dialogs.Error($"Umbenennen fehlgeschlagen:\n{renameResult.ErrorMessage}", "Umbenennen");
                    return;
                }

                // Name erst nach erfolgreichem Rename setzen
                hRecord.SetFieldValue("Haltungsname", newValue, FieldSource.Manual, userEdited: true);
                PdfCorrectionMetadata.RegisterHoldingRename(vm.Project, oldValue, newValue);
            }
        }

        // S3: Alle uebrigen (normalen) Textspalten ebenfalls als manuell editiert markieren.
        // Die Sonderfelder oben setzen userEdited bereits selbst. Ohne diesen Zweig bliebe
        // FieldMeta.UserEdited fuer normale Spalten (z.B. Bemerkungen) auf false, und ein
        // spaeterer Re-Import koennte handeditierte Werte still ueberschreiben
        // (HaltungRecord.SetFieldValue/MergeEngine schuetzen nur Felder mit UserEdited==true).
        if (!handled && e.Row?.Item is HaltungRecord genericRecord)
        {
            var value = GetEditedTextValue(e.EditingElement);
            if (value is not null)
                genericRecord.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: true);
        }

        vm.ScheduleAutoSave();
    }

    private void ApplyFieldMetaTooltip(DataGridColumn col, string field)
    {
        var baseStyle = col.CellStyle;
        var style = new Style(typeof(DataGridCell), baseStyle);

        var tooltip = new TextBlock();
        var mb = new MultiBinding { StringFormat = "Quelle: {0} | UserEdited: {1} | Konflikt: {2}" };
        mb.Bindings.Add(new Binding($"FieldMeta[{field}].Source"));
        mb.Bindings.Add(new Binding($"FieldMeta[{field}].UserEdited"));
        mb.Bindings.Add(new Binding($"FieldMeta[{field}].Conflict"));
        tooltip.SetBinding(TextBlock.TextProperty, mb);
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, tooltip));

        col.CellStyle = style;
    }


    private BeobachtungenWindow? _beobachtungenWindow;

    // --- Abdocken / Andocken ---
    private FloatingGridWindow? _floatingGridWindow;
    // Merkt sich, welche Ansicht (Tabelle ODER Haltungsansicht) gerade abgedockt ist,
    // damit sie beim Andocken an die richtige Stelle zurueckkommt.
    private UIElement? _undockedView;

    private void UndockGrid_Click(object sender, RoutedEventArgs e)
    {
        UndockGrid();
    }

    private void DockBackFromPlaceholder_Click(object sender, RoutedEventArgs e)
    {
        DockGridBack();
    }

    private void UndockGrid()
    {
        if (_floatingGridWindow is not null)
        {
            _floatingGridWindow.Activate();
            return;
        }

        try
        {
            // Guard-Flag setzen damit der Unloaded-Handler nicht interferiert
            _isUndocking = true;

            // Die aktuell gezeigte Ansicht abdocken: Haltungsansicht wenn der Umschalter
            // an ist, sonst die Tabelle. Umschalter sperren, solange abgedockt.
            var active = HaltungsansichtToggle.IsChecked == true
                ? (UIElement)HaltungsansichtView
                : Grid;
            _undockedView = active;
            HaltungsansichtToggle.IsEnabled = false;

            // FloatingGridWindow erstellen (VOR dem Entfernen der Ansicht!)
            _floatingGridWindow = new FloatingGridWindow();
            _floatingGridWindow.DockBackRequested += DockGridBack;
            _floatingGridWindow.Closed += FloatingGridWindow_Closed;

            // DataContext auf FloatingWindow setzen (damit Bindings funktionieren)
            _floatingGridWindow.DataContext = DataContext;

            // Aktive Ansicht aus dem visuellen Baum entfernen und ins Floating-Fenster verschieben
            GridHost.Children.Remove(active);
            _floatingGridWindow.SetGridContent(active);
            active.Visibility = Visibility.Visible;

            // Platzhalter anzeigen
            UndockedPlaceholder.Visibility = Visibility.Visible;
            UndockButton.IsEnabled = false;

            // Fensterposition aus Settings laden
            var settings = Services.Settings;
            _floatingGridWindow.ApplySavedBounds(settings?.FloatingGridBounds);

            // Titel und Info aktualisieren
            UpdateFloatingWindowInfo();

            _floatingGridWindow.Show();

            // Settings merken
            if (settings is not null)
                settings.IsGridFloating = true;
        }
        catch (Exception ex)
        {
            // Bei Fehler: alles zuruecksetzen
            System.Diagnostics.Debug.WriteLine($"Undock error: {ex}");
            Dialogs.Warn($"Fehler beim Abdocken:\n{ex.Message}", "Abdocken");

            // Abgedockte Ansicht zuruecksetzen falls sie schon entfernt wurde
            if (_undockedView is not null)
            {
                if (!GridHost.Children.Contains(_undockedView))
                    GridHost.Children.Add(_undockedView);
                _undockedView.Visibility = Visibility.Visible;
                _undockedView = null;
            }
            UndockedPlaceholder.Visibility = Visibility.Collapsed;
            UndockButton.IsEnabled = true;
            HaltungsansichtToggle.IsEnabled = true; // bei fehlgeschlagenem Abdocken Umschalter wieder freigeben

            if (_floatingGridWindow is not null)
            {
                _floatingGridWindow.DockBackRequested -= DockGridBack;
                _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
                try { _floatingGridWindow.Close(); } catch { }
                _floatingGridWindow = null;
            }
        }
        finally
        {
            _isUndocking = false;
        }
    }

    private void DockGridBack()
    {
        if (_floatingGridWindow is null)
            return;

        // Fensterposition speichern
        var settings = Services.Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
            settings.IsGridFloating = false;
        }

        // Ansicht aus dem Floating-Fenster entfernen
        var view = _floatingGridWindow.RemoveGridContent();
        _floatingGridWindow.DockBackRequested -= DockGridBack;
        _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
        _floatingGridWindow.Close();
        _floatingGridWindow = null;

        RestoreUndockedView(view);

        // Platzhalter ausblenden
        UndockedPlaceholder.Visibility = Visibility.Collapsed;
        UndockButton.IsEnabled = true;
        HaltungsansichtToggle.IsEnabled = true;
    }

    // Holt die abgedockte Ansicht (Tabelle ODER Haltungsansicht) zurueck in den GridHost.
    private void RestoreUndockedView(UIElement? view)
    {
        var element = view ?? _undockedView;
        if (element is null)
            return;
        if (!GridHost.Children.Contains(element))
            GridHost.Children.Add(element);
        element.Visibility = Visibility.Visible;
        _undockedView = null;
    }

    private void FloatingGridWindow_Closed(object? sender, EventArgs e)
    {
        // Wenn das Floating-Fenster geschlossen wird (X-Button), Grid zurueck docken
        if (_floatingGridWindow is null)
            return;

        var settings = Services.Settings;
        if (settings is not null)
        {
            settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
            settings.IsGridFloating = false;
        }

        var view = _floatingGridWindow.RemoveGridContent();
        _floatingGridWindow.DockBackRequested -= DockGridBack;
        _floatingGridWindow = null;

        RestoreUndockedView(view);

        UndockedPlaceholder.Visibility = Visibility.Collapsed;
        UndockButton.IsEnabled = true;
        HaltungsansichtToggle.IsEnabled = true;
    }

    private void UpdateFloatingWindowInfo()
    {
        if (_floatingGridWindow is null)
            return;

        var vm = DataContext as DataPageViewModel;
        var projectName = vm?.Project?.Name;
        var count = vm?.Records?.Count ?? 0;
        var selected = vm?.Selected?.GetFieldValue("Haltungsname");
        _floatingGridWindow.UpdateInfo(projectName, count, selected);
    }

    private void BeobachtungenMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Beobachtungen");
            return;
        }

        vm.Selected = record;

        var holdingName = record.GetFieldValue("Haltungsname");

        Action vsaUpdateAction = () =>
        {
            var sp = Services;
            if (sp?.Vsa is null) return;
            var res = sp.Vsa.EvaluateRecord(record);
            if (res.Ok)
            {
                vm.RefreshSelectedRecord();
                Dialogs.Info($"VSA Zustand aktualisiert für {holdingName}.", "VSA");
            }
            else
            {
                Dialogs.Warn($"VSA Fehler: {res.ErrorMessage}", "VSA");
            }
        };

        Action syncHoldingFieldsAction = () =>
        {
            vm.SyncObservationsToHoldingFields(record, showStatus: true);
        };

        if (_beobachtungenWindow is not null && _beobachtungenWindow.IsLoaded)
        {
            _beobachtungenWindow.UpdateEntries(vm.SelectedProtocolEntries, holdingName, vsaUpdateAction, syncHoldingFieldsAction);
            _beobachtungenWindow.Activate();
            return;
        }

        _beobachtungenWindow = new BeobachtungenWindow(
            vm.SelectedProtocolEntries,
            Services,
            holdingName,
            vm.OpenProtocolCommand,
            record,
            vsaUpdateAction,
            syncHoldingFieldsAction)
        {
            Owner = Window.GetWindow(this)
        };
        _beobachtungenWindow.Closed += (_, _) => _beobachtungenWindow = null;
        _beobachtungenWindow.Show();
    }

    private void PlayMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Video");
            return;
        }
        vm.PlayVideoCommand.Execute(record);
    }

    private void MoveRecordUpMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.", "Position");
            return;
        }

        vm.Selected = record;
        if (vm.MoveUpCommand.CanExecute(null))
            vm.MoveUpCommand.Execute(null);
    }

    private void MoveRecordDownMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte zuerst eine Haltung auswaehlen.", "Position");
            return;
        }

        vm.Selected = record;
        if (vm.MoveDownCommand.CanExecute(null))
            vm.MoveDownCommand.Execute(null);
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is null)
            return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = PlacementMode.Bottom;
        btn.ContextMenu.DataContext = DataContext;
        btn.ContextMenu.IsOpen = true;
    }

    private void RelinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Video");
            return;
        }
        vm.RelinkVideoCommand.Execute(record);
    }

    private void CostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Massnahmen");
            return;
        }
        vm.OpenCostsCommand.Execute(record);
    }

    private void PrintAwuHaltungsprotokollMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Haltungsprotokoll AWU");
            return;
        }
        vm.PrintAwuHaltungsprotokollCommand.Execute(record);
    }

    private void OpenOriginalPdfMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "PDF");
            return;
        }
        vm.OpenOriginalPdfCommand.Execute(record);
    }

    private void RestoreCostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Kosten/Massnahmen");
            return;
        }
        vm.RestoreCostsCommand.Execute(record);
    }

    private void SuggestMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = ResolveActionRecord(sender, vm);
        if (record is null)
        {
            Dialogs.Info("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken oder zuerst eine Zeile auswaehlen.", "Massnahmen");
            return;
        }
        vm.SuggestMeasuresCommand.Execute(record);
    }

    private void SuggestAllMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        vm.SuggestAllMeasuresCommand.Execute(null);
    }

    private void MediaSearchMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        vm.SearchAndLinkMediaCommand.Execute(null);
    }

    private void HydraulikMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.OpenHydraulikCommand.Execute(record);
    }

    private void HydraulikPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.PrintHydraulikCommand.Execute(record);
    }

    private void DossierPrint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender) ?? vm.Selected;
        vm.PrintDossierCommand.Execute(record);
    }

    private void MoveToPositionBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        MoveToPosition_Click(sender, e);
    }

    private void MoveToPosition_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!int.TryParse(MoveToPositionBox.Text.Trim(), out var pos))
        {
            Dialogs.Info("Bitte eine gueltige Zahl eingeben.", "Position");
            return;
        }
        if (!vm.MoveToPosition(pos))
            Dialogs.Info("Verschieben nicht moeglich. Bitte Zeile auswaehlen.", "Position");
    }

    private void GoToRowBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        GoToRow_Click(sender, e);
    }

    private void GoToRow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        if (!int.TryParse(GoToRowBox.Text.Trim(), out var row) || row < 1)
        {
            Dialogs.Info("Bitte eine gueltige Zeilennummer eingeben.", "Gehe zu Zeile");
            return;
        }
        var idx = row - 1;
        if (idx >= vm.Records.Count)
            idx = vm.Records.Count - 1;
        if (idx >= 0)
        {
            vm.Selected = vm.Records[idx];
            Grid.ScrollIntoView(vm.Selected);
        }
    }

    private static HaltungRecord? GetContextMenuRecord(object sender)
    {
        if (sender is not DependencyObject dep)
            return null;

        var current = dep;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is HaltungRecord rec)
                return rec;

            if (current is ContextMenu menu)
            {
                if (menu.PlacementTarget is DataGridRow row)
                    return row.Item as HaltungRecord;
                if (menu.PlacementTarget is DataGrid grid)
                    return grid.SelectedItem as HaltungRecord;
            }

            current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static HaltungRecord? ResolveActionRecord(object sender, DataPageViewModel vm)
        => GetContextMenuRecord(sender) ?? vm.Selected;

    private static string? GetEditedTextValue(FrameworkElement? element)
    {
        if (element is ComboBox combo)
            return ResolveComboBoxValue(combo);
        if (element is TextBox textBox)
            return textBox.Text;
        return null;
    }

    private static string ResolveComboBoxValue(ComboBox combo)
    {
        if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            return selected;

        return combo.Text ?? string.Empty;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
                return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private HaltungRecord? GetDataGridRowItem(object source)
    {
        if (source is not DependencyObject dep)
            return null;
        var row = FindAncestor<DataGridRow>(dep);
        return row?.Item as HaltungRecord;
    }

    private void ResetSort()
    {
        var view = CollectionViewSource.GetDefaultView(Grid.ItemsSource);
        if (view is null)
            return;

        view.SortDescriptions.Clear();
        if (view is ListCollectionView listView)
            listView.CustomSort = null;

        foreach (var col in Grid.Columns)
            col.SortDirection = null;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void ApplySearchFilter()
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        DataGridSearchFilterController.Apply(
            CollectionViewSource.GetDefaultView(Grid.ItemsSource),
            vm.Records,
            getSearchText: () => vm.SearchText,
            matches: vm.MatchesSearch,
            updateSearchResultInfo: vm.UpdateSearchResultInfo,
            deferRefresh: action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action));
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

    private void ShowZustandsklasseExplanation(HaltungRecord record)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var sp = Services;
        var project = vm.Project;
        if (project is null)
        {
            Dialogs.Info("Kein Projekt geladen.", "Zustandsklasse");
            return;
        }

        var res = sp.Vsa.Explain(project, record);
        if (!res.Ok || res.Value is null)
        {
            Dialogs.Error(res.ErrorMessage ?? "Berechnung fehlgeschlagen.", "Zustandsklasse");
            return;
        }

        var holding = record.GetFieldValue("Haltungsname");
        var title = string.IsNullOrWhiteSpace(holding)
            ? "Zustandsklasse - Rechnungsweg"
            : $"Zustandsklasse - Rechnungsweg - {holding}";

        ShowTextPreview(title, res.Value);
    }

}
