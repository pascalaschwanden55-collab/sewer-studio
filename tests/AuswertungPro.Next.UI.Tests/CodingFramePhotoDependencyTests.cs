using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Player;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFramePhotoDependencyTests
{
    [Fact]
    public void Player_und_ServiceProvider_verwenden_denselben_Frame_Foto_Dienst()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var playerDependencies = PlayerWindowDependencies.From(services);

        Assert.Same(services.CodingFramePhotos, playerDependencies.CodingFramePhotos);
        Assert.Same(
            services.CodingFramePhotos,
            services.GetService(typeof(ICodingFramePhotoStore)));
    }
}
