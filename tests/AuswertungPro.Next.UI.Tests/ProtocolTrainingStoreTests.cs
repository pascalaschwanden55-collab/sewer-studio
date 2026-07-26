using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

[Collection("EnvironmentVars")]
public sealed class ProtocolTrainingStoreTests
{
    [Fact]
    public void AddSample_IsAtomic_AndKeepsBackup()
    {
        WithTempAppData(() =>
        {
            ProtocolTrainingStore.AddSample(
                new ProtocolEntry
                {
                    Code = "BAB",
                    Beschreibung = "Laengsriss sichtbar",
                    MeterStart = 1.2
                },
                "H-001");

            ProtocolTrainingStore.AddSample(
                new ProtocolEntry
                {
                    Code = "BBA",
                    Beschreibung = "Ablagerung sichtbar",
                    MeterStart = 2.4
                },
                "H-001");

            var path = Path.Combine(KnowledgeBasePaths.GetRoot(), "protocol_training.json");

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    [Fact]
    public void AddSample_SkipsSameHoldingCodeAndRoundedMeter()
    {
        WithTempAppData(() =>
        {
            ProtocolTrainingStore.AddSample(
                new ProtocolEntry { Code = "BAB", MeterStart = 1.23 },
                "H-001");

            ProtocolTrainingStore.AddSample(
                new ProtocolEntry { Code = "BAB", MeterStart = 1.24 },
                "H-001");

            var samples = ProtocolTrainingStore.LoadRecent(10);

            Assert.Single(samples);
            Assert.Equal(1.23, samples[0].MeterStart);
        });
    }

    [Fact]
    public void LoadRecent_ReturnsNewestSamplesAndHonorsLimit()
    {
        WithTempAppData(() =>
        {
            var path = Path.Combine(KnowledgeBasePaths.GetRoot(), "protocol_training.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Samples = new[]
                {
                    new { AtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), HaltungId = "H-ALT", Code = "BAB" },
                    new { AtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), HaltungId = "H-NEU", Code = "BBA" }
                }
            }));

            var samples = ProtocolTrainingStore.LoadRecent(1);

            var sample = Assert.Single(samples);
            Assert.Equal("H-NEU", sample.HaltungId);
        });
    }

    [Fact]
    public void LoadRecent_ReturnsEmptyListForCorruptFile()
    {
        WithTempAppData(() =>
        {
            var path = Path.Combine(KnowledgeBasePaths.GetRoot(), "protocol_training.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ keine gueltige JSON-Datei");

            var samples = ProtocolTrainingStore.LoadRecent(10);

            Assert.Empty(samples);
        });
    }

    private static void WithTempAppData(Action body)
    {
        var previousAppData = Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar);
        var previousKnowledge = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var temp = Path.Combine(Path.GetTempPath(), "sewer-protocol-training-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, temp);
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, temp);
        KnowledgeBasePaths.InvalidateCache();
        try
        {
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, previousAppData);
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousKnowledge);
            KnowledgeBasePaths.InvalidateCache();
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
