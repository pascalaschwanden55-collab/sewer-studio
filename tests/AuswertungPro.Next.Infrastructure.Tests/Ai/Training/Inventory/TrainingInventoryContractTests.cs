using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public sealed class TrainingInventoryContractTests
{
    [Fact]
    public void Summary_TriageIstExklusivUndErgibtGesamtzahl()
    {
        var records = Enum.GetValues<TrainingInventoryDisposition>()
            .Select((disposition, index) => new TeacherInventoryRecord
            {
                RecordKey = $"record-{index}",
                VsaCode = "BABBB",
                Disposition = disposition
            })
            .ToArray();

        var summary = TrainingInventorySummaryBuilder.Build(records, []);

        Assert.Equal(records.Length, summary.Data.TeacherRecords);
        Assert.Equal(records.Length, summary.Triage.Total);
        Assert.Equal(1, summary.Triage.TrainValCandidates);
        Assert.Equal(1, summary.Triage.QuarantineOrigin);
        Assert.Equal(1, summary.Triage.QuarantineGeometry);
        Assert.Equal(1, summary.Triage.Archive);
        Assert.Equal(1, summary.Triage.EvaluationLocked);
        Assert.Equal(1, summary.Triage.EvaluationNotChecked);
    }

    [Fact]
    public void TeacherRecord_HatKeinAbgeleitetesQuarantineFlagMehr()
    {
        var property = typeof(TeacherInventoryRecord).GetProperty(
            "QuarantineFlag",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.Null(property);
        Assert.NotNull(typeof(TeacherInventoryRecord).GetProperty(
            nameof(TeacherInventoryRecord.Disposition),
            BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ExitPolicy_AkzeptiertBeideAktuellenTypisiertenQuellenUndWarnungen()
    {
        var report = CreateReportWithValidCurrentSources(
            new TrainingInventoryIssue
            {
                Severity = TrainingInventoryIssueSeverity.Warning,
                Code = "warning",
                Message = "Nur ein Hinweis."
            });

        Assert.True(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public void ExitPolicy_LehntAktuelleQuelleOhneTypisierteValidierungAb()
    {
        var report = CreateReportWithValidCurrentSources();
        var sources = report.Sources
            .Select(source => source.DataKind == TrainingInventoryDataKind.TrainingSamples
                ? CreateSource(
                    TrainingInventoryDataKind.TrainingSamples,
                    TrainingInventorySourceRole.Current,
                    TrainingInventoryValidationLevel.JsonArray)
                : source)
            .ToArray();
        report = new TrainingDataInventoryReport
        {
            KnowledgeRoot = report.KnowledgeRoot,
            Sources = sources,
            Summary = TrainingInventorySummaryBuilder.Build([], sources)
        };

        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public void ExitPolicy_LehntFehlendeAktuelleQuelleAb()
    {
        var sources = new[]
        {
            CreateSource(
                TrainingInventoryDataKind.TeacherAnnotations,
                TrainingInventorySourceRole.Current,
                TrainingInventoryValidationLevel.TypedRecords),
            CreateSource(
                TrainingInventoryDataKind.TrainingSamples,
                TrainingInventorySourceRole.Backup,
                TrainingInventoryValidationLevel.TypedRecords)
        };
        var report = new TrainingDataInventoryReport
        {
            KnowledgeRoot = @"C:\KI_BRAIN",
            Sources = sources,
            Summary = TrainingInventorySummaryBuilder.Build([], sources)
        };

        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public void ExitPolicy_LehntErrorIssueTrotzGueltigerQuellenAb()
    {
        var report = CreateReportWithValidCurrentSources(
            new TrainingInventoryIssue
            {
                Severity = TrainingInventoryIssueSeverity.Error,
                Code = "source-invalid",
                Message = "Quelle ungueltig."
            });

        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public void ExitPolicy_LehntDoppelteAktuelleQuelleAb()
    {
        var valid = CreateReportWithValidCurrentSources();
        var sources = valid.Sources.Append(valid.Sources[0]).ToArray();
        var report = new TrainingDataInventoryReport
        {
            GeneratedUtc = valid.GeneratedUtc,
            KnowledgeRoot = valid.KnowledgeRoot,
            Sources = sources,
            Summary = TrainingInventorySummaryBuilder.Build([], sources)
        };

        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public void ExitPolicy_LehntAktuelleQuelleAmFalschenPfadAb()
    {
        var valid = CreateReportWithValidCurrentSources();
        var sources = valid.Sources
            .Select(source => source.DataKind == TrainingInventoryDataKind.TeacherAnnotations
                ? CreateSource(
                    TrainingInventoryDataKind.TeacherAnnotations,
                    TrainingInventorySourceRole.Current,
                    TrainingInventoryValidationLevel.TypedRecords,
                    @"C:\KI_BRAIN\falsche_teacher_datei.json")
                : source)
            .ToArray();
        var report = new TrainingDataInventoryReport
        {
            GeneratedUtc = valid.GeneratedUtc,
            KnowledgeRoot = valid.KnowledgeRoot,
            Sources = sources,
            Summary = TrainingInventorySummaryBuilder.Build([], sources)
        };

        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    private static TrainingDataInventoryReport CreateReportWithValidCurrentSources(
        params TrainingInventoryIssue[] issues)
    {
        var sources = new[]
        {
            CreateSource(
                TrainingInventoryDataKind.TeacherAnnotations,
                TrainingInventorySourceRole.Current,
                TrainingInventoryValidationLevel.TypedRecords),
            CreateSource(
                TrainingInventoryDataKind.TrainingSamples,
                TrainingInventorySourceRole.Current,
                TrainingInventoryValidationLevel.TypedRecords)
        };
        return new TrainingDataInventoryReport
        {
            GeneratedUtc = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            KnowledgeRoot = @"C:\KI_BRAIN",
            Sources = sources,
            Issues = issues,
            Summary = TrainingInventorySummaryBuilder.Build([], sources)
        };
    }

    private static TrainingInventorySourceDocument CreateSource(
        TrainingInventoryDataKind dataKind,
        TrainingInventorySourceRole role,
        TrainingInventoryValidationLevel validationLevel,
        string? path = null)
        => new()
        {
            Path = path ?? (role == TrainingInventorySourceRole.Current
                ? Path.Combine(
                    @"C:\KI_BRAIN",
                    dataKind == TrainingInventoryDataKind.TeacherAnnotations
                        ? "teacher_annotations.json"
                        : "training_samples.json")
                : $@"C:\KI_BRAIN\{dataKind}-{role}.json"),
            DataKind = dataKind,
            Role = role,
            ParseState = TrainingInventoryParseState.Parsed,
            ValidationLevel = validationLevel,
            Bytes = 2,
            LastWriteUtc = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            Sha256 = new string('a', 64),
            RecordCount = 0
        };
}
