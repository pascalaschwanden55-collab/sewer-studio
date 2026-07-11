using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public enum EvalSetV2Group
{
    Damage,
    Empty,
    Structure,
    Condition,
    PreRollDataBoard
}

public enum EvalSetV2ImageQuality
{
    Good,
    Limited,
    Poor
}

public sealed class EvalSetV2Candidate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("source_image_path")]
    public string SourceImagePath { get; set; } = "";

    [JsonPropertyName("source_label_path")]
    public string? SourceLabelPath { get; set; }

    [JsonPropertyName("haltung_key")]
    public string CaseId { get; set; } = "";

    [JsonPropertyName("meter")]
    public double? Meter { get; set; }

    [JsonPropertyName("expected_code")]
    public string? ExpectedCode { get; set; }

    [JsonPropertyName("group")]
    public EvalSetV2Group Group { get; set; }

    [JsonPropertyName("dn_mm")]
    public int? DnMm { get; set; }

    [JsonPropertyName("pipe_material")]
    public string PipeMaterial { get; set; } = "";

    [JsonPropertyName("image_quality")]
    public EvalSetV2ImageQuality ImageQuality { get; set; }

    [JsonPropertyName("human_reviewed")]
    public bool HumanReviewed { get; set; }

    [JsonPropertyName("reviewed_by")]
    public string ReviewedBy { get; set; } = "";

    [JsonPropertyName("reviewed_at_utc")]
    public DateTimeOffset? ReviewedAtUtc { get; set; }
}

public sealed record EvalSetV2BuildOptions(
    string CandidateFile,
    string OutputRoot,
    string? ProtectedV1Root = null,
    bool DryRun = false,
    int MinimumHoldings = 20,
    int MinimumDnBands = 3,
    int MinimumMaterials = 2,
    bool RequireAllImageQualities = true);

public sealed record EvalSetV2BuildResult(
    string OutputRoot,
    int CandidateCount,
    int HoldingCount,
    IReadOnlyDictionary<string, int> Groups,
    IReadOnlyDictionary<string, int> DnBands,
    IReadOnlyDictionary<string, int> Materials,
    IReadOnlyDictionary<string, int> ImageQualities,
    int HashesCount,
    string? V1StableDigest,
    bool DryRun);

/// <summary>
/// Baut ein neues, eingefrorenes Eval-Set aus explizit menschlich geprueften Bildern.
/// V1 wird nur gelesen. Der Zielordner muss neu sein und wird atomar bereitgestellt.
/// </summary>
public static class EvalSetV2Builder
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static EvalSetV2BuildResult Build(EvalSetV2BuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var candidateFile = Path.GetFullPath(RequireValue(options.CandidateFile, nameof(options.CandidateFile)));
        var outputRoot = Path.GetFullPath(RequireValue(options.OutputRoot, nameof(options.OutputRoot)));
        var v1Root = string.IsNullOrWhiteSpace(options.ProtectedV1Root)
            ? null
            : Path.GetFullPath(options.ProtectedV1Root);

        if (!File.Exists(candidateFile))
            throw new FileNotFoundException("V2-Kandidatenliste nicht gefunden.", candidateFile);
        if (Directory.Exists(outputRoot) || File.Exists(outputRoot))
            throw new IOException($"V2-Ziel existiert bereits: {outputRoot}");

        EnsureOutputDoesNotReplaceV1(outputRoot, v1Root);

        var candidates = LoadCandidates(candidateFile);
        ValidateCandidates(candidates);

        var v1Hashes = v1Root is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : EvalContaminationGuard.LoadEvalImageHashes(v1Root)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prepared = Prepare(candidates, v1Hashes);
        var v1DigestBefore = ComputeStableDigest(v1Root);
        var distributions = BuildDistributions(prepared);
        ValidateDiversity(prepared, distributions, options);

        if (options.DryRun)
        {
            return CreateResult(
                outputRoot,
                prepared,
                distributions,
                hashesCount: 0,
                v1DigestBefore,
                dryRun: true);
        }

        var parent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidOperationException("V2-Ziel hat keinen Elternordner.");
        Directory.CreateDirectory(parent);
        var stagingRoot = Path.Combine(
            parent,
            $".{Path.GetFileName(outputRoot)}.building-{Guid.NewGuid():N}");

        try
        {
            WriteStagingSet(stagingRoot, prepared, distributions);
            var hashes = EvalSetManifestHasher.ComputeAndStoreHashes(stagingRoot);
            Directory.Move(stagingRoot, outputRoot);

            var v1DigestAfter = ComputeStableDigest(v1Root);
            if (!string.Equals(v1DigestBefore, v1DigestAfter, StringComparison.Ordinal))
                throw new InvalidOperationException("V1 wurde waehrend des V2-Baus veraendert.");

            return CreateResult(
                outputRoot,
                prepared,
                distributions,
                hashes.HashesCount,
                v1DigestAfter,
                dryRun: false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    public static IReadOnlyList<EvalSetV2Candidate> LoadCandidates(string candidateFile)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(candidateFile, Encoding.UTF8));
        var root = doc.RootElement;
        var json = root.ValueKind == JsonValueKind.Array
            ? root.GetRawText()
            : root.ValueKind == JsonValueKind.Object
              && root.TryGetProperty("candidates", out var nested)
              && nested.ValueKind == JsonValueKind.Array
                ? nested.GetRawText()
                : throw new InvalidDataException(
                    "V2-Kandidaten muessen ein JSON-Array oder ein Objekt mit 'candidates' sein.");

        return JsonSerializer.Deserialize<List<EvalSetV2Candidate>>(json, JsonOptions)
               ?? new List<EvalSetV2Candidate>();
    }

    private static IReadOnlyList<PreparedCandidate> Prepare(
        IReadOnlyList<EvalSetV2Candidate> candidates,
        IReadOnlySet<string> v1Hashes)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<PreparedCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var safeId = SanitizeId(candidate.Id);
            if (!ids.Add(safeId))
                throw new InvalidDataException($"Doppelte V2-ID: {candidate.Id}");

            var sourcePath = Path.GetFullPath(candidate.SourceImagePath);
            var hash = EvalContaminationGuard.ComputeFileHash(sourcePath)
                ?? throw new FileNotFoundException("V2-Quellbild nicht gefunden.", sourcePath);
            if (v1Hashes.Contains(hash))
                throw new InvalidDataException(
                    $"V2-Kandidat {candidate.Id} ist bereits Bestandteil von V1.");
            if (!hashes.Add(hash))
                throw new InvalidDataException(
                    $"Doppeltes Bild im V2-Kandidatenbestand: {candidate.Id}");

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var frameName = safeId + extension;
            var labelPath = string.IsNullOrWhiteSpace(candidate.SourceLabelPath)
                ? null
                : Path.GetFullPath(candidate.SourceLabelPath);
            if (labelPath is not null && !File.Exists(labelPath))
                throw new FileNotFoundException("V2-Quelllabel nicht gefunden.", labelPath);

            prepared.Add(new PreparedCandidate(
                candidate,
                sourcePath,
                labelPath,
                frameName,
                hash,
                NormalizeCode(candidate.ExpectedCode)));
        }

        return prepared;
    }

    private static void ValidateCandidates(IReadOnlyList<EvalSetV2Candidate> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidDataException("V2 enthaelt keine Kandidaten.");

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Id))
                throw new InvalidDataException("V2-Kandidat ohne ID.");
            if (string.IsNullOrWhiteSpace(candidate.SourceImagePath))
                throw new InvalidDataException($"V2-Kandidat {candidate.Id}: Bildpfad fehlt.");
            if (!ImageExtensions.Contains(Path.GetExtension(candidate.SourceImagePath)))
                throw new InvalidDataException($"V2-Kandidat {candidate.Id}: kein unterstuetztes Bild.");
            if (string.IsNullOrWhiteSpace(candidate.CaseId))
                throw new InvalidDataException($"V2-Kandidat {candidate.Id}: Haltung fehlt.");
            if (candidate.DnMm is null or <= 0)
                throw new InvalidDataException($"V2-Kandidat {candidate.Id}: DN fehlt.");
            if (string.IsNullOrWhiteSpace(candidate.PipeMaterial))
                throw new InvalidDataException($"V2-Kandidat {candidate.Id}: Rohrmaterial fehlt.");
            if (!candidate.HumanReviewed
                || string.IsNullOrWhiteSpace(candidate.ReviewedBy)
                || candidate.ReviewedAtUtc is null)
            {
                throw new InvalidDataException(
                    $"V2-Kandidat {candidate.Id}: menschliche Pruefung ist nicht vollstaendig belegt.");
            }

            ValidateGroupCode(candidate);
        }

        var represented = candidates.Select(c => c.Group).ToHashSet();
        var missingGroups = Enum.GetValues<EvalSetV2Group>()
            .Where(group => !represented.Contains(group))
            .ToArray();
        if (missingGroups.Length > 0)
        {
            throw new InvalidDataException(
                "V2 muss alle fuenf Gruppen enthalten. Fehlt: " + string.Join(", ", missingGroups));
        }
    }

    private static void ValidateGroupCode(EvalSetV2Candidate candidate)
    {
        var code = NormalizeCode(candidate.ExpectedCode);
        var valid = candidate.Group switch
        {
            EvalSetV2Group.Damage => code.StartsWith("BA", StringComparison.Ordinal)
                                     || code.StartsWith("BB", StringComparison.Ordinal),
            EvalSetV2Group.Empty => code == "LEER",
            EvalSetV2Group.Structure => code.StartsWith("BCC", StringComparison.Ordinal)
                                        || code.StartsWith("BCD", StringComparison.Ordinal)
                                        || code.StartsWith("BCE", StringComparison.Ordinal),
            EvalSetV2Group.Condition => code.StartsWith("BDA", StringComparison.Ordinal)
                                        || code.StartsWith("BDB", StringComparison.Ordinal)
                                        || code.StartsWith("BDC", StringComparison.Ordinal)
                                        || code.StartsWith("BDD", StringComparison.Ordinal),
            EvalSetV2Group.PreRollDataBoard => true,
            _ => false
        };

        if (!valid)
        {
            throw new InvalidDataException(
                $"V2-Kandidat {candidate.Id}: Code '{candidate.ExpectedCode}' passt nicht zur Gruppe {candidate.Group}.");
        }
    }

    private static void WriteStagingSet(
        string stagingRoot,
        IReadOnlyList<PreparedCandidate> candidates,
        Distributions distributions)
    {
        var imageRoot = Path.Combine(stagingRoot, "images");
        var labelRoot = Path.Combine(stagingRoot, "labels");
        Directory.CreateDirectory(imageRoot);
        Directory.CreateDirectory(labelRoot);

        foreach (var candidate in candidates)
        {
            File.Copy(
                candidate.SourceImagePath,
                Path.Combine(imageRoot, candidate.FrameName),
                overwrite: false);
            if (candidate.SourceLabelPath is not null)
            {
                File.Copy(
                    candidate.SourceLabelPath,
                    Path.Combine(labelRoot, Path.ChangeExtension(candidate.FrameName, ".txt")),
                    overwrite: false);
            }
        }

        var storedCandidates = candidates.Select(candidate => new
        {
            id = candidate.Source.Id.Trim(),
            frame_path = candidate.FrameName,
            haltung_key = candidate.Source.CaseId.Trim(),
            meter = candidate.Source.Meter,
            code_full = candidate.ExpectedCode,
            code_main = MainCode(candidate.ExpectedCode),
            korrektur = candidate.ExpectedCode,
            kategorie = ToGroupName(candidate.Source.Group),
            dn_mm = candidate.Source.DnMm,
            rohrmaterial = candidate.Source.PipeMaterial.Trim(),
            bildqualitaet = ToQualityName(candidate.Source.ImageQuality),
            human_reviewed = true,
            reviewed_by = candidate.Source.ReviewedBy.Trim(),
            reviewed_at_utc = candidate.Source.ReviewedAtUtc,
            source_sha256 = candidate.SourceHash
        }).ToList();

        AtomicTextFileWriter.WriteAllText(
            Path.Combine(stagingRoot, "_candidates.json"),
            JsonSerializer.Serialize(storedCandidates, JsonOptions));

        var manifest = new JsonObject
        {
            ["schema_version"] = 2,
            ["name"] = "SewerStudio Eval-Set V2",
            ["created_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["frozen"] = true,
            ["warning"] = "DIESES EVAL-SET DARF NICHT FUER TRAINING ODER FEW-SHOT VERWENDET WERDEN",
            ["total_candidates"] = candidates.Count,
            ["holdings_count"] = candidates.Select(c => NormalizeHolding(c.Source.CaseId)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ["groups"] = JsonSerializer.SerializeToNode(distributions.Groups, JsonOptions),
            ["dn_bands"] = JsonSerializer.SerializeToNode(distributions.DnBands, JsonOptions),
            ["materials"] = JsonSerializer.SerializeToNode(distributions.Materials, JsonOptions),
            ["image_qualities"] = JsonSerializer.SerializeToNode(distributions.ImageQualities, JsonOptions)
        };

        AtomicTextFileWriter.WriteAllText(
            Path.Combine(stagingRoot, "_manifest.json"),
            manifest.ToJsonString(JsonOptions));
    }

    private static Distributions BuildDistributions(IReadOnlyList<PreparedCandidate> candidates)
        => new(
            Groups: CountBy(candidates, c => ToGroupName(c.Source.Group)),
            DnBands: CountBy(candidates, c => ToDnBand(c.Source.DnMm!.Value)),
            Materials: CountBy(candidates, c => c.Source.PipeMaterial.Trim()),
            ImageQualities: CountBy(candidates, c => ToQualityName(c.Source.ImageQuality)));

    private static void ValidateDiversity(
        IReadOnlyList<PreparedCandidate> candidates,
        Distributions distributions,
        EvalSetV2BuildOptions options)
    {
        var holdings = candidates
            .Select(candidate => NormalizeHolding(candidate.Source.CaseId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var problems = new List<string>();

        if (holdings < options.MinimumHoldings)
            problems.Add($"Haltungen {holdings}/{options.MinimumHoldings}");
        if (distributions.DnBands.Count < options.MinimumDnBands)
            problems.Add($"DN-Bereiche {distributions.DnBands.Count}/{options.MinimumDnBands}");
        if (distributions.Materials.Count < options.MinimumMaterials)
            problems.Add($"Rohrmaterialien {distributions.Materials.Count}/{options.MinimumMaterials}");
        if (options.RequireAllImageQualities
            && distributions.ImageQualities.Count < Enum.GetValues<EvalSetV2ImageQuality>().Length)
        {
            problems.Add(
                $"Bildqualitaeten {distributions.ImageQualities.Count}/{Enum.GetValues<EvalSetV2ImageQuality>().Length}");
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                "V2 ist noch nicht breit genug fuer eine belastbare Auswertung: "
                + string.Join(", ", problems));
        }
    }

    private static IReadOnlyDictionary<string, int> CountBy(
        IEnumerable<PreparedCandidate> candidates,
        Func<PreparedCandidate, string> selector)
        => candidates
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    private static EvalSetV2BuildResult CreateResult(
        string outputRoot,
        IReadOnlyList<PreparedCandidate> candidates,
        Distributions distributions,
        int hashesCount,
        string? v1Digest,
        bool dryRun)
        => new(
            outputRoot,
            candidates.Count,
            candidates.Select(c => NormalizeHolding(c.Source.CaseId)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            distributions.Groups,
            distributions.DnBands,
            distributions.Materials,
            distributions.ImageQualities,
            hashesCount,
            v1Digest,
            dryRun);

    private static string? ComputeStableDigest(string? evalSetRoot)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot) || !Directory.Exists(evalSetRoot))
            return null;

        var hashes = EvalSetManifestHasher.ComputeHashes(evalSetRoot).Hashes;
        var canonical = string.Join(
            "\n",
            hashes.Select(entry => $"{entry.RelativePath}|{entry.SizeBytes}|{entry.Sha256Hex}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static void EnsureOutputDoesNotReplaceV1(string outputRoot, string? v1Root)
    {
        if (v1Root is null)
            return;

        if (PathsEqual(outputRoot, v1Root))
            throw new InvalidOperationException("V2 darf den V1-Ordner nicht ersetzen.");

        foreach (var protectedPath in new[]
                 {
                     Path.Combine(v1Root, "images"),
                     Path.Combine(v1Root, "labels")
                 })
        {
            if (IsInside(outputRoot, protectedPath))
                throw new InvalidOperationException("V2 darf nicht in V1/images oder V1/labels gebaut werden.");
        }
    }

    private static bool IsInside(string path, string parent)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string SanitizeId(string id)
    {
        var chars = id.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_')
            .ToArray();
        var value = new string(chars).Trim('.', '_');
        return value.Length > 0 ? value : throw new InvalidDataException("V2-ID ist nach Bereinigung leer.");
    }

    private static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";
        if (code.Trim().Equals("LEER", StringComparison.OrdinalIgnoreCase)
            || code.Trim().Equals("KEIN_SCHADEN", StringComparison.OrdinalIgnoreCase))
            return "LEER";
        return new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string MainCode(string? code)
    {
        var normalized = NormalizeCode(code);
        return normalized.Length <= 3 ? normalized : normalized[..3];
    }

    private static string NormalizeHolding(string value)
        => EvalContaminationGuard.NormalizeHaltungKey(value) ?? value.Trim();

    private static string ToGroupName(EvalSetV2Group group) => group switch
    {
        EvalSetV2Group.Damage => "damage",
        EvalSetV2Group.Empty => "empty",
        EvalSetV2Group.Structure => "structure",
        EvalSetV2Group.Condition => "condition",
        EvalSetV2Group.PreRollDataBoard => "pre_roll_data_board",
        _ => group.ToString().ToLowerInvariant()
    };

    private static string ToQualityName(EvalSetV2ImageQuality quality)
        => quality.ToString().ToLowerInvariant();

    private static string ToDnBand(int dn) => dn switch
    {
        <= 200 => "DN <= 200",
        <= 400 => "DN 201-400",
        <= 800 => "DN 401-800",
        _ => "DN > 800"
    };

    private static string RequireValue(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} fehlt.", name)
            : value;

    private sealed record PreparedCandidate(
        EvalSetV2Candidate Source,
        string SourceImagePath,
        string? SourceLabelPath,
        string FrameName,
        string SourceHash,
        string ExpectedCode);

    private sealed record Distributions(
        IReadOnlyDictionary<string, int> Groups,
        IReadOnlyDictionary<string, int> DnBands,
        IReadOnlyDictionary<string, int> Materials,
        IReadOnlyDictionary<string, int> ImageQualities);
}
