using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MapPreloadPolicyTests
{
    [Theory]
    [InlineData(64.0, true)]   // Workstation -> vorladen
    [InlineData(32.0, true)]
    [InlineData(24.0, true)]   // Schwelle einschliesslich
    [InlineData(23.9, false)]
    [InlineData(16.0, false)]  // schwacher Rechner -> Sparmodus, lazy laden
    [InlineData(8.0, false)]
    public void ShouldPreload_haengt_an_der_ram_schwelle(double totalRamGb, bool expected)
        => Assert.Equal(expected, MapPreloadPolicy.ShouldPreload(totalRamGb));
}
