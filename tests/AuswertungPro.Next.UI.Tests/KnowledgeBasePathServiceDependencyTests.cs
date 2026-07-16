using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KnowledgeBasePathServiceDependencyTests
{
    [Fact]
    public void ServiceProvider_und_kompatible_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.KnowledgePaths, KnowledgeBasePaths.Current);
        Assert.Same(
            services.KnowledgePaths,
            services.GetService(typeof(IKnowledgeBasePathService)));
    }

    [Fact]
    public void Kompatible_Fassade_kann_den_Pfaddienst_nicht_mehr_austauschen()
    {
        var before = KnowledgeBasePaths.Current;
        var replacement = new KnowledgeBasePathService();
        var use = typeof(KnowledgeBasePaths).GetMethod(nameof(KnowledgeBasePaths.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [replacement]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KnowledgeBasePaths.Current);
    }
}
