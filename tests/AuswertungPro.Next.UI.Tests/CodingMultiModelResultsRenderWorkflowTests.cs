using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelResultsRenderWorkflowTests
{
    [Fact]
    public void Execute_clears_masks_and_shows_reference_without_sam_response()
    {
        var calls = new List<string>();

        var result = CodingMultiModelResultsRenderWorkflow.Execute(
            new CodingMultiModelResultsRenderRequest(Result(samResponse: null), Segmented: []),
            Actions(
                calls,
                buildCandidates: _ => throw new InvalidOperationException("Candidates should not be built."),
                setVideoAspect: _ => throw new InvalidOperationException("Aspect should not be set."),
                renderCandidates: (_, _) => throw new InvalidOperationException("Masks should not render.")));

        Assert.Equal(CodingMultiModelResultsRenderWorkflowOutcome.NoSamResponse, result.Outcome);
        Assert.Equal(["clear", "reference"], calls);
        Assert.False(result.RenderedMasks);
    }

    [Fact]
    public void Execute_sets_aspect_before_rendering_visible_candidates()
    {
        var calls = new List<string>();
        IReadOnlyList<SegmentedFinding> segmented = [];
        var samResponse = SamResponse(width: 640, height: 480);
        var candidates = new[] { Candidate() };

        var result = CodingMultiModelResultsRenderWorkflow.Execute(
            new CodingMultiModelResultsRenderRequest(Result(samResponse), segmented),
            Actions(
                calls,
                buildCandidates: items =>
                {
                    calls.Add("build");
                    Assert.Same(segmented, items);
                    return candidates;
                },
                setVideoAspect: aspect => calls.Add($"aspect:{aspect:F3}"),
                renderCandidates: (items, sam) =>
                {
                    calls.Add($"render:{sam.ImageWidth}x{sam.ImageHeight}");
                    Assert.Same(candidates, items);
                    Assert.Same(samResponse, sam);
                }));

        Assert.Equal(CodingMultiModelResultsRenderWorkflowOutcome.RenderedMasks, result.Outcome);
        Assert.True(result.RenderedMasks);
        Assert.Equal(1, result.VisibleMaskCount);
        Assert.Equal(["clear", "aspect:1.333", "build", "render:640x480", "reference"], calls);
    }

    [Fact]
    public void Execute_does_not_render_without_visible_candidates()
    {
        var calls = new List<string>();

        var result = CodingMultiModelResultsRenderWorkflow.Execute(
            new CodingMultiModelResultsRenderRequest(Result(SamResponse(width: 320, height: 240)), Segmented: []),
            Actions(
                calls,
                buildCandidates: _ =>
                {
                    calls.Add("build");
                    return [];
                },
                setVideoAspect: aspect => calls.Add($"aspect:{aspect:F3}"),
                renderCandidates: (_, _) => throw new InvalidOperationException("Masks should not render.")));

        Assert.Equal(CodingMultiModelResultsRenderWorkflowOutcome.NoVisibleMasks, result.Outcome);
        Assert.False(result.RenderedMasks);
        Assert.Equal(0, result.VisibleMaskCount);
        Assert.Equal(["clear", "aspect:1.333", "build", "reference"], calls);
    }

    [Fact]
    public void Execute_skips_aspect_for_invalid_sam_size_but_keeps_render_decision()
    {
        var calls = new List<string>();

        var result = CodingMultiModelResultsRenderWorkflow.Execute(
            new CodingMultiModelResultsRenderRequest(Result(SamResponse(width: 0, height: 240)), Segmented: []),
            Actions(
                calls,
                buildCandidates: _ =>
                {
                    calls.Add("build");
                    return [Candidate()];
                },
                setVideoAspect: _ => throw new InvalidOperationException("Aspect should not be set."),
                renderCandidates: (_, sam) => calls.Add($"render:{sam.ImageWidth}x{sam.ImageHeight}")));

        Assert.Equal(CodingMultiModelResultsRenderWorkflowOutcome.RenderedMasks, result.Outcome);
        Assert.True(result.RenderedMasks);
        Assert.Equal(1, result.VisibleMaskCount);
        Assert.Equal(["clear", "build", "render:0x240", "reference"], calls);
    }

    private static CodingMultiModelResultsRenderActions Actions(
        List<string> calls,
        Func<IReadOnlyList<SegmentedFinding>, IReadOnlyList<SamMaskRenderer.MaskRenderCandidate>> buildCandidates,
        Action<double> setVideoAspect,
        Action<IReadOnlyList<SamMaskRenderer.MaskRenderCandidate>, SamResponse> renderCandidates)
        => new(
            ClearMasks: () => calls.Add("clear"),
            SetVideoAspect: setVideoAspect,
            BuildVisibleMaskRenderCandidates: buildCandidates,
            RenderCandidates: renderCandidates,
            ShowReferenceDn: () => calls.Add("reference"));

    private static SingleFrameResult Result(SamResponse? samResponse)
        => new(
            IsRelevant: true,
            DinoDetections: [],
            SamResponse: samResponse,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null);

    private static SamResponse SamResponse(int width, int height)
        => new(
            Masks: [],
            ImageWidth: width,
            ImageHeight: height,
            InferenceTimeMs: 1);

    private static SamMaskRenderer.MaskRenderCandidate Candidate()
        => new(
            new SamMaskResult(
                Label: "crack",
                Confidence: 0.9,
                Bbox: [0, 0, 10, 10],
                MaskRle: "0",
                MaskAreaPixels: 25,
                ImageAreaPixels: 100,
                HeightPixels: 5,
                WidthPixels: 5,
                CentroidX: 5,
                CentroidY: 5),
            Quant: null,
            DetectionConfidence: null);
}
