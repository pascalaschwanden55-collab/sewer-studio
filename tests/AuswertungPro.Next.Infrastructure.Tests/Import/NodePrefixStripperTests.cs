using AuswertungPro.Next.Infrastructure;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer das Entfernen von Knoten-Praefixen (ehemals NodePrefixStripper).
/// Logik liegt jetzt in HoldingIdNormalizer.StripNodePrefixes.
/// </summary>
public class NodePrefixStripperTests
{
    // --- StripNodePrefixes ---

    [Theory]
    [InlineData("07.1028055-10.1064892", "1028055-1064892")]   // beide Teile haben Praefix
    [InlineData("07.1028055-1064892",    "1028055-1064892")]   // nur linker Teil hat Praefix
    [InlineData("1028055-1064892",       "1028055-1064892")]   // kein Praefix → unveraendert
    [InlineData("07.1028055",            "1028055")]           // kein Bindestrich
    [InlineData("1028055",               "1028055")]           // kein Bindestrich, kein Praefix
    [InlineData("10.1064892-06.1099001", "1064892-1099001")]   // zweistelliger Praefix
    public void StripNodePrefixes_Korrekt(string eingabe, string erwartet)
        => Assert.Equal(erwartet, HoldingIdNormalizer.StripNodePrefixes(eingabe));
}
