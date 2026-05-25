using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleGeneratorCodeMetaTests
{
    [Fact]
    public void ToProtocolEntry_CopiesGroundTruthMetadataToCodeMeta()
    {
        var groundTruth = new GroundTruthEntry
        {
            MeterStart = 12.3,
            MeterEnd = 12.3,
            VsaCode = "BAB",
            Text = "Riss 3 mm",
            Characterization = "A",
            ClockPosition = "3",
            ConnectionClock = "9",
            Quantification = new QuantificationDetail
            {
                Value = 3,
                Unit = "mm",
                Type = "Spaltbreite",
                ClockPosition = "4"
            },
            Severity = "4",
            IsStreckenschaden = false,
            Zeit = TimeSpan.FromSeconds(7)
        };

        var entry = GroundTruthProtocolEntryMapper.ToProtocolEntry(groundTruth);

        Assert.Equal("BAB", entry.Code);
        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("BAB", entry.CodeMeta!.Code);
        Assert.Equal("3 mm", entry.CodeMeta.Parameters["vsa.q1"]);
        Assert.Equal("3 mm", entry.CodeMeta.Parameters["Quantifizierung1"]);
        Assert.Equal("3", entry.CodeMeta.Parameters["vsa.uhr.von"]);
        Assert.Equal("3", entry.CodeMeta.Parameters["ClockPos1"]);
        Assert.Equal("9", entry.CodeMeta.Parameters["vsa.anschluss.uhr"]);
        Assert.Equal("9", entry.CodeMeta.Parameters["ConnectionClock"]);
        Assert.Equal("A", entry.CodeMeta.Parameters["vsa.charakterisierung"]);
        Assert.Equal("A", entry.CodeMeta.Parameters["Char1"]);
        Assert.Equal("Spaltbreite", entry.CodeMeta.Parameters["vsa.q1.type"]);
        Assert.Equal("4", entry.CodeMeta.Parameters["vsa.q1.uhr"]);
        Assert.Equal("4", entry.CodeMeta.Severity);
    }

    [Fact]
    public void PdfProtocolExtractor_BuildEntry_RettetUhrlageUndSchadensstufeInCodeMeta()
    {
        var method = typeof(PdfProtocolExtractor).GetMethod(
            "BuildEntry",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var entry = (GroundTruthEntry?)method!.Invoke(
            null,
            [
                "12.30",
                "",
                "BAB",
                "A",
                "Riss bei 3 Uhr Schadensstufe 4, Spaltbreite 3 mm",
                TimeSpan.FromSeconds(12)
            ]);

        Assert.NotNull(entry);
        Assert.Equal("3", entry!.ClockPosition);
        Assert.Equal("4", entry.Severity);
        Assert.NotNull(entry.Quantification);
        Assert.Equal("3", entry.Quantification!.ClockPosition);

        var protocolEntry = GroundTruthProtocolEntryMapper.ToProtocolEntry(entry);

        Assert.NotNull(protocolEntry.CodeMeta);
        Assert.Equal("3", protocolEntry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("3", protocolEntry.CodeMeta.Parameters["ClockPos1"]);
        Assert.Equal("3", protocolEntry.CodeMeta.Parameters["vsa.q1.uhr"]);
        Assert.Equal("4", protocolEntry.CodeMeta.Severity);
    }

    [Fact]
    public async Task GenerateWithDiagnosticsAsync_CopiesProtocolCodeMetaToTrainingSample()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var protocolPath = Path.Combine(tempDir, "protocol.json");
            var doc = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    [
                        new ProtocolEntry
                        {
                            Code = "BAB",
                            Beschreibung = "Riss 3 mm",
                            MeterStart = 12.3,
                            MeterEnd = 12.3,
                            Source = ProtocolEntrySource.Imported,
                            CodeMeta = new ProtocolEntryCodeMeta
                            {
                                Code = "BAB",
                                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["vsa.q1"] = "3 mm",
                                    ["vsa.uhr.von"] = "3",
                                    ["Char1"] = "A"
                                }
                            }
                        }
                    ]
                }
            };
            await File.WriteAllTextAsync(protocolPath, JsonSerializer.Serialize(doc));

            var cfg = new AiRuntimeSettings(
                Enabled: false,
                OllamaBaseUri: new Uri("http://localhost:11434"),
                VisionModel: "",
                TextModel: "",
                EmbedModel: null,
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromMinutes(5),
                OllamaKeepAlive: "24h",
                OllamaNumCtx: 8192);
            var generator = new TrainingSampleGenerator(
                cfg,
                new MeterTimelineService(cfg),
                new TrainingCenterSettings());

            var result = await generator.GenerateWithDiagnosticsAsync(new TrainingCaseInput(
                CaseId: "H-1",
                FolderPath: tempDir,
                VideoPath: Path.Combine(tempDir, "missing.mp4"),
                ProtocolPath: protocolPath));

            var sample = Assert.Single(result.Samples);
            Assert.NotNull(sample.CodeMeta);
            Assert.Equal("BAB", sample.CodeMeta!.Code);
            Assert.Equal("3 mm", sample.CodeMeta.Parameters["vsa.q1"]);
            Assert.Equal("3", sample.CodeMeta.Parameters["vsa.uhr.von"]);
            Assert.Equal("A", sample.CodeMeta.Parameters["Char1"]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
