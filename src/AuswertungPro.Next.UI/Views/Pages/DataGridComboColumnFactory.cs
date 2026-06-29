using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed record DataGridComboColumnMenuCommands
{
    public DataGridComboColumnMenuCommands(
        string editCommand,
        string previewCommand,
        string resetCommand,
        string removeCommand,
        string addCommand)
    {
        EditCommand = editCommand;
        PreviewCommand = previewCommand;
        ResetCommand = resetCommand;
        RemoveCommand = removeCommand;
        AddCommand = addCommand;
    }

    public string EditCommand { get; }
    public string PreviewCommand { get; }
    public string ResetCommand { get; }
    public string RemoveCommand { get; }
    public string AddCommand { get; }
}

public static class DataGridComboColumnFactory
{
    private static readonly IValueConverter HorizontalAlignmentToTextAlignmentConverter =
        new DataGridHorizontalAlignmentToTextAlignmentConverter();

    public static DataGridTemplateColumn Create(
        string fieldName,
        string header,
        string itemsSourcePath,
        object tag,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged,
        bool allowFreeText,
        bool bindIsProjectReady,
        DataGridComboColumnMenuCommands? menuCommands = null)
    {
        var displayFactory = CreateDisplayFactory(fieldName);
        var comboFactory = CreateComboFactory(
            fieldName,
            itemsSourcePath,
            tag,
            lostKeyboardFocus,
            selectionChanged,
            allowFreeText,
            bindIsProjectReady);

        if (menuCommands is not null)
            comboFactory.SetValue(FrameworkElement.ContextMenuProperty, CreateContextMenu(menuCommands));

        return new DataGridTemplateColumn
        {
            Header = header,
            CellTemplate = new DataTemplate { VisualTree = displayFactory },
            CellEditingTemplate = new DataTemplate { VisualTree = comboFactory },
            Width = DataGridLength.SizeToHeader
        };
    }

    private static FrameworkElementFactory CreateDisplayFactory(string fieldName)
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
        return displayFactory;
    }

    private static FrameworkElementFactory CreateComboFactory(
        string fieldName,
        string itemsSourcePath,
        object tag,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged,
        bool allowFreeText,
        bool bindIsProjectReady)
    {
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

        if (bindIsProjectReady)
        {
            comboFactory.SetBinding(UIElement.IsHitTestVisibleProperty, new Binding("DataContext.IsProjectReady")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
            });
        }

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

        comboFactory.SetValue(FrameworkElement.TagProperty, tag);
        comboFactory.AddHandler(UIElement.LostKeyboardFocusEvent, lostKeyboardFocus);
        comboFactory.AddHandler(Selector.SelectionChangedEvent, selectionChanged);
        return comboFactory;
    }

    private static ContextMenu CreateContextMenu(DataGridComboColumnMenuCommands commands)
    {
        var contextMenu = new ContextMenu();
        contextMenu.Opened += (_, __) =>
        {
            if (contextMenu.PlacementTarget is not FrameworkElement target)
                return;

            var grid = FindAncestor<DataGrid>(target);
            contextMenu.DataContext = grid?.DataContext ?? target.DataContext;
        };

        contextMenu.Items.Add(BoundMenuItem("Liste bearbeiten...", commands.EditCommand));
        contextMenu.Items.Add(BoundMenuItem("Vorschau", commands.PreviewCommand));
        contextMenu.Items.Add(BoundMenuItem("Zuruecksetzen auf Standard", commands.ResetCommand));
        contextMenu.Items.Add(BoundMenuItem("Wert hinzufuegen", commands.AddCommand, withPlacementTargetParameter: true));
        contextMenu.Items.Add(BoundMenuItem("Wert entfernen", commands.RemoveCommand, withPlacementTargetParameter: true));
        return contextMenu;
    }

    private static MenuItem BoundMenuItem(string header, string commandPath, bool withPlacementTargetParameter = false)
    {
        var item = new MenuItem { Header = header };
        item.SetBinding(MenuItem.CommandProperty, new Binding(commandPath));
        if (withPlacementTargetParameter)
        {
            item.SetBinding(MenuItem.CommandParameterProperty, new Binding("PlacementTarget")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1)
            });
        }

        return item;
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
}
