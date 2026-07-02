using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataGridHorizontalAlignmentToTextAlignmentConverterTests
{
    [Theory]
    [InlineData(HorizontalAlignment.Left, TextAlignment.Left)]
    [InlineData(HorizontalAlignment.Stretch, TextAlignment.Left)]
    [InlineData(HorizontalAlignment.Center, TextAlignment.Center)]
    [InlineData(HorizontalAlignment.Right, TextAlignment.Right)]
    public void Convert_maps_horizontal_alignment_to_text_alignment(
        HorizontalAlignment input,
        TextAlignment expected)
    {
        var converter = new DataGridHorizontalAlignmentToTextAlignmentConverter();

        var actual = converter.Convert(input, typeof(TextAlignment), parameter: null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConvertBack_is_not_supported_for_one_way_template_bindings()
    {
        var converter = new DataGridHorizontalAlignmentToTextAlignmentConverter();

        var actual = converter.ConvertBack(TextAlignment.Right, typeof(HorizontalAlignment), parameter: null, CultureInfo.InvariantCulture);

        Assert.Same(Binding.DoNothing, actual);
    }

}
