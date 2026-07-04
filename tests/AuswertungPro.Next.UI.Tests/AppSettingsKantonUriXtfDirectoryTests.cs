using System.Text.Json;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsKantonUriXtfDirectoryTests
{
    [Fact]
    public void AbwasserkatasterXtfPath_survives_json_roundtrip()
    {
        var settings = new AppSettings { AbwasserkatasterXtfPath = @"D:\Uri\Kataster.xtf" };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(@"D:\Uri\Kataster.xtf", restored!.AbwasserkatasterXtfPath);
    }

    [Fact]
    public void AbwasserkatasterXtfPath_defaults_to_qgis_export_file()
    {
        var settings = new AppSettings();

        Assert.Equal(
            @"D:\QGIS_V4.03\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf",
            settings.AbwasserkatasterXtfPath);
    }

    [Fact]
    public void KantonUriXtfDirectory_survives_json_roundtrip()
    {
        var settings = new AppSettings { KantonUriXtfDirectory = @"D:\Uri\XTF" };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(@"D:\Uri\XTF", restored!.KantonUriXtfDirectory);
    }

    [Fact]
    public void KantonUriXtfDirectory_defaults_to_qgis_export_folder()
    {
        var settings = new AppSettings();

        Assert.False(string.IsNullOrWhiteSpace(settings.KantonUriXtfDirectory));
        Assert.Equal(@"D:\QGIS_V4.03\Export_Sewer_Studio", settings.KantonUriXtfDirectory);
    }
}
