using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiSettingsResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_KI_Einstellungen_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var settingsPage = new SettingsPageViewModel(services);
        var field = typeof(SettingsPageViewModel).GetField(
            "_aiSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsType<AiPlatformSettingsResolver>(services.AiSettings);
        Assert.Same(
            services.AiSettings,
            services.GetService(typeof(IAiPlatformSettingsResolver)));
        Assert.NotNull(field);
        Assert.Same(services.AiSettings, field!.GetValue(settingsPage));
        var before = AiSettingsFactory.Current;
        var use = typeof(AiSettingsFactory).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.AiSettings]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, AiSettingsFactory.Current);
    }
}
