using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridWrappingTextColumnFactoryTests
{
    [Fact]
    public void Create_builds_multiline_text_column_metadata()
    {
        RunOnSta(() =>
        {
            var column = DataGridWrappingTextColumnFactory.Create(
                "Empfohlene_Sanierungsmassnahmen",
                "Empfohlene Sanierungsmassnahmen");

            Assert.Equal("Empfohlene Sanierungsmassnahmen", column.Header);
            Assert.Equal(DataGridLengthUnitType.SizeToHeader, column.Width.UnitType);

            var binding = Assert.IsType<Binding>(column.Binding);
            Assert.Equal("Fields[Empfohlene_Sanierungsmassnahmen]", binding.Path.Path);
            Assert.Equal(BindingMode.TwoWay, binding.Mode);
            Assert.Equal(UpdateSourceTrigger.LostFocus, binding.UpdateSourceTrigger);
        });
    }

    [Fact]
    public void Create_preserves_display_and_edit_styles_from_data_page()
    {
        RunOnSta(() =>
        {
            var column = DataGridWrappingTextColumnFactory.Create(
                "Empfohlene_Sanierungsmassnahmen",
                "Empfohlene Sanierungsmassnahmen");

            AssertStyleSetter(column.ElementStyle, TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            AssertStyleSetter(column.ElementStyle, TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            AssertStyleSetter(column.ElementStyle, TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            AssertStyleSetter(column.EditingElementStyle, TextBox.TextWrappingProperty, TextWrapping.Wrap);
            AssertStyleSetter(column.EditingElementStyle, TextBox.AcceptsReturnProperty, true);
            AssertStyleSetter(column.EditingElementStyle, TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top);
            AssertStyleSetter(column.EditingElementStyle, TextBox.MinHeightProperty, 60d);
        });
    }

    private static void AssertStyleSetter(Style? style, DependencyProperty property, object expectedValue)
    {
        Assert.NotNull(style);
        Assert.Contains(
            style.Setters.OfType<Setter>(),
            setter => setter.Property == property && Equals(setter.Value, expectedValue));
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
