using System.Reflection;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Settings;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FolderOpenDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_Ordnerdienst_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.FolderOpen,
            services.GetService(typeof(IFolderOpenService)));
        Assert.Null(typeof(SettingsPathWorkflow).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }
}
