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

    [Theory]
    [InlineData(null, ZustandFarbe.Unbekannt)]
    [InlineData(4, ZustandFarbe.Gut)]       // bester Zustand
    [InlineData(3, ZustandFarbe.Gut)]
    [InlineData(2, ZustandFarbe.Mittel)]    // Mittelklasse darf NICHT rot sein
    [InlineData(1, ZustandFarbe.Schlecht)]
    [InlineData(0, ZustandFarbe.Schlecht)]  // schlechtester Zustand
    public void Map_InvertierteEzSkala_4gut_0schlecht(int? wert, ZustandFarbe erwartet)
        => Assert.Equal(erwartet, ZustandColorMapper.Map(wert, invertiert: true));
}
