using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class ZustandColorMapperTests
{
    [Theory]
    [InlineData(null, ZustandFarbe.Unbekannt)]
    [InlineData(0, ZustandFarbe.Gut)]
    [InlineData(1, ZustandFarbe.Gut)]
    [InlineData(2, ZustandFarbe.Mittel)]
    [InlineData(3, ZustandFarbe.Schlecht)]
    [InlineData(4, ZustandFarbe.Schlecht)]
    [InlineData(5, ZustandFarbe.Schlecht)]
    public void Map_VsaSkala_0gut_5schlecht(int? wert, ZustandFarbe erwartet)
        => Assert.Equal(erwartet, ZustandColorMapper.Map(wert, invertiert: false));

    [Fact]
    public void Map_InvertierteSkala_4istGut()
        => Assert.Equal(ZustandFarbe.Gut, ZustandColorMapper.Map(4, invertiert: true));
}
