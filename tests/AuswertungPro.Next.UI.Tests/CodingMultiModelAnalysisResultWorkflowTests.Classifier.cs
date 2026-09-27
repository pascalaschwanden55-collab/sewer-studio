using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed partial class CodingMultiModelAnalysisResultWorkflowTests
{
    [Theory]
    [InlineData("BAI")]
    [InlineData("BAJ")]
    [InlineData("BBA")]
    public void Bildhinweis_bleibt_nach_Segmentauswertung_sichtbar_ohne_Masken_oder_Ereignisse_umzudeuten(string code)
    {
        string? status = null, detail = null;
        var addCalls = 0;
        var segment = Finding("pipe defect connection pipe bend", MetrierungProximity.Codierbar);
        var frame = ResultWithDetections() with
        {
            ClassifierCode = code, ClassifierConfidence = 0.93,
            Degraded = true, DetectorQualified = false, DegradedReason = "Detektor nicht qualifiziert"
        };
        CodingMultiModelAnalysisResultWorkflow.Execute(new(frame, "Analysiere"), Actions(
            setAiState: (s, color, d, pulse) =>
            {
                if (pulse) return;
                status = s; detail = d;
                Assert.Equal(PlayerStatusColors.Warning, color);
            },
            buildSegmentedFindings: _ => [segment],
            showMultiModelResults: (_, _) => { },
            addFindingsAsEvents: (findings, _, _, _) =>
            {
                addCalls++;
                Assert.Same(segment, Assert.Single(findings));
                Assert.Equal("pipe defect connection pipe bend", segment.Dino!.Label);
            }));

        Assert.StartsWith("Bildhinweis", status);
        Assert.Contains("bitte prüfen", status);
        Assert.Contains("Detektor nicht qualifiziert", detail);
        Assert.Contains("Ungeprüfte Bildklassifikation", detail);
        Assert.Equal(1, addCalls);
        Assert.False(frame.DetectorQualified);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bildhinweis_umgeht_weder_fehlende_Maske_noch_Abstandsregel(bool ahead)
    {
        string? status = null;
        var frame = ResultWithDetections() with { ClassifierCode = "BAI", ClassifierConfidence = 0.98 };
        var result = CodingMultiModelAnalysisResultWorkflow.Execute(new(frame, "Analysiere"), Actions(
            setAiState: (s, _, _, pulse) => { if (!pulse) status = s; },
            buildSegmentedFindings: _ => ahead ? [Finding("seal", MetrierungProximity.Voraus)] : [],
            showMultiModelResults: (_, _) => { }));
        Assert.Equal(ahead ? CodingMultiModelAnalysisResultWorkflowOutcome.AheadOnly
            : CodingMultiModelAnalysisResultWorkflowOutcome.NoSegmentedFindings, result.Outcome);
        Assert.Contains("Bildhinweis", status);
        Assert.Equal(0, result.VisibleFindingCount);
    }

    [Fact]
    public void Technischer_Fehler_wird_nicht_durch_Bildhinweis_verdeckt()
    {
        string? status = null;
        var result = CodingMultiModelAnalysisResultWorkflow.Execute(
            new(SingleFrameResult.Empty("Sidecar down") with
            { ClassifierCode = "BAI", ClassifierConfidence = 0.98 }, "Analysiere"),
            Actions(setAiState: (s, color, _, _) =>
            { status = s; Assert.Equal(PlayerStatusColors.Error, color); }));
        Assert.Equal(CodingMultiModelAnalysisResultWorkflowOutcome.Error, result.Outcome);
        Assert.Equal("Fehler: Sidecar down", status);
    }

    [Fact]
    public void Nicht_lokalisierter_Schadenshinweis_wird_nicht_als_schadenfrei_angezeigt()
    {
        string? status = null;
        Color? color = null;
        var result = CodingMultiModelAnalysisResultWorkflow.Execute(
            new(SingleFrameResult.Empty() with
            {
                ClassifierCode = "BAI", ClassifierConfidence = 0.98,
                DetectorQualified = true
            }, "Analysiere"),
            Actions(
                setAiState: (s, c, _, _) => { status = s; color = c; },
                clearMasks: () => { }));

        Assert.Equal(CodingMultiModelAnalysisResultWorkflowOutcome.ReviewRequired, result.Outcome);
        Assert.Equal(PlayerStatusColors.Warning, color);
        Assert.DoesNotContain("Kein Schaden", status);
        Assert.Contains("Bildhinweis", status);
    }
}
