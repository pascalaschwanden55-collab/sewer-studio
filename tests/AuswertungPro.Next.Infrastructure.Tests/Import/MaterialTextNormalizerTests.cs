using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer MaterialTextNormalizer.
/// Sichert das IST-Verhalten aus WinCanDbImportService.NormalizeMaterial
/// und M150MdbImportHelper.NormalizeMaterialValue.
/// </summary>
public class MaterialTextNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Normalize_Leer_GibtNullZurueck(string? input, string? erwartet)
        => Assert.Equal(erwartet, MaterialTextNormalizer.Normalize(input));

    [Fact]
    public void Normalize_NurMaterial_UnveraendertZurueck()
        => Assert.Equal("PVC", MaterialTextNormalizer.Normalize("PVC"));

    [Fact]
    public void Normalize_MaterialMitLeerzeichen_Getrimmt()
        => Assert.Equal("Beton", MaterialTextNormalizer.Normalize("  Beton  "));

    [Fact]
    public void Normalize_MehrereZeilen_NurErsteZeile()
        => Assert.Equal("Zement", MaterialTextNormalizer.Normalize("Zement\nGereinigt    Ja"));

    [Fact]
    public void Normalize_GereinigJa_EntferntAmEnde()
        => Assert.Equal("Beton", MaterialTextNormalizer.Normalize("Beton Gereinigt Ja"));

    [Fact]
    public void Normalize_GereinigNein_EntferntAmEnde()
        => Assert.Equal("Steinzeug", MaterialTextNormalizer.Normalize("Steinzeug Gereinigt Nein"));

    [Fact]
    public void Normalize_NichtGereinigt_EntferntAmEnde()
        => Assert.Equal("PVC", MaterialTextNormalizer.Normalize("PVC nicht gereinigt"));

    [Fact]
    public void Normalize_Verschmutzt_EntferntAmEnde()
        => Assert.Equal("Beton", MaterialTextNormalizer.Normalize("Beton verschmutzt"));

    [Fact]
    public void Normalize_NurBereinigungstoken_GibtNullZurueck()
        => Assert.Null(MaterialTextNormalizer.Normalize("Gereinigt Ja"));

    [Fact]
    public void Normalize_MaterialMitNurBereinigungsTokenInZeile1_NullZurueck()
        => Assert.Null(MaterialTextNormalizer.Normalize("Gereinigt\nBeton"));

    [Fact]
    public void Normalize_CaseInsensitiv()
    {
        Assert.Equal("Beton", MaterialTextNormalizer.Normalize("Beton GEREINIGT JA"));
        Assert.Equal("PVC", MaterialTextNormalizer.Normalize("PVC NICHT GEREINIGT"));
    }
}
