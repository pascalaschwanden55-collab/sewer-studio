using System.Reflection;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KnowledgeBaseHealthInspectorDependencyTests
{
    [Fact]
    public void ServiceProvider_UsesInjectedHealthInspectorAndPublishesClearWarning()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var inspector = new RecordingHealthInspector();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory,
            new SettingsQuarantineStore(),
            new SettingsMigrationService(),
            knowledgeBaseHealth: inspector);

        Assert.Equal(1, inspector.Calls);
        Assert.Equal(services.KnowledgeDbPath, inspector.LastPath);
        Assert.Same(inspector, services.KnowledgeBaseHealth);
        Assert.Same(
            inspector,
            services.GetService(typeof(IKnowledgeBaseHealthInspector)));
        Assert.Contains(
            "Test-Korruption",
            services.KnowledgeRootStartupWarning,
            StringComparison.Ordinal);

        var before = KnowledgeBaseHealthChecker.Current;
        var use = typeof(KnowledgeBaseHealthChecker).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [inspector]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, KnowledgeBaseHealthChecker.Current);
    }

    private sealed class RecordingHealthInspector : IKnowledgeBaseHealthInspector
    {
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }

        public KnowledgeBaseHealthInspection Inspect(string dbPath)
        {
            Calls++;
            LastPath = dbPath;
            return new KnowledgeBaseHealthInspection(
                DatabaseExists: true,
                IsHealthy: false,
                Error: "Test-Korruption");
        }
    }
}
