using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.UI.ViewModels.Pages;
using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage : System.Windows.Controls.UserControl
{
    private static readonly IValueConverter CostDisplayConverter = new ChfAccountingDisplayConverter();
    private static readonly IValueConverter HorizontalAlignmentToTextAlignmentConverter = new HorizontalAlignmentToTextAlignmentValueConverter();

    private bool _columnsBuilt;
    private System.Windows.Point _dragStartPoint;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly Dictionary<DataGridColumn, HorizontalAlignment> _columnHorizontalAlignments = new();
    private readonly Dictionary<DataGridColumn, VerticalAlignment> _columnVerticalAlignments = new();
    private readonly Dictionary<DataGridColumn, Style?> _baseCellStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextElementStyles = new();
    private readonly Dictionary<DataGridTextColumn, Style?> _baseTextEditingStyles = new();
    private readonly DispatcherTimer _layoutSaveDebounceTimer;
    private bool _updatingAlignmentButtons;
    private bool _isRestoringLayout;
    private DataGridColumn? _activeColumn;

    public DataPage()
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

        Grid.AddHandler(DataGridColumnHeader.ClickEvent, new RoutedEventHandler(Grid_ColumnHeaderClick), true);
        Grid.ColumnReordered += Grid_ColumnReordered;
        Loaded += (_, __) =>
        {
            EnsureColumns();
            UpdateAlignmentButtonsForCurrentColumn();
        };
        Unloaded += (_, __) =>
        {
            _layoutSaveDebounceTimer.Stop();
            SaveLayoutToSettings();
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
        }
    }

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
        _columnHorizontalAlignments.Clear();
        _columnVerticalAlignments.Clear();
        _baseCellStyles.Clear();
        _baseTextElementStyles.Clear();
        _baseTextEditingStyles.Clear();
        _activeColumn = null;

        foreach (var field in FieldCatalog.ColumnOrder)
        {
            var def = FieldCatalog.Get(field);
            DataGridColumn col;

            if (field == "Sanieren_JaNein")
            {
                col = CreateComboColumn(
                    field,
                    def.Label,
                    "SanierenOptions",
                    "EditSanierenOptionsCommand",
                    "PreviewSanierenOptionsCommand",
                    "ResetSanierenOptionsCommand",
                    "RemoveSanierenOptionCommand",
                    "AddSanierenOptionCommand");
            }
            else if (field == "Eigentuemer")
            {
                col = CreateComboColumn(
                    field,
                    def.Label,
                    "EigentuemerOptions",
                    "EditEigentuemerOptionsCommand",
                    "PreviewEigentuemerOptionsCommand",
                    "ResetEigentuemerOptionsCommand",
                    "RemoveEigentuemerOptionCommand",
                    "AddEigentuemerOptionCommand",
                    allowFreeText: false);
            }
            else if (field == "Pruefungsresultat")
            {
                col = CreateComboColumn(
                    field,
                    def.Label,
                    "PruefungsresultatOptions",
                    "EditPruefungsresultatOptionsCommand",
                    "PreviewPruefungsresultatOptionsCommand",
                    "ResetPruefungsresultatOptionsCommand",
                    "RemovePruefungsresultatOptionCommand",
                    "AddPruefungsresultatOptionCommand");
            }
            else if (field == "Referenzpruefung")
            {
                col = CreateComboColumn(
                    field,
                    def.Label,
                    "ReferenzpruefungOptions",
                    "EditReferenzpruefungOptionsCommand",
                    "PreviewReferenzpruefungOptionsCommand",
                    "ResetReferenzpruefungOptionsCommand",
                    "RemoveReferenzpruefungOptionCommand",
                    "AddReferenzpruefungOptionCommand");
            }
            else if (field == "Ausgefuehrt_durch")
            {
                col = CreateSimpleComboColumn(field, def.Label, "AusgefuehrtDurchOptions");
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
                col = CreateCostColumn(field, def.Label);
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

            _columnHorizontalAlignments[col] = string.Equals(field, "Kosten", StringComparison.Ordinal)
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            _columnVerticalAlignments[col] = VerticalAlignment.Center;
            ApplyColumnAlignment(col, _columnHorizontalAlignments[col], _columnVerticalAlignments[col]);
        }

        Grid.FrozenColumnCount = 2;
        RestoreLayoutFromSettings();
        ResetSort();
    }

    private DataGridTemplateColumn CreateCostColumn(string fieldName, string header)
    {
        var displayPanel = new FrameworkElementFactory(typeof(DockPanel));
        displayPanel.SetValue(DockPanel.LastChildFillProperty, true);
        displayPanel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        displayPanel.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });

        var displayCurrency = new FrameworkElementFactory(typeof(TextBlock));
        displayCurrency.SetValue(DockPanel.DockProperty, Dock.Left);
        displayCurrency.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        displayCurrency.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        displayCurrency.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]")
        {
            Converter = CostDisplayConverter,
            ConverterParameter = "currency"
        });
        displayPanel.AppendChild(displayCurrency);

        var displayAmount = new FrameworkElementFactory(typeof(TextBlock));
        displayAmount.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        displayAmount.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        displayAmount.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        displayAmount.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        displayAmount.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]")
        {
            Converter = CostDisplayConverter,
            ConverterParameter = "amount"
        });
        displayPanel.AppendChild(displayAmount);

        var editPanel = new FrameworkElementFactory(typeof(DockPanel));
        editPanel.SetValue(DockPanel.LastChildFillProperty, true);
        editPanel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        editPanel.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });

        var editCurrency = new FrameworkElementFactory(typeof(TextBlock));
        editCurrency.SetValue(DockPanel.DockProperty, Dock.Left);
        editCurrency.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        editCurrency.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        editCurrency.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]")
        {
            Converter = CostDisplayConverter,
            ConverterParameter = "currency"
        });
        editPanel.AppendChild(editCurrency);

        var editAmount = new FrameworkElementFactory(typeof(TextBox));
        editAmount.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        editAmount.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Right);
        editAmount.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        editAmount.SetBinding(TextBox.TextProperty, new Binding($"Fields[{fieldName}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            Converter = CostDisplayConverter,
            ConverterParameter = "amount"
        });
        editPanel.AppendChild(editAmount);

        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = new DataTemplate { VisualTree = displayPanel },
            CellEditingTemplate = new DataTemplate { VisualTree = editPanel },
            SortMemberPath = $"Fields[{fieldName}]",
            Width = DataGridLength.SizeToHeader
        };
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
    {
        var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
        displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]"));
        displayFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        displayFactory.SetBinding(TextBlock.VerticalAlignmentProperty, new Binding("VerticalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        displayFactory.SetBinding(TextBlock.TextAlignmentProperty, new Binding("HorizontalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1),
            Converter = HorizontalAlignmentToTextAlignmentConverter
        });
        displayFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

        var comboFactory = new FrameworkElementFactory(typeof(ComboBox));
        comboFactory.SetValue(ComboBox.IsEditableProperty, allowFreeText);
        comboFactory.SetValue(ComboBox.StaysOpenOnEditProperty, allowFreeText);
        comboFactory.SetValue(ComboBox.IsTextSearchEnabledProperty, false);
        comboFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        comboFactory.SetBinding(Control.BackgroundProperty, new Binding("Background")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.HorizontalContentAlignmentProperty, new Binding("HorizontalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.VerticalContentAlignmentProperty, new Binding("VerticalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(UIElement.IsHitTestVisibleProperty, new Binding("DataContext.IsProjectReady")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        comboFactory.SetBinding(ComboBox.ItemsSourceProperty, new Binding($"DataContext.{itemsSourcePath}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        if (allowFreeText)
        {
            comboFactory.SetBinding(ComboBox.TextProperty, new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }
        else
        {
            comboFactory.SetBinding(Selector.SelectedItemProperty, new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }
        comboFactory.SetValue(FrameworkElement.TagProperty, fieldName);
        comboFactory.AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(ComboBox_LostKeyboardFocus));
        comboFactory.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));

        var contextMenu = new ContextMenu();
        contextMenu.Opened += (_, __) =>
        {
            if (contextMenu.PlacementTarget is not FrameworkElement target)
                return;
            var grid = FindAncestor<DataGrid>(target);
            contextMenu.DataContext = grid?.DataContext ?? target.DataContext;
        };

        var editItem = new MenuItem { Header = "Liste bearbeiten..." };
        editItem.SetBinding(MenuItem.CommandProperty, new Binding(editCommand));
        var previewItem = new MenuItem { Header = "Vorschau" };
        previewItem.SetBinding(MenuItem.CommandProperty, new Binding(previewCommand));
        var resetItem = new MenuItem { Header = "Zuruecksetzen auf Standard" };
        resetItem.SetBinding(MenuItem.CommandProperty, new Binding(resetCommand));
        var addItem = new MenuItem { Header = "Wert hinzufuegen" };
        addItem.SetBinding(MenuItem.CommandProperty, new Binding(addCommand));
        addItem.SetBinding(MenuItem.CommandParameterProperty, new Binding("PlacementTarget")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1)
        });
        var removeItem = new MenuItem { Header = "Wert entfernen" };
        removeItem.SetBinding(MenuItem.CommandProperty, new Binding(removeCommand));
        removeItem.SetBinding(MenuItem.CommandParameterProperty, new Binding("PlacementTarget")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1)
        });
        contextMenu.Items.Add(editItem);
        contextMenu.Items.Add(previewItem);
        contextMenu.Items.Add(resetItem);
        contextMenu.Items.Add(addItem);
        contextMenu.Items.Add(removeItem);

        comboFactory.SetValue(FrameworkElement.ContextMenuProperty, contextMenu);

        var displayTemplate = new DataTemplate { VisualTree = displayFactory };
        var editTemplate = new DataTemplate { VisualTree = comboFactory };
        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = displayTemplate,
            CellEditingTemplate = editTemplate,
            Width = DataGridLength.SizeToHeader
        };
    }

    private DataGridTemplateColumn CreateSimpleComboColumn(
        string fieldName,
        string header,
        string itemsSourcePath)
    {
        var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
        displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]"));
        displayFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        displayFactory.SetBinding(TextBlock.VerticalAlignmentProperty, new Binding("VerticalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        displayFactory.SetBinding(TextBlock.TextAlignmentProperty, new Binding("HorizontalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1),
            Converter = HorizontalAlignmentToTextAlignmentConverter
        });
        displayFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

        var comboFactory = new FrameworkElementFactory(typeof(ComboBox));
        comboFactory.SetValue(ComboBox.IsEditableProperty, true);
        comboFactory.SetValue(ComboBox.StaysOpenOnEditProperty, true);
        comboFactory.SetValue(ComboBox.IsTextSearchEnabledProperty, false);
        comboFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        comboFactory.SetBinding(Control.BackgroundProperty, new Binding("Background")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.ForegroundProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.HorizontalContentAlignmentProperty, new Binding("HorizontalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(Control.VerticalContentAlignmentProperty, new Binding("VerticalContentAlignment")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        });
        comboFactory.SetBinding(UIElement.IsHitTestVisibleProperty, new Binding("DataContext.IsProjectReady")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        comboFactory.SetBinding(ComboBox.ItemsSourceProperty, new Binding($"DataContext.{itemsSourcePath}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        comboFactory.SetBinding(ComboBox.TextProperty, new Binding($"Fields[{fieldName}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        comboFactory.SetValue(FrameworkElement.TagProperty, fieldName);
        comboFactory.AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(ComboBox_LostKeyboardFocus));
        comboFactory.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));

        var displayTemplate = new DataTemplate { VisualTree = displayFactory };
        var editTemplate = new DataTemplate { VisualTree = comboFactory };
        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = displayTemplate,
            CellEditingTemplate = editTemplate,
            Width = DataGridLength.SizeToHeader
        };
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

    private void AlignLeftButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Left);
    }

    private void AlignCenterButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Center);
    }

    private void AlignRightButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment.Right);
    }

    private void AlignTopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Top);
    }

    private void AlignMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Center);
    }

    private void AlignBottomButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment.Bottom);
    }

    private void ApplyHorizontalAlignmentToCurrentColumn(HorizontalAlignment horizontalAlignment)
    {
        if (_updatingAlignmentButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var verticalAlignment = GetColumnVerticalAlignment(column);
        ApplyColumnAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private void ApplyVerticalAlignmentToCurrentColumn(VerticalAlignment verticalAlignment)
    {
        if (_updatingAlignmentButtons)
            return;

        var column = GetActiveColumn();
        if (column is null)
            return;

        var horizontalAlignment = GetColumnHorizontalAlignment(column);
        ApplyColumnAlignment(column, horizontalAlignment, verticalAlignment);
        UpdateAlignmentButtonsForCurrentColumn();
    }

    private DataGridColumn? GetActiveColumn()
    {
        if (_activeColumn is not null)
            return _activeColumn;

        if (Grid.CurrentCell.Column is not null)
            return Grid.CurrentCell.Column;

        if (Grid.SelectedCells.Count > 0)
            return Grid.SelectedCells[0].Column;

        return null;
    }

    private HorizontalAlignment GetColumnHorizontalAlignment(DataGridColumn column)
    {
        if (_columnHorizontalAlignments.TryGetValue(column, out var value))
            return value;
        return HorizontalAlignment.Left;
    }

    private VerticalAlignment GetColumnVerticalAlignment(DataGridColumn column)
    {
        if (_columnVerticalAlignments.TryGetValue(column, out var value))
            return value;
        return VerticalAlignment.Center;
    }

    private void TrySetCurrentCellForColumn(DataGridColumn column)
    {
        var rowItem = Grid.SelectedItem ?? Grid.Items.Cast<object>().FirstOrDefault();
        if (rowItem is null)
            return;

        Grid.CurrentCell = new DataGridCellInfo(rowItem, column);
    }

    private void ApplyColumnAlignment(DataGridColumn column, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
    {
        _columnHorizontalAlignments[column] = horizontalAlignment;
        _columnVerticalAlignments[column] = verticalAlignment;

        ApplyCellAlignment(column, horizontalAlignment, verticalAlignment);

        if (column is DataGridTextColumn textColumn)
            ApplyTextColumnAlignment(textColumn, horizontalAlignment, verticalAlignment);

        QueueLayoutSave();
    }

    private void ApplyCellAlignment(DataGridColumn column, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
    {
        if (!_baseCellStyles.ContainsKey(column))
            _baseCellStyles[column] = column.CellStyle;

        var style = new Style(typeof(DataGridCell), _baseCellStyles[column]);
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, horizontalAlignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, verticalAlignment));
        column.CellStyle = style;
    }

    private void ApplyTextColumnAlignment(DataGridTextColumn column, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
    {
        if (!_baseTextElementStyles.ContainsKey(column))
            _baseTextElementStyles[column] = column.ElementStyle;
        if (!_baseTextEditingStyles.ContainsKey(column))
            _baseTextEditingStyles[column] = column.EditingElementStyle;

        var textAlignment = ToTextAlignment(horizontalAlignment);

        var elementStyle = new Style(typeof(TextBlock), _baseTextElementStyles[column]);
        elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, horizontalAlignment));
        elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, verticalAlignment));
        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, textAlignment));
        column.ElementStyle = elementStyle;

        var editingStyle = new Style(typeof(TextBox), _baseTextEditingStyles[column]);
        editingStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, textAlignment));
        editingStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, horizontalAlignment));
        editingStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, ToTextBoxVerticalAlignment(verticalAlignment)));
        column.EditingElementStyle = editingStyle;
    }

    private static TextAlignment ToTextAlignment(HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }

    private static VerticalAlignment ToTextBoxVerticalAlignment(VerticalAlignment alignment)
    {
        return alignment switch
        {
            VerticalAlignment.Top => VerticalAlignment.Top,
            VerticalAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
    }

    private void UpdateAlignmentButtonsForCurrentColumn()
    {
        _updatingAlignmentButtons = true;
        try
        {
            var column = GetActiveColumn();
            if (column is null)
            {
                SetAlignmentButtonsUnchecked();
                return;
            }

            var horizontal = GetColumnHorizontalAlignment(column);
            var vertical = GetColumnVerticalAlignment(column);

            AlignLeftButton.IsChecked = horizontal == HorizontalAlignment.Left;
            AlignCenterButton.IsChecked = horizontal == HorizontalAlignment.Center;
            AlignRightButton.IsChecked = horizontal == HorizontalAlignment.Right;

            AlignTopButton.IsChecked = vertical == VerticalAlignment.Top;
            AlignMiddleButton.IsChecked = vertical == VerticalAlignment.Center;
            AlignBottomButton.IsChecked = vertical == VerticalAlignment.Bottom;
        }
        finally
        {
            _updatingAlignmentButtons = false;
        }
    }

    private void SetAlignmentButtonsUnchecked()
    {
        AlignLeftButton.IsChecked = false;
        AlignCenterButton.IsChecked = false;
        AlignRightButton.IsChecked = false;
        AlignTopButton.IsChecked = false;
        AlignMiddleButton.IsChecked = false;
        AlignBottomButton.IsChecked = false;
    }

    private void RestoreLayoutFromSettings()
    {
        var sp = (ServiceProvider)App.Services;
        var layout = sp.Settings.DataPageLayout;
        if (layout is null)
            return;

        _isRestoringLayout = true;
        try
        {
            foreach (var col in Grid.Columns)
                AttachColumnLayoutChangeHandlers(col);

            var byField = layout.Columns?
                .Where(c => !string.IsNullOrWhiteSpace(c.FieldName))
                .GroupBy(c => c.FieldName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
                ?? new Dictionary<string, DataPageColumnLayout>(StringComparer.Ordinal);

            foreach (var col in Grid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string fieldName)
                    continue;
                if (!byField.TryGetValue(fieldName, out var state))
                    continue;

                if (state.WidthValue > 0 && Enum.TryParse<DataGridLengthUnitType>(state.WidthUnitType, out var widthType))
                    col.Width = new DataGridLength(state.WidthValue, widthType);

                var horizontal = ParseHorizontalAlignment(state.HorizontalAlignment);
                var vertical = ParseVerticalAlignment(state.VerticalAlignment);
                ApplyColumnAlignment(col, horizontal, vertical);
            }

            var orderedColumns = Grid.Columns
                .Select(col =>
                {
                    var field = col.GetValue(FrameworkElement.TagProperty) as string;
                    if (field is not null && byField.TryGetValue(field, out var state))
                        return new { Column = col, Target = state.DisplayIndex, HasState = true };
                    return new { Column = col, Target = col.DisplayIndex, HasState = false };
                })
                .OrderBy(x => x.HasState ? 0 : 1)
                .ThenBy(x => x.Target)
                .ToList();

            for (var i = 0; i < orderedColumns.Count; i++)
            {
                try
                {
                    orderedColumns[i].Column.DisplayIndex = i;
                }
                catch
                {
                    // ignore invalid display index operations
                }
            }
        }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    private void AttachColumnLayoutChangeHandlers(DataGridColumn column)
    {
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
            ?.AddValueChanged(column, ColumnLayoutPropertyChanged);
    }

    private void ColumnLayoutPropertyChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        QueueLayoutSave();
    }

    private void QueueLayoutSave()
    {
        if (_isRestoringLayout)
            return;

        _layoutSaveDebounceTimer.Stop();
        _layoutSaveDebounceTimer.Start();
    }

    private void SaveLayoutToSettings()
    {
        if (_isRestoringLayout || Grid.Columns.Count == 0)
            return;

        var sp = (ServiceProvider)App.Services;
        var layout = sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.Columns = Grid.Columns
            .Select(col =>
            {
                var fieldName = col.GetValue(FrameworkElement.TagProperty) as string ?? "";
                var horizontal = GetColumnHorizontalAlignment(col).ToString();
                var vertical = GetColumnVerticalAlignment(col).ToString();
                return new DataPageColumnLayout
                {
                    FieldName = fieldName,
                    DisplayIndex = col.DisplayIndex,
                    WidthValue = col.Width.Value,
                    WidthUnitType = col.Width.UnitType.ToString(),
                    HorizontalAlignment = horizontal,
                    VerticalAlignment = vertical
                };
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldName))
            .ToList();
        sp.Settings.DataPageLayout = layout;
        sp.Settings.Save();
    }

    private static HorizontalAlignment ParseHorizontalAlignment(string? value)
    {
        if (Enum.TryParse<HorizontalAlignment>(value, out var parsed))
            return parsed;
        return HorizontalAlignment.Left;
    }

    private static VerticalAlignment ParseVerticalAlignment(string? value)
    {
        if (Enum.TryParse<VerticalAlignment>(value, out var parsed))
            return parsed;
        return VerticalAlignment.Center;
    }

    private void Grid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row is not null)
            Grid.SelectedItem = row.Item;
    }

    private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        if (DataContext is not DataPageViewModel vm)
            return;

        const double step = 2d;
        var delta = e.Delta > 0 ? step : -step;
        var next = Math.Clamp(vm.GridMinRowHeight + delta, 24d, 120d);
        if (Math.Abs(next - vm.GridMinRowHeight) < 0.001d)
            return;

        vm.GridMinRowHeight = next;
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
            ShowTextPreview(title, record.GetFieldValue(fieldName));
            e.Handled = true;
            return;
        }

        if (fieldName == "Zustandsklasse")
        {
            ShowZustandsklasseExplanation(record);
            e.Handled = true;
        }
    }

    private void OpenPhotoLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;

        var rawPath = fe.Tag as string;
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        var sp = App.Services as ServiceProvider;
        var resolved = TryResolvePath(rawPath, sp?.Settings.LastProjectPath) ?? rawPath;
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            MessageBox.Show($"Foto nicht gefunden:\n{rawPath}", "Foto",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resolved,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Foto konnte nicht geoeffnet werden:\n{ex.Message}", "Foto",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFilmLink_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = vm.Selected;
        if (record is null)
        {
            MessageBox.Show("Bitte zuerst eine Haltung waehlen.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = ResolveProtocolEntry(sender);
        if (entry is null)
        {
            MessageBox.Show("Keine Beobachtung erkannt.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

        if (fieldName == "Sanieren_JaNein" || fieldName == "Eigentuemer" ||
            fieldName == "Pruefungsresultat" || fieldName == "Referenzpruefung")
        {
            var value = GetEditedTextValue(e.EditingElement);
            if (!string.IsNullOrWhiteSpace(value) && e.Row?.Item is HaltungRecord editedRecord)
                editedRecord.SetFieldValue(fieldName, value ?? string.Empty, FieldSource.Manual, userEdited: true);
            vm.EnsureOptionForField(fieldName, value);
        }

        if (fieldName == "Zustandsklasse" && e.Row?.Item is HaltungRecord record)
        {
            var value = GetEditedTextValue(e.EditingElement) ?? record.GetFieldValue(fieldName);
            record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: true);
        }

        if (fieldName == "Haltungsname" && e.Row?.Item is HaltungRecord hRecord)
        {
            var oldValue = hRecord.GetFieldValue("Haltungsname");
            var newValue = GetEditedTextValue(e.EditingElement) ?? oldValue;
            if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                hRecord.SetFieldValue("Haltungsname", newValue, FieldSource.Manual, userEdited: true);
                PdfCorrectionMetadata.RegisterHoldingRename(vm.Project, oldValue, newValue);
                TryRenameHoldingAssets(hRecord, oldValue, newValue);
            }
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

    private void TryRenameHoldingAssets(HaltungRecord record, string oldHolding, string newHolding)
    {
        try
        {
            var oldSan = SanitizePathSegment(oldHolding);
            var newSan = SanitizePathSegment(newHolding);
            if (string.IsNullOrWhiteSpace(oldSan) || string.IsNullOrWhiteSpace(newSan))
                return;
            if (string.Equals(oldSan, newSan, StringComparison.OrdinalIgnoreCase))
                return;

            var sp = App.Services as ServiceProvider;
            var link = record.GetFieldValue("Link");
            var linkPath = TryResolvePath(link, sp?.Settings.LastProjectPath);

            var folder = !string.IsNullOrWhiteSpace(linkPath) ? Path.GetDirectoryName(linkPath) : null;
            if (string.IsNullOrWhiteSpace(folder))
            {
                var projectPath = sp?.Settings.LastProjectPath;
                var projectDir = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetDirectoryName(projectPath);
                if (!string.IsNullOrWhiteSpace(projectDir))
                {
                    var holdingsRoot = Path.Combine(projectDir, "Haltungen");
                    if (Directory.Exists(holdingsRoot))
                    {
                        folder = Directory.EnumerateDirectories(holdingsRoot, oldSan, SearchOption.AllDirectories)
                            .FirstOrDefault();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parent))
                return;

            var targetFolder = Path.Combine(parent, newSan);
            if (Directory.Exists(targetFolder))
            {
                MessageBox.Show($"Zielordner existiert bereits:\n{targetFolder}", "Umbenennen",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = Directory.EnumerateFiles(folder).ToList();
            var oldRx = new Regex(@"^(?<d>\d{8})_" + Regex.Escape(oldSan) + @"(?<g>-g)?(?<rest>.*)$", RegexOptions.IgnoreCase);
            var renamedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var m = oldRx.Match(Path.GetFileNameWithoutExtension(name));
                if (!m.Success)
                    continue;

                var ext = Path.GetExtension(name);
                var date = m.Groups["d"].Value;
                var g = m.Groups["g"].Value;
                var newName = $"{date}_{newSan}{g}{ext}";
                var dest = Path.Combine(folder, newName);
                if (!string.Equals(f, dest, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(f, dest);
                    renamedMap[f] = dest;
                }
            }

            Directory.Move(folder, targetFolder);

            if (!string.IsNullOrWhiteSpace(linkPath))
            {
                var newLinkPath = linkPath.Replace(folder, targetFolder, StringComparison.OrdinalIgnoreCase);
                if (renamedMap.TryGetValue(linkPath, out var renamed))
                    newLinkPath = renamed.Replace(folder, targetFolder, StringComparison.OrdinalIgnoreCase);
                record.SetFieldValue("Link", newLinkPath, FieldSource.Manual, userEdited: true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Umbenennen fehlgeschlagen: {ex.Message}", "Umbenennen",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? TryResolvePath(string? raw, string? lastProjectPath)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (File.Exists(path))
            return path;
        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(lastProjectPath))
        {
            var baseDir = Path.GetDirectoryName(lastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(combined))
                    return combined;
            }
        }
        return null;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }

    private void PlayMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.PlayVideoCommand.Execute(record);
    }

    private void ProtocolMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Protokoll",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OpenProtocolCommand.Execute(record);
    }

    private void RelinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Video",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.RelinkVideoCommand.Execute(record);
    }

    private void CostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OpenCostsCommand.Execute(record);
    }

    private void RestoreCostsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.RestoreCostsCommand.Execute(record);
    }

    private void SuggestMeasuresMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.SuggestMeasuresCommand.Execute(record);
    }

    private void SanierungKiMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;
        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "KI Sanierung",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        vm.OptimizeSanierungKiCommand.Execute(record);
    }

    private void VideoAiPipelineMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        var record = GetContextMenuRecord(sender);
        if (record is null)
        {
            MessageBox.Show("Keine Zeile erkannt. Bitte direkt auf eine Zeile rechtsklicken.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        vm.OpenVideoAiPipelineCommand.Execute(record);
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
        var view = CollectionViewSource.GetDefaultView(Grid.ItemsSource);
        if (view is null)
            return;

        if (string.IsNullOrWhiteSpace(vm.SearchText))
        {
            using (view.DeferRefresh())
                view.Filter = null;
            vm.UpdateSearchResultInfo(vm.Records.Count);
        }
        else
        {
            using (view.DeferRefresh())
                view.Filter = obj => obj is HaltungRecord rec && vm.MatchesSearch(rec);
            var count = view.Cast<object>().Count();
            vm.UpdateSearchResultInfo(count);
        }
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

        var sp = (ServiceProvider)App.Services;
        var project = vm.Project;
        if (project is null)
        {
            MessageBox.Show("Kein Projekt geladen.", "Zustandsklasse",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var res = sp.Vsa.Explain(project, record);
        if (!res.Ok || res.Value is null)
        {
            MessageBox.Show(res.ErrorMessage ?? "Berechnung fehlgeschlagen.", "Zustandsklasse",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var holding = record.GetFieldValue("Haltungsname");
        var title = string.IsNullOrWhiteSpace(holding)
            ? "Zustandsklasse - Rechnungsweg"
            : $"Zustandsklasse - Rechnungsweg - {holding}";

        ShowTextPreview(title, res.Value);
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
}
