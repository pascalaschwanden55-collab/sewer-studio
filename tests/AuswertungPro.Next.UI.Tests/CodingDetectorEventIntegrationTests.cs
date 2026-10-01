using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using CodingReplay;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDetectorEventIntegrationTests
{
    [Fact]
    public async Task Frischer_OSD_Cache_ist_keine_Zeitschaetzung_aber_berechtigt_nicht_zur_Folgebelegersetzung()
    {
        using var run = new Run { DetectionConfidence = .4 };
        run.Apply(await run.Analyze(), 10.13, 115, CodingMeterSource.RecentOsd);
        var before = Assert.Single(run.Session.Events);
        Assert.False(before.Entry.CodeMeta!.Parameters.ContainsKey("vsa.meter.quelle"));
        Assert.Equal("recent_osd", before.Entry.CodeMeta.Parameters["ai.frame.meter_source"]);
        run.DetectionConfidence = .97;
        run.Apply(await run.Analyze(), 10.74, 120, CodingMeterSource.RecentOsd);
        Assert.Equal(10.13, before.MeterAtCapture);
        Assert.Empty(before.AiContext!.PreviousEvidence);
        run.Apply(await run.Analyze(), 10.74, 120, CodingMeterSource.SameFrameOsd);
        Assert.Equal(10.74, before.MeterAtCapture);
        Assert.Single(before.AiContext.PreviousEvidence);
    }

    [Fact]
    public async Task Staerkerer_Folgebeleg_aktualisiert_ID_Meter_Zeit_Bild_und_Herkunft_gemeinsam()
    {
        using var run = new Run { DetectionConfidence = 0.4 };
        run.Apply(await run.Analyze(), 10.13, 115);
        var before = Assert.Single(run.Session.Events);
        var id = before.EventId;
        var entryId = before.Entry.EntryId;
        run.DetectionConfidence = 0.97;
        run.Apply(await run.Analyze(), 10.74, 120);
        var after = Assert.Single(run.Session.Events);
        Assert.Equal(id, after.EventId);
        Assert.Equal(entryId, after.Entry.EntryId);
        Assert.Equal(10.74, after.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(120), after.VideoTimestamp);
        Assert.Equal("frame-120.png", Assert.Single(after.Entry.FotoPaths));
        Assert.Equal(0.97, after.AiContext!.Evidence!.YoloConf);
        var previous = Assert.Single(after.AiContext.PreviousEvidence);
        Assert.Equal(10.13, previous.MeterAtCapture);
        Assert.Equal("frame-115.png", Assert.Single(previous.Entry.FotoPaths));
        Assert.Equal(0.4, previous.AiContext.Evidence!.YoloConf);
        Assert.Empty(previous.AiContext.PreviousEvidence);
    }

    [Fact]
    public async Task Staerkerer_Kandidaten_Folgebeleg_bleibt_ausdruecklich_unqualifiziert_und_ungeprueft()
    {
        using var run = new Run { Qualified = false, DetectionConfidence = 0.4 };
        run.Apply(await run.AnalyzeCandidate(), 10.13, 115);
        run.DetectionConfidence = 0.97;
        var next = await run.AnalyzeCandidate();
        Assert.False(next.DetectorQualified);
        run.Apply(next, 10.74, 120);
        var ev = Assert.Single(run.Session.Events);
        Assert.Single(ev.AiContext!.PreviousEvidence);
        Assert.Equal("Yellow", ev.AiContext.QualityGateLevel);
        Assert.Equal(CodingUserDecision.Ignored, ev.AiContext.Decision);
        Assert.Equal("development_candidate", ev.Entry.CodeMeta!.Parameters["ai.detector.purpose"]);
    }

    [Fact]
    public async Task Technischer_Fehler_im_staerkeren_Beleg_darf_die_alte_Zeile_nicht_ersetzen()
    {
        using var run = new Run { DetectionConfidence = 0.4 };
        run.Apply(await run.Analyze(), 10.13, 115);
        run.DetectionConfidence = 0.97;
        run.DinoDegraded = true;
        run.Apply(await run.Analyze(), 10.74, 120);
        var ev = Assert.Single(run.Session.Events);
        Assert.Empty(ev.AiContext!.PreviousEvidence);
        Assert.Equal(10.13, ev.MeterAtCapture);
    }

    [Fact]
    public async Task Technischer_Kandidatenfehler_bleibt_unqualifiziert_und_erzeugt_keine_Ereigniszeile()
    {
        using var run = new Run { SamFailure = true };
        var raw = await run.AnalyzeCandidate();
        Assert.NotNull(raw.Error);
        Assert.False(raw.DetectorQualified);
        run.Apply(raw, 10);
        Assert.Empty(run.Session.Events);
    }

    [Fact]
    public async Task Expliziter_Entwicklungskandidat_erreicht_Ereignisse_ohne_produktive_Freigabe()
    {
        using var run = new Run { Qualified = false };
        var raw = await run.AnalyzeCandidate();
        Assert.False(raw.DetectorQualified);
        Assert.True(raw.Degraded);
        var origin = Assert.Single(raw.LocalizedDetections!);
        Assert.True(origin.DevelopmentCandidate);
        Assert.True(origin.RequiresReview);
        Assert.Equal(0, run.YoloCalls);
        run.Apply(raw, 10);
        var e = Assert.Single(run.Session.Events);
        Assert.NotEqual("Green", e.AiContext!.QualityGateLevel);
        Assert.Equal("test-candidate", e.AiContext.SuggestedByModelId);
        Assert.Equal("development_candidate", e.Entry.CodeMeta!.Parameters["ai.detector.purpose"]);
        Assert.Equal(new string('a', 64), e.AiContext.SuggestedByModelSha256);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Falscher_Kandidaten_Bild_oder_Gewichtshash_stoppt_vor_jedem_Modellaufruf(bool imageMismatch, bool weightMismatch)
    {
        using var run = new Run { Qualified = false };
        var raw = await run.AnalyzeCandidate(imageMismatch, weightMismatch);
        Assert.NotNull(raw.Error);
        Assert.False(raw.DetectorQualified);
        Assert.Equal(0, run.HealthCalls);
        run.Apply(raw, 10);
        Assert.Empty(run.Session.Events);
    }

    [Fact]
    public async Task Yolo_ohne_Dino_erzeugt_echte_unbestaetigte_Hauptcodezeile_mit_Originalbeleg()
    {
        using var run = new Run();
        var raw = await run.Analyze();
        run.Apply(raw, 10.82);
        var e = Assert.Single(run.Session.Events);
        Assert.Equal("BAI", e.Entry.Code);
        Assert.Equal(10.82, e.Entry.MeterStart);
        Assert.Equal(CodingUserDecision.Ignored, e.AiContext!.Decision);
        Assert.Equal(0.95, e.AiContext.Evidence!.YoloConf);
        Assert.Null(e.AiContext.Evidence.DinoConf);
        Assert.Null(e.AiContext.Evidence.PlausibilityScore);
        Assert.DoesNotContain("DINO", e.AiContext.Reason);
        Assert.Equal(new string('a', 64), e.Entry.CodeMeta!.Parameters["ai.detector.sha256"]);
        Assert.Equal(new string('a', 64), e.AiContext.SuggestedByModelSha256);
        Assert.Equal("Hauptgruppe", e.Entry.CodeMeta.Parameters["ai.code.detail"]);
        Assert.NotNull(e.Overlay);
        Assert.NotNull(e.AiContext.SamMaskRle);
        run.Apply(raw, 10.82);
        Assert.Single(run.Session.Events);
    }

    [Fact]
    public async Task Modellfehler_mit_erhaltener_Yolo_Maske_bleibt_ungeprueft_und_nie_gruen()
    {
        using var run = new Run { DinoDegraded = true };
        var raw = await run.Analyze();
        Assert.True(raw.Degraded);
        run.Apply(raw, 10);
        var e = Assert.Single(run.Session.Events);
        Assert.NotEqual("Green", e.AiContext!.QualityGateLevel);
        Assert.Equal(CodingUserDecision.Ignored, e.AiContext.Decision);
    }

    [Fact]
    public async Task Unqualifizierter_Detektor_erzeugt_keine_Schadenszeile()
    {
        using var run = new Run { Qualified = false };
        var raw = await run.Analyze();
        run.Apply(raw, 10);
        Assert.Empty(run.Session.Events);
        Assert.Equal(0, run.YoloCalls);
        Assert.True(raw.Degraded);
    }

    [Theory]
    [InlineData("BBD_boden")]
    [InlineData("SONST_schaden")]
    [InlineData("BABBA_erfunden")]
    public async Task Allgemeine_oder_unbekannte_Klasse_erfindet_keinen_Untercode(string label)
    {
        using var run = new Run { Label = label };
        var raw = await run.Analyze();
        run.Apply(raw, 10);
        Assert.Empty(run.Session.Events);
        Assert.True(raw.Degraded);
    }

    [Fact]
    public async Task Offene_Wurzelstrecke_behaelt_Qualitaetsampel_und_Modellherkunft()
    {
        using var run = new Run { Label = "BBA_wurzeln" };
        var raw = await run.Analyze();
        run.Apply(raw, 10);
        run.Apply(raw, 11.5);
        var e = Assert.Single(run.Session.Events);
        Assert.Equal("BBA", e.Entry.Code);
        Assert.True(e.Entry.IsStreckenschaden);
        Assert.NotNull(e.AiContext!.QualityGateLevel);
        Assert.Equal(0.95, e.AiContext.Evidence!.YoloConf);
        Assert.Null(e.AiContext.Evidence.DinoConf);
        Assert.Equal("YOLO", e.Entry.CodeMeta!.Parameters["ai.detector.source"]);
    }

    [Theory]
    [InlineData("touched")]
    [InlineData("accepted")]
    [InlineData("review")]
    [InlineData("overlay")]
    [InlineData("deleted")]
    [InlineData("photo_command")]
    [InlineData("edit_command")]
    public async Task Neue_Trackerzeile_mit_menschlicher_Aenderung_im_selben_Tick_behaelt_ihren_Beleg(string change)
    {
        using var run = new Run { Label = "BBA_wurzeln", OnAdded = e =>
        {
            switch (change)
            {
                case "touched": e.AiContext = new() { HumanTouchedAtUtc = DateTimeOffset.UnixEpoch, Reason = "Mensch" }; break;
                case "accepted": e.AiContext = new() { Decision = CodingUserDecision.Accepted, Reason = "Mensch" }; break;
                case "review": e.ReviewContext = new() { Decision = CodingUserDecision.AcceptedWithEdit }; break;
                case "overlay": e.Overlay = new() { ToolType = OverlayToolType.Point, Points = [new(.2, .3)] }; break;
                case "deleted": e.Entry.IsDeleted = true; break;
                case "photo_command": CodingEventPhotoApplier.Apply(e, "manual.png", null); break;
                case "edit_command": CodingEventEditApplier.Apply(e, null); break;
            }
        } };
        run.Apply(await run.Analyze(), 10);
        var ev = Assert.Single(run.Session.Events);
        Assert.Null(ev.AiContext!.Evidence);
        Assert.Null(ev.AiContext.SuggestedByModelSha256);
        if (change is "touched" or "accepted") Assert.Equal("Mensch", ev.AiContext.Reason);
        if (change == "touched") Assert.Equal(DateTimeOffset.UnixEpoch, ev.AiContext.HumanTouchedAtUtc);
        if (change == "accepted") Assert.Equal(CodingUserDecision.Accepted, ev.AiContext.Decision);
        if (change == "review") Assert.Equal(CodingUserDecision.AcceptedWithEdit, ev.ReviewContext!.Decision);
        if (change == "overlay") Assert.Equal(OverlayToolType.Point, ev.Overlay!.ToolType);
        if (change == "deleted") Assert.True(ev.Entry.IsDeleted);
        if (change is "photo_command" or "edit_command") Assert.NotNull(ev.AiContext.HumanTouchedAtUtc);
        if (change == "photo_command") Assert.Contains("manual.png", ev.Entry.FotoPaths);
    }

    [Fact]
    public async Task Tracker_Zusatzwerte_bleiben_beim_Ergaenzen_der_Modellherkunft_erhalten()
    {
        using var run = new Run { Label = "BBA_wurzeln", WithExistingStretchMetadata = true };
        run.Apply(await run.Analyze(), 10);
        var e = Assert.Single(run.Session.Events);
        Assert.Equal("erhalten", e.Entry.CodeMeta!.Parameters["vorhandener-wert"]);
        Assert.Equal("YOLO", e.Entry.CodeMeta.Parameters["ai.detector.source"]);
        Assert.Equal(new string('a', 64), e.AiContext!.SuggestedByModelSha256);
    }

    [Fact]
    public async Task Quellenfremde_Sam_Box_wird_nicht_nach_Maskenlabel_protokolliert()
    {
        using var run = new Run();
        var raw = await run.Analyze();
        var mask = raw.SamResponse!.Masks[0] with { Bbox = new double[] { 0, 0, 10, 10 } };
        run.Apply(raw with { SamResponse = raw.SamResponse with { Masks = [mask] } }, 10);
        Assert.Empty(run.Session.Events);
    }

    private sealed class Run : IVisionPipelineClient, IDisposable
    {
        private readonly ICodeCatalogProvider? _previous = VsaCodeResolver.CurrentCatalog;
        private readonly CodingStreckenschadenTrackerOwner _tracker = new();
        public CodingSessionService Session { get; } = new(trainingSamples: new ReplayClosedTrainingStore());
        public bool Qualified { get; init; } = true;
        public bool DinoDegraded { get; set; }
        public double DetectionConfidence { get; set; } = 0.95;
        public bool SamFailure { get; init; }
        public string Label { get; init; } = "BAI_dichtung";
        public bool WithExistingStretchMetadata { get; init; }
        public Action<CodingEvent>? OnAdded { get; init; }
        public int YoloCalls { get; private set; }
        public int HealthCalls { get; private set; }
        public Run()
        {
            VsaCodeResolver.ConfigureCatalog(new Catalog());
            var holding = new HaltungRecord();
            holding.SetFieldValue("Haltungslaenge_m", "50", FieldSource.Manual, false);
            Session.StartSession(holding, null);
            Session.EventAdded += (_, e) =>
            {
                OnAdded?.Invoke(e);
                if (!WithExistingStretchMetadata || !e.Entry.IsStreckenschaden) return;
                e.Entry.CodeMeta = new ProtocolEntryCodeMeta { Code = e.Entry.Code };
                e.Entry.CodeMeta.Parameters["vorhandener-wert"] = "erhalten";
            };
        }
        public Task<SingleFrameResult> Analyze() => new SingleFrameMultiModelService(this)
            .AnalyzeFrameAsync(Image(), 300, currentMeterM: 10, reachLengthM: 50);
        public Task<SingleFrameResult> AnalyzeCandidate(bool imageMismatch = false, bool weightMismatch = false)
        {
            var image = Image();
            var candidate = new CodingDetectorCandidateFrame("test-candidate", new string('a', 64),
                new string(weightMismatch ? 'b' : 'a', 64),
                imageMismatch ? new string('b', 64) : Convert.ToHexString(SHA256.HashData(image)),
                [new(70, 40, 100, 60, Label, DetectionConfidence)], 3);
            return new SingleFrameMultiModelService(this).AnalyzeCandidateFrameAsync(image, 300, candidate,
                currentMeterM: 10, reachLengthM: 50);
        }
        public void Apply(SingleFrameResult result, double meter, double seconds = 120,
            CodingMeterSource source = CodingMeterSource.SameFrameOsd)
        {
            var analyzedBytes = Image();
            var frame = CodingAnalyzedFrameEvidence.FromResolution(analyzedBytes, TimeSpan.FromSeconds(seconds),
                new(meter, source is CodingMeterSource.SameFrameOsd or CodingMeterSource.RecentOsd) { Source = source });
            var context = CodingAnalysisContext.CreateDefault(() => Session.Events, () => Session.Events,
                () => [], () => null, () => 1, _ => false);
            CodingMultiModelAnalysisResultWorkflow.Execute(new(result, "test"), new(
                (_, _, _, _) => { }, () => { }, context.BuildSegmentedFindings, (_, _) => { },
                (items, w, h, yolo) => CodingMultiModelFindingEventCommandWorkflow.ExecuteAnalyzedFrame(
                    new(true, items, w, h, yolo, seconds, meter, Session, Session.Events,
                        new QualityGateService(), false, null, null, TimeSpan.FromSeconds(30), TimeSpan.Zero),
                    frame,
                    new((_, _) => meter,
                        (segments, m, time) => CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
                            new(segments, m, time, true, true),
                            new((s, at) => CodingStreckenschadenObservationBuilder.Build(s, at, (_, _) => "BAIZ"),
                                _tracker.Update,
                                (actions, at) => CodingStreckenschadenActionApplier.Apply(actions, Session.Events, Session,
                                    at, VsaCodeResolver.LookupLabel, _ => { }), () => { })).ConsumedSegments,
                        (_, _) => "BAIZ", VsaCodeResolver.LookupLabel,
                        (entry, bytes) =>
                        {
                            Assert.Same(analyzedBytes, bytes);
                            frame.WriteAnalysisMetadata(entry);
                            entry.FotoPaths.Add($"frame-{seconds}.png");
                        }, _ => { }, () => { }, () => { }))));
        }
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
        {
            HealthCalls++;
            return Task.FromResult<SidecarHealthResponse?>(new("ok", "test", null, DetectorQualification:
                new(Qualified, null, new(new string('a', 64)))));
        }
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) =>
            Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
        {
            YoloCalls++;
            return Task.FromResult(new YoloResponse(true, [new(70, 40, 100, 60, Label, DetectionConfidence)], "defect", 1,
                DetectorQualified: true, DetectorArtifactSha256: new string('a', 64)));
        }
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default) =>
            Task.FromResult(new YoloClassifyResponse([], 1));
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) =>
            Task.FromResult(new DinoResponse([], 1, DinoDegraded, DinoDegraded ? "Modellfehler" : null));
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) =>
            SamFailure ? throw new InvalidOperationException("SAM-Testfehler") :
            Task.FromResult(new SamResponse([new(Label, 0.95, [70, 40, 100, 60], "0,10000", 200, 10000, 20, 30, 85, 50)], 100, 100, 1));
        public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previous);
    }

    private static byte[] Image()
    {
        var bitmap = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Gray8, null, new byte[10000], 100);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
    private sealed class Catalog : ICodeCatalogProvider
    {
        private readonly CodeDefinition[] _codes = [new() { Code = "BAI", Title = "Einragendes Dichtungsmaterial", IsSelectable = true },
            new() { Code = "BBA", Title = "Wurzeln", IsSelectable = true }];
        public IReadOnlyList<CodeDefinition> GetAll() => _codes;
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => c.Code == code)!; return def is not null;
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new InvalidOperationException();
        public IReadOnlyList<string> AllowedCodes() => _codes.Select(c => c.Code).ToArray();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
