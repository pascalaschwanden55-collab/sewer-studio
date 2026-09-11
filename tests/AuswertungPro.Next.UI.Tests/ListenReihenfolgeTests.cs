using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ListenReihenfolgeTests
{
    [Theory]
    [InlineData(0, 3, false, 3)]
    [InlineData(0, 3, true, 4)]
    [InlineData(3, 0, false, 1)]
    [InlineData(3, 0, true, 2)]
    [InlineData(1, 1, false, 2)]
    [InlineData(1, 1, true, 2)]
    [InlineData(1, 2, false, 2)]
    [InlineData(2, 1, true, 3)]
    [InlineData(-1, 1, true, 0)]
    [InlineData(1, 4, false, 0)]
    public void Einfuegelinie_bestimmt_die_Position_nach_dem_Herausnehmen_der_Quellzeile(
        int quelle, int ziel, bool danach, int erwartet)
        => Assert.Equal(erwartet, ListenReihenfolgeController.Zielposition(quelle, ziel, danach, 4));

    [Fact]
    public void Gleiche_Bezeichnung_ist_kein_Beweis_fuer_dasselbe_Projekt()
    {
        var a = new string('A', 2);
        var b = new string('A', 2);
        Assert.True(ListenReihenfolgeController.GleicheFolge(new[] { a }, new[] { a }));
        Assert.False(ListenReihenfolgeController.GleicheFolge(new[] { a }, new[] { b }));
    }
}
