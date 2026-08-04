using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportRegistryFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-registry-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadBundle_liefert_freigegebene_Rollen_und_gepruefte_Schutzpfade()
    {
        var paths = CreateFiles();

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(TrainingExportRegistryApprovalStatus.Approved, bundle.Snapshot.ApprovalStatus);
        Assert.Equal(TrainingExportHoldingRole.Train, bundle.Snapshot.HoldingRoles["100-200"]);
        Assert.Equal(
            TrainingExportHoldingRole.DevelopmentValidation,
            bundle.Snapshot.HoldingRoles["200-300"]);
        Assert.Equal(["sample-a", "sample-b"], bundle.Snapshot.ApprovedSampleIds.Order());
        Assert.Equal(64, bundle.Snapshot.RegistryHash.Length);
        var protectedSet = Assert.Single(bundle.Snapshot.ProtectedSets);
        Assert.Equal("dev-val-v1", protectedSet.SetId);
        Assert.Equal(paths.SetRoot, bundle.ProtectedSetRootPaths["dev-val-v1"]);
    }

    [Fact]
    public void ReadBundle_blockiert_unbekannte_JSON_Felder()
    {
        var paths = CreateFiles();
        File.AppendAllText(paths.RegistryPath, "\n");
        var text = File.ReadAllText(paths.RegistryPath)
            .Replace("\"schema_version\"", "\"unknown\": true, \"schema_version\"", StringComparison.Ordinal);
        File.WriteAllText(paths.RegistryPath, text);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("sicher gelesen", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_geaendertes_Schutzmanifest()
    {
        var paths = CreateFiles();
        File.AppendAllText(paths.ManifestPath, "geaendert");

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Manifest-Hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_liefert_Kandidaten_ohne_sie_stillschweigend_freizugeben()
    {
        var paths = CreateFiles(approvalStatus: "candidate", approvedBy: null, approvedUtc: null);

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(TrainingExportRegistryApprovalStatus.Candidate, bundle.Snapshot.ApprovalStatus);
        Assert.Null(bundle.Snapshot.ApprovedBy);
        Assert.Null(bundle.Snapshot.ApprovedUtc);
    }

    [Fact]
    public void ReadBundle_blockiert_doppelte_Pilot_Sample_IDs()
    {
        var paths = CreateFiles(approvedSampleIdsJson: "[\"sample-a\", \"SAMPLE-A\"]");

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("mehrfach", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_liest_kuratierte_Negativbilder_mit_und_ohne_Split_Hinweis()
    {
        var shaA = new string('1', 64);
        var shaB = new string('2', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              { "path": "training/negatives/bcc_pilot/normal_01.png", "sha256": "{{shaA}}" },
              { "path": "training/negatives/bcc_pilot/normal_02.png", "sha256": "{{shaB}}", "split": "validation" }
            ]
            """);

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Equal(2, bundle.Snapshot.NegativeImages.Count);
        var first = bundle.Snapshot.NegativeImages[0];
        Assert.Equal(shaA, first.Sha256);
        Assert.Null(first.SplitHint);
        Assert.True(Path.IsPathFullyQualified(first.Path));   // relativ -> KnowledgeRoot aufgeloest
        var second = bundle.Snapshot.NegativeImages[1];
        Assert.Equal(shaB, second.Sha256);
        Assert.Equal(TrainingExportTarget.Validation, second.SplitHint);
    }

    [Fact]
    public void ReadBundle_liest_streng_gebundenes_Negativbild()
    {
        var negativeSet = CreateBoundNegativeSet();
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        var negative = Assert.Single(bundle.Snapshot.NegativeImages);
        Assert.Equal(negativeSet.HoldingKey, negative.HoldingKey);
        Assert.Equal(negativeSet.PhysicalHoldingKey, negative.PhysicalHoldingKey);
        Assert.Equal("reviewed_negative_set", negative.NegativeSourceType);
        Assert.Equal(TrainingExportTarget.Train, negative.SplitHint);
        Assert.Equal(negativeSet.SetId, negative.NegativeSetId);
        Assert.Equal(negativeSet.ManifestSha256, negative.NegativeSetManifestSha256);
        Assert.Equal(negativeSet.QueueId, negative.QueueId);
        Assert.Equal(negativeSet.ReviewSha256, negative.ReviewSha256);
        Assert.Equal(negativeSet.QueueManifestSha256, negative.QueueManifestSha256);
        Assert.Equal(negativeSet.CandidatesSha256, negative.CandidatesSha256);
        Assert.Equal(3, negative.ClassMapVersion);
        Assert.Equal(negativeSet.ClassMapSha256, negative.ClassMapSha256);
        Assert.Equal(negativeSet.VsaManifestHash, negative.VsaManifestHash);
        Assert.Equal(negativeSet.ReviewItemId, negative.ReviewItemId);
        Assert.Equal(negativeSet.ReviewDecision, negative.ReviewDecision);
    }

    [Fact]
    public void ReadBundle_bewahrt_Legacy_Negativbild_mit_Split_Hinweis()
    {
        var sha = new string('1', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              {
                "path": "training/negatives/bcc_pilot/normal_01.png",
                "sha256": "{{sha}}",
                "split": "validation"
              }
            ]
            """);

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        var negative = Assert.Single(bundle.Snapshot.NegativeImages);
        Assert.Null(negative.HoldingKey);
        Assert.Equal(TrainingExportTarget.Validation, negative.SplitHint);
        Assert.Null(negative.NegativeSetId);
    }

    [Theory]
    [InlineData("\"holding_key\": \"100-200\",")]
    [InlineData(
        "\"source_type\": \"reviewed_negative_set\", "
        + "\"holding_key\": \"100-200\", \"physical_holding_key\": \"100|200\", \"split\": \"train\", "
        + "\"set_id\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", "
        + "\"set_manifest_sha256\": \"2222222222222222222222222222222222222222222222222222222222222222\", "
        + "\"queue_id\": \"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\", "
        + "\"queue_manifest_sha256\": \"4444444444444444444444444444444444444444444444444444444444444444\", "
        + "\"class_map_sha256\": \"5555555555555555555555555555555555555555555555555555555555555555\",")]
    public void ReadBundle_blockiert_unvollstaendige_neue_Negativbindung(string bindingFields)
    {
        var sha = new string('1', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              {
                "path": "training/negatives/normal_01.png",
                "sha256": "{{sha}}",
                {{bindingFields}}
              }
            ]
            """);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("vollstaendig", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ungueltige/id")]
    [InlineData(" id-mit-leerzeichen")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void ReadBundle_blockiert_ungueltige_Negativ_Set_ID(string setId)
    {
        var negativeSet = CreateBoundNegativeSet();
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet, setId: setId));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Set-ID", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_nicht_numerische_Negativ_Haltung()
    {
        var negativeSet = CreateBoundNegativeSet();
        var json = BoundNegativeJson(negativeSet, holdingKey: "keine-haltung");
        var paths = CreateFiles(negativesJson: json);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Schachtpaar", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_falschen_Ordner_eines_gebundenen_Negativbilds()
    {
        var negativeSet = CreateBoundNegativeSet();
        var wrongPath =
            $"training/negatives/sets/bcc_hn_{new string('b', 12)}/images/normal_01.png";
        var paths = CreateFiles(negativesJson: BoundNegativeJson(
            negativeSet,
            path: wrongPath));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Set-Ordner", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_Traversal_im_gebundenen_Negativpfad()
    {
        var negativeSet = CreateBoundNegativeSet();
        var traversalPath =
            $"training/negatives/sets/bcc_hn_{negativeSet.SetId[..12]}/images/../images/"
            + Path.GetFileName(negativeSet.RelativeImagePath);
        var paths = CreateFiles(negativesJson: BoundNegativeJson(
            negativeSet,
            path: traversalPath));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Traversal", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_abweichenden_Negativ_Set_Manifest_Hash()
    {
        var negativeSet = CreateBoundNegativeSet();
        var paths = CreateFiles(negativesJson: BoundNegativeJson(
            negativeSet,
            manifestSha256: new string('f', 64)));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Manifest-Hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_gebundenes_Negativbild_ohne_semantischen_Bildbeleg()
    {
        var otherSha = new string('f', 64);
        var negativeSet = CreateBoundNegativeSet(new NegativeManifestOverrides(
            FileName: $"img_{otherSha}.png",
            ImageSha256: otherSha));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Negativbild", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("image_sha256")]
    [InlineData("holding_key")]
    [InlineData("physical_holding_key")]
    [InlineData("split")]
    [InlineData("set_id")]
    [InlineData("queue_id")]
    [InlineData("queue_manifest_sha256")]
    [InlineData("candidates_sha256")]
    [InlineData("review_sha256")]
    [InlineData("class_map_version")]
    [InlineData("class_map_sha256")]
    [InlineData("vsa_manifest_hash")]
    [InlineData("review_item_id")]
    [InlineData("review_decision")]
    public void ReadBundle_blockiert_abweichende_strikte_Registry_Bindung(string field)
    {
        var negativeSet = CreateBoundNegativeSet();
        var json = BoundNegativeJson(negativeSet);
        var tampered = field switch
        {
            "image_sha256" => ReplaceJsonString(json, "sha256", negativeSet.ImageSha256, new string('f', 64)),
            "holding_key" => ReplaceJsonString(json, field, negativeSet.HoldingKey, "300-400"),
            "physical_holding_key" => ReplaceJsonString(
                json,
                field,
                negativeSet.PhysicalHoldingKey,
                "100|300"),
            "split" => ReplaceJsonString(json, field, "train", "validation"),
            "set_id" => ReplaceJsonString(json, field, negativeSet.SetId, new string('f', 64)),
            "queue_id" => ReplaceJsonString(json, field, negativeSet.QueueId, new string('f', 64)),
            "queue_manifest_sha256" => ReplaceJsonString(
                json,
                field,
                negativeSet.QueueManifestSha256,
                new string('f', 64)),
            "candidates_sha256" => ReplaceJsonString(
                json,
                field,
                negativeSet.CandidatesSha256,
                new string('f', 64)),
            "review_sha256" => ReplaceJsonString(
                json,
                field,
                negativeSet.ReviewSha256,
                new string('f', 64)),
            "class_map_version" => json.Replace(
                "\"class_map_version\": 3",
                "\"class_map_version\": 2",
                StringComparison.Ordinal),
            "class_map_sha256" => ReplaceJsonString(
                json,
                field,
                negativeSet.ClassMapSha256,
                new string('f', 64)),
            "vsa_manifest_hash" => ReplaceJsonString(
                json,
                field,
                negativeSet.VsaManifestHash,
                new string('f', 64)),
            "review_item_id" => ReplaceJsonString(
                json,
                field,
                negativeSet.ReviewItemId,
                "other-review-item"),
            "review_decision" => ReplaceJsonString(
                json,
                field,
                negativeSet.ReviewDecision,
                "mapped_object_visible"),
            _ => throw new InvalidOperationException($"Unbekanntes Testfeld: {field}")
        };
        Assert.NotEqual(json, tampered);
        var paths = CreateFiles(negativesJson: tampered);

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Theory]
    [InlineData("sha256")]
    [InlineData("size_bytes")]
    public void ReadBundle_blockiert_manipulierten_Bild_Hashbeleg(string field)
    {
        var overrides = field == "sha256"
            ? new NegativeManifestOverrides(HashEntrySha256: new string('f', 64))
            : new NegativeManifestOverrides(HashEntrySize: 99);
        var negativeSet = CreateBoundNegativeSet(overrides);
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("image")]
    public void ReadBundle_blockiert_unbekannte_Felder_im_Negativ_Set_Manifest(string location)
    {
        var overrides = location == "manifest"
            ? new NegativeManifestOverrides(UnknownManifestField: true)
            : new NegativeManifestOverrides(UnknownImageField: true);
        var negativeSet = CreateBoundNegativeSet(overrides);
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_doppeltes_JSON_Feld_im_Negativ_Set_Manifest()
    {
        var negativeSet = CreateBoundNegativeSet(
            new NegativeManifestOverrides(DuplicateImageShaField: true));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("doppelte", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_doppelten_semantischen_Bildpfad()
    {
        var negativeSet = CreateBoundNegativeSet(
            new NegativeManifestOverrides(DuplicateImage: true));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_abweichende_Set_ID_im_Manifest()
    {
        var negativeSet = CreateBoundNegativeSet(
            new NegativeManifestOverrides(ManifestSetId: new string('f', 64)));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Set-ID", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_nachtraeglich_veraenderte_Bildbytes()
    {
        var negativeSet = CreateBoundNegativeSet();
        var imagePath = Path.GetFullPath(negativeSet.RelativeImagePath, _root);
        File.AppendAllText(imagePath, "manipuliert");
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_gemischte_Legacy_und_strikte_Negative()
    {
        var negativeSet = CreateBoundNegativeSet();
        var negativesJson = $$"""
            [
              {
                "path": "training/negatives/bcc_pilot/normal_01.png",
                "sha256": "{{new string('1', 64)}}"
              },
              {{BoundNegativeEntryJson(negativeSet)}}
            ]
            """;
        var paths = CreateFiles(negativesJson: negativesJson);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("mischen", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_nur_teilweise_im_Register_enthaltenes_Negativ_Set()
    {
        var negativeSet = CreateBoundNegativeSet(
            new NegativeManifestOverrides(IncludeSecondImage: true));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("exakt alle Bilder", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_gleiche_physische_Haltung_in_zwei_strikten_Sets()
    {
        var first = CreateBoundNegativeSet(new NegativeManifestOverrides(ImageFill: 0x31));
        var second = CreateBoundNegativeSet(new NegativeManifestOverrides(ImageFill: 0x32));
        var negativesJson = $$"""
            [
              {{BoundNegativeEntryJson(first)}},
              {{BoundNegativeEntryJson(second)}}
            ]
            """;
        var paths = CreateFiles(negativesJson: negativesJson);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Physische Haltung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("queue_id")]
    [InlineData("review")]
    [InlineData("class_map")]
    [InlineData("candidate")]
    [InlineData("unknown_field")]
    [InlineData("empty_model_scope")]
    [InlineData("missing_model_trigger")]
    [InlineData("invalid_queue_item")]
    [InlineData("invalid_image_signature")]
    public void ReadBundle_blockiert_semantisch_manipulierten_Receipt(string receipt)
    {
        var overrides = receipt switch
        {
            "queue_id" => new NegativeManifestOverrides(ReceiptQueueId: new string('f', 64)),
            "review" => new NegativeManifestOverrides(
                ReceiptReviewDecision: "mapped_object_visible"),
            "class_map" => new NegativeManifestOverrides(ReceiptClassMapVersion: 2),
            "candidate" => new NegativeManifestOverrides(ReceiptCandidateStatus: "accepted"),
            "unknown_field" => new NegativeManifestOverrides(UnknownQueueReceiptField: true),
            "empty_model_scope" => new NegativeManifestOverrides(EmptyModelScope: true),
            "missing_model_trigger" => new NegativeManifestOverrides(MissingPredictionTrigger: true),
            "invalid_queue_item" => new NegativeManifestOverrides(InvalidQueueItemId: true),
            "invalid_image_signature" => new NegativeManifestOverrides(InvalidImageSignature: true),
            _ => throw new InvalidOperationException($"Unbekannter Receipt-Test: {receipt}")
        };
        var negativeSet = CreateBoundNegativeSet(overrides);
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_fehlenden_Receipt_und_unvollstaendige_Hashabdeckung()
    {
        var negativeSet = CreateBoundNegativeSet();
        var imagePath = Path.GetFullPath(negativeSet.RelativeImagePath, _root);
        var setRoot = Directory.GetParent(Directory.GetParent(imagePath)!.FullName)!.FullName;
        File.Delete(Path.Combine(setRoot, "receipts", "review.json"));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_zusaetzliche_nicht_gehashte_Set_Datei()
    {
        var negativeSet = CreateBoundNegativeSet();
        var imagePath = Path.GetFullPath(negativeSet.RelativeImagePath, _root);
        var setRoot = Directory.GetParent(Directory.GetParent(imagePath)!.FullName)!.FullName;
        File.WriteAllText(Path.Combine(setRoot, "receipts", "extra.json"), "{}");
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_manipuliertes_Split_Salt()
    {
        var negativeSet = CreateBoundNegativeSet(
            new NegativeManifestOverrides(SplitSalt: "manipuliert"));
        var paths = CreateFiles(negativesJson: BoundNegativeJson(negativeSet));

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Splitregel", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_Legacy_Negativbild_aus_falschem_Ordner()
    {
        var paths = CreateFiles(negativesJson: $$"""
            [
              {
                "path": "training/negatives/normal_01.png",
                "sha256": "{{new string('1', 64)}}"
              }
            ]
            """);

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_Legacy_Negativbild_in_Unterordner()
    {
        var nestedRoot = Directory.CreateDirectory(Path.Combine(
            _root,
            "training",
            "negatives",
            "bcc_pilot",
            "nested")).FullName;
        File.WriteAllBytes(Path.Combine(nestedRoot, "legacy.png"), [1, 2, 3]);
        var paths = CreateFiles(negativesJson: $$"""
            [
              {
                "path": "training/negatives/bcc_pilot/nested/legacy.png",
                "sha256": "{{new string('1', 64)}}"
              }
            ]
            """);

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_Traversal_im_Legacy_Negativpfad()
    {
        var paths = CreateFiles(negativesJson: $$"""
            [
              {
                "path": "training/negatives/bcc_pilot/../bcc_pilot/normal_01.png",
                "sha256": "{{new string('1', 64)}}"
              }
            ]
            """);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("Traversal", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadBundle_blockiert_Verknuepfung_als_Legacy_Negativbild()
    {
        var paths = CreateFiles();
        var targetPath = Path.Combine(_root, "legacy-link-target.png");
        File.WriteAllBytes(targetPath, [1, 2, 3]);
        var linkPath = Path.Combine(
            _root,
            "training",
            "negatives",
            "bcc_pilot",
            "linked.png");
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                   or IOException
                                   or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            var registryText = File.ReadAllText(paths.RegistryPath).Replace(
                "\"holding_roles\"",
                $$"""
                  "negative_images": [
                    {
                      "path": "training/negatives/bcc_pilot/linked.png",
                      "sha256": "{{new string('1', 64)}}"
                    }
                  ],
                  "holding_roles"
                  """,
                StringComparison.Ordinal);
            File.WriteAllText(paths.RegistryPath, registryText);

            var error = Assert.Throws<TrainingExportPlanException>(() =>
                new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

            Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(linkPath))
                File.Delete(linkPath);
        }
    }

    [Fact]
    public void ReadBundle_blockiert_Verknuepfung_im_gebundenen_Negativ_Setpfad()
    {
        var setId = new string('a', 64);
        var targetRoot = Directory.CreateDirectory(Path.Combine(_root, "linked-set-target")).FullName;
        var targetImages = Directory.CreateDirectory(Path.Combine(targetRoot, "images")).FullName;
        File.WriteAllBytes(Path.Combine(targetImages, "normal_01.png"), [1, 2, 3]);
        var targetManifest = Path.Combine(targetRoot, "_manifest.json");
        File.WriteAllText(targetManifest, "{\"frozen\":true}");
        var setsRoot = Directory.CreateDirectory(
            Path.Combine(_root, "training", "negatives", "sets")).FullName;
        var linkRoot = Path.Combine(setsRoot, $"bcc_hn_{setId[..12]}");
        try
        {
            Directory.CreateSymbolicLink(linkRoot, targetRoot);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                   or IOException
                                   or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            var relativeImagePath = Path.GetRelativePath(
                    _root,
                    Path.Combine(linkRoot, "images", "normal_01.png"))
                .Replace(Path.DirectorySeparatorChar, '/');
            var paths = CreateFiles(negativesJson: BoundNegativeJson(
                new string('1', 64),
                setId,
                relativeImagePath,
                Hash(targetManifest)));

            var error = Assert.Throws<TrainingExportPlanException>(() =>
                new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

            Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(linkRoot))
                Directory.Delete(linkRoot);
        }
    }

    [Fact]
    public void ReadBundle_ohne_Negativfeld_bleibt_abwaertskompatibel()
    {
        var paths = CreateFiles();

        var bundle = new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle();

        Assert.Empty(bundle.Snapshot.NegativeImages);
    }

    [Fact]
    public void ReadBundle_blockiert_Negativbild_ohne_Hash()
    {
        var paths = CreateFiles(negativesJson: """
            [ { "path": "training/negatives/normal_01.png" } ]
            """);

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());
    }

    [Fact]
    public void ReadBundle_blockiert_doppelte_Negativ_Hashes()
    {
        var sha = new string('1', 64);
        var paths = CreateFiles(negativesJson: $$"""
            [
              { "path": "training/negatives/bcc_pilot/a.png", "sha256": "{{sha}}" },
              { "path": "training/negatives/bcc_pilot/b.png", "sha256": "{{sha}}" }
            ]
            """);

        var error = Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportRegistryFileStore(paths.RegistryPath, _root).ReadBundle());

        Assert.Contains("mehrfach", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private TestPaths CreateFiles(
        string approvalStatus = "approved",
        string? approvedBy = "Test User",
        string? approvedUtc = "2026-07-17T08:00:00Z",
        string approvedSampleIdsJson = "[\"sample-a\", \"sample-b\"]",
        string? negativesJson = null)
    {
        Directory.CreateDirectory(_root);
        var legacyRoot = Directory.CreateDirectory(
            Path.Combine(_root, "training", "negatives", "bcc_pilot")).FullName;
        foreach (var fileName in new[] { "normal_01.png", "normal_02.png", "a.png", "b.png" })
            File.WriteAllBytes(Path.Combine(legacyRoot, fileName), [1, 2, 3]);
        var setRoot = Directory.CreateDirectory(Path.Combine(_root, "eval_set")).FullName;
        var manifestPath = Path.Combine(setRoot, "_manifest.json");
        File.WriteAllText(manifestPath, "{\"frozen\":true}");
        var manifestHash = Hash(manifestPath);
        var registryPath = Path.Combine(_root, "export_registry_v1.json");
        var approvedByJson = approvedBy is null ? "null" : $"\"{approvedBy}\"";
        var approvedUtcJson = approvedUtc is null ? "null" : $"\"{approvedUtc}\"";
        var negativesSection = negativesJson is null ? string.Empty : $"\"negative_images\": {negativesJson},";
        File.WriteAllText(
            registryPath,
            $$"""
              {
                "schema_version": "1.0",
                "approval_status": "{{approvalStatus}}",
                "approved_by": {{approvedByJson}},
                "approved_utc": {{approvedUtcJson}},
                "approved_sample_ids": {{approvedSampleIdsJson}},
                {{negativesSection}}
                "holding_roles": {
                  "100-200": "train",
                  "200-300": "development_validation"
                },
                "protected_sets": [
                  {
                    "set_id": "dev-val-v1",
                    "role": "development_validation",
                    "root_path": "eval_set",
                    "manifest_sha256": "{{manifestHash}}"
                  }
                ]
              }
              """);
        return new TestPaths(registryPath, setRoot, manifestPath);
    }

    private BoundNegativeSet CreateBoundNegativeSet(
        NegativeManifestOverrides? manifestOverrides = null)
    {
        var overrides = manifestOverrides ?? new NegativeManifestOverrides();
        var imageBytes = CreatePngSignatureBytes(overrides.ImageFill ?? 0x11);
        if (overrides.InvalidImageSignature)
            imageBytes[0] = 0;
        var imageSha256 = Hash(imageBytes);
        var imageFileName = $"img_{imageSha256}.png";
        const string holdingKey = "100-200";
        const string physicalHoldingKey = "100|200";
        const string split = "train";
        var vsaManifestHash = new string('d', 64);
        var reviewItemId = $"bcc-hn-{imageSha256[..16]}";
        const string reviewDecision = "all_classes_clear";
        var sourceRef = new string('e', 64);
        const string inspectionDate = "2026-07-28";
        var secondImageBytes = CreatePngSignatureBytes(0x22);
        var secondImageSha256 = Hash(secondImageBytes);
        var secondImageFileName = $"img_{secondImageSha256}.png";
        const string secondHoldingKey = "300-400";
        const string secondPhysicalHoldingKey = "300|400";
        var secondReviewItemId = $"bcc-hn-{secondImageSha256[..16]}";
        var secondSourceRef = new string('f', 64);
        var classNames = Enumerable.Range(0, 14)
            .Select(index => $"class_{index}")
            .Append("BCC_bogen")
            .ToArray();
        var classNamesJson = JsonSerializer.Serialize(classNames);
        var classesJson = string.Join(
            ",",
            classNames.Select((name, id) => $"\"{name}\":{id}"));
        var protectedSetsJson = "[]";
        var protectionSnapshotJson = $$"""
            {
              "training_samples_sha256": "{{new string('1', 64)}}",
              "export_registry_sha256": "{{new string('2', 64)}}",
              "known_image_hashes": 1,
              "known_image_hashes_sha256": "{{new string('6', 64)}}",
              "known_holding_aliases": 1,
              "known_holding_aliases_sha256": "{{new string('7', 64)}}",
              "candidate_scope_sha256": "{{new string('8', 64)}}",
              "base_model_sha256": "{{new string('9', 64)}}"
            }
            """;

        var classMapJson = $$"""
            {
              "version": {{overrides.ReceiptClassMapVersion ?? 3}},
              "vsa_manifest_hash": "{{vsaManifestHash}}",
              "classes": { {{classesJson}} }
            }
            """;
        var classMapBytes = System.Text.Encoding.UTF8.GetBytes(classMapJson);
        var classMapSha256 = Hash(classMapBytes);
        var candidateItemJson = $$"""
            {
              "id": "{{reviewItemId}}",
              "frame_path": "{{imageFileName}}",
              "category": "all_class_background_review",
              "status": "{{overrides.ReceiptCandidateStatus ?? "pending_review"}}",
              "source_sha256": "{{imageSha256}}"
            }
            """;
        var secondCandidateItemJson = $$"""
            {
              "id": "{{secondReviewItemId}}",
              "frame_path": "{{secondImageFileName}}",
              "category": "all_class_background_review",
              "status": "pending_review",
              "source_sha256": "{{secondImageSha256}}"
            }
            """;
        var candidateItemsJson = overrides.IncludeSecondImage
            ? $"{candidateItemJson},{secondCandidateItemJson}"
            : candidateItemJson;
        var candidatesJson = $"[{candidateItemsJson}]";
        var candidatesBytes = System.Text.Encoding.UTF8.GetBytes(candidatesJson);
        var candidatesSha256 = Hash(candidatesBytes);
        var queueItemId = overrides.InvalidQueueItemId ? "bcc-hn-invalid" : reviewItemId;
        var predictionJson = overrides.MissingPredictionTrigger
            ? """
              [{
                "model_id": "test-model",
                "predicted_bcc": false,
                "bcc_detection_count": 0,
                "max_bcc_confidence": null
              }]
              """
            : """
              [{
                "model_id": "test-model",
                "predicted_bcc": true,
                "bcc_detection_count": 1,
                "max_bcc_confidence": 0.75
              }]
              """;
        var queueItemJson = $$"""
            {
              "id": "{{queueItemId}}",
              "image_sha256": "{{imageSha256}}",
              "holding_key": "{{holdingKey}}",
              "physical_holding_key": "{{physicalHoldingKey}}",
              "source_ref": "{{sourceRef}}",
              "inspection_date": "{{inspectionDate}}",
              "size_bytes": {{imageBytes.LongLength}},
              "image_format": "png",
              "predictions": {{predictionJson}}
            }
            """;
        var secondQueueItemJson = $$"""
            {
              "id": "{{secondReviewItemId}}",
              "image_sha256": "{{secondImageSha256}}",
              "holding_key": "{{secondHoldingKey}}",
              "physical_holding_key": "{{secondPhysicalHoldingKey}}",
              "source_ref": "{{secondSourceRef}}",
              "inspection_date": "{{inspectionDate}}",
              "size_bytes": {{secondImageBytes.LongLength}},
              "image_format": "png",
              "predictions": {{predictionJson}}
            }
            """;
        var queueItemsJson = overrides.IncludeSecondImage
            ? $"{queueItemJson},{secondQueueItemJson}"
            : queueItemJson;
        var modelScopeJson = overrides.EmptyModelScope
            ? "[]"
            : $$"""
              [{
                "candidate_id": "test-model",
                "candidate_manifest_sha256": "{{new string('a', 64)}}",
                "weights_sha256": "{{new string('b', 64)}}",
                "dataset_plan_id": "{{new string('c', 64)}}",
                "dataset_manifest_sha256": "{{new string('4', 64)}}"
              }]
              """;
        var queueSemanticJson = $$"""
            {
              "schema_version": "1.0",
              "purpose": "bcc_hard_negative_review_queue",
              "pilot": "BCC_bogen",
              "role": "training_candidate_review",
              "class_map_version": 3,
              "class_map_sha256": "{{classMapSha256}}",
              "vsa_manifest_hash": "{{vsaManifestHash}}",
              "class_names": {{classNamesJson}},
              "protected_sets": {{protectedSetsJson}},
              "protection_snapshot": {{protectionSnapshotJson}},
              "model_scope": {{modelScopeJson}},
              "selection_rule": {
                "one_image_per_physical_holding": true,
                "requires_current_model_bcc_trigger": true,
                "review_target": "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
              },
              "sources": [],
              "items": [{{queueItemsJson}}]
            }
            """;
        var queueId = HashCanonicalJson(queueSemanticJson);
        var queueCount = overrides.IncludeSecondImage ? 2 : 1;
        var secondQueueHashJson = overrides.IncludeSecondImage
            ? $$"""
              ,
                "images/{{secondImageFileName}}": {
                  "sha256": "{{secondImageSha256}}",
                  "size_bytes": {{secondImageBytes.LongLength}}
                }
              """
            : string.Empty;
        var queueManifestJson = $$"""
            {
              "schema_version": "1.0",
              "purpose": "bcc_hard_negative_review_queue",
              "queue_id": "{{overrides.ReceiptQueueId ?? queueId}}",
              "pilot": "BCC_bogen",
              "role": "training_candidate_review",
              "created_utc": "2026-07-28T10:00:00Z",
              "frozen": true,
              "dataset_status": "review_incomplete",
              "warning": "NUR all_classes_clear DARF SPAETER ALS TRAININGSNEGATIV VEROEFFENTLICHT WERDEN",
              "review_target": "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse",
              "class_map_version": 3,
              "class_map_sha256": "{{classMapSha256}}",
              "vsa_manifest_hash": "{{vsaManifestHash}}",
              "class_names": {{classNamesJson}},
              "protected_sets": {{protectedSetsJson}},
              "protection_snapshot": {{protectionSnapshotJson}},
              "selection_rule": {
                "one_image_per_physical_holding": true,
                "requires_current_model_bcc_trigger": true,
                "reviewer_sees_model_signals": false
              },
              "sources": [],
              "candidates_count": {{queueCount}},
              "images_count": {{queueCount}},
              "holdings_count": {{queueCount}},
              "hash_algorithm": "sha256",
              "hashes_count": {{queueCount + 1}},
              "hashes": {
                "_candidates.json": {
                  "sha256": "{{candidatesSha256}}",
                  "size_bytes": {{candidatesBytes.LongLength}}
                },
                "images/{{imageFileName}}": {
                  "sha256": "{{imageSha256}}",
                  "size_bytes": {{imageBytes.LongLength}}
                }{{secondQueueHashJson}}
              },
              "semantic": {{queueSemanticJson}},
              "selection_receipt": {
                "models": {{modelScopeJson}},
                "items": [{{queueItemsJson}}]
              }{{(overrides.UnknownQueueReceiptField ? ", \"unknown_queue_field\": true" : string.Empty)}}
            }
            """;
        var queueManifestBytes = System.Text.Encoding.UTF8.GetBytes(queueManifestJson);
        var queueManifestSha256 = Hash(queueManifestBytes);
        var secondReviewDecisionJson = overrides.IncludeSecondImage
            ? $$"""
              ,
                "{{secondReviewItemId}}": {
                  "decision": "all_classes_clear",
                  "comment": "",
                  "reviewed_at_utc": "2026-07-28T11:00:00Z"
                }
              """
            : string.Empty;
        var reviewJson = $$"""
            {
              "schema_version": "1.0",
              "purpose": "bcc_hard_negative_review",
              "queue_id": "{{queueId}}",
              "queue_manifest_sha256": "{{queueManifestSha256}}",
              "candidates_sha256": "{{candidatesSha256}}",
              "class_map_sha256": "{{classMapSha256}}",
              "reviewer": "Test User",
              "updated_at_utc": "2026-07-28T11:00:00Z",
              "decisions": {
                "{{reviewItemId}}": {
                  "decision": "{{overrides.ReceiptReviewDecision ?? "all_classes_clear"}}",
                  "comment": "",
                  "reviewed_at_utc": "2026-07-28T11:00:00Z"
                }{{secondReviewDecisionJson}}
              }
            }
            """;
        var reviewBytes = System.Text.Encoding.UTF8.GetBytes(reviewJson);
        var reviewSha256 = Hash(reviewBytes);

        var semanticImageSha256 = overrides.ImageSha256 ?? imageSha256;
        var semanticFileName = overrides.FileName ?? imageFileName;
        var semanticHoldingKey = overrides.HoldingKey ?? holdingKey;
        var semanticPhysicalHoldingKey = overrides.PhysicalHoldingKey ?? physicalHoldingKey;
        var semanticSplit = overrides.Split ?? split;
        var semanticReviewItemId = overrides.ReviewItemId ?? reviewItemId;
        var semanticReviewDecision = overrides.ReviewDecision ?? reviewDecision;
        var semanticImageSize = overrides.SemanticImageSize ?? imageBytes.LongLength;
        var unknownImageField = overrides.UnknownImageField ? ", \"unknown_field\": true" : string.Empty;
        var semanticImage = $$"""
            {
              "id": "bcc-neg-{{semanticImageSha256}}",
              "file_name": "{{semanticFileName}}",
              "image_sha256": "{{semanticImageSha256}}",
              "size_bytes": {{semanticImageSize}},
              "image_format": "png",
              "holding_key": "{{semanticHoldingKey}}",
              "physical_holding_key": "{{semanticPhysicalHoldingKey}}",
              "split": "{{semanticSplit}}",
              "review_item_id": "{{semanticReviewItemId}}",
              "review_decision": "{{semanticReviewDecision}}",
              "source_ref": "{{sourceRef}}",
              "inspection_date": "{{inspectionDate}}"{{unknownImageField}}
            }
            """;
        var secondSemanticImage = $$"""
            {
              "id": "bcc-neg-{{secondImageSha256}}",
              "file_name": "{{secondImageFileName}}",
              "image_sha256": "{{secondImageSha256}}",
              "size_bytes": {{secondImageBytes.LongLength}},
              "image_format": "png",
              "holding_key": "{{secondHoldingKey}}",
              "physical_holding_key": "{{secondPhysicalHoldingKey}}",
              "split": "validation",
              "review_item_id": "{{secondReviewItemId}}",
              "review_decision": "all_classes_clear",
              "source_ref": "{{secondSourceRef}}",
              "inspection_date": "{{inspectionDate}}"
            }
            """;
        var semanticImages = overrides.DuplicateImage
            ? $"{semanticImage},{semanticImage}"
            : overrides.IncludeSecondImage
                ? $"{semanticImage},{secondSemanticImage}"
                : semanticImage;
        var imageCount = overrides.DuplicateImage || overrides.IncludeSecondImage ? 2 : 1;
        var validationCount = overrides.IncludeSecondImage
            ? 1
            : string.Equals(semanticSplit, "validation", StringComparison.Ordinal)
                ? imageCount
                : 0;
        var semanticJson = $$"""
            {
              "schema_version": "1.0",
              "purpose": "bcc_reviewed_negative_set",
              "pilot": "BCC_bogen",
              "role": "training_negative_set",
              "queue": {
                "queue_id": "{{overrides.QueueId ?? queueId}}",
                "queue_manifest_sha256": "{{overrides.QueueManifestSha256 ?? queueManifestSha256}}",
                "queue_manifest_receipt_path": "receipts/queue_manifest.json",
                "candidates_sha256": "{{overrides.CandidatesSha256 ?? candidatesSha256}}",
                "candidates_receipt_path": "receipts/queue_candidates.json"
              },
              "review": {
                "purpose": "bcc_hard_negative_review",
                "review_sha256": "{{overrides.ReviewSha256 ?? reviewSha256}}",
                "receipt_path": "receipts/review.json",
                "reviewed_images": {{queueCount}},
                "decision_counts": {
                  "all_classes_clear": {{queueCount}},
                  "mapped_object_visible": 0,
                  "exclude_uncertain": 0
                }
              },
              "class_map_version": {{overrides.ClassMapVersion ?? 3}},
              "class_map_sha256": "{{overrides.ClassMapSha256 ?? classMapSha256}}",
              "class_map_receipt_path": "receipts/class_map.json",
              "vsa_manifest_hash": "{{overrides.VsaManifestHash ?? vsaManifestHash}}",
              "class_names": {{classNamesJson}},
              "protected_sets": {{protectedSetsJson}},
              "protection_snapshot": {{protectionSnapshotJson}},
              "split_rule": {
                "name": "stable_rank_v1",
                "salt": "{{overrides.SplitSalt ?? "bcc-hard-negative-split-v1"}}",
                "one_image_per_physical_holding": true,
                "validation_count": {{validationCount}},
                "train_count": {{imageCount - validationCount}}
              },
              "images": [{{semanticImages}}]
            }
            """;
        var setId = HashCanonicalJson(semanticJson);
        var setRoot = Directory.CreateDirectory(Path.Combine(
            _root,
            "training",
            "negatives",
            "sets",
            $"bcc_hn_{setId[..12]}")).FullName;
        var imagesRoot = Directory.CreateDirectory(Path.Combine(setRoot, "images")).FullName;
        var receiptsRoot = Directory.CreateDirectory(Path.Combine(setRoot, "receipts")).FullName;
        var imagePath = Path.Combine(imagesRoot, imageFileName);
        File.WriteAllBytes(imagePath, imageBytes);
        if (overrides.IncludeSecondImage)
        {
            File.WriteAllBytes(
                Path.Combine(imagesRoot, secondImageFileName),
                secondImageBytes);
        }
        File.WriteAllBytes(Path.Combine(receiptsRoot, "queue_manifest.json"), queueManifestBytes);
        File.WriteAllBytes(Path.Combine(receiptsRoot, "queue_candidates.json"), candidatesBytes);
        File.WriteAllBytes(Path.Combine(receiptsRoot, "review.json"), reviewBytes);
        File.WriteAllBytes(Path.Combine(receiptsRoot, "class_map.json"), classMapBytes);
        var relativeImagePath = Path.GetRelativePath(_root, imagePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var relativeHashPath = $"images/{imageFileName}";
        var secondSetImageHashJson = overrides.IncludeSecondImage
            ? $$"""
              ,
                "images/{{secondImageFileName}}": {
                  "sha256": "{{secondImageSha256}}",
                  "size_bytes": {{secondImageBytes.LongLength}}
                }
              """
            : string.Empty;
        var receiptHashes = new[]
        {
            ("receipts/queue_manifest.json", queueManifestSha256, queueManifestBytes.LongLength),
            ("receipts/queue_candidates.json", candidatesSha256, candidatesBytes.LongLength),
            ("receipts/review.json", reviewSha256, reviewBytes.LongLength),
            ("receipts/class_map.json", classMapSha256, classMapBytes.LongLength)
        };
        var receiptHashJson = string.Join(
            ",",
            receiptHashes.Select(item => $$"""
                "{{item.Item1}}": {
                  "sha256": "{{item.Item2}}",
                  "size_bytes": {{item.Item3}}
                }
                """));
        var manifestJson = $$"""
            {
              "schema_version": "1.0",
              "purpose": "bcc_reviewed_negative_set",
              "set_id": "{{overrides.ManifestSetId ?? setId}}",
              "pilot": "BCC_bogen",
              "role": "training_negative_set",
              "created_utc": "2026-07-28T12:00:00Z",
              "frozen": true,
              "dataset_status": "ready_for_training",
              "hash_algorithm": "sha256",
              "images_count": {{imageCount}},
              "holdings_count": {{imageCount}},
              "hashes_count": {{(overrides.IncludeSecondImage ? 6 : 5)}},
              "hashes": {
                "{{relativeHashPath}}": {
                  "sha256": "{{overrides.HashEntrySha256 ?? imageSha256}}",
                  "size_bytes": {{overrides.HashEntrySize ?? imageBytes.LongLength}}
                }{{secondSetImageHashJson}},
                {{receiptHashJson}}
              },
              "semantic": {{semanticJson}}
            }
            """;
        if (overrides.DuplicateImageShaField)
        {
            manifestJson = manifestJson.Replace(
                $"\"image_sha256\": \"{semanticImageSha256}\"",
                $"\"image_sha256\": \"{semanticImageSha256}\", \"image_sha256\": \"{semanticImageSha256}\"",
                StringComparison.Ordinal);
        }
        if (overrides.UnknownManifestField)
        {
            manifestJson = manifestJson.Replace(
                "\"schema_version\": \"1.0\",",
                "\"schema_version\": \"1.0\", \"unknown_manifest_field\": true,",
                StringComparison.Ordinal);
        }

        var manifestPath = Path.Combine(setRoot, "_manifest.json");
        File.WriteAllText(manifestPath, manifestJson);
        return new BoundNegativeSet(
            setId,
            relativeImagePath,
            Hash(manifestPath),
            imageSha256,
            holdingKey,
            physicalHoldingKey,
            TrainingExportTarget.Train,
            queueId,
            queueManifestSha256,
            candidatesSha256,
            reviewSha256,
            classMapSha256,
            vsaManifestHash,
            reviewItemId,
            reviewDecision);
    }

    private static string BoundNegativeJson(
        BoundNegativeSet negativeSet,
        string? path = null,
        string? setId = null,
        string? manifestSha256 = null,
        string? holdingKey = null)
        => $$"""
           [
             {{BoundNegativeEntryJson(
                 negativeSet,
                 path,
                 setId,
                 manifestSha256,
                 holdingKey)}}
           ]
           """;

    private static string BoundNegativeJson(
        string imageSha256,
        string setId,
        string path,
        string manifestSha256,
        string holdingKey = "100-200")
        => BoundNegativeJson(
            new BoundNegativeSet(
                setId,
                path,
                manifestSha256,
                imageSha256,
                holdingKey,
                "100|200",
                TrainingExportTarget.Train,
                new string('b', 64),
                new string('4', 64),
                new string('c', 64),
                new string('3', 64),
                new string('5', 64),
                new string('d', 64),
                "bcc-hn-review-item",
                "all_classes_clear"));

    private static string BoundNegativeEntryJson(
        BoundNegativeSet negativeSet,
        string? path = null,
        string? setId = null,
        string? manifestSha256 = null,
        string? holdingKey = null)
        => $$"""
             {
               "path": "{{path ?? negativeSet.RelativeImagePath}}",
               "sha256": "{{negativeSet.ImageSha256}}",
               "split": "train",
               "source_type": "reviewed_negative_set",
               "holding_key": "{{holdingKey ?? negativeSet.HoldingKey}}",
               "physical_holding_key": "{{negativeSet.PhysicalHoldingKey}}",
               "set_id": "{{setId ?? negativeSet.SetId}}",
               "set_manifest_sha256": "{{manifestSha256 ?? negativeSet.ManifestSha256}}",
               "queue_id": "{{negativeSet.QueueId}}",
               "queue_manifest_sha256": "{{negativeSet.QueueManifestSha256}}",
               "candidates_sha256": "{{negativeSet.CandidatesSha256}}",
               "review_sha256": "{{negativeSet.ReviewSha256}}",
               "class_map_version": 3,
               "class_map_sha256": "{{negativeSet.ClassMapSha256}}",
               "vsa_manifest_hash": "{{negativeSet.VsaManifestHash}}",
               "review_item_id": "{{negativeSet.ReviewItemId}}",
               "review_decision": "{{negativeSet.ReviewDecision}}"
             }
             """;

    private static string ReplaceJsonString(
        string json,
        string field,
        string oldValue,
        string newValue)
        => json.Replace(
            $"\"{field}\": \"{oldValue}\"",
            $"\"{field}\": \"{newValue}\"",
            StringComparison.Ordinal);

    private static string Hash(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static byte[] CreatePngSignatureBytes(byte fill)
    {
        var bytes = Enumerable.Repeat(fill, 2048).ToArray();
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }
            .CopyTo(bytes, 0);
        return bytes;
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HashCanonicalJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                   }))
        {
            WriteCanonicalJson(writer, document.RootElement);
        }
        return Hash(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                             .EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unerwarteter JSON-Typ: {element.ValueKind}");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed record TestPaths(string RegistryPath, string SetRoot, string ManifestPath);

    private sealed record BoundNegativeSet(
        string SetId,
        string RelativeImagePath,
        string ManifestSha256,
        string ImageSha256,
        string HoldingKey,
        string PhysicalHoldingKey,
        TrainingExportTarget Split,
        string QueueId,
        string QueueManifestSha256,
        string CandidatesSha256,
        string ReviewSha256,
        string ClassMapSha256,
        string VsaManifestHash,
        string ReviewItemId,
        string ReviewDecision);

    private sealed record NegativeManifestOverrides(
        string? ManifestSetId = null,
        string? FileName = null,
        string? ImageSha256 = null,
        long? SemanticImageSize = null,
        string? HoldingKey = null,
        string? PhysicalHoldingKey = null,
        string? Split = null,
        string? QueueId = null,
        string? QueueManifestSha256 = null,
        string? CandidatesSha256 = null,
        string? ReviewSha256 = null,
        int? ClassMapVersion = null,
        string? ClassMapSha256 = null,
        string? VsaManifestHash = null,
        string? ReviewItemId = null,
        string? ReviewDecision = null,
        string? HashEntrySha256 = null,
        long? HashEntrySize = null,
        string? SplitSalt = null,
        string? ReceiptQueueId = null,
        string? ReceiptReviewDecision = null,
        int? ReceiptClassMapVersion = null,
        string? ReceiptCandidateStatus = null,
        bool UnknownQueueReceiptField = false,
        bool EmptyModelScope = false,
        bool MissingPredictionTrigger = false,
        bool InvalidQueueItemId = false,
        bool InvalidImageSignature = false,
        byte? ImageFill = null,
        bool IncludeSecondImage = false,
        bool DuplicateImage = false,
        bool UnknownImageField = false,
        bool DuplicateImageShaField = false,
        bool UnknownManifestField = false);
}
