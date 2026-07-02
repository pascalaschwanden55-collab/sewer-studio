using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteSettingsPathTests
{
    [Fact]
    public void KarteViewModel_NutztSettingsPfadeAusServiceProvider()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings
        {
            AbwasserkatasterXtfPath = @"C:\SewerStudio-Test\netz.xtf",
            QgisTilesPath = @"C:\SewerStudio-Test\tiles"
        };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var viewModel = new KarteViewModel(shell: null!, services);

        Assert.Equal(settings.AbwasserkatasterXtfPath, ReadPrivateStringProperty(viewModel, "XtfPath"));
        Assert.Equal(settings.QgisTilesPath, ReadPrivateStringProperty(viewModel, "QgisTilesPath"));
    }

    [Fact]
    public void AppSettings_EnthaeltQgisTilesPathDefault()
    {
        var settings = new AppSettings();

        Assert.False(string.IsNullOrWhiteSpace(settings.QgisTilesPath));
        Assert.Contains("tiles_test", settings.QgisTilesPath, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadPrivateStringProperty(object instance, string name)
        => instance.GetType()
            .GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(instance) as string;
}
