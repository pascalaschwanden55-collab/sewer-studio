using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
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

    [Fact]
    public async Task CompleteSessionAsync_uebernimmt_nur_freigegebene_neue_Events()
    {
        var previousRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", root);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            var configCalls = 0;
            var modelConfig = new OllamaConfig(
                new Uri("http://localhost:11434"),
                "vision-test:1",
                "text-test:2",
                "embed-test:1",
                TimeSpan.FromSeconds(5));
            var service = new CodingSessionService(() =>
                Interlocked.Increment(ref configCalls) == 1 ? modelConfig : null);
            service.StartSession(CreateHaltung("22147-22151", "12.5"), videoPath: null);

            var aiAccepted = service.AddEvent(Entry("BAB", ProtocolEntrySource.Ai));
            aiAccepted.AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BAB",
                Confidence = 0.95,
                QualityGateLevel = "Green",
                Evidence = new CodingEventAiEvidence { KbCodeAgreement = true },
                Decision = CodingUserDecision.Accepted
            };

            var aiRejected = service.AddEvent(Entry("BAF", ProtocolEntrySource.Ai));
            aiRejected.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Rejected };

            var manualOpen = service.AddEvent(Entry("BCA", ProtocolEntrySource.Manual));
            manualOpen.ReviewContext = new CodingEventReviewContext { Decision = CodingUserDecision.Ignored };

            var manualAccepted = service.AddEvent(Entry("BDD", ProtocolEntrySource.Manual));
            manualAccepted.ReviewContext = new CodingEventReviewContext { Decision = CodingUserDecision.Accepted };

            service.AddEvent(Entry("BDA", ProtocolEntrySource.Imported));

            var document = await service.CompleteSessionAsync();
            var codes = document.Current.Entries.Select(e => e.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("BAB", codes);
            Assert.Contains("BDD", codes);
            Assert.Contains("BDA", codes);
            Assert.DoesNotContain("BAF", codes);
            Assert.DoesNotContain("BCA", codes);

            var acceptedEntry = Assert.Single(document.Current.Entries, e => e.Code == "BAB");
            var audit = acceptedEntry.Ai!.CentralDecision!;
            Assert.Equal("EvidenceConfirmed", audit.ReasonCode);
            Assert.Equal(StandardAiDecisionPolicy.PolicyVersion, audit.PolicyVersion);
            Assert.Equal("vision-test:1", audit.VisionModel);
            Assert.Equal("text-test:2", audit.TextModel);
            Assert.Equal("quality-gate-v1", audit.QualityGateVersion);
            Assert.NotNull(aiRejected.AiContext!.CentralDecision);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousRoot);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IndexConfirmedSampleAsync_startet_KB_nur_fuer_persoenliches_Gold_mit_Bild()
    {
        var configCalls = 0;
        var service = new CodingSessionService(
            () =>
            {
                configCalls++;
                return null;
            });
        var personalGold = new TrainingSample
        {
            SampleId = "gold-1",
            Code = "BAB",
            Beschreibung = "BAB - Riss",
            FramePath = @"C:\KI_BRAIN\gold_frames\gold_hash.png",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            MatchLevel = MatchLevelNames.ReviewApproved,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = "Besitzer",
            ConfirmedAtUtc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc)
        };
        var unownedApproval = new TrainingSample
        {
            Status = TrainingSampleStatus.Approved,
            HumanConfirmed = true,
            FramePath = "frame.png"
        };
        var missingFrame = new TrainingSample
        {
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            MatchLevel = MatchLevelNames.ReviewApproved,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = "Besitzer",
            ConfirmedAtUtc = DateTime.UtcNow
        };

        await service.IndexConfirmedSampleAsync(unownedApproval);
        await service.IndexConfirmedSampleAsync(missingFrame);
        Assert.Equal(0, configCalls);

        await service.IndexConfirmedSampleAsync(personalGold);
        Assert.Equal(1, configCalls);
    }

    private static ProtocolEntry Entry(string code, ProtocolEntrySource source)
        => new() { Code = code, Beschreibung = code, Source = source, MeterStart = 1.0 };

    private static HaltungRecord CreateHaltung(string name, string length)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, userEdited: false);
        return record;
    }
}
