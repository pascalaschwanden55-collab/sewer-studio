using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDefectPreviewDependencyTests
{
    [Fact]
    public void Player_bekommt_den_Vorschaudienst_direkt_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsAssignableFrom<ICodingDefectPreviewRenderer>(services.CodingDefectPreviews);
        Assert.Same(
            services.CodingDefectPreviews,
            PlayerWindowDependencies.From(services).CodingDefectPreviews);
        Assert.Same(
            services.CodingDefectPreviews,
            services.GetService(typeof(ICodingDefectPreviewRenderer)));
        Assert.Null(typeof(CodingDefectPreviewService).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }
}
