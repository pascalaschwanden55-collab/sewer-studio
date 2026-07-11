using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AiFieldQualityReportTests
{
    [Fact]
    public void Analyze_ZaehltDeduplizierteBefunde_Misses_MeterfehlerUndSchatten()
    {
        var samples = new List<TrainingSample>
        {
            Ai("a1", "100-200", 1.0, "BAB", "BAB", true, green: true),
            Ai("a1-duplicate", "100-200", 1.1, "BAB", "BAB", true, green: true),
            Ai("a2", "101-201", 5.0, "BAB", "BAF", true, green: true, corrected: true),
            Ai("a3", "102-202", 7.0, "BBA", "BBA", false, green: false),
            Ai("a4", "103-203", 20.8, "BAB", "BAB", null, green: false, meterSource: "LinearEstimate"),
            Manual("m1", "100-200", 1.1, "BAB"),
            Manual("m2", "103-203", 20.0, "BAB"),
            Manual("m3", "104-204", 10.0, "BBA")
        };
        var metadata = new[]
        {
            new FieldQualityCaseMetadata("100-200", 200, "PVC", "good"),
            new FieldQualityCaseMetadata("101-201", 300, "Beton", "limited"),
            new FieldQualityCaseMetadata("102-202", 500, "Steinzeug", "poor")
        };
        var shadow = new[]
        {
            new ShadowQualityInput("100-200", "2", "Roboter", "10000", "2", "Roboter", 10500, false, "qwen"),
            new ShadowQualityInput("101-201", "2", "Roboter", "10000", "4", "Erneuern", 20000, true, "qwen")
        };

        var report = AiFieldQualityReportAnalyzer.Analyze(samples, metadata, shadow);

        Assert.Equal(5, report.Detection.RawAiSamples);
        Assert.Equal(4, report.Detection.DeduplicatedAiFindings);
        Assert.Equal(3, report.Detection.ReviewedAiFindings);
        Assert.Equal(1, report.Detection.ExactCodeCorrect);
        Assert.Equal(1, report.Detection.WrongCodeFamily);
        Assert.Equal(1, report.Detection.RejectedFalsePositive);
        Assert.Equal(3, report.Detection.ManualDamageFindings);
        Assert.Equal(1, report.Detection.ManualFindingsMatched);
        Assert.Equal(2, report.Detection.PossibleMisses);
        Assert.Equal(1, report.Detection.PossibleMeterMismatches);
        Assert.Equal(1.0 / 3.0, report.Detection.DetectionRecall, precision: 5);

        Assert.Equal(2, report.GreenRelease.DeduplicatedGreenFindings);
        Assert.Equal(2, report.GreenRelease.ReviewedGreenFindings);
        Assert.Equal(1, report.GreenRelease.GreenErrors);
        Assert.False(report.GreenRelease.ReleaseCriterionMet);

        Assert.Equal(1, report.Shadow.Comparable);
        Assert.Equal(1, report.Shadow.Equal);
        Assert.Equal(0, report.Shadow.StrongDifference);
        Assert.Equal(1, report.Shadow.NoComparison);
        Assert.Equal(1, report.Shadow.Stale);
        Assert.Contains(report.Issues, issue => issue.Category == "possible_miss" && issue.CaseId == "104-204");
        Assert.Contains(report.Issues, issue => issue.Category == "possible_meter_mismatch" && issue.CaseId == "103-203");
        Assert.Contains(report.Issues, issue => issue.Category == "green_decision_error" && issue.SampleId == "a2");
    }

    [Fact]
    public void BinomialUpper95_BelegtStatistischeGrenzenFuer300Faelle()
    {
        var zeroErrors = AiFieldQualityReportAnalyzer.BinomialUpper95(300, 0);
        var oneError = AiFieldQualityReportAnalyzer.BinomialUpper95(300, 1);

        Assert.InRange(zeroErrors, 0.0098, 0.0101);
        Assert.InRange(oneError, 0.0155, 0.0160);
    }

    [Fact]
    public void Writer_SchreibtJsonMarkdownUndFehlerCsv()
    {
        var root = Path.Combine(Path.GetTempPath(), "AiQualityReportTests", Guid.NewGuid().ToString("N"));
        try
        {
            var report = AiFieldQualityReportAnalyzer.Analyze([
                Ai("a1", "100-200", 1.0, "BAB", "BAB", true, green: true)
            ]);

            var files = AiFieldQualityReportWriter.Write(root, report, "test-report");

            Assert.True(File.Exists(files.JsonPath));
            Assert.True(File.Exists(files.MarkdownPath));
            Assert.True(File.Exists(files.IssuesCsvPath));
            Assert.Contains("KI-Qualitaetsbericht", File.ReadAllText(files.MarkdownPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static TrainingSample Ai(
        string id,
        string caseId,
        double meter,
        string predicted,
        string final,
        bool? confirmed,
        bool green,
        bool corrected = false,
        string meterSource = "OSD")
        => new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = final,
            KiCode = predicted,
            Beschreibung = predicted,
            MeterStart = meter,
            MeterEnd = meter,
            MeterSource = meterSource,
            Status = confirmed == false ? TrainingSampleStatus.Rejected : TrainingSampleStatus.Approved,
            HumanConfirmed = confirmed,
            Corrected = confirmed.HasValue ? corrected : null,
            SourceType = SourceTypeNames.VideoTimestamp,
            CentralDecision = new AiDecisionAudit
            {
                Outcome = green ? "AutoAccept" : "Review",
                ReasonCode = green ? "EvidenceConfirmed" : "KbMissing",
                PolicyVersion = "test"
            }
        };

    private static TrainingSample Manual(string id, string caseId, double meter, string code)
        => new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = code,
            Beschreibung = code,
            MeterStart = meter,
            MeterEnd = meter,
            Status = TrainingSampleStatus.Approved,
            HumanConfirmed = true,
            SourceType = SourceTypeNames.ManualCoding
        };
}
