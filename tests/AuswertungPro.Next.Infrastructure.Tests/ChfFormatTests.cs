using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ChfFormatTests
{
    [Theory]
    [InlineData(12345.67, "12'345.67 CHF")]
    [InlineData(1234567.89, "1'234'567.89 CHF")]
    public void Money_Formatiert_Schweizer_Betraege_Einheitlich(double value, string expected)
    {
        Assert.Equal(expected, ChfFormat.Money((decimal)value));
    }
}
