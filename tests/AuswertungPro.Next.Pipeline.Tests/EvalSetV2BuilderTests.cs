using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalSetV2BuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio-EvalV2Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_ErzeugtFuenfGruppen_Hashes_UndLaesstV1Unveraendert()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates();
        var output = Path.Combine(v1, "v2");
        var v1ManifestBefore = HashFile(Path.Combine(v1, "_manifest.json"));
        var v1CandidatesBefore = HashFile(Path.Combine(v1, "_candidates.json"));
        var v1ImageBefore = HashFile(Path.Combine(v1, "images", "v1.png"));

        var result = EvalSetV2Builder.Build(new EvalSetV2BuildOptions(
            candidateFile,
            output,
            v1,
            MinimumHoldings: 5));

        Assert.False(result.DryRun);
        Assert.Equal(5, result.CandidateCount);
        Assert.Equal(5, result.HoldingCount);
        Assert.Equal(5, result.Groups.Count);
        Assert.All(result.Groups, group => Assert.Equal(1, group.Value));
        Assert.True(result.HashesCount >= 6);
        Assert.True(File.Exists(Path.Combine(output, "_manifest.json")));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "_manifest.json")));
        Assert.True(manifest.RootElement.GetProperty("frozen").GetBoolean());
        Assert.Equal(2, manifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(5, manifest.RootElement.GetProperty("groups").EnumerateObject().Count());

        Assert.Equal(v1ManifestBefore, HashFile(Path.Combine(v1, "_manifest.json")));
        Assert.Equal(v1CandidatesBefore, HashFile(Path.Combine(v1, "_candidates.json")));
        Assert.Equal(v1ImageBefore, HashFile(Path.Combine(v1, "images", "v1.png")));

        var protectedHashes = EvalContaminationGuard.LoadEvalImageHashes(v1);
        var v2FirstImage = Directory.EnumerateFiles(Path.Combine(output, "images")).First();
        Assert.Contains(EvalContaminationGuard.ComputeFileHash(v2FirstImage)!, protectedHashes);
        Assert.Contains("100-200", EvalContaminationGuard.LoadEvalHaltungKeys(v1));
        var benchmarkCases = EvalSetBenchmarkDataset.Load(output);
        Assert.Equal(5, benchmarkCases.Count);
        var damage = Assert.Single(benchmarkCases, item => item.Id == "damage");
        Assert.Equal("100-200", damage.HoldingKey);
        Assert.Equal(4, damage.ExpectedSeverity);
        Assert.Equal("damage-event-1", damage.EventId);
        Assert.Equal(0.0, damage.MeterStart);
        Assert.Equal(1.0, damage.MeterEnd);
    }

    [Fact]
    public void Build_BlockiertBildDasSchonInV1Liegt()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(
            firstImageOverride: Path.Combine(v1, "images", "v1.png"));

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1)));

        Assert.Contains("V1", error.Message);
        Assert.False(Directory.Exists(Path.Combine(v1, "v2")));
    }

    [Fact]
    public void DryRun_SchreibtKeineDatei()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates();
        var output = Path.Combine(v1, "v2");

        var result = EvalSetV2Builder.Build(new EvalSetV2BuildOptions(
            candidateFile,
            output,
            v1,
            DryRun: true,
            MinimumHoldings: 5));

        Assert.True(result.DryRun);
        Assert.Equal(5, result.CandidateCount);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Build_BlockiertZuWenigUnabhaengigeHaltungen()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates();

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1)));

        Assert.Contains("Haltungen 5/20", error.Message);
        Assert.False(Directory.Exists(Path.Combine(v1, "v2")));
    }

    [Theory]
    [InlineData(true, false, "Severity")]
    [InlineData(false, true, "Ereignis-ID")]
    public void Build_BlockiertSchadenOhnePflichtmetadaten(
        bool omitSeverity,
        bool omitEventId,
        string expectedMessage)
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(
            omitDamageSeverity: omitSeverity,
            omitDamageEventId: omitEventId);

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1, MinimumHoldings: 5)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Build_BlockiertUngueltigenMeterbereich()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(invalidDamageMeterRange: true);

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1, MinimumHoldings: 5)));

        Assert.Contains("Meterbereich", error.Message);
    }

    [Fact]
    public void Build_BlockiertWiderspruechlicheBereicheDesselbenEreignisses()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(inconsistentDamageEventRange: true);

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1, MinimumHoldings: 5)));

        Assert.Contains("widerspruechliche Metadaten", error.Message);
    }

    [Fact]
    public void Build_AkzeptiertGleicheEreignisIdInVerschiedenenHaltungen()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(sameEventIdInDifferentHolding: true);

        var result = EvalSetV2Builder.Build(new EvalSetV2BuildOptions(
            candidateFile,
            Path.Combine(v1, "v2"),
            v1,
            DryRun: true,
            MinimumHoldings: 5));

        Assert.Equal(6, result.CandidateCount);
        Assert.Equal(6, result.HoldingCount);
    }

    [Fact]
    public void Build_BlockiertLeereOptionaleEreignisId()
    {
        var v1 = CreateV1();
        var candidateFile = CreateV2Candidates(blankOptionalEventId: true);

        var error = Assert.Throws<InvalidDataException>(() => EvalSetV2Builder.Build(
            new EvalSetV2BuildOptions(candidateFile, Path.Combine(v1, "v2"), v1, MinimumHoldings: 5)));

        Assert.Contains("vorhandene Ereignis-ID", error.Message);
    }

    private string CreateV1()
    {
        var v1 = Path.Combine(_root, "eval_set");
        Directory.CreateDirectory(Path.Combine(v1, "images"));
        Directory.CreateDirectory(Path.Combine(v1, "labels"));
        File.WriteAllBytes(Path.Combine(v1, "images", "v1.png"), [1, 2, 3, 4]);
        File.WriteAllText(Path.Combine(v1, "_candidates.json"), """
            [
              {
                "id": "v1",
                "frame_path": "v1.png",
                "haltung_key": "900-901",
                "code_full": "BAB"
              }
            ]
            """);
        File.WriteAllText(Path.Combine(v1, "_manifest.json"), "{\"frozen\":true}");
        EvalSetManifestHasher.ComputeAndStoreHashes(v1);
        return v1;
    }

    private string CreateV2Candidates(
        string? firstImageOverride = null,
        bool omitDamageSeverity = false,
        bool omitDamageEventId = false,
        bool invalidDamageMeterRange = false,
        bool inconsistentDamageEventRange = false,
        bool sameEventIdInDifferentHolding = false,
        bool blankOptionalEventId = false)
    {
        var sources = Path.Combine(_root, "sources");
        Directory.CreateDirectory(sources);
        var definitions = new[]
        {
            new { Id = "damage", Code = "BAB", Group = "damage", Case = "100-200", Dn = 200, Material = "PVC", Quality = "good" },
            new { Id = "empty", Code = "LEER", Group = "empty", Case = "101-201", Dn = 300, Material = "Beton", Quality = "limited" },
            new { Id = "structure", Code = "BCD", Group = "structure", Case = "102-202", Dn = 400, Material = "Steinzeug", Quality = "poor" },
            new { Id = "condition", Code = "BDA", Group = "condition", Case = "103-203", Dn = 500, Material = "Beton", Quality = "good" },
            new { Id = "preroll", Code = "", Group = "pre_roll_data_board", Case = "104-204", Dn = 600, Material = "GFK", Quality = "limited" }
        };

        var rows = new List<object>();
        for (var i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            var source = i == 0 && firstImageOverride is not null
                ? firstImageOverride
                : Path.Combine(sources, definition.Id + ".png");
            if (!File.Exists(source))
                File.WriteAllBytes(source, [(byte)(20 + i), (byte)(30 + i), (byte)(40 + i)]);

            rows.Add(new
            {
                id = definition.Id,
                source_image_path = source,
                haltung_key = definition.Case,
                meter = i + 0.5,
                expected_severity = i == 0 && !omitDamageSeverity ? 4 : (int?)null,
                event_id = i == 0 && !omitDamageEventId
                    ? "damage-event-1"
                    : i == 1 && blankOptionalEventId
                        ? " "
                        : null,
                meter_start = i == 0 ? (invalidDamageMeterRange ? 1.0 : 0.0) : (double?)null,
                meter_end = i == 0 ? (invalidDamageMeterRange ? 0.0 : 1.0) : (double?)null,
                expected_code = definition.Code,
                group = definition.Group,
                dn_mm = definition.Dn,
                pipe_material = definition.Material,
                image_quality = definition.Quality,
                human_reviewed = true,
                reviewed_by = "Tester",
                reviewed_at_utc = "2026-07-11T12:00:00Z"
            });
        }

        if (inconsistentDamageEventRange || sameEventIdInDifferentHolding)
        {
            var source = Path.Combine(sources, "damage-second-frame.png");
            File.WriteAllBytes(source, [91, 92, 93]);
            rows.Add(new
            {
                id = "damage-second-frame",
                source_image_path = source,
                haltung_key = sameEventIdInDifferentHolding ? "200-300" : "100-200",
                meter = 1.5,
                expected_severity = 4,
                event_id = "damage-event-1",
                meter_start = 1.0,
                meter_end = 2.0,
                expected_code = "BAB",
                group = "damage",
                dn_mm = 200,
                pipe_material = "PVC",
                image_quality = "good",
                human_reviewed = true,
                reviewed_by = "Tester",
                reviewed_at_utc = "2026-07-11T12:00:00Z"
            });
        }

        var path = Path.Combine(_root, "v2_candidates.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, JsonSerializer.Serialize(rows));
        return path;
    }

    private static string HashFile(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
