using System.Text;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public sealed class TrainingDataInventoryServiceTests : TrainingInventoryTestBase
{
    [Fact]
    public async Task InspectAsync_LiestNurUndBildetExklusiveTriageGruppen()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        WriteCurrentSources(
            CreateAnnotation("usable", "100-200", imagePath, width: 0.2, height: 0.2),
            CreateAnnotation("quarantine", "Training", imagePath, width: 0.2, height: 0.2),
            CreateAnnotation("archive", "100-200", imagePath, width: 0, height: 0.2));
        var request = CreateRequest([imageRoot]);
        var before = SnapshotFiles(Root);

        var report = await CreateService().InspectAsync(request);

        Assert.Equal(before, SnapshotFiles(Root));
        Assert.True(report.ReadOnly);
        Assert.Equal(3, report.Summary.Data.TeacherRecords);
        Assert.Equal(2, report.Summary.Data.PositiveAreaBoxes);
        Assert.Equal(2, report.Summary.Holdings.Explicit);
        Assert.Equal(1, report.Summary.Holdings.NonExplicit);
        Assert.Equal(1, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(1, report.Summary.Triage.QuarantineOrigin);
        Assert.Equal(1, report.Summary.Triage.Archive);
        Assert.Equal(report.Summary.Data.TeacherRecords, report.Summary.Triage.Total);
        Assert.Equal(
            TrainingInventoryDisposition.QuarantineOrigin,
            report.TeacherRecords.Single(record => record.RecordKey == "quarantine").Disposition);
    }

    [Fact]
    public async Task InspectAsync_ZaehltPositiveAberUnnormierteBoxGetrennt()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(
            CreateAnnotation("out-of-bounds", "100-200", imagePath, width: 1.1, height: 0.2));

        var report = await CreateService().InspectAsync(CreateRequest([imageRoot]));

        Assert.Equal(1, report.Summary.Data.PositiveAreaBoxes);
        Assert.Equal(0, report.Summary.Data.StrictlyValidBoxes);
        Assert.Equal(0, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(1, report.Summary.Triage.QuarantineGeometry);
        Assert.Equal(
            TrainingInventoryBoxState.PositiveOutOfNormalizedRange,
            Assert.Single(report.TeacherRecords).BoxState);
    }

    [Fact]
    public void ClassifyBox_NichtEndlicheKoordinatenSindPositiveAberUngueltigeGeometrie()
    {
        var annotation = CreateAnnotation("non-finite", "100-200", "frame.png", 0.2, 0.2);
        annotation.BoundingBox.XCenter = double.NaN;

        var boxState = TeacherInventoryPolicy.ClassifyBox(annotation);
        var disposition = TeacherInventoryPolicy.ClassifyDisposition(
            new TrainingInventoryPathReference
            {
                State = TrainingInventoryPathState.Existing,
                HashState = TrainingInventoryHashState.Computed
            },
            TrainingInventoryHoldingState.Explicit,
            boxState,
            TrainingInventoryEvalState.Clean);

        Assert.Equal(TrainingInventoryBoxState.NonFiniteCoordinates, boxState);
        Assert.Equal(TrainingInventoryDisposition.QuarantineGeometry, disposition);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("?")]
    [InlineData("nicht zugeordnet")]
    public void ClassifyHolding_FreierPlatzhalterIstKeineBestaetigteHerkunft(string holding)
    {
        var annotation = CreateAnnotation("origin", holding, "frame.png", 0.2, 0.2);

        var assessment = TeacherInventoryPolicy.ClassifyHolding(annotation);

        Assert.False(assessment.IsExplicit);
    }

    [Fact]
    public async Task InspectAsync_BoxAusserhalbDesBildrandsKommtInGeometrieQuarantaene()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        var annotation = CreateAnnotation("edge", "100-200", imagePath, width: 0.4, height: 0.2);
        annotation.BoundingBox.XCenter = 0.9;
        WriteCurrentSources(annotation);

        var report = await CreateService().InspectAsync(CreateRequest([imageRoot]));

        var record = Assert.Single(report.TeacherRecords);
        Assert.Equal(TrainingInventoryBoxState.ExtendsOutsideImage, record.BoxState);
        Assert.Equal(TrainingInventoryDisposition.QuarantineGeometry, record.Disposition);
    }

    [Fact]
    public async Task InspectAsync_EvalGleichesBildIstNieTrainValKandidat()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [7, 8, 9]);
        var evalImages = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "images")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(evalImages, "reserved.png"), [7, 8, 9]);
        WriteCurrentSources(CreateAnnotation("eval", "100-200", imagePath, 0.2, 0.2));

        var report = await CreateService().InspectAsync(CreateRequest([imageRoot]));

        Assert.True(report.EvalProtection.Complete);
        Assert.Equal(1, report.Summary.Holdings.ExistingFramePositiveAreaExplicit);
        Assert.Equal(0, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(1, report.Summary.Triage.EvaluationLocked);
        Assert.Equal(1, report.Summary.Evaluation.ReservedRecords);
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationLocked,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_FehlenderEvalBestandBleibtUngeprueftUndGesperrt()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("unchecked", "100-200", imagePath, 0.2, 0.2));
        var missingEvalRoot = Path.Combine(Root, "missing-eval");

        var report = await CreateService().InspectAsync(new TrainingDataInventoryRequest
        {
            KnowledgeRoot = Root,
            EvalSetRoot = missingEvalRoot,
            SearchRoots = [imageRoot],
            ProtectedRoots = [missingEvalRoot],
            IncludeBackups = false,
            ComputeAssetHashes = true
        });

        Assert.False(report.EvalProtection.Complete);
        Assert.Equal(0, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(1, report.Summary.Triage.EvaluationNotChecked);
        Assert.Equal(1, report.Summary.Evaluation.UncheckedRecords);
        Assert.Contains(report.Issues, issue => issue.Code == "eval-protection-unavailable");
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationNotChecked,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_EvalOhneHaltungsschluesselBleibtUngeprueftUndGesperrt()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        var evalImages = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "images")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(evalImages, "ohne_haltung.png"), [8, 8, 8]);
        WriteCompleteEvalSet(Path.Combine(Root, "eval_set"), "900-901");
        await File.WriteAllTextAsync(Path.Combine(Root, "eval_set", "_candidates.json"), "[]");
        WriteCurrentSources(CreateAnnotation("unchecked-holding", "100-200", imagePath, 0.2, 0.2));

        var report = await CreateService().InspectAsync(new TrainingDataInventoryRequest
        {
            KnowledgeRoot = Root,
            EvalSetRoot = Path.Combine(Root, "eval_set"),
            SearchRoots = [imageRoot],
            ProtectedRoots = [Path.Combine(Root, "eval_set")],
            IncludeBackups = false,
            ComputeAssetHashes = true
        });

        Assert.False(report.EvalProtection.ImageHashesAvailable);
        Assert.False(report.EvalProtection.HoldingKeysAvailable);
        Assert.False(report.EvalProtection.Complete);
        Assert.Equal(0, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationNotChecked,
            Assert.Single(report.TeacherRecords).Disposition);
        Assert.Contains(
            Assert.Single(report.EvalProtection.Sets).Errors,
            error => error.Contains("Manifest-Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InspectAsync_NichtEingefrorenesEvalSetBleibtGesperrt()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("not-frozen", "100-200", imagePath, 0.2, 0.2));

        var request = CreateRequest([imageRoot]);
        var evalRoot = Path.Combine(Root, "eval_set");
        var manifestPath = Path.Combine(evalRoot, "_manifest.json");
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var changedJson = manifestJson.Replace("\"frozen\": true", "\"frozen\": false", StringComparison.Ordinal);
        Assert.NotEqual(manifestJson, changedJson);
        await File.WriteAllTextAsync(manifestPath, changedJson);

        var report = await CreateService().InspectAsync(request);

        var set = Assert.Single(report.EvalProtection.Sets);
        Assert.False(
            set.Complete,
            $"Image={set.ImageHashesComplete}, Holding={set.HoldingKeysComplete}, Errors={string.Join(" | ", set.Errors)}");
        Assert.False(report.EvalProtection.Complete);
        Assert.Contains(set.Errors, error => error.Contains("frozen=true", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationNotChecked,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_AkzeptiertGehashtenUtf8BomBeiEvalKandidaten()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("bom", "100-200", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);

        var evalRoot = Path.Combine(Root, "eval_set");
        var candidatesPath = Path.Combine(evalRoot, "_candidates.json");
        var json = await File.ReadAllTextAsync(candidatesPath);
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(json))
            .ToArray();
        await File.WriteAllBytesAsync(candidatesPath, bytes);
        _ = EvalSetManifestHasher.ComputeAndStoreHashes(evalRoot);

        var report = await CreateService().InspectAsync(request);

        Assert.True(report.EvalProtection.Complete);
        Assert.Empty(Assert.Single(report.EvalProtection.Sets).Errors);
    }

    [Fact]
    public async Task InspectAsync_EvalBildOhneKandidatSperrtDasSet()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("missing-candidate", "100-200", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);

        var evalRoot = Path.Combine(Root, "eval_set");
        await File.WriteAllBytesAsync(
            Path.Combine(evalRoot, "images", "zweites_bild.png"),
            [7, 8, 9]);
        _ = EvalSetManifestHasher.ComputeAndStoreHashes(evalRoot);

        var report = await CreateService().InspectAsync(request);

        var set = Assert.Single(report.EvalProtection.Sets);
        Assert.False(set.Complete);
        Assert.Contains(
            set.Errors,
            error => error.Contains("keinen Kandidateneintrag", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationNotChecked,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_ZweiEvalSets_EinDefektesSetBleibtUnvollstaendigUndSperrtTrainVal()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("two-sets-broken", "100-200", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);

        var brokenSet = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "v2")).FullName;
        var brokenImage = Path.Combine(Directory.CreateDirectory(Path.Combine(brokenSet, "images")).FullName, "v2.png");
        await File.WriteAllBytesAsync(brokenImage, [21, 22, 23]);
        WriteCompleteEvalSet(brokenSet, "800-801");
        await File.WriteAllTextAsync(
            Path.Combine(brokenSet, "_manifest.json"),
            $$"""
              {
                "hashes": {
                  "images/v2.png": {
                    "sha256": "{{new string('0', 64)}}"
                  }
                }
              }
              """);

        var report = await CreateService().InspectAsync(request);

        Assert.Equal(2, report.EvalProtection.Sets.Count);
        var brokenStatus = Assert.Single(
            report.EvalProtection.Sets,
            set => set.RootPath.Equals(brokenSet, StringComparison.OrdinalIgnoreCase));
        Assert.False(brokenStatus.ImageHashesComplete);
        Assert.False(brokenStatus.HoldingKeysComplete);
        Assert.NotEmpty(brokenStatus.Errors);
        Assert.False(report.EvalProtection.Complete);
        Assert.Equal(0, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(1, report.Summary.Triage.EvaluationNotChecked);
        Assert.Equal(
            TrainingInventoryDisposition.EvaluationNotChecked,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_ZweiVollstaendigeEvalSetsErlaubenSauberenTrainValKandidaten()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        WriteCurrentSources(CreateAnnotation("two-sets-complete", "100-200", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);

        var secondSet = Directory.CreateDirectory(Path.Combine(Root, "eval_set", "v2")).FullName;
        var secondImage = Path.Combine(Directory.CreateDirectory(Path.Combine(secondSet, "images")).FullName, "v2.png");
        await File.WriteAllBytesAsync(secondImage, [31, 32, 33]);
        WriteCompleteEvalSet(secondSet, "800-801");

        var report = await CreateService().InspectAsync(request);

        Assert.Equal(2, report.EvalProtection.Sets.Count);
        Assert.All(report.EvalProtection.Sets, set => Assert.True(set.Complete));
        Assert.True(report.EvalProtection.Complete);
        Assert.Equal(1, report.Summary.Triage.TrainValCandidates);
        Assert.Equal(
            TrainingInventoryDisposition.TrainValCandidate,
            Assert.Single(report.TeacherRecords).Disposition);
    }

    [Fact]
    public async Task InspectAsync_LeererTeacherEintragMachtAktuelleQuelleUngueltig()
    {
        WriteCurrentSources((AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation?)null);

        var report = await CreateService().InspectAsync(CreateRequest([]));

        Assert.Empty(report.TeacherRecords);
        Assert.Contains(report.Issues, issue => issue.Code == TrainingInventoryIssueCodes.SourceInvalid);
        Assert.False(TrainingInventoryExitPolicy.IsSuccessful(report));
    }

    [Fact]
    public async Task InspectRuntimeSnapshotAsync_liefert_Quellen_und_Schutz_aus_demselben_Lauf()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        var annotation = CreateAnnotation("teacher-live", "100-200", imagePath, 0.2, 0.2);
        var sample = new TrainingSample
        {
            SampleId = "sample-live",
            CaseId = "100-200",
            Code = "BABBB",
            FramePath = imagePath,
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.2
        };
        WriteRawCurrentSources(
            System.Text.Json.JsonSerializer.Serialize(new[] { annotation }, JsonDefaults.IndentedCamel),
            System.Text.Json.JsonSerializer.Serialize(new[] { sample }, JsonDefaults.IndentedCamel));

        var snapshot = await CreateService().InspectRuntimeSnapshotAsync(CreateRequest([imageRoot]));

        Assert.False(string.IsNullOrWhiteSpace(snapshot.Report.RunId));
        Assert.Equal("teacher-live", Assert.Single(snapshot.TeacherAnnotations).AnnotationId);
        Assert.Equal("sample-live", Assert.Single(snapshot.TrainingSamples).SampleId);
        Assert.True(snapshot.Protection.Status.Complete);
        Assert.NotEmpty(snapshot.Protection.ImageHashes);
        Assert.NotEmpty(snapshot.Protection.HoldingKeys);
        var protectedSet = Assert.Single(snapshot.Protection.Sets);
        Assert.Equal("eval_set", protectedSet.SetId);
        Assert.Equal(64, protectedSet.ManifestSha256.Length);
        Assert.Equal(64, snapshot.Protection.Fingerprint.Length);
        Assert.True(TrainingInventoryExitPolicy.IsSuccessful(snapshot.Report));
    }

    [Fact]
    public async Task InspectRuntimeSnapshotAsync_prueft_explizite_DevVal_und_Abnahme_Sets()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        WriteCurrentSources(CreateAnnotation("teacher-live", "100-200", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);
        var acceptanceRoot = Directory.CreateDirectory(Path.Combine(Root, "training", "testset_gold")).FullName;
        var acceptanceImageRoot = Directory.CreateDirectory(Path.Combine(acceptanceRoot, "images")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(acceptanceImageRoot, "gold.png"), [7, 8, 9]);
        WriteCompleteEvalSet(acceptanceRoot, "800-801");
        request = request with
        {
            ProtectedRoots = request.ProtectedRoots.Append(acceptanceRoot).ToArray(),
            ProtectedSetRoots = new Dictionary<string, string>
            {
                ["dev-val-v1"] = request.EvalSetRoot!,
                ["acceptance-v1"] = acceptanceRoot
            }
        };

        var snapshot = await CreateService().InspectRuntimeSnapshotAsync(request);

        Assert.True(snapshot.Protection.Status.Complete);
        Assert.Equal(2, snapshot.Protection.Sets.Count);
        Assert.Contains(snapshot.Protection.Sets, set => set.SetId == "dev-val-v1");
        Assert.Contains(snapshot.Protection.Sets, set => set.SetId == "acceptance-v1");
        Assert.True(TrainingInventoryExitPolicy.IsSuccessful(snapshot.Report));
    }

    [Fact]
    public async Task PlanInputBuilder_verwendet_Teacher_Disposition_aus_genau_diesem_Snapshot()
    {
        var imageRoot = Directory.CreateDirectory(Path.Combine(Root, "teacher_images")).FullName;
        var imagePath = Path.Combine(imageRoot, "frame.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        WriteCurrentSources(
            CreateAnnotation("clean", "100-200", imagePath, 0.2, 0.2),
            CreateAnnotation("origin", "Training", imagePath, 0.2, 0.2));
        var request = CreateRequest([imageRoot]);
        request = request with
        {
            ProtectedSetRoots = new Dictionary<string, string>
            {
                ["dev-val-v1"] = request.EvalSetRoot!
            }
        };
        var inventory = await CreateService().InspectRuntimeSnapshotAsync(request);
        var protectedSet = Assert.Single(inventory.Protection.Sets);
        var registry = new TrainingExportRegistrySnapshot(
            TrainingExportRegistrySnapshot.CurrentSchemaVersion,
            new string('a', 64),
            TrainingExportRegistryApprovalStatus.Approved,
            "Test User",
            DateTimeOffset.UtcNow,
            new Dictionary<string, TrainingExportHoldingRole>
            {
                ["100-200"] = TrainingExportHoldingRole.Train
            },
            [new TrainingExportProtectedSetReference(
                protectedSet.SetId,
                TrainingExportProtectedSetRole.DevelopmentValidation,
                protectedSet.ManifestSha256)]);
        var classMap = new TrainingYoloClassMapSnapshot(
            YoloDetectClassMapV2.Version,
            new string('b', 64),
            YoloDetectClassMapV2.Classes,
            [new TrainingYoloClassMapping(
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                "BABBB",
                null,
                TrainingYoloClassAction.Map,
                "BAB_riss",
                TrainingYoloClassApprovalStatus.Approved)]);

        var input = await new TrainingExportPlanInputBuilder().BuildAsync(
            inventory,
            registry,
            new HashSet<string>(),
            classMap,
            DateTimeOffset.UtcNow);

        Assert.Equal(2, input.Candidates.Count);
        Assert.Equal(
            TrainingInventoryDisposition.TrainValCandidate,
            input.Candidates.Single(item => item.Source.SourceId == "clean").InventoryDisposition);
        Assert.Equal(
            TrainingInventoryDisposition.QuarantineOrigin,
            input.Candidates.Single(item => item.Source.SourceId == "origin").InventoryDisposition);
        Assert.Equal(2, input.SourceSnapshotHashes.Count);
    }
}
