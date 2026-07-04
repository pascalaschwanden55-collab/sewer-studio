using System.Globalization;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public class ByteSizeFormatterTests
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-CH");

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(-5, "0 B")]
    public void Format_KleineGroessen(long bytes, string expected)
        => Assert.Equal(expected, ByteSizeFormatter.Format(bytes, De));

    [Fact]
    public void Format_Megabytes_EineNachkommastelle()
        => Assert.Equal("1.5 MB", ByteSizeFormatter.Format(1_572_864, CultureInfo.InvariantCulture));

    [Fact]
    public void Format_Gigabytes_EineNachkommastelle()
        => Assert.Equal("57.4 GB", ByteSizeFormatter.Format(61_628_951_099, CultureInfo.InvariantCulture));
}
