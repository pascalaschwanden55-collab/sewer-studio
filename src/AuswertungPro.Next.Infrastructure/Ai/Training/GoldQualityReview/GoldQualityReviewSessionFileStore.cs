using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.GoldQualityReview;

/// <summary>
/// Speichert genau eine unveraenderliche aktuelle Goldpruefung je Bearbeiter unter
/// dem KnowledgeRoot. Ein vorhandenes oder defektes Manifest wird nie ueberschrieben.
/// </summary>
public sealed class GoldQualityReviewSessionFileStore : IGoldQualityReviewSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private readonly string _knowledgeRoot;
    private readonly Func<string, string?> _findReparsePoint;

    public GoldQualityReviewSessionFileStore(string knowledgeRoot)
        : this(knowledgeRoot, TrainingInventoryPaths.FindReparsePoint)
    {
    }

    internal GoldQualityReviewSessionFileStore(
        string knowledgeRoot,
        Func<string, string?> findReparsePoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        _knowledgeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(knowledgeRoot));
        _findReparsePoint = findReparsePoint
                            ?? throw new ArgumentNullException(nameof(findReparsePoint));
    }

    public GoldQualityReviewSession? LoadCurrent(string reviewer)
    {
        var path = ResolvePath(reviewer, createDirectory: false);
        if (!File.Exists(path))
            return null;

        try
        {
            RejectReparsePoint(path);
            var bytes = File.ReadAllBytes(path);
            var session = JsonSerializer.Deserialize<GoldQualityReviewSession>(bytes, JsonOptions)
                          ?? throw new InvalidDataException("Sitzungsmanifest ist leer.");
            Validate(session, reviewer);
            return session;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Goldpruefungs-Sitzung '{path}' ist nicht sicher lesbar: {ex.Message}",
                ex);
        }
    }

    public void SaveCurrent(GoldQualityReviewSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Validate(session, session.Reviewer);
        var path = ResolvePath(session.Reviewer, createDirectory: true);
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                "Eine Goldpruefungs-Sitzung ist bereits vorhanden und wird nicht ueberschrieben.");
        }

        var directory = Path.GetDirectoryName(path)!;
        RejectReparsePointChain(directory);
        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 81920,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            RejectReparsePointChain(directory);
            try
            {
                File.Move(temporary, path, overwrite: false);
            }
            catch (IOException ex) when (File.Exists(path))
            {
                throw new InvalidOperationException(
                    "Eine Goldpruefungs-Sitzung ist bereits vorhanden und wird nicht ueberschrieben.",
                    ex);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch
            {
                // Die eigentliche Speicherfehlermeldung darf nicht durch das
                // best-effort Aufraeumen einer Temp-Datei verdeckt werden.
            }
        }
    }

    public IReadOnlySet<string> LoadCompletedSampleIds(GoldQualityReviewSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Validate(session, session.Reviewer);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in session.Entries)
        {
            var path = ResolveCompletionPath(session, entry.SampleId, createDirectory: false);
            if (!File.Exists(path))
                continue;

            var receipt = ReadCompletionReceipt(path, session, entry.SampleId);
            completed.Add(receipt.SampleId);
        }

        return completed;
    }

    public void MarkCompleted(
        GoldQualityReviewSession session,
        string sampleId,
        DateTimeOffset completedUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        Validate(session, session.Reviewer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleId);
        var canonicalSampleId = session.Entries
            .SingleOrDefault(entry => string.Equals(
                entry.SampleId,
                sampleId.Trim(),
                StringComparison.OrdinalIgnoreCase))
            ?.SampleId
            ?? throw new InvalidOperationException(
                "Der Abschluss gehoert nicht zu dieser Goldpruefungs-Sitzung.");
        var path = ResolveCompletionPath(session, canonicalSampleId, createDirectory: true);
        if (File.Exists(path))
        {
            ReadCompletionReceipt(path, session, canonicalSampleId);
            return;
        }

        var receipt = new GoldQualityReviewCompletionReceipt(
            GoldQualityReviewCompletionReceipt.CurrentSchemaVersion,
            session.SessionId,
            session.Reviewer,
            canonicalSampleId,
            completedUtc.ToUniversalTime());
        try
        {
            WriteNewJsonFile(path, receipt);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Zwei gleichzeitige, identische Bestaetigungen duerfen den bereits
            // sicher geschriebenen Abschlussbeleg gemeinsam verwenden.
            ReadCompletionReceipt(path, session, canonicalSampleId);
        }
    }

    internal string GetCurrentPath(string reviewer)
        => ResolvePath(reviewer, createDirectory: false);

    internal string GetCompletionPath(GoldQualityReviewSession session, string sampleId)
        => ResolveCompletionPath(session, sampleId, createDirectory: false);

    private GoldQualityReviewCompletionReceipt ReadCompletionReceipt(
        string path,
        GoldQualityReviewSession session,
        string expectedSampleId)
    {
        try
        {
            RejectReparsePoint(path);
            var receipt = JsonSerializer.Deserialize<GoldQualityReviewCompletionReceipt>(
                              File.ReadAllBytes(path),
                              JsonOptions)
                          ?? throw new InvalidDataException("Abschlussbeleg ist leer.");
            if (!string.Equals(
                    receipt.SchemaVersion,
                    GoldQualityReviewCompletionReceipt.CurrentSchemaVersion,
                    StringComparison.Ordinal)
                || !string.Equals(receipt.SessionId, session.SessionId, StringComparison.Ordinal)
                || !string.Equals(receipt.Reviewer, session.Reviewer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.SampleId, expectedSampleId, StringComparison.OrdinalIgnoreCase)
                || receipt.CompletedUtc == default)
            {
                throw new InvalidDataException("Abschlussbeleg ist unvollstaendig oder ungueltig.");
            }

            return receipt;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Goldpruefungs-Abschlussbeleg '{path}' ist nicht sicher lesbar: {ex.Message}",
                ex);
        }
    }

    private string ResolveCompletionPath(
        GoldQualityReviewSession session,
        string sampleId,
        bool createDirectory)
    {
        var currentPath = ResolvePath(session.Reviewer, createDirectory);
        var directory = Path.GetDirectoryName(currentPath)!;
        var sessionHash = ShortHash(session.SessionId);
        var sampleHash = ShortHash(sampleId);
        return Path.Combine(directory, $"completed_{sessionHash}_{sampleHash}.json");
    }

    private static string ShortHash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(value.Trim())))[..16];

    private void WriteNewJsonFile<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)!;
        RejectReparsePointChain(directory);
        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 81920,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            RejectReparsePointChain(directory);
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch
            {
                // Best effort; die eigentliche Speicherfehlermeldung bleibt erhalten.
            }
        }
    }

    private string ResolvePath(string reviewer, bool createDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewer);
        var reviewRoot = Path.GetFullPath(Path.Combine(
            _knowledgeRoot,
            "training",
            "gold_quality_reviews"));
        EnsureUnderKnowledgeRoot(reviewRoot);
        RejectReparsePointChain(reviewRoot);
        if (createDirectory)
            Directory.CreateDirectory(reviewRoot);
        RejectReparsePointChain(reviewRoot);

        var reviewerHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(reviewer.Trim().ToUpperInvariant())))[..16];
        return Path.Combine(reviewRoot, $"current_{reviewerHash}.json");
    }

    private void EnsureUnderKnowledgeRoot(string path)
    {
        var relative = Path.GetRelativePath(_knowledgeRoot, path);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Goldpruefungs-Pfad liegt ausserhalb des KnowledgeRoot.");
        }
    }

    private void RejectReparsePointChain(string path)
    {
        if (_findReparsePoint(path) is { } reparsePoint)
            throw new InvalidDataException(
                $"Verknuepfte Pfade sind fuer Goldpruefungen nicht erlaubt: {reparsePoint}");
    }

    private static void RejectReparsePoint(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Verknuepfte Pfade sind fuer Goldpruefungen nicht erlaubt: {path}");
    }

    private static void Validate(GoldQualityReviewSession session, string reviewer)
    {
        if (!string.Equals(session.SchemaVersion, GoldQualityReviewSession.CurrentSchemaVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(session.SessionId)
            || string.IsNullOrWhiteSpace(session.Reviewer)
            || !string.Equals(session.Reviewer.Trim(), reviewer.Trim(), StringComparison.OrdinalIgnoreCase)
            || !IsSha256(session.RegistryHash)
            || !IsSha256(session.ProtectionFingerprint)
            || session.MainCodes is null
            || session.MainCodes.Count == 0
            || session.SamplesPerMainCode <= 0
            || session.Entries is null
            || session.Entries.Count != session.MainCodes.Count * session.SamplesPerMainCode
            || session.Entries.Any(entry =>
                entry is null
                || string.IsNullOrWhiteSpace(entry.SampleId)
                || string.IsNullOrWhiteSpace(entry.MainCode)
                || !IsSha256(entry.ImageSha256))
            || session.Entries.Select(entry => entry.SampleId)
                   .Distinct(StringComparer.OrdinalIgnoreCase).Count() != session.Entries.Count)
        {
            throw new InvalidDataException("Goldpruefungs-Sitzung ist unvollstaendig oder ungueltig.");
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 }
           && value.All(character => character is >= '0' and <= '9'
                                     or >= 'a' and <= 'f'
                                     or >= 'A' and <= 'F');

    private sealed record GoldQualityReviewCompletionReceipt(
        string SchemaVersion,
        string SessionId,
        string Reviewer,
        string SampleId,
        DateTimeOffset CompletedUtc)
    {
        public const string CurrentSchemaVersion = "1.0";
    }
}
