using System.Net.Http;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolDataIntegrityTests
{
    [Fact]
    public void CodingSessionCompleteSession_creates_independent_original_and_current()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "5.0", FieldSource.Manual, userEdited: false);

        var service = new CodingSessionService();
        service.StartSession(record, videoPath: null);

        var document = service.CompleteSession();

        AssertIndependentRevisions(document);
        Assert.NotEmpty(document.Current.Entries);

        var originalCode = document.Original.Entries[0].Code;
        document.Current.Entries[0].Code = "CHANGED";

        Assert.Equal(originalCode, document.Original.Entries[0].Code);
    }

    [Fact]
    public async Task FullProtocolGeneration_empty_detections_creates_independent_original_and_current()
    {
        using var service = new FullProtocolGenerationService(
            CreateRuntimeSettings(),
            new PassThroughPlausibility(),
            new HttpClient(),
            new EmptyRetrievalService());

        var result = await service.GenerateFromDetectionsAsync(
            Array.Empty<RawVideoDetection>(),
            new FullProtocolGenerationRequest("H-01", "video.mp4", new[] { "BCD" }));

        Assert.True(result.IsSuccess, result.Error);
        var document = Assert.IsType<ProtocolDocument>(result.Document);
        AssertIndependentRevisions(document);

        document.Current.Entries.Add(new ProtocolEntry { Code = "BAB" });

        Assert.Empty(document.Original.Entries);
    }

    [Fact]
    public async Task FullProtocolGeneration_LlmFailure_StillSetsQualityGate_Red()
    {
        // Audit R3: Bei LLM-Fehler darf ein Befund NICHT ohne QualityGate-Status entstehen
        // (sonst zeigt die UI stilles "Gelb"). Ohne Evidenz liefert der Gate korrekt Rot.
        using var service = new FullProtocolGenerationService(
            CreateRuntimeSettings(),
            new PassThroughPlausibility(),
            new HttpClient(new FailingHandler()),
            new EmptyRetrievalService());

        var detection = new RawVideoDetection(
            FindingLabel: "Riss",
            MeterStart: 1.2,
            MeterEnd: 1.2,
            Severity: "high",
            VsaCodeHint: "BAB",
            PositionClock: "3",
            ExtentPercent: 20,
            HeightMm: 7,
            WidthMm: 10,
            IntrusionPercent: 15,
            CrossSectionReductionPercent: 30,
            DiameterReductionMm: 8,
            MeterSource: "LinearEstimate",
            IsMeterEstimated: true);

        var result = await service.GenerateFromDetectionsAsync(
            new[] { detection },
            new FullProtocolGenerationRequest("H-01", "video.mp4", new[] { "BAB" }));

        var entry = Assert.Single(result.MappedEntries);
        Assert.NotNull(entry.QualityGateResult);       // nie null trotz LLM-Fehler
        Assert.True(entry.QualityGateResult!.IsRed);    // ohne Evidenz: Rot, nicht Pseudo-Gelb
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom")
            });
    }

    [Fact]
    public void ProtocolReplacement_archives_existing_current_before_reanalysis_replace()
    {
        var existing = new ProtocolDocument
        {
            HaltungId = "H-01",
            Original = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "BCD", Source = ProtocolEntrySource.Imported } }
            },
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "BAB", Source = ProtocolEntrySource.Manual } }
            }
        };
        var incoming = new ProtocolDocument
        {
            HaltungId = "H-01",
            Original = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "BAA", Source = ProtocolEntrySource.Ai } }
            },
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "BAA", Source = ProtocolEntrySource.Ai } }
            }
        };

        var prepared = ProtocolReplacementService.PrepareReplacement(
            existing,
            incoming,
            user: "test",
            archiveComment: "Archiv vor KI-Reanalyse");

        AssertIndependentRevisions(prepared);
        Assert.Single(prepared.History);
        Assert.Equal("BAB", prepared.History[0].Entries.Single().Code);

        prepared.Current.Entries[0].Code = "CHANGED";

        Assert.Equal("BAA", prepared.Original.Entries.Single().Code);
        Assert.Equal("BAB", prepared.History[0].Entries.Single().Code);
    }

    [Fact]
    public void FullProtocolGeneration_maps_detection_metadata_to_code_meta_and_ai_meter_meta()
    {
        var detection = new RawVideoDetection(
            FindingLabel: "Riss mit Einragung",
            MeterStart: 1.2,
            MeterEnd: 1.2,
            Severity: "high",
            VsaCodeHint: "BAB",
            PositionClock: "3",
            ExtentPercent: 20,
            HeightMm: 7,
            WidthMm: 10,
            IntrusionPercent: 15,
            CrossSectionReductionPercent: 30,
            DiameterReductionMm: 8,
            MeterSource: "LinearEstimate",
            IsMeterEstimated: true);

        var mapped = new MappedProtocolEntry(
            detection,
            SuggestedCode: "BAB",
            Confidence: 0.82,
            Reason: "test",
            Warnings: Array.Empty<string>());

        var entry = InvokeBuildProtocolEntry(mapped);

        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("BAB", entry.CodeMeta!.Code);
        Assert.Equal("high", entry.CodeMeta.Severity);
        Assert.Equal("3", entry.CodeMeta.Parameters["vsa.uhr.von"]);
        Assert.Equal("7 mm", entry.CodeMeta.Parameters["vsa.q1"]);
        Assert.Equal("10 mm", entry.CodeMeta.Parameters["vsa.q2"]);
        Assert.Equal("15", entry.CodeMeta.Parameters["vsa.einragung.prozent"]);
        Assert.Equal("30", entry.CodeMeta.Parameters["vsa.querschnitt.prozent"]);
        Assert.Equal("8 mm", entry.CodeMeta.Parameters["vsa.dn.reduktion"]);
        Assert.NotNull(entry.Ai);
        Assert.Equal("LinearEstimate", entry.Ai!.MeterSource);
        Assert.True(entry.Ai.IsMeterEstimated);
    }

    [Fact]
    public void MergeEngine_does_not_overwrite_user_edited_field_and_logs_conflict()
    {
        var target = new HaltungRecord();
        target.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
        target.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);

        var source = new HaltungRecord();
        source.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
        source.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);

        var log = new ImportRunLog();
        var ctx = new ImportRunContext(CancellationToken.None, null, log, dryRun: false);

        var result = MergeEngine.MergeRecord(target, source, FieldSource.Xtf, ctx: ctx);

        Assert.Equal("PVC", target.GetFieldValue("Rohrmaterial"));
        Assert.Equal(1, result.Conflicts);
        Assert.Contains(log.EntriesList, e =>
            e.Status == ImportLogStatus.Conflict &&
            e.Field == "Rohrmaterial" &&
            e.Detail?.Contains("UserEdited", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void HaltungRecord_does_not_replace_user_edited_value_with_import_value()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);

        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);

        Assert.Equal("PVC", record.GetFieldValue("Rohrmaterial"));
        Assert.True(record.FieldMeta["Rohrmaterial"].UserEdited);
        Assert.Equal(FieldSource.Manual, record.FieldMeta["Rohrmaterial"].Source);
    }

    [Fact]
    public void JsonProjectRepository_roundtrips_field_meta_vsa_findings_and_independent_protocol_revisions()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "project.json");

        try
        {
            Directory.CreateDirectory(root);

            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
            record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
            record.VsaFindings.Add(new VsaFinding
            {
                KanalSchadencode = "BAB",
                MeterStart = 1.2,
                MeterEnd = 1.4,
                Quantifizierung1 = "7 mm"
            });
            record.Protocol = new ProtocolDocument
            {
                HaltungId = "H-01",
                Original = new ProtocolRevision
                {
                    Entries = { new ProtocolEntry { Code = "BCD", Beschreibung = "Original" } }
                },
                Current = new ProtocolRevision
                {
                    Entries = { new ProtocolEntry { Code = "BAB", Beschreibung = "Current" } }
                }
            };
            project.Data.Add(record);

            var repo = new JsonProjectRepository();
            var save = repo.Save(project, path);
            Assert.True(save.Ok, save.ErrorMessage);

            var load = repo.Load(path);
            Assert.True(load.Ok, load.ErrorMessage);
            var loaded = Assert.Single(load.Value!.Data);

            Assert.Equal("PVC", loaded.GetFieldValue("Rohrmaterial"));
            Assert.True(loaded.FieldMeta["Rohrmaterial"].UserEdited);
            var finding = Assert.Single(loaded.VsaFindings);
            Assert.Equal("BAB", finding.KanalSchadencode);
            Assert.Equal(1.2, finding.MeterStart);

            Assert.NotNull(loaded.Protocol);
            AssertIndependentRevisions(loaded.Protocol!);
            loaded.Protocol!.Current.Entries[0].Code = "CHANGED";
            Assert.Equal("BCD", loaded.Protocol.Original.Entries[0].Code);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProtocolPdfExporter_marks_estimated_meter_with_ca_prefix()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            MeterStart = 1.2,
            Source = ProtocolEntrySource.Ai,
            Ai = new ProtocolEntryAiMeta
            {
                MeterSource = "LinearEstimate",
                IsMeterEstimated = true
            }
        };

        var text = ProtocolPdfObservationText.BuildObservationMeterStartText(entry);

        Assert.Equal("ca. 1.20", text);
    }

    private static ProtocolEntry InvokeBuildProtocolEntry(MappedProtocolEntry mapped)
    {
        var method = typeof(FullProtocolGenerationService)
            .GetMethod("BuildProtocolEntry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<ProtocolEntry>(method!.Invoke(null, new object[] { mapped }));
    }

    private static void AssertIndependentRevisions(ProtocolDocument document)
    {
        Assert.NotSame(document.Original, document.Current);
        Assert.NotSame(document.Original.Entries, document.Current.Entries);
    }

    private static AiRuntimeSettings CreateRuntimeSettings()
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: null,
            FfmpegPath: null,
            OllamaRequestTimeout: TimeSpan.FromSeconds(1),
            OllamaKeepAlive: "1m",
            OllamaNumCtx: 2048);

    private sealed class PassThroughPlausibility : IAiSuggestionPlausibilityService
    {
        public AiSuggestionResult ApplyChecks(AiSuggestionResult suggestion, ObservationContext context)
            => suggestion;
    }

    private sealed class EmptyRetrievalService : IRetrievalService
    {
        public bool CheckModelConsistency() => true;
        public string? StoredEmbedModel => null;
        public bool HasModelMismatch => false;

        public Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
            string queryText,
            int topK = 5,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RetrievalResult>>(Array.Empty<RetrievalResult>());
    }
}
