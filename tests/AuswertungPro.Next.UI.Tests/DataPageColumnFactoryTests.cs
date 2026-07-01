using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnFactoryTests
{
    [Fact]
    public void Create_builds_managed_combo_column_for_known_dropdown_field()
    {
        RunOnSta(() =>
        {
            var column = DataPageColumnFactory.Create(
                "Eigentuemer",
                "Eigentuemer",
                NoKeyboardFocus,
                NoSelectionChanged);

            var comboColumn = Assert.IsType<DataGridTemplateColumn>(column);
            Assert.Equal("Eigentuemer", comboColumn.Header);
            Assert.Equal(DataGridLengthUnitType.SizeToHeader, comboColumn.Width.UnitType);

            var combo = AssertTemplateRoot<ComboBox>(comboColumn.CellEditingTemplate);
            Assert.Equal("Eigentuemer", GetFactoryValue(combo, FrameworkElement.TagProperty));
            Assert.Equal(false, GetFactoryValue(combo, ComboBox.IsEditableProperty));
            AssertFactoryBinding(combo, ComboBox.ItemsSourceProperty, "DataContext.EigentuemerOptions");
        });
    }

    [Fact]
    public void Create_builds_special_text_and_cost_columns()
    {
        RunOnSta(() =>
        {
            var recommendation = Assert.IsType<DataGridTextColumn>(DataPageColumnFactory.Create(
                "Empfohlene_Sanierungsmassnahmen",
                "Empfehlung",
                NoKeyboardFocus,
                NoSelectionChanged));
            Assert.NotNull(recommendation.EditingElementStyle);

            var cost = Assert.IsType<DataGridTemplateColumn>(DataPageColumnFactory.Create(
                "Kosten",
                "Kosten",
                NoKeyboardFocus,
                NoSelectionChanged));
            Assert.Equal("Fields[Kosten]", cost.SortMemberPath);
        });
    }

    [Fact]
    public void Create_builds_standard_text_column_for_plain_fields()
    {
        RunOnSta(() =>
        {
            var column = Assert.IsType<DataGridTextColumn>(DataPageColumnFactory.Create(
                "Bemerkungen",
                "Bemerkungen",
                NoKeyboardFocus,
                NoSelectionChanged));

            var binding = Assert.IsType<Binding>(column.Binding);
            Assert.Equal("Fields[Bemerkungen]", binding.Path.Path);
        });
    }

    [Fact]
    public void DataPage_delegates_column_type_selection_to_factory()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var ensureColumns = SourceTextTestHelpers.ExtractMethodBody(source, "private void EnsureColumns()");

        Assert.Contains("DataPageColumnFactory.Create(", ensureColumns, StringComparison.Ordinal);
        Assert.DoesNotContain("GridDropdownFieldPolicy.TryResolve", ensureColumns, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGridWrappingTextColumnFactory.Create", ensureColumns, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGridCostColumnFactory.Create", ensureColumns, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGridStandardTextColumnFactory.Create", ensureColumns, StringComparison.Ordinal);
        Assert.DoesNotContain("private DataGridTemplateColumn CreateComboColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private DataGridTemplateColumn CreateSimpleComboColumn", source, StringComparison.Ordinal);
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
    {
        var values = typeof(FrameworkElementFactory)
            .GetField("PropertyValues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(factory)!;
        var valuesType = values.GetType();
        var count = (int)valuesType.GetProperty("Count")!.GetValue(values)!;
        var itemProperty = valuesType.GetProperty("Item")!;
        for (var i = 0; i < count; i++)
        {
            var propertyValue = itemProperty.GetValue(values, new object[] { i })!;
            var propertyField = propertyValue.GetType()
                .GetField("Property", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            var storedProperty = propertyField.GetValue(propertyValue);
            if (!Equals(storedProperty, property))
                continue;

            return propertyValue.GetType()
                .GetField("ValueInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(propertyValue);
        }

        throw new InvalidOperationException($"Factory-Wert nicht gefunden: {property.Name}");
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
