using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;
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

            var path = Path.Combine(AppDataPathResolver.Resolve(), "data", "protocol_training.json");

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    private static void WithTempAppData(Action body)
    {
        var previous = Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar);
        var temp = Path.Combine(Path.GetTempPath(), "sewer-protocol-training-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, temp);
        try
        {
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, previous);
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
