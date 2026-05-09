using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Pages;

// SchaechtePage WPF-DataGridColumn-Factories: ApplyColorStyle (Hervorhebung
// pro Spaltenname), CreateManagedComboColumn (mit ContextMenu fuer Liste-
// bearbeiten/Reset/Add/Remove), CreateSimpleComboColumn, CreateCostColumn
// (CHF + rechtsbuendig), CreateZustandsklasseColumn (1-5 Validation).
// Aus dem Hauptdatei extrahiert (Slice 10d).
public partial class SchaechtePage
{
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
}
