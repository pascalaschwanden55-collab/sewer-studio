using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SidecarTokenResolverDependencyTests
{
    [Fact]
    public void Einstellungsseite_verwendet_registrierte_Token_Aufloesung_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var settingsPage = new SettingsPageViewModel(services);
        var field = typeof(SettingsPageViewModel).GetField(
            "_sidecarTokens",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.SidecarTokens, field!.GetValue(settingsPage));
        Assert.Same(
            services.SidecarTokens,
            services.GetService(typeof(ISidecarTokenResolver)));

        var before = SidecarTokenResolver.Current;
        var use = typeof(SidecarTokenResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.SidecarTokens]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SidecarTokenResolver.Current);
    }
}
