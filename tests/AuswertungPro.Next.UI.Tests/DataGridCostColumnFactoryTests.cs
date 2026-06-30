using System.Threading;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Reflection;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridCostColumnFactoryTests
{
    [Fact]
    public void Create_sets_cost_column_metadata()
    {
        RunOnSta(() =>
        {
            var column = DataGridCostColumnFactory.Create("Kosten", "Kosten CHF");

            Assert.Equal("Kosten CHF", column.Header);
            Assert.Equal("Fields[Kosten]", column.SortMemberPath);
            Assert.Equal(DataGridLengthUnitType.SizeToHeader, column.Width.UnitType);
        });
    }

    [Fact]
    public void Create_uses_chf_converter_for_display_and_edit_templates()
    {
        RunOnSta(() =>
        {
            var column = DataGridCostColumnFactory.Create("Kosten", "Kosten CHF");

            var displayPanel = AssertTemplateRoot<DockPanel>(column.CellTemplate);
            var displayCurrency = AssertChild<TextBlock>(displayPanel, 0);
            var displayAmount = AssertChild<TextBlock>(displayPanel, 1);
            AssertConverterBinding(displayCurrency, TextBlock.TextProperty, "currency");
            AssertConverterBinding(displayAmount, TextBlock.TextProperty, "amount");
            Assert.Equal(TextAlignment.Right, GetFactoryValue(displayAmount, TextBlock.TextAlignmentProperty));
            Assert.Equal(TextTrimming.CharacterEllipsis, GetFactoryValue(displayAmount, TextBlock.TextTrimmingProperty));

            var editPanel = AssertTemplateRoot<DockPanel>(column.CellEditingTemplate);
            var editCurrency = AssertChild<TextBlock>(editPanel, 0);
            var editAmount = AssertChild<TextBox>(editPanel, 1);
            AssertConverterBinding(editCurrency, TextBlock.TextProperty, "currency");
            var editBinding = AssertConverterBinding(editAmount, TextBox.TextProperty, "amount");
            Assert.Equal(BindingMode.TwoWay, editBinding.Mode);
            Assert.Equal(UpdateSourceTrigger.LostFocus, editBinding.UpdateSourceTrigger);
            Assert.Equal(TextAlignment.Right, GetFactoryValue(editAmount, TextBox.TextAlignmentProperty));
            Assert.Equal(VerticalAlignment.Center, GetFactoryValue(editAmount, Control.VerticalContentAlignmentProperty));
        });
    }

    private static Binding AssertConverterBinding(FrameworkElementFactory factory, DependencyProperty property, string converterParameter)
    {
        var binding = Assert.IsType<Binding>(GetFactoryValue(factory, property));
        Assert.Equal("Fields[Kosten]", binding.Path.Path);
        Assert.IsType<ChfAccountingDisplayConverter>(binding.Converter);
        Assert.Equal(converterParameter, binding.ConverterParameter);
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

    private static FrameworkElementFactory AssertChild<T>(FrameworkElementFactory parent, int index)
    {
        var child = parent.FirstChild;
        Assert.NotNull(child);
        for (var i = 0; i < index; i++)
        {
            child = child.NextSibling;
            Assert.NotNull(child);
        }

        Assert.Equal(typeof(T), child.Type);
        return child;
    }

    private static object? GetFactoryValue(FrameworkElementFactory factory, DependencyProperty property)
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

        throw new InvalidOperationException($"Factory-Wert nicht gefunden: {property.Name}");
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
