using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnSetupTests
{
    [Fact]
    public void Apply_sets_common_column_properties_and_left_alignment()
    {
        RunOnSta(() =>
        {
            var column = new DataGridTextColumn();

            var setup = DataPageColumnSetup.Apply(column, "Bemerkungen");

            Assert.Equal("Bemerkungen", column.GetValue(FrameworkElement.TagProperty));
            Assert.True(column.CanUserResize);
            Assert.Equal(72, column.MinWidth);
            Assert.Equal(HorizontalAlignment.Left, setup.DefaultHorizontalAlignment);
            Assert.Equal(VerticalAlignment.Center, setup.DefaultVerticalAlignment);
            AssertFieldMetaTooltipBinding(column.CellStyle, "Bemerkungen");
        });
    }

    [Fact]
    public void Apply_preserves_color_style_and_returns_right_alignment_for_costs()
    {
        RunOnSta(() =>
        {
            var column = new DataGridTextColumn();

            var setup = DataPageColumnSetup.Apply(column, "Kosten");

            Assert.Equal(72, column.MinWidth);
            Assert.Equal(HorizontalAlignment.Right, setup.DefaultHorizontalAlignment);
            AssertFieldMetaTooltipBinding(column.CellStyle, "Kosten");
        });
    }

    [Fact]
    public void Apply_uses_compact_min_width_for_nr_column()
    {
        RunOnSta(() =>
        {
            var column = new DataGridTextColumn();

            DataPageColumnSetup.Apply(column, "NR");

            Assert.Equal(56, column.MinWidth);
        });
    }

    private static void AssertFieldMetaTooltipBinding(Style? style, string fieldName)
    {
        Assert.NotNull(style);
        var tooltipSetter = style!.Setters
            .OfType<Setter>()
            .Single(setter => setter.Property == FrameworkElement.ToolTipProperty);

        var tooltip = Assert.IsType<TextBlock>(tooltipSetter.Value);
        var binding = Assert.IsType<MultiBinding>(BindingOperations.GetBindingBase(tooltip, TextBlock.TextProperty));

        Assert.Collection(
            binding.Bindings.OfType<Binding>(),
            x => Assert.Equal($"FieldMeta[{fieldName}].Source", x.Path.Path),
            x => Assert.Equal($"FieldMeta[{fieldName}].UserEdited", x.Path.Path),
            x => Assert.Equal($"FieldMeta[{fieldName}].Conflict", x.Path.Path));
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
