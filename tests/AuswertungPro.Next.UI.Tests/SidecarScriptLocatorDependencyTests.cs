using System.Reflection;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SidecarScriptLocatorDependencyTests
{
    [Fact]
    public void Einstellungsseite_verwendet_registrierte_Sidecar_Pfadsuche_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var settingsPage = new SettingsPageViewModel(services);
        var field = typeof(SettingsPageViewModel).GetField(
            "_sidecarScripts",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.SidecarScripts, field!.GetValue(settingsPage));
        Assert.Same(
            services.SidecarScripts,
            services.GetService(typeof(ISidecarScriptLocator)));

        var before = SidecarScriptLocator.Current;
        var use = typeof(SidecarScriptLocator).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.SidecarScripts]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SidecarScriptLocator.Current);
    }
}
