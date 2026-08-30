using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Die neue persoenliche Gestaltung der Detailansicht nach einer Aenderung.
/// </summary>
public sealed class RecordDetailLayoutChangedEventArgs : EventArgs
{
    public RecordDetailLayoutChangedEventArgs(RecordDetailLayout layout)
        => Layout = layout ?? throw new ArgumentNullException(nameof(layout));

    /// <summary>Spalten, Feldreihenfolge und ausgeblendete Felder - genau das wird gespeichert.</summary>
    public RecordDetailLayout Layout { get; }
}

public partial class RecordDetailsView : UserControl
{
    private static readonly Regex NonNumericRegex = new("[^0-9]", RegexOptions.Compiled);

    public RecordDetailsView() => InitializeComponent();

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata("Details"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty SubHeaderProperty =
        DependencyProperty.Register(nameof(SubHeader), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata(string.Empty));

    public string SubHeader
    {
        get => (string)GetValue(SubHeaderProperty);
        set => SetValue(SubHeaderProperty, value);
    }

    public static readonly DependencyProperty IsCompactLayoutProperty =
        DependencyProperty.Register(nameof(IsCompactLayout), typeof(bool), typeof(RecordDetailsView),
            new PropertyMetadata(false, OnColumnVisibilityInputChanged));

    private static void OnColumnVisibilityInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        _ = e;
        (d as RecordDetailsView)?.RefreshVisibleGroups();
    }

    public bool IsCompactLayout
    {
        get => (bool)GetValue(IsCompactLayoutProperty);
        set => SetValue(IsCompactLayoutProperty, value);
    }

    public static readonly DependencyProperty GroupsProperty =
        DependencyProperty.Register(nameof(Groups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(RecordDetailsView),
            new PropertyMetadata(null, OnGroupsChanged));

    /// <summary>Alle Gruppen, wie der Builder sie liefert.</summary>
    public IReadOnlyList<RecordDetailGroup>? Groups
    {
        get => (IReadOnlyList<RecordDetailGroup>?)GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public static readonly DependencyProperty VisibleGroupsProperty =
        DependencyProperty.Register(nameof(VisibleGroups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    /// <summary>
    /// Die tatsaechlich angezeigten Spalten: ohne die, in denen keine Karte mehr sichtbar
    /// ist. Da sich die Spalten gleichmaessig auf die Breite verteilen, bekommen die
    /// uebrigen den Platz von selbst.
    /// </summary>
    public IReadOnlyList<RecordDetailGroup>? VisibleGroups
    {
        get => (IReadOnlyList<RecordDetailGroup>?)GetValue(VisibleGroupsProperty);
        private set => SetValue(VisibleGroupsProperty, value);
    }

    private static void OnGroupsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RecordDetailsView view)
            return;

        view.SubscribeItems(e.OldValue as IReadOnlyList<RecordDetailGroup>, subscribe: false);
        view.SubscribeItems(e.NewValue as IReadOnlyList<RecordDetailGroup>, subscribe: true);
        view.RefreshVisibleGroups();
        view.RefreshHiddenFields();
    }

    /// <summary>
    /// Haengt an jeder Karte, damit eine zur Laufzeit umschlagende Sichtbarkeit
    /// ("Sanieren = Nein") die Spalten sofort neu aufteilt.
    /// </summary>
    private void SubscribeItems(IReadOnlyList<RecordDetailGroup>? groups, bool subscribe)
    {
        if (groups is null)
            return;

        foreach (var group in groups)
        {
            foreach (var item in group.Items)
            {
                item.PropertyChanged -= Item_VisibilityChanged;
                if (subscribe)
                    item.PropertyChanged += Item_VisibilityChanged;
            }
        }
    }

    private void Item_VisibilityChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.PropertyName is not (nameof(RecordDetailItem.IsVisible) or nameof(RecordDetailItem.IsHiddenByUser)))
            return;

        RefreshVisibleGroups();
    }

    private void RefreshVisibleGroups()
    {
        var groups = Groups;
        VisibleGroups = groups is null
            ? null
            : RecordDetailColumnVisibility.Filter(groups, IsCustomizing, IsCompactLayout);
    }

    public static readonly DependencyProperty SuggestMeasuresCommandProperty =
        DependencyProperty.Register(nameof(SuggestMeasuresCommand), typeof(ICommand), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    public ICommand? SuggestMeasuresCommand
    {
        get => (ICommand?)GetValue(SuggestMeasuresCommandProperty);
        set => SetValue(SuggestMeasuresCommandProperty, value);
    }

    public static readonly DependencyProperty IsCustomizingProperty =
        DependencyProperty.Register(nameof(IsCustomizing), typeof(bool), typeof(RecordDetailsView),
            new PropertyMetadata(false, OnColumnVisibilityInputChanged));

    /// <summary>
    /// Anpassen-Modus: erlaubt Ziehen von Karten und Spalten, das Ausblenden einzelner
    /// Karten und zeigt die Leiste der ausgeblendeten Felder.
    /// Standard aus - im normalen Betrieb laesst sich dadurch nichts versehentlich
    /// verschieben, und Ansichten ohne Speicherweg bieten gar nichts erst an.
    /// </summary>
    public bool IsCustomizing
    {
        get => (bool)GetValue(IsCustomizingProperty);
        set => SetValue(IsCustomizingProperty, value);
    }

    public static readonly DependencyProperty CanCustomizeProperty =
        DependencyProperty.Register(nameof(CanCustomize), typeof(bool), typeof(RecordDetailsView),
            new PropertyMetadata(false));

    /// <summary>
    /// Blendet den Knopf "Ansicht anpassen" ein. Nur Ansichten, die das Ergebnis auch
    /// speichern, setzen das - sonst waere die Einstellung nach dem naechsten Klick weg.
    /// </summary>
    public bool CanCustomize
    {
        get => (bool)GetValue(CanCustomizeProperty);
        set => SetValue(CanCustomizeProperty, value);
    }

    public static readonly DependencyProperty HiddenFieldsProperty =
        DependencyProperty.Register(nameof(HiddenFields), typeof(IReadOnlyList<RecordDetailItem>), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    /// <summary>Die ausgeblendeten Karten fuer die Leiste am unteren Rand.</summary>
    public IReadOnlyList<RecordDetailItem>? HiddenFields
    {
        get => (IReadOnlyList<RecordDetailItem>?)GetValue(HiddenFieldsProperty);
        private set => SetValue(HiddenFieldsProperty, value);
    }

    /// <summary>
    /// Meldet die neue Gestaltung, nachdem der Benutzer etwas verschoben, ausgeblendet
    /// oder zurueckgeholt hat. Genau das gehoert gespeichert.
    /// </summary>
    public event EventHandler<RecordDetailLayoutChangedEventArgs>? LayoutChanged;

    /// <summary>Der Benutzer hat "Standard wiederherstellen" gewaehlt.</summary>
    public event EventHandler? LayoutResetRequested;

    private const string FieldDragFormat = "SewerStudio.RecordDetailField";
    private const string ColumnDragFormat = "SewerStudio.RecordDetailColumn";
    private Point _dragOrigin;
    private RecordDetailItem? _fieldDragCandidate;
    private RecordDetailGroup? _columnDragCandidate;
    private RecordDetailInsertionAdorner? _insertionAdorner;
    private AdornerLayer? _insertionLayer;

    // --- Bedienknoepfe ------------------------------------------------------

    private void CustomizeStart_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        IsCustomizing = true;
        RefreshHiddenFields();
    }

    private void CustomizeDone_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        IsCustomizing = false;
    }

    private void CustomizeReset_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        LayoutResetRequested?.Invoke(this, EventArgs.Empty);
        RefreshHiddenFields();
    }

    private void HideField_Click(object sender, RoutedEventArgs e)
    {
        _ = e;
        if (!IsCustomizing || sender is not FrameworkElement { DataContext: RecordDetailItem item })
            return;

        item.IsHiddenByUser = true;
        PublishLayout();
    }

    private void ShowField_Click(object sender, RoutedEventArgs e)
    {
        _ = e;
        if (!IsCustomizing || sender is not FrameworkElement { DataContext: RecordDetailItem item })
            return;

        item.IsHiddenByUser = false;
        PublishLayout();
    }

    /// <summary>
    /// Baut die Leiste der ausgeblendeten Karten neu auf und meldet den Stand nach aussen.
    /// </summary>
    private void PublishLayout()
    {
        var groups = Groups;
        if (groups is null)
            return;

        RefreshHiddenFields();
        LayoutChanged?.Invoke(this, new RecordDetailLayoutChangedEventArgs(RecordDetailLayoutApplier.Capture(groups)));
    }

    private void RefreshHiddenFields()
    {
        var groups = Groups;
        if (groups is null)
        {
            HiddenFields = Array.Empty<RecordDetailItem>();
            return;
        }

        HiddenFields = groups
            .SelectMany(g => g.Items)
            .Where(x => x.IsHiddenByUser)
            .ToList();
    }

    // --- Ziehen: Karte ------------------------------------------------------

    private void FieldHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _fieldDragCandidate = null;
        if (!IsCustomizing || sender is not FrameworkElement { DataContext: RecordDetailItem item })
            return;

        _dragOrigin = e.GetPosition(this);
        _fieldDragCandidate = item;
    }

    private void FieldHandle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;
        if (_fieldDragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!DragSchwelleUeberschritten(e))
            return;

        var dragged = _fieldDragCandidate;
        _fieldDragCandidate = null;
        StartDrag(FieldDragFormat, dragged);
    }

    // --- Ziehen: Spalte -----------------------------------------------------

    private void ColumnHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _columnDragCandidate = null;
        if (!IsCustomizing || sender is not FrameworkElement { DataContext: RecordDetailGroup group })
            return;

        _dragOrigin = e.GetPosition(this);
        _columnDragCandidate = group;
    }

    private void ColumnHandle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;
        if (_columnDragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!DragSchwelleUeberschritten(e))
            return;

        var dragged = _columnDragCandidate;
        _columnDragCandidate = null;
        StartDrag(ColumnDragFormat, dragged);
    }

    private bool DragSchwelleUeberschritten(MouseEventArgs e)
    {
        var current = e.GetPosition(this);
        return Math.Abs(current.X - _dragOrigin.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - _dragOrigin.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void StartDrag(string format, object payload)
    {
        try
        {
            // Blockiert bis zum Ende des Ziehens - danach ist die Einfuegemarke in jedem
            // Fall wieder weg, auch bei Abbruch mit Escape.
            DragDrop.DoDragDrop(this, new DataObject(format, payload), DragDropEffects.Move);
        }
        finally
        {
            ClearInsertionAdorner();
        }
    }

    // --- Ablegen: Karte -----------------------------------------------------

    private void FieldCard_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!TryResolveFieldDrop(sender, e, out var card, out _, out var insertAfter))
        {
            if (!e.Data.GetDataPresent(FieldDragFormat))
                return;

            ClearInsertionAdorner();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        ShowInsertionAdorner(card, insertAfter, vertical: false);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void FieldCard_PreviewDrop(object sender, DragEventArgs e)
    {
        ClearInsertionAdorner();

        var groups = Groups;
        if (groups is null || !TryResolveFieldDrop(sender, e, out _, out var move, out _))
        {
            if (!e.Data.GetDataPresent(FieldDragFormat))
                return;

            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var reordered = RecordDetailDragOperations.MoveField(
            groups, move.FromTitle, move.FromIndex, move.ToTitle, move.ToIndex);

        e.Effects = reordered is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
        if (reordered is null)
            return;

        Groups = reordered;
        PublishLayout();
    }

    /// <summary>
    /// Ablage einer Karte auf eine andere Karte - in derselben oder in einer fremden Spalte.
    /// </summary>
    private bool TryResolveFieldDrop(
        object sender,
        DragEventArgs e,
        out FrameworkElement card,
        out (string FromTitle, int FromIndex, string ToTitle, int ToIndex) move,
        out bool insertAfter)
    {
        card = null!;
        move = (string.Empty, -1, string.Empty, -1);
        insertAfter = false;

        var groups = Groups;
        if (!IsCustomizing || groups is null)
            return false;

        if (sender is not FrameworkElement { DataContext: RecordDetailItem target } element)
            return false;

        if (!e.Data.GetDataPresent(FieldDragFormat) || e.Data.GetData(FieldDragFormat) is not RecordDetailItem source)
            return false;

        if (ReferenceEquals(source, target))
            return false;

        if (!RecordDetailDragOperations.TryLocateField(groups, source, out var fromTitle, out var fromIndex))
            return false;

        if (!RecordDetailDragOperations.TryLocateField(groups, target, out var toTitle, out var targetIndex))
            return false;

        var sameColumn = string.Equals(fromTitle, toTitle, StringComparison.Ordinal);
        var count = ItemCount(groups, toTitle);

        insertAfter = e.GetPosition(element).Y > element.ActualHeight / 2d;
        var toIndex = RecordDetailDragOperations.ResolveDropTarget(fromIndex, targetIndex, insertAfter, count, sameColumn);
        if (toIndex < 0)
            return false;

        card = element;
        move = (fromTitle, fromIndex, toTitle, toIndex);
        return true;
    }

    // --- Ablegen: Spalte ----------------------------------------------------

    private void ColumnCard_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (TryResolveColumnDrop(sender, e, out var column, out _, out var insertAfter))
        {
            ShowInsertionAdorner(column, insertAfter, vertical: true);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (TryResolveEmptyColumnDrop(sender, e, out var leereSpalte, out _))
        {
            ShowInsertionAdorner(leereSpalte, insertAfter: false, vertical: false);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        // Eine gezogene Karte gehoert der Kartenebene - hier nicht anfassen.
        if (!e.Data.GetDataPresent(ColumnDragFormat))
            return;

        ClearInsertionAdorner();
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void ColumnCard_PreviewDrop(object sender, DragEventArgs e)
    {
        var groups = Groups;
        if (groups is null)
            return;

        if (TryResolveColumnDrop(sender, e, out _, out var columnMove, out _))
        {
            ClearInsertionAdorner();
            var reordered = RecordDetailDragOperations.MoveColumn(groups, columnMove.FromIndex, columnMove.ToIndex);
            e.Effects = reordered is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
            if (reordered is null)
                return;

            Groups = reordered;
            PublishLayout();
            return;
        }

        // Karte auf den freien Bereich einer Spalte: ans Ende dieser Spalte.
        if (TryResolveEmptyColumnDrop(sender, e, out _, out var fieldMove))
        {
            ClearInsertionAdorner();
            var reordered = RecordDetailDragOperations.MoveField(
                groups, fieldMove.FromTitle, fieldMove.FromIndex, fieldMove.ToTitle, fieldMove.ToIndex);

            e.Effects = reordered is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
            if (reordered is null)
                return;

            Groups = reordered;
            PublishLayout();
        }
    }

    private bool TryResolveColumnDrop(
        object sender,
        DragEventArgs e,
        out FrameworkElement column,
        out (int FromIndex, int ToIndex) move,
        out bool insertAfter)
    {
        column = null!;
        move = (-1, -1);
        insertAfter = false;

        var groups = Groups;
        if (!IsCustomizing || groups is null)
            return false;

        if (sender is not FrameworkElement { DataContext: RecordDetailGroup target } element)
            return false;

        if (!e.Data.GetDataPresent(ColumnDragFormat) || e.Data.GetData(ColumnDragFormat) is not RecordDetailGroup source)
            return false;

        if (ReferenceEquals(source, target))
            return false;

        if (!RecordDetailDragOperations.TryLocateColumn(groups, source, out var fromIndex))
            return false;

        if (!RecordDetailDragOperations.TryLocateColumn(groups, target, out var targetIndex))
            return false;

        // Spalten stehen nebeneinander: links oder rechts der Mitte entscheidet.
        insertAfter = e.GetPosition(element).X > element.ActualWidth / 2d;
        var toIndex = RecordDetailDragOperations.ResolveDropTarget(
            fromIndex, targetIndex, insertAfter, groups.Count, sameColumn: true);
        if (toIndex < 0)
            return false;

        column = element;
        move = (fromIndex, toIndex);
        return true;
    }

    /// <summary>
    /// Eine Karte, die auf den freien Bereich einer Spalte faellt (nicht auf eine andere
    /// Karte), landet am Ende dieser Spalte. Nur so laesst sich eine leergeraeumte Spalte
    /// ueberhaupt wieder befuellen.
    /// </summary>
    private bool TryResolveEmptyColumnDrop(
        object sender,
        DragEventArgs e,
        out FrameworkElement column,
        out (string FromTitle, int FromIndex, string ToTitle, int ToIndex) move)
    {
        column = null!;
        move = (string.Empty, -1, string.Empty, -1);

        var groups = Groups;
        if (!IsCustomizing || groups is null)
            return false;

        if (sender is not FrameworkElement { DataContext: RecordDetailGroup target } element)
            return false;

        if (!e.Data.GetDataPresent(FieldDragFormat) || e.Data.GetData(FieldDragFormat) is not RecordDetailItem source)
            return false;

        if (!RecordDetailDragOperations.TryLocateField(groups, source, out var fromTitle, out var fromIndex))
            return false;

        if (string.Equals(fromTitle, target.Title, StringComparison.Ordinal))
            return false;

        column = element;
        move = (fromTitle, fromIndex, target.Title, target.Items.Count);
        return true;
    }

    private static int ItemCount(IReadOnlyList<RecordDetailGroup> groups, string title)
    {
        foreach (var group in groups)
        {
            if (string.Equals(group.Title, title, StringComparison.Ordinal))
                return group.Items.Count;
        }

        return 0;
    }

    // --- Einfuegemarke ------------------------------------------------------

    private void ShowInsertionAdorner(FrameworkElement target, bool insertAfter, bool vertical)
    {
        if (_insertionAdorner is not null
            && ReferenceEquals(_insertionAdorner.AdornedElement, target)
            && _insertionAdorner.InsertAfter == insertAfter
            && _insertionAdorner.IsVertical == vertical)
            return;

        ClearInsertionAdorner();

        var layer = AdornerLayer.GetAdornerLayer(target);
        if (layer is null)
            return;

        var brush = TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
        _insertionAdorner = new RecordDetailInsertionAdorner(target, insertAfter, vertical, brush);
        _insertionLayer = layer;
        layer.Add(_insertionAdorner);
    }

    private void ClearInsertionAdorner()
    {
        if (_insertionAdorner is not null)
            _insertionLayer?.Remove(_insertionAdorner);

        _insertionAdorner = null;
        _insertionLayer = null;
    }

    private void EditorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private void EditorComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private static void UpdateComboBindingSource(ComboBox? comboBox)
    {
        if (comboBox?.DataContext is not RecordDetailItem item)
            return;

        var property = item.AllowFreeText ? ComboBox.TextProperty : Selector.SelectedItemProperty;
        comboBox.GetBindingExpression(property)?.UpdateSource();
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        e.Handled = NonNumericRegex.IsMatch(e.Text ?? string.Empty);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (NonNumericRegex.IsMatch(text))
            e.CancelCommand();
    }
}
