using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public sealed class TrainingDataInventorySourceAndPathTests : TrainingInventoryTestBase
{
    [Fact]
    public async Task InspectAsync_UnterscheidetEindeutigeMehrdeutigeUndGeschuetzteTreffer()
    {
        var firstRoot = Directory.CreateDirectory(Path.Combine(Root, "first")).FullName;
        var secondRoot = Directory.CreateDirectory(Path.Combine(Root, "second")).FullName;
        var protectedRoot = Directory.CreateDirectory(Path.Combine(Root, "eval_set")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(firstRoot, "unique.png"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(firstRoot, "duplicate.png"), [2]);
        await File.WriteAllBytesAsync(Path.Combine(secondRoot, "duplicate.png"), [3]);
        await File.WriteAllBytesAsync(Path.Combine(protectedRoot, "locked.png"), [4]);
        WriteCurrentSources(
            CreateAnnotation("unique", "100-200", Path.Combine("X:\\old", "unique.png"), 0.2, 0.2),
            CreateAnnotation("duplicate", "100-200", Path.Combine("X:\\old", "duplicate.png"), 0.2, 0.2),
            CreateAnnotation("locked", "100-200", Path.Combine("X:\\old", "locked.png"), 0.2, 0.2));

        var report = await CreateService().InspectAsync(new TrainingDataInventoryRequest
        {
            KnowledgeRoot = Root,
            EvalSetRoot = protectedRoot,
            SearchRoots = [firstRoot, secondRoot],
            ProtectedRoots = [protectedRoot],
            IncludeBackups = false,
            ComputeAssetHashes = false
        });

        var unique = report.TeacherRecords.Single(record => record.RecordKey == "unique");
        Assert.Equal(TrainingInventoryPathState.SuggestedForManualReview, unique.FullFrame.State);
        Assert.False(unique.FullFrame.Exists);
        Assert.Equal(TrainingInventoryHashState.NotRequested, unique.FullFrame.HashState);
        Assert.Equal(Path.Combine(firstRoot, "unique.png"), unique.FullFrame.SuggestedPath);

        var duplicate = report.TeacherRecords.Single(record => record.RecordKey == "duplicate");
        Assert.Equal(TrainingInventoryPathState.Ambiguous, duplicate.FullFrame.State);
        Assert.Equal(TrainingInventoryHashState.NotApplicable, duplicate.FullFrame.HashState);
        Assert.Equal(2, duplicate.FullFrame.Candidates.Count);

        var locked = report.TeacherRecords.Single(record => record.RecordKey == "locked");
        Assert.Equal(TrainingInventoryPathState.ProtectedCandidate, locked.FullFrame.State);
        Assert.True(locked.FullFrame.IsProtected);
        Assert.Equal(TrainingInventoryHashState.NotApplicable, locked.FullFrame.HashState);
        Assert.Null(locked.FullFrame.SuggestedPath);
        Assert.Equal(TrainingInventoryEvalState.ProtectedPath, locked.EvalState);
    }

    [Fact]
    public async Task InspectAsync_LoestRelativenGespeichertenPfadGegenKnowledgeRootAuf()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "relative.png");
        await File.WriteAllBytesAsync(imagePath, [5, 4, 3, 2, 1]);
        WriteCurrentSources(CreateAnnotation(
            "relative",
            "100-200",
            Path.Combine("teacher_images", "relative.png"),
            0.2,
            0.2));

        var report = await CreateService().InspectAsync(CreateRequest([imageRoot]));

        var frame = Assert.Single(report.TeacherRecords).FullFrame;
        Assert.Equal(TrainingInventoryPathState.Existing, frame.State);
        Assert.True(frame.Exists);
        Assert.False(frame.IsProtected);
        Assert.Equal(TrainingInventoryHashState.Computed, frame.HashState);
        Assert.Equal(Path.GetFullPath(imagePath), frame.ExistingPath);
        Assert.NotNull(frame.Sha256);
        Assert.Null(frame.SuggestedPath);
    }

    [Fact]
    public async Task InspectAsync_VorhandenerGeschuetzterPfadBleibtExistingUndTraegtSchutzSeparat()
    {
        var evalImages = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "images")).FullName;
        var protectedImage = Path.Combine(evalImages, "900-901_reserved.png");
        await File.WriteAllBytesAsync(protectedImage, [7, 7, 7]);
        WriteCurrentSources(CreateAnnotation("protected", "100-200", protectedImage, 0.2, 0.2));

        var report = await CreateService().InspectAsync(CreateRequest([]));

        var record = Assert.Single(report.TeacherRecords);
        Assert.Equal(TrainingInventoryPathState.Existing, record.FullFrame.State);
        Assert.True(record.FullFrame.Exists);
        Assert.True(record.FullFrame.IsProtected);
        Assert.Equal(TrainingInventoryHashState.Computed, record.FullFrame.HashState);
        Assert.Equal(TrainingInventoryDisposition.EvaluationLocked, record.Disposition);
    }

    [Fact]
    public async Task InspectAsync_UngueltigerGespeicherterPfadHatEigenenPfadstatusOhneHashfehler()
    {
        WriteCurrentSources(CreateAnnotation("invalid-path", "100-200", "bad\0frame.png", 0.2, 0.2));

        var report = await CreateService().InspectAsync(CreateRequest([]));

        var frame = Assert.Single(report.TeacherRecords).FullFrame;
        Assert.Equal(TrainingInventoryPathState.Invalid, frame.State);
        Assert.False(frame.Exists);
        Assert.Equal(TrainingInventoryHashState.NotApplicable, frame.HashState);
        Assert.Contains(report.Issues, issue =>
            issue.Code == "path-invalid" && issue.RecordKey == "invalid-path");
    }

    [Fact]
    public async Task InspectAsync_DefektesJsonErzeugtKeineReparaturOderSicherungsdatei()
    {
        WriteRawCurrentSources("[{ kaputt", "[]");
        var request = CreateRequest([]);
        var before = SnapshotFiles(Root);

        var report = await CreateService().InspectAsync(request);

        Assert.Equal(before, SnapshotFiles(Root));
        Assert.Equal(1, report.Summary.Sources.InvalidDocuments);
        Assert.Contains(report.Issues, issue => issue.Code == "source-invalid");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Root),
            path => path.Contains(".bad", StringComparison.OrdinalIgnoreCase)
                    || path.Contains(".corrupt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InspectAsync_FuehrtDatenartUndRolleOhneDoppelteIsCurrentAngabe()
    {
        WriteCurrentSources();
        await File.WriteAllTextAsync(Path.Combine(Root, "teacher_annotations.json.bak"), "[]");
        await File.WriteAllTextAsync(Path.Combine(Root, "training_samples.json.bak.20260101"), "[]");

        var report = await CreateService().InspectAsync(CreateRequest([]) with { IncludeBackups = true });

        Assert.Contains(report.Sources, source =>
            source.DataKind == TrainingInventoryDataKind.TeacherAnnotations
            && source.Role == TrainingInventorySourceRole.Current
            && source.ValidationLevel == TrainingInventoryValidationLevel.TypedRecords);
        Assert.Contains(report.Sources, source =>
            source.DataKind == TrainingInventoryDataKind.TeacherAnnotations
            && source.Role == TrainingInventorySourceRole.Backup
            && source.ValidationLevel == TrainingInventoryValidationLevel.JsonArray);
        Assert.Contains(report.Sources, source =>
            source.DataKind == TrainingInventoryDataKind.TrainingSamples
            && source.Role == TrainingInventorySourceRole.Backup
            && source.ValidationLevel == TrainingInventoryValidationLevel.JsonArray);
        Assert.All(report.Sources, source => Assert.Equal(TrainingInventoryParseState.Parsed, source.ParseState));
    }

    [Fact]
    public async Task InspectAsync_ValidiertAktuelleTrainingSamplesAlsTypisierteDatensaetze()
    {
        WriteRawCurrentSources("[]", "[{\"timeSeconds\":\"keine-zahl\"}]");

        var report = await CreateService().InspectAsync(CreateRequest([]));

        var source = Assert.Single(report.Sources, candidate =>
            candidate.DataKind == TrainingInventoryDataKind.TrainingSamples
            && candidate.Role == TrainingInventorySourceRole.Current);
        Assert.Equal(TrainingInventoryParseState.Invalid, source.ParseState);
        Assert.Contains(report.Issues, issue =>
            issue.Code == "source-invalid" && issue.Path == source.Path);
        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public async Task InspectAsync_LeererTrainingSampleMachtAktuelleQuelleUngueltig()
    {
        WriteRawCurrentSources("[]", "[null]");

        var report = await CreateService().InspectAsync(CreateRequest([]));

        var source = Assert.Single(report.Sources, candidate =>
            candidate.DataKind == TrainingInventoryDataKind.TrainingSamples
            && candidate.Role == TrainingInventorySourceRole.Current);
        Assert.Equal(TrainingInventoryParseState.Invalid, source.ParseState);
        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public async Task InspectAsync_DefektesBackupWirdGemeldetAberMachtAktuelleQuellenNichtUngueltig()
    {
        WriteCurrentSources();
        await File.WriteAllTextAsync(Path.Combine(Root, "training_samples.json.bak"), "[{ kaputt");

        var report = await CreateService().InspectAsync(CreateRequest([]) with { IncludeBackups = true });

        Assert.Contains(report.Issues, issue =>
            issue.Code == TrainingInventoryIssueCodes.SourceInvalid
            && issue.Severity == TrainingInventoryIssueSeverity.Warning);
        Assert.True(TrainingInventoryExitPolicy.IsSuccessful(report));
    }
}
