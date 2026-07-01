using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridFieldMetaTooltipStyleFactoryTests
{
    [Fact]
    public void Create_preserves_base_style_and_adds_field_meta_tooltip_binding()
    {
        RunOnSta(() =>
        {
            var baseStyle = new Style(typeof(DataGridCell));

            var style = DataGridFieldMetaTooltipStyleFactory.Create("Zustandsklasse", baseStyle);

            Assert.Equal(typeof(DataGridCell), style.TargetType);
            Assert.Same(baseStyle, style.BasedOn);

            var setter = style.Setters
                .OfType<Setter>()
                .Single(x => x.Property == FrameworkElement.ToolTipProperty);

            var tooltip = Assert.IsType<TextBlock>(setter.Value);
            var binding = Assert.IsType<MultiBinding>(
                BindingOperations.GetBindingBase(tooltip, TextBlock.TextProperty));

            Assert.Equal("Quelle: {0} | UserEdited: {1} | Konflikt: {2}", binding.StringFormat);
            Assert.Collection(
                binding.Bindings.OfType<Binding>(),
                x => Assert.Equal("FieldMeta[Zustandsklasse].Source", x.Path.Path),
                x => Assert.Equal("FieldMeta[Zustandsklasse].UserEdited", x.Path.Path),
                x => Assert.Equal("FieldMeta[Zustandsklasse].Conflict", x.Path.Path));
        });
    }

    [Fact]
    public void DataPage_delegates_field_meta_tooltip_style_to_factory()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var method = SourceTextTestHelpers.ExtractMethodBody(source, "private void ApplyFieldMetaTooltip(DataGridColumn col, string field)");

        Assert.Contains("DataGridFieldMetaTooltipStyleFactory.Create(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new MultiBinding", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new TextBlock", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new Style(typeof(DataGridCell)", method, StringComparison.Ordinal);
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
