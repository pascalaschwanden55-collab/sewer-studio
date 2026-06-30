using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer CodeSourceBadgeFormatter (IST-Verhalten aus CodeTreeNode.BuildSourceBadge).
/// </summary>
public sealed class CodeSourceBadgeFormatterTests
{
    [Fact]
    public void GetBadgeText_null_ergibt_leerstring()
        => Assert.Equal(string.Empty, CodeSourceBadgeFormatter.GetBadgeText(null));

    [Fact]
    public void GetBadgeText_leerstring_ergibt_leerstring()
        => Assert.Equal(string.Empty, CodeSourceBadgeFormatter.GetBadgeText(string.Empty));

    [Fact]
    public void GetBadgeText_ili_ergibt_leerstring()
        => Assert.Equal(string.Empty, CodeSourceBadgeFormatter.GetBadgeText(VsaKekCatalogSources.Ili));

    [Fact]
    public void GetBadgeText_icm_ergibt_ICM()
        => Assert.Equal("ICM", CodeSourceBadgeFormatter.GetBadgeText(VsaKekCatalogSources.Icm));

    [Fact]
    public void GetBadgeText_xtf_observed_ergibt_XTF()
        => Assert.Equal("XTF", CodeSourceBadgeFormatter.GetBadgeText(VsaKekCatalogSources.XtfObserved));

    [Fact]
    public void GetBadgeText_wincan_fallback_ergibt_WinCan()
        => Assert.Equal("WinCan", CodeSourceBadgeFormatter.GetBadgeText(VsaKekCatalogSources.WinCanFallback));

    [Fact]
    public void GetBadgeText_unbekannte_quelle_gibt_wert_unveraendert_zurueck()
        => Assert.Equal("CustomSource", CodeSourceBadgeFormatter.GetBadgeText("CustomSource"));

    [Fact]
    public void GetBadgeText_wert_wird_getrimmt_vor_vergleich()
        => Assert.Equal("ICM", CodeSourceBadgeFormatter.GetBadgeText("  " + VsaKekCatalogSources.Icm + "  "));
}
