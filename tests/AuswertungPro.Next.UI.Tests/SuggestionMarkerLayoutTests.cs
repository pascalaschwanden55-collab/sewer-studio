using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SuggestionMarkerLayoutTests
{
    [Fact]
    public void Position_folgt_der_Videozeit_auf_der_Spurbreite()
    {
        Assert.Equal(10.0, SuggestionMarkerLayout.CalculateX(0, 200, 10, 400));
        Assert.Equal(210.0, SuggestionMarkerLayout.CalculateX(100, 200, 10, 400));
        Assert.Equal(410.0, SuggestionMarkerLayout.CalculateX(200, 200, 10, 400));
    }

    [Fact]
    public void Ausserhalb_der_Dauer_oder_ohne_Dauer_gibt_es_keine_Lage()
    {
        Assert.Null(SuggestionMarkerLayout.CalculateX(201, 200, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(-1, 200, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(5, 0, 10, 400));
        Assert.Null(SuggestionMarkerLayout.CalculateX(5, 200, 10, 0));
    }
}
