using AuswertungPro.Next.Domain.VsaCatalog;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaObservationMapTests
{
    [Theory]
    [InlineData("Bogen", "BCC")]
    [InlineData("Rohrbogen links", "BCC")]
    [InlineData("ein Bogen 90 Grad", "BCC")]
    [InlineData("Rohranfang", "BCD")]
    [InlineData("Rohrende", "BCE")]
    [InlineData("Verschobene Rohrverbindung", "BAJ")]
    public void MapsKnownObservations(string text, string expected)
        => Assert.Equal(expected, VsaObservationMap.MapGermanObservationToCode(text));

    [Theory]
    [InlineData("Rohr verbogen")]        // Verformung, NICHT Bogen (Audit)
    [InlineData("Rohr stark abgebogen")]
    [InlineData("Rohrmaterialwechsel")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotMapAmbiguousOrDeformation(string? text)
        => Assert.Null(VsaObservationMap.MapGermanObservationToCode(text));
}
