using System.Globalization;
using AuswertungPro.Next.UI.Controls.Animations;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Formatlogik des animierten Zahlen-Zaehlers (vom Control getrennt, damit testbar).</summary>
public sealed class CounterTextFormatterTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Fact]
    public void No_format_renders_plain_number()
    {
        Assert.Equal("5", CounterTextFormatter.Format(5d, null, Inv));
    }

    [Fact]
    public void Numeric_format_is_applied()
    {
        Assert.Equal("1,235", CounterTextFormatter.Format(1234.56, "N0", Inv));
        Assert.Equal("12.3", CounterTextFormatter.Format(12.34, "0.0", Inv));
    }

    [Fact]
    public void Composite_format_with_suffix_is_applied()
    {
        Assert.Equal("42 Stk", CounterTextFormatter.Format(42d, "{0:N0} Stk", Inv));
        Assert.Equal("3.5 m", CounterTextFormatter.Format(3.5, "{0:0.0} m", Inv));
    }

    [Fact]
    public void Invalid_format_falls_back_to_plain_number()
    {
        Assert.Equal("7", CounterTextFormatter.Format(7d, "{0:Z9!!", Inv));
    }
}
