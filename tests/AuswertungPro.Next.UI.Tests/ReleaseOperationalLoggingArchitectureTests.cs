using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ReleaseOperationalLoggingArchitectureTests
{
    [Fact]
    public void App_verbindet_BestEffort_mit_dem_Tageslog()
    {
        var app = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "App.xaml.cs"));

        Assert.Contains("BestEffort.ConfigureDefaultErrorSink", app, StringComparison.Ordinal);
        Assert.Contains("bestEffortLogger.LogWarning", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Produktionscode_nutzt_keinen_Debug_only_Log()
    {
        var srcRoot = TestRepoPaths.RepoFile("src");
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
                && !file.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("Debug.WriteLine", source, StringComparison.Ordinal);
        }
    }
}

[Collection("Release logging global sink")]
public sealed class ReleaseOperationalLoggingIntegrationTests
{
    [Fact]
    public void BestEffort_Warnung_wird_wirklich_in_die_Tageslog_Datei_geschrieben()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sewerstudio-logtest-{Guid.NewGuid():N}");
        var logPath = Path.Combine(tempDir, "app-test.log");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddProvider(new FileLoggerProvider(logPath)));
            var logger = loggerFactory.CreateLogger("BestEffortIntegrationTest");
            BestEffort.ConfigureDefaultErrorSink(
                message => logger.LogWarning("{Message}", message));

            BestEffort.ReportWarning("Trainingsdaten-Testwarnung");

            var log = File.ReadAllText(logPath);
            Assert.Contains("[Warning]", log, StringComparison.Ordinal);
            Assert.Contains("BestEffortIntegrationTest", log, StringComparison.Ordinal);
            Assert.Contains("Trainingsdaten-Testwarnung", log, StringComparison.Ordinal);
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

[CollectionDefinition("Release logging global sink", DisableParallelization = true)]
public sealed class ReleaseLoggingGlobalSinkCollection;
