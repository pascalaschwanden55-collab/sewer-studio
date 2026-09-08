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
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.DataPage.SchaechteColumnPolicy;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SchaechtePage : UserControl
{
    private SchaechtePageViewModel Vm => DataContext as SchaechtePageViewModel
        ?? throw new InvalidOperationException("SchaechtePage benoetigt SchaechtePageViewModel als DataContext.");
    private AppSettings Settings => Vm.Settings;
    private IDialogService Dialogs => Vm.Dialogs;

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
    private readonly SchaechteRecordDetailsBuilder _recordDetailsBuilder;
    private SchachtMassnahmenDialogController? _massnahmenController;
    // Alle Nachschlag-Befehle der Seite teilen sich diese Sperre: immer
    // nur eine Abfrage zur Zeit.
    private readonly NachschlagTor _nachschlagTor = new();
    private bool _isRestoringLayout;

    /// <summary>
    /// Der Wert der Zustandsklasse beim Oeffnen der Zelle (Task 6, Fix-Runde 1). Nur damit
    /// laesst sich beim Schliessen sagen, ob wirklich eine andere Klasse gewaehlt wurde: Die
    /// Marke ist eine Vorlagenspalte, deren Editierelement der Textleser nicht lesen kann.
    /// </summary>
    private string? _zustandsklasseBeimOeffnen;

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
        _recordDetailsBuilder = new SchaechteRecordDetailsBuilder(
            ResolveOptions,
            ResolveViewModelCommand,
            CommitSchachtDetailKonsolidiert,
            () => _vm is not null,
            BaueNachschlagBefehl,
            BaueStrassenBefehl);

        SchachtansichtView.DetailBuilder = BuildRecordDetailsForAnsicht;
        SchachtansichtView.DamageLineBuilder = SchachtDamageLineBuilder.Build;
        SchachtansichtView.ActionRequested = RouteSchachtansichtAction;
        // Robuster Grundzustand bis zum DataContext-Wechsel (wie DataPage): Standard ist die
        // Nova-Arbeitsflaeche; OnDataContextChanged wendet ShowSchaechteNovaLayout an.
        // Die Ansicht selbst wendet VerdrahteAufklappListe() am Ende des Konstruktors an.
        SchachtansichtToggle.IsChecked = false;

        DataContextChanged += OnDataContextChanged;
        Grid.AddHandler(DataGridColumnHeader.ClickEvent, new RoutedEventHandler(Grid_ColumnHeaderClick), true);
        Grid.ColumnReordered += Grid_ColumnReordered;

        Loaded += (_, __) =>
        {
            // Nach einem Unloaded sind Controller und Abo abgemeldet; WPF kann dieselbe Seite
            // wieder laden. Verdrahte() ist mehrfach sicher aufrufbar.
            _aufklappListe?.Verdrahte();
            _columnAlignmentToolbar.UpdateButtons();
            ApplySearchFilter();
        };
        Unloaded += (_, __) =>
        {
            // Beide Debounce-Timer stoppen (wie DataPage), sonst laeuft ein Such-Tick auf der
            // bereits entladenen Seite nach.
            _searchDebounceTimer.Stop();
            _layoutSaveDebounceTimer.Stop();
            SaveLayoutToSettings();
            _aufklappListe?.Dispose();
        };
        SizeChanged += (_, __) => ApplyDrawerHeight();
        VerdrahteNovaWorkspace();
        VerdrahteAufklappListe();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;

        _vm = e.NewValue as SchaechtePageViewModel;
        if (_vm is null)
        {
            _subscriptionController.Detach();
            _massnahmenController = null;
            SchachtansichtView.Settings = null;
            VerdrahteAufklappAbo(null);
            return;
        }

        SchachtansichtView.Settings = _vm.Settings;
        _massnahmenController = new SchachtMassnahmenDialogController(
            _vm.Settings,
            _vm.Dialogs,
            _vm.SchachtMassnahmenKatalog,
            _vm.SchachtRecommendationCosts,
            this,
            MarkProjectDirty,
            ApplySearchFilter,
            _vm.SchachtCostCatalog);
        _subscriptionController.Switch(_vm.Columns, _vm.Records, () => _vm.Records);
        InitNovaWorkspace(_vm);
        VerdrahteAufklappAbo(_vm);
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
                // Nova-Etappe 2b: Der Anzeigename wird EINMAL geholt; der Tabellenkopf schreibt
                // gross (siehe DataPageColumnFactory, GrossbuchstabenConverter-Doku).
                // GetDisplayHeader bleibt selbst unveraendert, weil SchaechteRecordDetailsBuilder
                // denselben Text auch als normale Feldbeschriftung im Formular verwendet.
                var kopf = GetDisplayHeader(col);
                var grossKopf = GrossbuchstabenConverter.Anwenden(kopf) ?? kopf;

                var istZustandsklasse = IsZustandsklasseColumn(col);
                DataGridColumn column;
                if (IsCostColumn(col))
                {
                    column = DataGridCostColumnFactory.Create(col, col);
                }
                else if (istZustandsklasse)
                {
                    column = CreateZustandsklasseColumn(col, grossKopf);
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
                        Binding = new Binding($"Fields[{col}]")
                        {
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                        },
                        // Nova-Fixwelle 2b (P3): Ein zu langer Wert wird mit Auslassungspunkten
                        // gekuerzt statt hart abgeschnitten ("KontrollschachDorfstrasse").
                        // Runde 2: Zahlenspalten bekommen zusaetzlich das rechte Polster.
                        ElementStyle = NovaTextZellenStil.MitAuslassungspunkten(
                            DataPageColumnStyleRules.IstZahlenspalte(col, SchachtFeldnamen.Falte)),
                        Width = DataGridLength.SizeToHeader,
                        MinWidth = 90,
                        // Die GEONIS-Kennung ist nur Anzeige; der Export liest das Geonis-Objekt.
                        IsReadOnly = string.Equals(
                            SchachtFeldnamen.Falte(col),
                            SchachtFeldnamen.Falte(FieldKeys.GeonisId),
                            StringComparison.Ordinal)
                    };
                }

                column.Header = grossKopf;
                column.SetValue(FrameworkElement.TagProperty, col);

                // Die Zustandsklasse traegt ihre Farbe seit Etappe 2b in der Marke, nicht mehr
                // in der ganzen Zelle: sonst stuende der Chip auf einer zweiten Farbflaeche.
                if (!istZustandsklasse)
                    ApplyColorStyle(column, col);
                // Nova-Fixwelle 2b (P3): Volltext oben im Hinweis, Herkunftszeile darunter —
                // dieselbe Regel wie in der Haltungsliste.
                column.CellStyle = DataGridFieldMetaTooltipStyleFactory.Create(col, column.CellStyle, mitVolltext: true);
                column.MinWidth = 90;
                // Startbreite aus dem Prototyp; ein gespeichertes Spaltenlayout gewinnt, weil
                // es erst mit RestoreLayoutFromSettings gelesen wird.
                if (NovaSpaltenbreiten.Startbreite(col, SchachtFeldnamen.Falte) is double startbreite)
                    column.Width = new DataGridLength(startbreite);
                Grid.Columns.Add(column);
                _columnFields[column] = col;

                // Nova-Fixwelle 2b (P1): Zahlen stehen rechts, auch die beiden Schachtmasse.
                // Schachtfelder heissen nach der Excel-Kopfzeile, deshalb der gefaltete
                // Vergleich. Eine gespeicherte Nutzerausrichtung gewinnt weiterhin: Sie kommt
                // erst mit RestoreLayoutFromSettings.
                var defaultHorizontal =
                    IsCostColumn(col) || DataPageColumnStyleRules.IstZahlenspalte(col, SchachtFeldnamen.Falte)
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left;
                _columnAlignmentToolbar.SetAlignment(column, defaultHorizontal, VerticalAlignment.Center);
            }

            ErgaenzeProtokollspalte();
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

    /// <summary>
    /// Nova-Etappe 2b: dieselbe Marke wie in der Haltungsliste. Anzeigen als Chip mit lesbarer
    /// Tinte, bearbeiten weiterhin als Auswahl 0 bis 4.
    /// </summary>
    private DataGridColumn CreateZustandsklasseColumn(string recordField, string header)
        => ZustandsklasseChipColumnFactory.Create(recordField, header);

    private void Grid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        _columnAlignmentToolbar.TrackSelectedCells();
        AktualisiereFelderDrawer();
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
        var layout = Settings.SchaechtePageLayout;

        // Nova-Fixwelle 2b, Runde 2: VOR dem Wiederherstellen; Schachtfelder heissen nach der
        // Kopfzeile der Excel-Vorlage, deshalb der gefaltete Vergleich.
        if (layout is not null)
            ZahlenRechtsMigration.WendeAn(layout, Settings.Save, SchachtFeldnamen.Falte);

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
        // null sein. Dann nichts speichern - kein Zugriff auf Vm/Settings erzwingen.
        if (_isRestoringLayout || _columnLayoutController.IsRestoring ||
            Grid.Columns.Count == 0 || DataContext is not SchaechtePageViewModel)
            return;

        var layout = Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
        layout.Columns = _columnLayoutController.Capture(Grid.Columns).Columns;

        Settings.SchaechtePageLayout = layout;
        Settings.Save();
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
            Dialogs.Info);
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
            Dialogs.Info,
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

        if (DataContext is not SchaechtePageViewModel vm)
            return;

        var zoom = DataPageGridZoomController.Resolve(
            vm.GridZoom,
            e.Delta,
            hasControlModifier: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
        if (!zoom.Handled)
            return;

        vm.GridZoom = zoom.NextZoom;
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
        if (record is null
            || !vm.CanMutateRecord(record, "Schachtfeld aendern"))
            return;

        var value = DataGridEditedTextValueResolver.ResolveComboBoxValue(combo);
        if (string.IsNullOrWhiteSpace(value))
            return;

        record.SetFieldValue(tag.RecordField, value, FieldSource.Manual, userEdited: true);
        vm.EnsureOptionForField(tag.OptionField, value);
        MarkProjectDirty();
        ApplySearchFilter();
    }

    private SchachtRecord? ResolveRecordFromComboBox(ComboBox combo)
    {
        if (combo.DataContext is SchachtRecord direct)
            return direct;

        var row = VisualTreeSafe.FindAncestor<DataGridRow>(combo);
        if (row?.Item is SchachtRecord fromRow)
            return fromRow;

        return Grid.CurrentItem as SchachtRecord;
    }

    private void Grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        _ = sender;

        _zustandsklasseBeimOeffnen = e.Row?.Item is SchachtRecord geoeffnet
            && e.Column?.GetValue(FrameworkElement.TagProperty) is string feld
            && IsZustandsklasseColumn(feld)
                ? geoeffnet.GetFieldValue(feld)
                : null;

        if (e.Row?.Item is not SchachtRecord record
            || DataContext is not SchaechtePageViewModel vm
            || vm.CanMutateRecord(record, "Schachtfeld aendern"))
        {
            return;
        }

        e.Cancel = true;
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        _ = sender;

        var zustandsklasseBeimOeffnen = _zustandsklasseBeimOeffnen;
        _zustandsklasseBeimOeffnen = null;

        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Row?.Item is not SchachtRecord record)
            return;
        if (DataContext is not SchaechtePageViewModel vm
            || !vm.CanMutateRecord(record, "Schachtfeld aendern"))
        {
            e.Cancel = true;
            return;
        }
        if (e.Column.GetValue(FrameworkElement.TagProperty) is not string recordField)
            return;

        if (IsCostColumn(recordField))
        {
            MarkProjectDirty();
            ApplySearchFilter();
            return;
        }

        // Die Zustandsklasse steht in einer Vorlagenspalte: Ihr Editierelement ist ein
        // ContentPresenter, aus dem der Textleser nichts holen kann. Die Auswahl hat ihren Wert
        // ueber die Bindung schon geschrieben — hier wird nur noch Herkunft und Handmarkierung
        // nachgezogen, und das nur bei echter Aenderung.
        if (IsZustandsklasseColumn(recordField))
        {
            if (!SchaechteFieldEditController.ApplyZustandsklasse(recordField, record, zustandsklasseBeimOeffnen))
                return;

            MarkProjectDirty();
            ApplySearchFilter();
            return;
        }

        if (!DataGridEditedTextValueResolver.TryResolve(e.EditingElement, out var value))
            return;

        if (!SchaechteFieldEditController.Apply(
                recordField,
                record,
                value,
                ApplySchachtNumberChange,
                EnsureSchachtOption))
            return;

        MarkProjectDirty();
        ApplySearchFilter();
    }

    private void Grid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        if (e.OriginalSource is not DependencyObject source)
            return;

        var cell = VisualTreeSafe.FindAncestor<DataGridCell>(source);
        if (cell is null)
            return;

        if (cell.Column?.GetValue(FrameworkElement.TagProperty) is not string fieldName)
            return;

        var row = VisualTreeSafe.FindAncestor<DataGridRow>(cell);
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
        var groups = _recordDetailsBuilder.Build(_vm?.Columns ?? [], record);
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
        => _recordDetailsBuilder.Build(_vm?.Columns ?? [], record);

    private void SchachtansichtToggle_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        WendeSchachtAnsichtAn();
    }

    private void RouteSchachtansichtAction(string actionKey, SchachtRecord record)
    {
        if (_vm is null)
            return;

        _vm.Selected = record;
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
            "SchachtformOptions" => _vm.SchachtformOptions,
            "BelastungsklasseOptions" => _vm.BelastungsklasseOptions,
            "SchachtFunktionOptions" => _vm.SchachtFunktionOptions,
            "SchachtMaterialOptions" => _vm.SchachtMaterialOptions,
            _ => Array.Empty<string>()
        };
    }

    private ICommand? ResolveViewModelCommand(string propertyName)
    {
        if (_vm is null || string.IsNullOrWhiteSpace(propertyName))
            return null;

        return _vm.GetType().GetProperty(propertyName)?.GetValue(_vm) as ICommand;
    }

    private void EnsureSchachtOption(string optionField, string? value)
        => _vm?.EnsureOptionForField(optionField, value);

    // Schreibt den Wert auf ALLE Encoding-Varianten des Feldes, damit keine "Geister-Duplikate"
    // mit altem Wert zurueckbleiben (die der Konsolidierer sonst wieder anzeigen wuerde).
    // Schachtnummer-Umbenennung laeuft wie gehabt; Optionen/Filter werden einmal aktualisiert.
    private void CommitSchachtDetailKonsolidiert(SchachtRecord record, KonsolidiertesSchachtFeld feld, string? value)
    {
        if (_vm is null
            || !_vm.CanMutateRecord(record, "Schachtdetail aendern"))
        {
            return;
        }

        var next = value ?? string.Empty;

        if (string.Equals(feld.PrimaerKey, "Schachtnummer", StringComparison.Ordinal))
        {
            var oldShaftNumber = record.GetFieldValue("Schachtnummer");
            if (!ApplySchachtNumberChange(record, oldShaftNumber, next))
                return;
        }
        else
        {
            foreach (var key in feld.AlleKeys)
                record.SetFieldValue(key, next, FieldSource.Manual, userEdited: true);
        }

        if (_vm is not null)
        {
            var optionField = ResolveOptionField(feld.AnzeigeName);
            if (!string.IsNullOrWhiteSpace(optionField))
                _vm.EnsureOptionForField(optionField, next);
        }

        MarkProjectDirty();
        ApplySearchFilter();
    }

    private bool ApplySchachtNumberChange(SchachtRecord record, string? oldValue, string? newValue)
        => SchaechteShaftRenameController.Apply(
            Vm.ShaftRename,
            Vm.PdfTextLayerRewrite,
            record,
            oldValue,
            newValue,
            Settings.LastProjectPath,
            GetCurrentProject(),
            (message, title) => DialogHost.Current.Error(message, title));

    /// <summary>
    /// Nova-Fixwelle 2b, Runde 2: Derselbe <see cref="DataPageRightClickController"/> wie auf
    /// der Haltungsseite. Vorher entschied dieser Pfad selbst — und kannte den Schutz gegen
    /// virtuelle Spalten nicht: „Spalte leeren" auf dem Kopf der Protokollspalte schrieb
    /// <c>Nova_Protokoll</c> in JEDEN Schachtdatensatz. Zwei Wege zu derselben Entscheidung
    /// heisst, dass nur einer den Schutz bekommt.
    /// </summary>
    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject originalSource)
            return;

        var header = VisualTreeSafe.FindAncestor<DataGridColumnHeader>(originalSource);
        var row = VisualTreeSafe.FindAncestor<DataGridRow>(originalSource);

        var ergebnis = DataPageRightClickController.Resolve(
            ClearColumnModeButton.IsChecked == true,
            header?.Column.GetValue(FrameworkElement.TagProperty) as string,
            header?.Column.Header?.ToString(),
            row?.Item);

        switch (ergebnis.Action)
        {
            case DataPageRightClickAction.ClearColumn when ergebnis.FieldName is { } feld:
                ClearColumn(feld, ergebnis.DisplayName ?? feld);
                e.Handled = true;
                break;
            case DataPageRightClickAction.SelectRow:
                Grid.SelectedItem = ergebnis.RowItem;
                break;
        }
    }

    private void ClearColumn(string fieldName, string displayName)
    {
        if (_vm is null || !_vm.CanMutateShaftData)
            return;

        if (!DialogHost.Current.ConfirmWarn(
            $"Alle Werte in Spalte \"{displayName}\" löschen?",
            "Spalte leeren"))
        {
            return;
        }

        // Bewusstes Leeren ist ebenfalls eine Entscheidung des Menschen und darf
        // nicht spaeter von einem automatischen Schreiber wieder gefuellt werden.
        foreach (var record in _vm.Records)
        {
            if (!_vm.CanMutateRecord(record, "Schachtspalte leeren"))
                return;

            record.SetFieldValue(fieldName, string.Empty, FieldSource.Manual, userEdited: true);
        }

        MarkProjectDirty();
    }

    private void ProtokollMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_vm is not { } vm)
            return;

        CreateFileActionController(vm).OpenProtocol(
            vm.Selected,
            vm.Settings.LastProjectPath);
    }

    private void OpenContainingFolderMenu_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_vm is not { } vm)
            return;

        CreateFileActionController(vm).RevealContainingFolder(
            vm.Selected,
            vm.Settings.LastProjectPath);
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

    private void OpenSchachtMassnahmen(SchachtRecord record)
    {
        if (_vm is null
            || !_vm.CanMutateRecord(record, "Sanierungsmassnahmen bearbeiten"))
        {
            return;
        }

        _massnahmenController?.Open(record);
    }

    private static SchaechteFileActionController CreateFileActionController(
        SchaechtePageViewModel viewModel)
        => new(
            viewModel.SchachtFileTargets,
            viewModel.ShellOpen,
            viewModel.ExplorerReveal,
            viewModel.Dialogs);

}
