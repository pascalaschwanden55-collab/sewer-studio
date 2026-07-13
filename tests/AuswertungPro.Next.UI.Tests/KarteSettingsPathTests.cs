using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.ViewModels.Pages;
using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteSettingsPathTests
{
    [Fact]
    public void KarteViewModel_Nutzt_uebergebene_Einstellungspfade()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var explicitFile = Path.Combine(dir, "netz.xtf");
        var tileDir = Path.Combine(dir, "tiles");
        File.WriteAllText(explicitFile, "<TRANSFER />");
        Directory.CreateDirectory(tileDir);

        var settings = new AppSettings
        {
            AbwasserkatasterXtfPath = explicitFile,
            OfflineBasemapPath = tileDir
        };
        var viewModel = new KarteViewModel(
            shell: null!,
            settings,
            new NetworkFeatureCache(),
            playVideo: (_, _) => { });

        Assert.Equal(settings.AbwasserkatasterXtfPath, ReadPrivateStringProperty(viewModel, "XtfPath"));
        Assert.Equal(settings.OfflineBasemapPath, ReadPrivateStringProperty(viewModel, "OfflineBasemapPath"));

        Directory.Delete(dir, true);
    }

    [Fact]
    public void KarteViewModel_NutztKatasterXtfAusOrdnerWennDateipfadAltIst()
    {
        using var dir = new TempDirectory();
        var expected = Path.Combine(dir.Path, "Abwasserkataster_Uri_korrigiert.xtf");
        File.WriteAllText(expected, "<TRANSFER />");

        var settings = new AppSettings
        {
            AbwasserkatasterXtfPath = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf",
            KantonUriXtfDirectory = dir.Path
        };
        var viewModel = new KarteViewModel(
            shell: null!,
            settings,
            new NetworkFeatureCache(),
            playVideo: (_, _) => { });

        Assert.Equal(expected, ReadPrivateStringProperty(viewModel, "XtfPath"));
    }

    [Fact]
    public void AppSettings_EnthaeltQgisTilesPathDefault()
    {
        var settings = new AppSettings();

        Assert.False(string.IsNullOrWhiteSpace(settings.QgisTilesPath));
        Assert.Contains("tiles_test", settings.QgisTilesPath, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KarteViewModel_speichert_keinen_ServiceProvider_als_Feld()
    {
        var fields = typeof(KarteViewModel).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
    }

    private static string? ReadPrivateStringProperty(object instance, string name)
        => instance.GetType()
            .GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(instance) as string;

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
