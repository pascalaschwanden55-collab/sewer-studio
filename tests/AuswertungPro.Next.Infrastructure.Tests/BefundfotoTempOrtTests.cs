using AuswertungPro.Next.Application.Media;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Eine Regel fuer «liegt im Temp-Ordner» (Deepscan 02.10.2026, R3): Aufnahmewege
/// und Projektpruefung fragen dieselbe Stelle.
/// </summary>
public sealed class BefundfotoTempOrtTests
{
    private static readonly string[] SimulierterTemp = [@"C:\Sim\Temp\"];

    [Theory]
    [InlineData(@"C:\Sim\Temp\vsa_foto1_abc.png", true)]
    [InlineData(@"c:\sim\temp\Unterordner\coding_live_1.png", true)]
    [InlineData(@"C:\Sim\Temp", true)]
    [InlineData(@"C:\Sim\TempAblage\foto.png", false)]
    [InlineData(@"D:\Projekte\Bauen\Fotos\foto.png", false)]
    [InlineData(@"Fotos\foto.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LiegtImTemp_erkennt_nur_pfade_unter_dem_temp_ordner(string? pfad, bool erwartet)
        => Assert.Equal(erwartet, BefundfotoTempOrt.LiegtImTemp(pfad, SimulierterTemp));

    [Fact]
    public void Standardwurzeln_enthalten_den_windows_temp_ordner()
    {
        var foto = Path.Combine(Path.GetTempPath(), "vsa_foto1_test.png");

        Assert.True(BefundfotoTempOrt.LiegtImTemp(foto));
    }
}
