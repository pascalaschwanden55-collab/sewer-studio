using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RecordMovePositionCalculatorTests
{
    [Theory]
    [InlineData(2, 5, 1, 0)]   // an den Anfang
    [InlineData(2, 5, 5, 4)]   // ans Ende
    [InlineData(0, 5, 3, 2)]   // 1-basiert -> 0-basiert
    [InlineData(2, 5, 0, 0)]   // zu kleine Position -> auf ersten geklemmt
    [InlineData(2, 5, 99, 4)]  // zu grosse Position -> auf letzten geklemmt
    public void TryResolveTargetIndex_liefert_geklemmten_nullbasierten_index(
        int oldIndex, int count, int targetPosition, int expectedIndex)
    {
        var ok = RecordMovePositionCalculator.TryResolveTargetIndex(
            oldIndex, count, targetPosition, out var targetIndex);

        Assert.True(ok);
        Assert.Equal(expectedIndex, targetIndex);
    }

    [Theory]
    [InlineData(2, 5, 3)]   // Ziel == Start -> kein Zug
    [InlineData(0, 1, 1)]   // einziger Eintrag, bleibt an Position 1
    [InlineData(-1, 5, 1)]  // nichts ausgewaehlt (ungueltiger Startindex)
    [InlineData(0, 0, 1)]   // leere Liste
    [InlineData(7, 5, 1)]   // Startindex ausserhalb der Liste
    public void TryResolveTargetIndex_liefert_false_wenn_kein_sinnvoller_zug(
        int oldIndex, int count, int targetPosition)
    {
        var ok = RecordMovePositionCalculator.TryResolveTargetIndex(
            oldIndex, count, targetPosition, out var targetIndex);

        Assert.False(ok);
        Assert.Equal(-1, targetIndex);
    }
}
