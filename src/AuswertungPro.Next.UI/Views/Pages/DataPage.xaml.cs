using System;
using System.Collections.Generic;
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
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Behaviors;

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
    private readonly DataPageDetailItemFactory _haltungDetailItemFactory;
    private readonly DataPageRecordDetailsDialogController _recordDetailsDialogController;
    private readonly DataPageBeobachtungenController _beobachtungenController;
    private readonly DispatcherTimer _layoutSaveDebounceTimer;
    private bool _updatingAlignmentButtons;
    private bool _isUndocking;
    private DataGridColumn? _activeColumn;
    private bool _startFilterApplied;

    public DataPage()
    {
        InitializeComponent();
        PhotoHoverPreviewBehavior.SetPhotoPathsSelector(Grid, PhotoHoverPreviewSelectors.HaltungRecordPhotos);
        PhotoHoverPreviewBehavior.SetProjectRootProvider(
            Grid,
            () => DataContext is DataPageViewModel vm
                ? ProjectFileLocator.ProjectRootFromFile(vm.Services.Settings.LastProjectPath)
                : null);
        FilterChips.FilterGeaendert += WendeChipFilterAn;
        _haltungDetailItemFactory = new DataPageDetailItemFactory(
            ResolveManagedComboSpec,
            CommitHaltungDetailField);
        _recordDetailsDialogController = new DataPageRecordDetailsDialogController(
            BuildHaltungRecordDetails,
            CreateSuggestMeasuresCommand);
        _beobachtungenController = new DataPageBeobachtungenController(
            (message, title) => Dialogs.Info(message, title),
            (message, title) => Dialogs.Warn(message, title),
            record =>
            {
                var result = Services.Vsa.EvaluateRecord(record);
                return new DataPageBeobachtungenVsaResult(result.Ok, result.ErrorMessage);
            });

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
            ApplyStartFilter();
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
        // Aussen gewaehlte Haltung (z.B. Klick auf der Karte) in der Liste sichtbar scrollen.
        if (e.PropertyName == nameof(ViewModels.Pages.DataPageViewModel.Selected)
            && DataContext is ViewModels.Pages.DataPageViewModel vm
            && vm.Selected is { } selected)
        {
            Dispatcher.InvokeAsync(
                () => Grid.ScrollIntoView(selected),
                System.Windows.Threading.DispatcherPriority.Background);
        }
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
            var col = DataPageColumnFactory.Create(
                field,
                def.Label,
                ComboBox_LostKeyboardFocus,
                ComboBox_SelectionChanged);

            var setup = DataPageColumnSetup.Apply(col, field);
            Grid.Columns.Add(col);

            ApplyColumnAlignment(col, setup.DefaultHorizontalAlignment, setup.DefaultVerticalAlignment);
        }

        Grid.FrozenColumnCount = 2;
        RestoreLayoutFromSettings();
        ResetSort();
    }

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
        if (e.OriginalSource is not DependencyObject originalSource)
            return;

        var header = FindAncestor<DataGridColumnHeader>(originalSource);
        var row = FindAncestor<DataGridRow>(originalSource);
        var fieldName = header?.Column.GetValue(FrameworkElement.TagProperty) as string;
        var displayName = header?.Column.Header?.ToString() ?? fieldName;

        var result = DataPageRightClickController.Resolve(
            ClearColumnMenuItem.IsChecked,
            fieldName,
            displayName,
            row?.Item);

        switch (result.Action)
        {
            case DataPageRightClickAction.ClearColumn:
                ClearColumn(result.FieldName!, result.DisplayName!);
                e.Handled = true;
                break;
            case DataPageRightClickAction.SelectRow:
                Grid.SelectedItem = result.RowItem;
                break;
        }
    }

    private void ClearColumn(string fieldName, string displayName)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var plan = DataPageClearColumnController.BuildPlan(vm.Records, fieldName);
        if (plan.Status == DataPageClearColumnStatus.AlreadyEmpty)
        {
            Dialogs.Info($"Spalte \"{displayName}\" ist bereits leer.", "Spalte leeren");
            return;
        }

        if (!Dialogs.ConfirmWarn(
            $"ACHTUNG: Alle Werte in Spalte \"{displayName}\" werden geloescht.\n\n" +
            $"Betroffen: {plan.AffectedCount} von {plan.TotalCount} Haltungen.\n" +
            "Auch manuell bearbeitete Werte gehen verloren und koennen nicht rueckgaengig gemacht werden.\n\n" +
            "Wirklich loeschen?",
            "Spalte leeren"))
            return;

        DataPageClearColumnController.ClearColumn(vm.Records, fieldName);
        vm.ScheduleAutoSave();
    }

    private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        if (DataContext is not DataPageViewModel vm)
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

        var record = ResolveRecordFromComboBox(combo);
        DataPageComboBoxCommitController.Commit(
            fieldName,
            vm.IsProjectReady,
            record,
            DataGridEditedTextValueResolver.ResolveComboBoxValue(combo),
            vm.EnsureOptionForField,
            vm.ScheduleAutoSave);
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
        if (e.OriginalSource is not DependencyObject dep)
            return;

        System.Windows.Point mousePos = e.GetPosition(null);
        System.Windows.Vector diff = _dragStartPoint - mousePos;
        if (!DataPageDragStartPolicy.ShouldStartDrag(
                isProjectReady: DataContext is not DataPageViewModel vm || vm.IsProjectReady,
                isLeftButtonPressed: e.LeftButton == System.Windows.Input.MouseButtonState.Pressed,
                isEditingTextBox: FindAncestor<TextBox>(dep) is not null,
                deltaX: diff.X,
                deltaY: diff.Y,
                minimumHorizontalDragDistance: SystemParameters.MinimumHorizontalDragDistance,
                minimumVerticalDragDistance: SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(dep);
        if (row == null) return;
        var record = row.Item as HaltungRecord;
        if (record == null) return;
        DragDrop.DoDragDrop(row, record, DragDropEffects.Move);
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

            if (DataPageDropReorderController.TryMoveAndRenumber(vm.Records, droppedData, target))
                ResetSort();
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
        // ContextMenu) -> der Resolver faellt sauber auf die aktuelle Auswahl zurueck.
        // faellt auf das gerade gesetzte vm.Selected zurueck. Darum reicht 'this' als Sender.
        vm.Selected = record;
        var e = new RoutedEventArgs();
        switch (actionKey)
        {
            case "codieren": vm.OpenProtocolCommand.Execute(record); break;
            case "play": PlayMenu_Click(this, e); break;
            case "playgegen": PlayGegenMenu_Click(this, e); break;
            case "beobachtungen": BeobachtungenMenu_Click(this, e); break;
            case "printawu": PrintAwuHaltungsprotokollMenu_Click(this, e); break;
            case "openpdf": OpenOriginalPdfMenu_Click(this, e); break;
            case "openfolder": OpenContainingFolderMenu_Click(this, e); break;
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
        var request = _recordDetailsDialogController.Build(record);
        ShowRecordDetailsWindow(request);
    }

    private ICommand? CreateSuggestMeasuresCommand(HaltungRecord record)
    {
        if (DataContext is DataPageViewModel vm)
            return new RelayCommand(() => vm.OpenCostsCommand.Execute(record));

        return null;
    }

    private void ShowRecordDetailsWindow(DataPageRecordDetailsDialogRequest request)
    {
        var window = new RecordDetailsWindow(
            title: request.Title,
            header: request.Header,
            subHeader: request.SubHeader,
            groups: request.Groups,
            suggestMeasuresCommand: request.SuggestMeasuresCommand)
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
        return _haltungDetailItemFactory.Create(fieldName, record);
    }

    private DataPageManagedComboSpec? ResolveManagedComboSpec(string fieldName)
    {
        if (DataContext is not DataPageViewModel vm)
            return null;

        if (!GridDropdownFieldPolicy.TryResolve(fieldName, out var spec) || !spec.Managed)
            return null;

        return spec.OptionField switch
        {
            "Sanieren_JaNein" => new DataPageManagedComboSpec(
                vm.SanierenOptions,
                spec.AllowFreeText,
                vm.EditSanierenOptionsCommand,
                vm.PreviewSanierenOptionsCommand,
                vm.ResetSanierenOptionsCommand),
            "Eigentuemer" => new DataPageManagedComboSpec(
                vm.EigentuemerOptions,
                spec.AllowFreeText,
                vm.EditEigentuemerOptionsCommand,
                vm.PreviewEigentuemerOptionsCommand,
                vm.ResetEigentuemerOptionsCommand),
            "Pruefungsresultat" => new DataPageManagedComboSpec(
                vm.PruefungsresultatOptions,
                spec.AllowFreeText,
                vm.EditPruefungsresultatOptionsCommand,
                vm.PreviewPruefungsresultatOptionsCommand,
                vm.ResetPruefungsresultatOptionsCommand),
            "Referenzpruefung" => new DataPageManagedComboSpec(
                vm.ReferenzpruefungOptions,
                spec.AllowFreeText,
                vm.EditReferenzpruefungOptionsCommand,
                vm.PreviewReferenzpruefungOptionsCommand,
                vm.ResetReferenzpruefungOptionsCommand),
            _ => null
        };
    }

    private void CommitHaltungDetailField(HaltungRecord record, string fieldName, string? value)
    {
        var next = value ?? string.Empty;
        var vm = DataContext as DataPageViewModel;

        if (fieldName == "Haltungsname" && vm is not null)
        {
            // Haltungsnummer-Aenderung MUSS ueber den Rename laufen (Verteil-Ordner/Dateien mitziehen),
            // sonst zeigen Link/PDF nach dem Umbenennen ins Leere. Gleicher Pfad wie der Datagrid-Edit.
            var oldValue = record.GetFieldValue("Haltungsname");
            if (!ApplyHoldingNameChange(record, oldValue, next, vm))
                return;   // Rename fehlgeschlagen -> Name nicht aendern
        }
        else
        {
            record.SetFieldValue(fieldName, next, FieldSource.Manual, userEdited: true);
        }

        if (vm is not null)
        {
            vm.EnsureOptionForField(fieldName, next);
            vm.ScheduleAutoSave();
        }
    }

    /// <summary>
    /// Wendet eine Haltungsnamen-Aenderung an: benennt Verteil-Ordner + Dateien um
    /// (<see cref="AuswertungPro.Next.Application.Common.HoldingRenameService"/>), setzt den Namen,
    /// registriert den Rename und zieht die Nummer im Protokoll-PDF-Text mit. Gibt false zurueck, wenn
    /// der Rename fehlschlaegt (dann bleibt der Name unveraendert). Wird aus dem Datagrid-Zellen-Edit
    /// UND dem Formular-Detail-Editor aufgerufen, damit die Verteilung in beiden Faellen konsistent bleibt.
    /// </summary>
    private bool ApplyHoldingNameChange(HaltungRecord record, string? oldValue, string? newValue, DataPageViewModel vm)
    {
        var oldName = oldValue ?? string.Empty;
        var newName = newValue ?? string.Empty;
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (vm.Project.HasDuplicateHoldingName(newName, record.Id))
        {
            Dialogs.Warn($"Die Haltungsnummer '{newName.Trim()}' ist bereits vorhanden.", "Doppelte Haltungsnummer");
            return false;
        }

        var projectPath = Services?.Settings.LastProjectPath;

        // Erst Ordner + Pfade umbenennen, DANN erst den Namen setzen
        var renameResult = AuswertungPro.Next.Application.Common.HoldingRenameService.Rename(
            record, oldName, newName, projectPath);

        if (!renameResult.Success)
        {
            Dialogs.Error($"Umbenennen fehlgeschlagen:\n{renameResult.ErrorMessage}", "Umbenennen");
            return false;
        }

        record.SetFieldValue("Haltungsname", newName, FieldSource.Manual, userEdited: true);
        PdfCorrectionMetadata.RegisterHoldingRename(vm.Project, oldName, newName);

        // Haltungsnummer auch im Protokoll-PDF-Text mitziehen (best-effort, nur Text-PDFs;
        // Bild-/Scan-PDFs bleiben unveraendert). Die PDF-Pfade wurden vom Rename bereits aktualisiert.
        var pdfSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void CollectPdf(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolved = AuswertungPro.Next.Application.Common.ProjectPathResolver.ResolveFilePath(part.Trim(), projectPath);
                if (!string.IsNullOrWhiteSpace(resolved) && resolved.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    pdfSet.Add(resolved);
            }
        }
        CollectPdf(record.GetFieldValue("PDF_Path"));
        CollectPdf(record.GetFieldValue("PDF_All"));
        if (pdfSet.Count > 0)
        {
            var pdfRewrite = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor.RewriteHoldingInPdfFiles(
                new System.Collections.Generic.List<string>(pdfSet), oldName, newName);
            if (pdfRewrite.Failed > 0)
            {
                Dialogs.Error(
                    $"{pdfRewrite.Failed} Protokoll-PDF(s) konnten nicht aktualisiert werden.\n" +
                    "Die bisherigen PDF-Dateien wurden nicht ueberschrieben.",
                    "PDF nicht aktualisiert");
            }
        }

        return true;
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

        var plan = DataPagePhotoLinkController.BuildOpenPlan(
            fe.Tag as string,
            Services?.Settings.LastProjectPath,
            AuswertungPro.Next.Application.Common.ProjectPathResolver.ResolveFilePath,
            File.Exists);

        if (plan.Status == DataPagePhotoLinkStatus.Noop)
            return;

        if (plan.Status == DataPagePhotoLinkStatus.Missing)
        {
            Dialogs.Info($"Foto nicht gefunden:\n{plan.RawPath}", "Foto");
            return;
        }

        if (!AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(plan.ResolvedPath!, out var error))
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

        if (sender is not FrameworkElement fe)
            return;

        var entry = DataPageProtocolMediaLinkController.ResolveEntry(fe.Tag, fe.DataContext);
        if (entry is null)
        {
            Dialogs.Info("Keine Beobachtung erkannt.", "Video");
            return;
        }

        var targetTime = DataPageProtocolMediaLinkController.ResolveTargetTime(entry);
        vm.PlayVideoCommand.Execute(record);

        if (targetTime is null)
            return;

        var overlayText = DataPageProtocolMediaLinkController.BuildOverlayText(entry);
        SeekVideoWithRetry(targetTime.Value, overlayText);
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

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Column.GetValue(FrameworkElement.TagProperty) is not string fieldName)
            return;

        if (DataContext is not DataPageViewModel vm)
            return;

        var record = e.Row?.Item as HaltungRecord;
        var editedValue = DataGridEditedTextValueResolver.Resolve(e.EditingElement);
        var shouldSave = DataPageCellEditController.Apply(
            fieldName,
            record,
            editedValue,
            (message, title) => Dialogs.ConfirmWarn(message, title, defaultNo: true),
            vm.EnsureOptionForField,
            (item, oldValue, newValue) => ApplyHoldingNameChange(item, oldValue, newValue, vm));
        if (!shouldSave)
            return;

        vm.ScheduleAutoSave();
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
        DataGridSortResetController.Reset(
            CollectionViewSource.GetDefaultView(Grid.ItemsSource),
            Grid.Columns);
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
