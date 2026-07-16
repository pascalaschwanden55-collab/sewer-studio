using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolTrainingStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_Protokollspeicher_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.ProtocolTraining,
            services.GetService(typeof(IProtocolTrainingStore)));
        Assert.Null(typeof(ProtocolTrainingStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }

    [Fact]
    public void TrainingCenter_haelt_den_Application_Vertrag()
    {
        var field = typeof(TrainingCenterViewModel).GetField(
            "_protocolTraining",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IProtocolTrainingStore), field!.FieldType);
    }
}
