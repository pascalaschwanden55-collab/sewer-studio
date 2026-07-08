using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Kanonische Einstufung des Feldes "Ausgefuehrt_durch" fuer den QGIS-Layer
/// "Ausgefuehrt durch": Baumeister / Sanierer / Gartenbauer / leer.
/// </summary>
public sealed class AusgefuehrtDurchKategorieTests
{
    [Theory]
    [InlineData("Baumeister", "Baumeister")]
    [InlineData("baumeister", "Baumeister")]
    [InlineData("Kanalsanierer", "Sanierer")]   // Dropdown-Wert -> kanonisch "Sanierer"
    [InlineData("Sanierer", "Sanierer")]
    [InlineData("Gartenbauer", "Gartenbauer")]
    [InlineData("Gärtner", "Gartenbauer")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    [InlineData("Irgendetwas", "")]
    public void Resolve_ordnet_ausfuehrenden_kanonisch_zu(string? input, string expected)
    {
        Assert.Equal(expected, AusgefuehrtDurchKategorie.Resolve(input));
    }
}
