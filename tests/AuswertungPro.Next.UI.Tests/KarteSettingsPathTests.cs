using System.IO;
using AuswertungPro.Next.UI;
using Xunit;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteSettingsPathTests
{
    [Fact]
    public void KarteViewModel_NutztSettingsStattHartkodierteQgisPfade()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "KarteViewModel.cs"));

        Assert.DoesNotContain(@"D:\QGIS_V4", source, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AbwasserkatasterXtfPath", source, System.StringComparison.Ordinal);
        Assert.Contains("QgisTilesPath", source, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_EnthaeltQgisTilesPathDefault()
    {
        var settings = new AppSettings();

        Assert.False(string.IsNullOrWhiteSpace(settings.QgisTilesPath));
        Assert.Contains("tiles_test", settings.QgisTilesPath, System.StringComparison.OrdinalIgnoreCase);
    }
}
