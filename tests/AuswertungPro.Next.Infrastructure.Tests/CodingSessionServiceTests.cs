using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class CodingSessionServiceTests
{
    [Fact]
    public async Task CompleteSessionAsync_persistiert_Samples_bevor_es_zurueckkehrt()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", root);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            var service = new CodingSessionService();
            var haltung = CreateHaltung("22147-22151", "12.5");
            service.StartSession(haltung, videoPath: null);
            service.AddEvent(new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = "Riss",
                MeterStart = 1.2,
                FotoPaths = { @"frames\frame-001.png" }
            });

            var document = await service.CompleteSessionAsync();

            Assert.Equal("22147-22151", document.HaltungId);
            Assert.True(File.Exists(KnowledgeBasePaths.GetTrainingSamplesPath()));

            var sample = Assert.Single(await TrainingSamplesStore.LoadAsync());
            Assert.Equal("22147-22151", sample.CaseId);
            Assert.Equal("BAB", sample.Code);
            Assert.Equal(@"frames\frame-001.png", sample.FramePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static HaltungRecord CreateHaltung(string name, string length)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, userEdited: false);
        return record;
    }
}
