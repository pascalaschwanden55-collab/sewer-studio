using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class NetworkHoldingTests
{
    [Theory]
    [InlineData("Privat", true)]
    [InlineData("privat", true)]
    [InlineData("Abwasser Uri", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPrivate_beurteilt_die_Eigentuemerangabe(string? eigentuemer, bool erwartet)
    {
        var haltung = new NetworkHolding("36051-36329", eigentuemer!, 11.46, "LINESTRING(1 1,2 2)");

        Assert.Equal(erwartet, haltung.IsPrivate);
    }
}
