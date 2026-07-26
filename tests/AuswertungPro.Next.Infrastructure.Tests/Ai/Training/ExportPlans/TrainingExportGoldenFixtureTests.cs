using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportGoldenFixtureTests : IDisposable
{
    private const string FixtureRelativePath = "tests/Fixtures/TrainingExport/ap03-export-golden-v1.json";
    private static readonly JsonSerializerOptions FixtureJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "training-export-golden-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalExecutor_schreibt_exakt_die_gemeinsame_Golden_Fixture()
    {
        var fixture = LoadFixture();
        var bundle = CreateBundle(fixture);
        var result = await new TrainingExportPlanLocalExecutor().ExecuteAsync(
            bundle,
            Path.Combine(_root, "datasets"));

        var actualFiles = ReadDatasetFiles(result.DatasetPath);
        Assert.Equal(fixture.Expected.PlanId, bundle.Plan.PlanId);
        Assert.Equal(
            fixture.Expected.Files.Select(file => file.Path).Order(StringComparer.Ordinal),
            actualFiles.Keys.Order(StringComparer.Ordinal));
        foreach (var expected in fixture.Expected.Files)
        {
            var actual = actualFiles[expected.Path];
            Assert.Equal(expected.Sha256, Hash(actual));
            Assert.Equal(Convert.FromBase64String(expected.Base64), actual);
        }
    }

    private TrainingExportPlanBundle CreateBundle(GoldenFixture fixture)
    {
        Directory.CreateDirectory(_root);
        var candidates = fixture.Candidates.Select(candidate =>
        {
            var imageBytes = Convert.FromBase64String(candidate.ImageBase64);
            var imageHash = Hash(imageBytes);
            var imagePath = Path.Combine(_root, $"source_{imageHash}{candidate.ImageExtension}");
            if (!File.Exists(imagePath))
                File.WriteAllBytes(imagePath, imageBytes);
            return new TrainingExportPlanCandidate(
                new TrainingExportSourceRef(ParseSourceType(candidate.SourceType), candidate.SourceId),
                imagePath,
                imageHash,
                candidate.ImageExtension,
                candidate.HoldingKey,
                candidate.ClassKey,
                candidate.ClassSourceKind,
                new TrainingExportBoundingBox(
                    candidate.BoundingBox.XCenter,
                    candidate.BoundingBox.YCenter,
                    candidate.BoundingBox.Width,
                    candidate.BoundingBox.Height),
                TrainingInventoryDisposition.TrainValCandidate);
        }).ToArray();

        var classMap = new TrainingYoloClassMapSnapshot(
            YoloDetectClassMapV3.Version,
            fixture.VsaManifestHash,
            YoloDetectClassMapV3.Classes,
            []);
        var registry = new TrainingExportRegistrySnapshot(
            TrainingExportRegistrySnapshot.CurrentSchemaVersion,
            fixture.RegistryHash,
            TrainingExportRegistryApprovalStatus.Approved,
            "AP0.3 Golden Test",
            DateTimeOffset.Parse(fixture.GeneratedUtc, CultureInfo.InvariantCulture),
            fixture.HoldingRoles.ToDictionary(
                item => item.HoldingKey,
                item => ParseHoldingRole(item.Role),
                StringComparer.OrdinalIgnoreCase),
            fixture.ProtectedSets.Select(item => new TrainingExportProtectedSetReference(
                item.SetId,
                ParseProtectedSetRole(item.Role),
                item.ManifestSha256)).ToArray());

        return new TrainingExportPlanService().CreatePlan(new TrainingExportPlanRequest(
            candidates,
            classMap,
            registry,
            fixture.InventoryRunId,
            fixture.SourceSnapshotHashes,
            EvaluationProtectionComplete: true,
            ProtectedImageHashes: new HashSet<string>(),
            ProtectedHoldingKeys: new HashSet<string>(),
            DateTimeOffset.Parse(fixture.GeneratedUtc, CultureInfo.InvariantCulture)));
    }

    private static GoldenFixture LoadFixture()
    {
        var path = Path.Combine(FindProjectRoot(), FixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return JsonSerializer.Deserialize<GoldenFixture>(File.ReadAllBytes(path), FixtureJson)
               ?? throw new InvalidOperationException("Golden-Fixture konnte nicht gelesen werden.");
    }

    private static string FindProjectRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "sidecar")))
            {
                return current.FullName;
            }
        }
        throw new InvalidOperationException("SewerStudio-Projektwurzel wurde nicht gefunden.");
    }

    private static IReadOnlyDictionary<string, byte[]> ReadDatasetFiles(string datasetPath)
        => Directory.EnumerateFiles(datasetPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(datasetPath, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static TrainingExportSourceType ParseSourceType(string value)
        => value switch
        {
            "teacher_annotation" => TrainingExportSourceType.TeacherAnnotation,
            "training_sample" => TrainingExportSourceType.TrainingSample,
            _ => throw new InvalidOperationException($"Unbekannter Fixture-Quelltyp: {value}")
        };

    private static TrainingExportHoldingRole ParseHoldingRole(string value)
        => value switch
        {
            "train" => TrainingExportHoldingRole.Train,
            "development_validation" => TrainingExportHoldingRole.DevelopmentValidation,
            _ => throw new InvalidOperationException($"Unbekannte Fixture-Haltungsrolle: {value}")
        };

    private static TrainingExportProtectedSetRole ParseProtectedSetRole(string value)
        => value switch
        {
            "development_validation" => TrainingExportProtectedSetRole.DevelopmentValidation,
            "acceptance" => TrainingExportProtectedSetRole.Acceptance,
            _ => throw new InvalidOperationException($"Unbekannte Fixture-Schutzrolle: {value}")
        };

    private static string Hash(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed record GoldenFixture(
        string SchemaVersion,
        string GeneratedUtc,
        string InventoryRunId,
        string VsaManifestHash,
        string RegistryHash,
        IReadOnlyDictionary<string, string> SourceSnapshotHashes,
        IReadOnlyList<GoldenHoldingRole> HoldingRoles,
        IReadOnlyList<GoldenProtectedSet> ProtectedSets,
        IReadOnlyList<GoldenCandidate> Candidates,
        GoldenExpected Expected);

    private sealed record GoldenHoldingRole(string HoldingKey, string Role);

    private sealed record GoldenProtectedSet(string SetId, string Role, string ManifestSha256);

    private sealed record GoldenBoundingBox(double XCenter, double YCenter, double Width, double Height);

    private sealed record GoldenCandidate(
        string SourceType,
        string SourceId,
        string ImageBase64,
        string ImageExtension,
        string HoldingKey,
        string ClassKey,
        string ClassSourceKind,
        GoldenBoundingBox BoundingBox);

    private sealed record GoldenExpected(string PlanId, IReadOnlyList<GoldenFile> Files);

    private sealed record GoldenFile(string Path, string Sha256, string Base64);
}
