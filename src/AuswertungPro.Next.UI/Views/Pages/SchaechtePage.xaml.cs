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

    private static void ApplyColorStyle(DataGridColumn column, string columnName)
    {
        var normalizedHeader = Normalize(columnName);

        if (normalizedHeader.Contains("zustandsklasse", StringComparison.Ordinal))
            column.CellStyle = ZustandsklasseCellStyleFactory.CreateSchaechteStyle(columnName);
        else if (normalizedHeader.Contains("eigentuemer", StringComparison.Ordinal) ||
                 normalizedHeader.Contains("eigentumer", StringComparison.Ordinal) ||
                 normalizedHeader.Contains("eigentum", StringComparison.Ordinal))
            column.CellStyle = ZustandsklasseCellStyleFactory.CreateEigentuemerStyle(columnName);
        else if ((normalizedHeader.Contains("ausgefuehrt", StringComparison.Ordinal) ||
                  normalizedHeader.Contains("ausgefuhrt", StringComparison.Ordinal)) &&
                 normalizedHeader.Contains("durch", StringComparison.Ordinal))
            column.CellStyle = ZustandsklasseCellStyleFactory.CreateAusgefuehrtDurchStyle(columnName);
        else if (normalizedHeader.Contains("pruefung", StringComparison.Ordinal) ||
                 normalizedHeader.Contains("dichtheit", StringComparison.Ordinal) ||
                 normalizedHeader.Contains("dichtigkeit", StringComparison.Ordinal))
            column.CellStyle = ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(columnName);
    }

    private DataGridTemplateColumn CreateManagedComboColumn(
        string recordField,
        string optionField,
        string itemsSourcePath,
        string editCommand,
        string previewCommand,
        string resetCommand,
        string removeCommand,
        string addCommand,
        bool allowFreeText)
    {
        var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
        displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]"));
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

        comboFactory.SetBinding(ComboBox.ItemsSourceProperty, new Binding($"DataContext.{itemsSourcePath}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });

        if (allowFreeText)
        {
            comboFactory.SetBinding(ComboBox.TextProperty, new Binding($"Fields[{recordField}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }
        else
        {
            comboFactory.SetBinding(Selector.SelectedItemProperty, new Binding($"Fields[{recordField}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        }

        comboFactory.SetValue(FrameworkElement.TagProperty, new ComboBindingTag(recordField, optionField));
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

        return new DataGridTemplateColumn
        {
            Header = recordField,
            CellTemplate = new DataTemplate { VisualTree = displayFactory },
            CellEditingTemplate = new DataTemplate { VisualTree = comboFactory },
            Width = DataGridLength.SizeToHeader
        };
    }

    private DataGridTemplateColumn CreateSimpleComboColumn(
        string recordField,
        string optionField,
        string itemsSourcePath,
        bool allowFreeText)
    {
        var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
        displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]"));
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
        comboFactory.SetBinding(ComboBox.ItemsSourceProperty, new Binding($"DataContext.{itemsSourcePath}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        comboFactory.SetBinding(ComboBox.TextProperty, new Binding($"Fields[{recordField}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        comboFactory.SetValue(FrameworkElement.TagProperty, new ComboBindingTag(recordField, optionField));
        comboFactory.AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(ComboBox_LostKeyboardFocus));
        comboFactory.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));

        return new DataGridTemplateColumn
        {
            Header = recordField,
            CellTemplate = new DataTemplate { VisualTree = displayFactory },
            CellEditingTemplate = new DataTemplate { VisualTree = comboFactory },
            Width = DataGridLength.SizeToHeader
        };
    }

    private DataGridTemplateColumn CreateCostColumn(string recordField)
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
        displayCurrency.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]")
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
        displayAmount.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]")
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
        editCurrency.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{recordField}]")
        {
            Converter = CostDisplayConverter,
            ConverterParameter = "currency"
        });
        editPanel.AppendChild(editCurrency);

        var editAmount = new FrameworkElementFactory(typeof(TextBox));
        editAmount.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        editAmount.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Right);
        editAmount.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
        editAmount.SetBinding(TextBox.TextProperty, new Binding($"Fields[{recordField}]")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            Converter = CostDisplayConverter,
            ConverterParameter = "amount"
        });
        editPanel.AppendChild(editAmount);

        return new DataGridTemplateColumn
        {
            Header = recordField,
            CellTemplate = new DataTemplate { VisualTree = displayPanel },
            CellEditingTemplate = new DataTemplate { VisualTree = editPanel },
            SortMemberPath = $"Fields[{recordField}]",
            Width = DataGridLength.SizeToHeader
        };
    }

    private DataGridTextColumn CreateZustandsklasseColumn(string recordField)
    {
        var displayStyle = new Style(typeof(TextBlock));
        displayStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        displayStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

        var editStyle = new Style(typeof(TextBox));
        editStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        editStyle.Setters.Add(new EventSetter(UIElement.PreviewTextInputEvent, new TextCompositionEventHandler(ZustandsklasseTextBox_PreviewTextInput)));
        editStyle.Setters.Add(new EventSetter(DataObject.PastingEvent, new DataObjectPastingEventHandler(ZustandsklasseTextBox_Pasting)));

        return new DataGridTextColumn
        {
            Header = GetDisplayHeader(recordField),
            Binding = new Binding($"Fields[{recordField}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            ElementStyle = displayStyle,
            EditingElementStyle = editStyle,
            Width = DataGridLength.SizeToHeader,
            MinWidth = 90
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
        var layout = sp.Settings.SchaechtePageLayout;
        if (layout is null)
            return;

        _isRestoringLayout = true;
        try
        {
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

            EnsureSchachtnummerBeforeFunktion();
        }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    private void EnsureSchachtnummerBeforeFunktion()
    {
        var schachtnummerColumn = FindColumnByName("Schachtnummer");
        var funktionColumn = FindColumnByName("Funktion");
        if (schachtnummerColumn is null || funktionColumn is null)
            return;

        if (schachtnummerColumn.DisplayIndex < funktionColumn.DisplayIndex)
            return;

        try
        {
            var target = funktionColumn.DisplayIndex;
            schachtnummerColumn.DisplayIndex = target;
            funktionColumn.DisplayIndex = target + 1;
        }
        catch
        {
            // ignore invalid display index operations
        }
    }

    private DataGridColumn? FindColumnByName(string fieldName)
    {
        return Grid.Columns.FirstOrDefault(c =>
            c.GetValue(FrameworkElement.TagProperty) is string tag &&
            string.Equals(tag, fieldName, StringComparison.OrdinalIgnoreCase));
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
        var layout = sp.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
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

        sp.Settings.SchaechtePageLayout = layout;
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

        const double step = 2d;
        var delta = e.Delta > 0 ? step : -step;
        var next = Math.Clamp(vm.GridMinRowHeight + delta, 24d, 120d);
        if (Math.Abs(next - vm.GridMinRowHeight) < 0.001d)
            return;

        vm.GridMinRowHeight = next;
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
        if (!IsPrimaryDamagesColumn(fieldName))
            return;

        var row = FindAncestor<DataGridRow>(cell);
        if (row?.Item is not SchachtRecord record)
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

    private static bool TryGetEditedTextValue(FrameworkElement? element, out string value)
    {
        if (element is ComboBox combo)
        {
            value = ResolveComboBoxValue(combo);
            return true;
        }

        if (element is TextBox textBox)
        {
            value = textBox.Text ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
