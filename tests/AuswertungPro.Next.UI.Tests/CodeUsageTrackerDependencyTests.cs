using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodeUsageTrackerDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Player_verwenden_denselben_CodeNutzungszaehler()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var dependencies = PlayerWindowDependencies.From(services);

        Assert.Same(services.CodeUsage, dependencies.CodeUsage);
        Assert.Same(
            services.CodeUsage,
            services.GetService(typeof(ICodeUsageTracker)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_den_Zaehler_nicht_mehr_global_austauschen()
    {
        var before = CodeUsageTrackers.Current;
        var property = typeof(CodeUsageTrackers).GetProperty(nameof(CodeUsageTrackers.Current));

        Assert.NotNull(property);
        var error = Assert.Throws<TargetInvocationException>(
            () => property.SetValue(null, new RecordingCodeUsageTracker()));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, CodeUsageTrackers.Current);
    }

    private sealed class RecordingCodeUsageTracker : ICodeUsageTracker
    {
        public void Erfasse(string? code)
        {
        }

        public IReadOnlyList<CodeUsageEintrag> TopCodes(int n) => [];

        public IReadOnlyList<string> Zuletzt(int n) => [];
    }
}
