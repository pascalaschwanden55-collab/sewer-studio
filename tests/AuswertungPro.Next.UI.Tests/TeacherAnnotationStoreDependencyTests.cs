using System.Reflection;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TeacherAnnotationStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_LehrerAnnotationen_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TeacherAnnotationFileStore>(services.TeacherAnnotations);
        Assert.Same(
            services.TeacherAnnotations,
            services.GetService(typeof(ITeacherAnnotationStore)));

        var before = TeacherAnnotationStore.Current;
        var use = typeof(TeacherAnnotationStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.TeacherAnnotations]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, TeacherAnnotationStore.Current);
    }
}
