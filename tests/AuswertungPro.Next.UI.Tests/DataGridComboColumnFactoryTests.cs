using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridComboColumnFactoryTests
{
    [Fact]
    public void Create_builds_free_text_combo_column_with_project_ready_binding()
    {
        RunOnSta(() =>
        {
            var column = DataGridComboColumnFactory.Create(
                fieldName: "Eigentuemer",
                header: "Eigentuemer",
                itemsSourcePath: "EigentuemerOptions",
                tag: "Eigentuemer",
                lostKeyboardFocus: NoKeyboardFocus,
                selectionChanged: NoSelectionChanged,
                allowFreeText: true,
                bindIsProjectReady: true);

            Assert.Equal("Eigentuemer", column.Header);
            Assert.Equal(DataGridLengthUnitType.SizeToHeader, column.Width.UnitType);

            var display = AssertTemplateRoot<TextBlock>(column.CellTemplate);
            AssertFactoryBinding(display, TextBlock.TextProperty, "Fields[Eigentuemer]");
            Assert.Equal(TextTrimming.CharacterEllipsis, GetFactoryValue(display, TextBlock.TextTrimmingProperty));

            var combo = AssertTemplateRoot<ComboBox>(column.CellEditingTemplate);
            Assert.Equal("Eigentuemer", GetFactoryValue(combo, FrameworkElement.TagProperty));
            Assert.Equal(true, GetFactoryValue(combo, ComboBox.IsEditableProperty));
            Assert.Equal(true, GetFactoryValue(combo, ComboBox.StaysOpenOnEditProperty));
            Assert.Equal(false, GetFactoryValue(combo, ComboBox.IsTextSearchEnabledProperty));
            AssertFactoryBinding(combo, ComboBox.ItemsSourceProperty, "DataContext.EigentuemerOptions");
            var textBinding = AssertFactoryBinding(combo, ComboBox.TextProperty, "Fields[Eigentuemer]");
            Assert.Equal(BindingMode.TwoWay, textBinding.Mode);
            Assert.Equal(UpdateSourceTrigger.PropertyChanged, textBinding.UpdateSourceTrigger);
            AssertFactoryBinding(combo, UIElement.IsHitTestVisibleProperty, "DataContext.IsProjectReady");
        });
    }

    [Fact]
    public void Create_builds_selected_item_combo_column_with_managed_context_menu()
    {
        RunOnSta(() =>
        {
            var tag = new object();
            var commands = new DataGridComboColumnMenuCommands(
                editCommand: "EditCommand",
                previewCommand: "PreviewCommand",
                resetCommand: "ResetCommand",
                removeCommand: "RemoveCommand",
                addCommand: "AddCommand");

            var column = DataGridComboColumnFactory.Create(
                fieldName: "Pruefungsresultat",
                header: "Pruefungsresultat",
                itemsSourcePath: "PruefungsresultatOptions",
                tag: tag,
                lostKeyboardFocus: NoKeyboardFocus,
                selectionChanged: NoSelectionChanged,
                allowFreeText: false,
                bindIsProjectReady: false,
                menuCommands: commands);

            var combo = AssertTemplateRoot<ComboBox>(column.CellEditingTemplate);
            Assert.Same(tag, GetFactoryValue(combo, FrameworkElement.TagProperty));
            Assert.Equal(false, GetFactoryValue(combo, ComboBox.IsEditableProperty));
            Assert.Equal(false, GetFactoryValue(combo, ComboBox.StaysOpenOnEditProperty));
            AssertFactoryBinding(combo, Selector.SelectedItemProperty, "Fields[Pruefungsresultat]");
            Assert.Null(TryGetFactoryValue(combo, UIElement.IsHitTestVisibleProperty));

            var contextMenu = Assert.IsType<ContextMenu>(GetFactoryValue(combo, FrameworkElement.ContextMenuProperty));
            Assert.Equal(5, contextMenu.Items.Count);
            AssertMenuItem(contextMenu, 0, "Liste bearbeiten...", "EditCommand", hasPlacementTargetParameter: false);
            AssertMenuItem(contextMenu, 1, "Vorschau", "PreviewCommand", hasPlacementTargetParameter: false);
            AssertMenuItem(contextMenu, 2, "Zuruecksetzen auf Standard", "ResetCommand", hasPlacementTargetParameter: false);
            AssertMenuItem(contextMenu, 3, "Wert hinzufuegen", "AddCommand", hasPlacementTargetParameter: true);
            AssertMenuItem(contextMenu, 4, "Wert entfernen", "RemoveCommand", hasPlacementTargetParameter: true);
        });
    }

    private static void AssertMenuItem(
        ContextMenu contextMenu,
        int index,
        string header,
        string commandPath,
        bool hasPlacementTargetParameter)
    {
        var item = Assert.IsType<MenuItem>(contextMenu.Items[index]);
        Assert.Equal(header, item.Header);
        Assert.Equal(commandPath, AssertElementBinding(item, MenuItem.CommandProperty, commandPath).Path.Path);
        var parameterBinding = BindingOperations.GetBinding(item, MenuItem.CommandParameterProperty);
        if (hasPlacementTargetParameter)
        {
            Assert.NotNull(parameterBinding);
            Assert.Equal("PlacementTarget", parameterBinding.Path.Path);
        }
        else
        {
            Assert.Null(parameterBinding);
        }
    }

    private static Binding AssertElementBinding(DependencyObject target, DependencyProperty property, string path)
    {
        var binding = Assert.IsType<Binding>(BindingOperations.GetBinding(target, property));
        Assert.Equal(path, binding.Path.Path);
        return binding;
    }

    private static Binding AssertFactoryBinding(FrameworkElementFactory target, DependencyProperty property, string path)
    {
        var binding = Assert.IsType<Binding>(GetFactoryValue(target, property));
        Assert.Equal(path, binding.Path.Path);
        return binding;
    }

    private static FrameworkElementFactory AssertTemplateRoot<T>(DataTemplate? template)
    {
        Assert.NotNull(template);
        var root = template.VisualTree;
        Assert.NotNull(root);
        Assert.Equal(typeof(T), root.Type);
        return root;
    }

    private static object? GetFactoryValue(FrameworkElementFactory factory, DependencyProperty property)
        => TryGetFactoryValue(factory, property)
           ?? throw new InvalidOperationException($"Factory-Wert nicht gefunden: {property.Name}");

    private static object? TryGetFactoryValue(FrameworkElementFactory factory, DependencyProperty property)
    {
        var values = typeof(FrameworkElementFactory)
            .GetField("PropertyValues", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(factory)!;
        var valuesType = values.GetType();
        var count = (int)valuesType.GetProperty("Count")!.GetValue(values)!;
        var itemProperty = valuesType.GetProperty("Item")!;
        for (var i = 0; i < count; i++)
        {
            var propertyValue = itemProperty.GetValue(values, new object[] { i })!;
            var propertyField = propertyValue.GetType()
                .GetField("Property", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            var storedProperty = propertyField.GetValue(propertyValue);
            if (!Equals(storedProperty, property))
                continue;

            return propertyValue.GetType()
                .GetField("ValueInternal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(propertyValue);
        }

        return null;
    }

    private static void NoKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private static void NoSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
