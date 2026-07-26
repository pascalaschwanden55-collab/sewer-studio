using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal static class LegacyPersonalGoldDatabaseReader
{
    public static IReadOnlyList<LegacyPersonalGoldCandidate> ReadMissing(
        string databasePath,
        string confirmedByUser,
        IReadOnlySet<string> knownSampleIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedByUser);
        ArgumentNullException.ThrowIfNull(knownSampleIds);

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString());
        connection.Open();

        var candidates = new List<LegacyPersonalGoldCandidate>();
        var sampleColumns = ReadColumns(connection, "Samples");
        var embeddingColumns = ReadColumns(connection, "Embeddings");
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                s.SampleId, s.CaseId, s.VsaCode, s.Beschreibung,
                s.MeterStart, s.MeterEnd, s.IsStreck, s.FramePath,
                s.ExportedUtc, s.VersionId, s.SourceType,
                {Optional(sampleColumns, "s", "Rohrmaterial", "NULL")},
                {Optional(sampleColumns, "s", "NennweiteMm", "NULL")},
                {Optional(sampleColumns, "s", "IsKorrigiert", "0")},
                {Optional(sampleColumns, "s", "QualityGateLevel", "NULL")},
                {Optional(sampleColumns, "s", "ContextSource", "NULL")},
                {Optional(sampleColumns, "s", "ContextUpdatedAt", "NULL")},
                {Optional(sampleColumns, "s", "ContextConfidence", "NULL")},
                {Optional(sampleColumns, "s", "RunId", "NULL")},
                s.HumanConfirmed,
                s.Corrected, s.ConfirmedByUser, s.ConfirmedAtUtc,
                e.Model,
                {Optional(embeddingColumns, "e", "ModelVersion", "''")},
                e.Vector, e.CreatedAt
            FROM Samples s
            LEFT JOIN Embeddings e ON e.SampleId = s.SampleId
            WHERE lower(trim(COALESCE(s.ConfirmedByUser, ''))) = lower(trim($user))
              AND s.HumanConfirmed = 1
              AND s.Corrected IS NOT NULL
              AND lower(replace(COALESCE(s.SourceType, ''), '_', '')) = 'manualcoding'
            ORDER BY s.ConfirmedAtUtc, s.SampleId;
            """;
        command.Parameters.AddWithValue("$user", confirmedByUser);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var sampleId = reader.GetString(0);
            if (knownSampleIds.Contains(sampleId))
                continue;
            if (reader.IsDBNull(23))
            {
                throw new InvalidDataException(
                    $"Persoenlich bestaetigtes Archiv-Sample '{sampleId}' besitzt kein Embedding.");
            }

            var corrected = reader.GetInt32(20) == 1;
            var confirmedAt = ParseRequiredUtc(reader.GetString(22), "ConfirmedAtUtc", sampleId);
            var exportedAt = ParseOptionalUtc(reader.GetString(8));
            var sample = new TrainingSample
            {
                SampleId = sampleId,
                CaseId = reader.GetString(1),
                Code = reader.GetString(2),
                Beschreibung = reader.GetString(3),
                MeterStart = reader.GetDouble(4),
                MeterEnd = reader.GetDouble(5),
                IsStreckenschaden = reader.GetInt32(6) == 1,
                FramePath = reader.GetString(7),
                Status = TrainingSampleStatus.Approved,
                ExportedUtc = exportedAt,
                Signature = TrainingSample.BuildCanonicalSignature(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDouble(4),
                    reader.GetDouble(5)),
                MatchLevel = corrected
                    ? MatchLevelNames.ReviewCorrected
                    : MatchLevelNames.ReviewApproved,
                SourceType = SourceTypeNames.ManualCoding,
                KbIndexState = KbIndexState.Indexed,
                TrainingEligible = true,
                HumanConfirmed = true,
                Corrected = corrected,
                ConfirmedByUser = reader.GetString(21),
                ConfirmedAtUtc = confirmedAt,
                QualityGateLevel = reader.IsDBNull(14) ? null : reader.GetString(14)
            };
            if (!ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, confirmedByUser))
            {
                throw new InvalidDataException(
                    $"Archiv-Sample '{sampleId}' erfuellt die persoenliche Goldregel nicht.");
            }

            candidates.Add(new LegacyPersonalGoldCandidate(
                sample,
                new LegacyKnowledgeSampleRow(
                    sampleId,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5),
                    reader.GetInt32(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? null : reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetString(16),
                    reader.IsDBNull(17) ? null : reader.GetDouble(17),
                    reader.IsDBNull(18) ? null : reader.GetString(18),
                    reader.GetInt32(19),
                    reader.GetInt32(20),
                    reader.GetString(21),
                    reader.GetString(22)),
                new LegacyEmbeddingRow(
                    sampleId,
                    reader.GetString(23),
                    reader.GetString(24),
                    (byte[])reader[25],
                    reader.GetString(26))));
        }

        var unique = candidates
            .Select(candidate => candidate.Sample.SampleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (unique.Count != candidates.Count)
            throw new InvalidDataException("Doppelte persoenliche ManualCoding-ID im Altarchiv.");
        return candidates;
    }

    private static HashSet<string> ReadColumns(
        SqliteConnection connection,
        string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static string Optional(
        IReadOnlySet<string> columns,
        string alias,
        string column,
        string fallback)
        => columns.Contains(column) ? $"{alias}.\"{column}\"" : fallback;

    private static DateTime ParseRequiredUtc(string value, string field, string sampleId)
        => ParseOptionalUtc(value)
           ?? throw new InvalidDataException(
               $"Archiv-Sample '{sampleId}' besitzt kein gueltiges {field}.");

    private static DateTime? ParseOptionalUtc(string value)
        => DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
}

internal sealed record LegacyPersonalGoldCandidate(
    TrainingSample Sample,
    LegacyKnowledgeSampleRow Knowledge,
    LegacyEmbeddingRow Embedding);

internal sealed record LegacyKnowledgeSampleRow(
    string SampleId,
    string CaseId,
    string VsaCode,
    string Beschreibung,
    double MeterStart,
    double MeterEnd,
    int IsStreck,
    string FramePath,
    string ExportedUtc,
    string VersionId,
    string SourceType,
    string? Rohrmaterial,
    int? NennweiteMm,
    int IsKorrigiert,
    string? QualityGateLevel,
    string? ContextSource,
    string? ContextUpdatedAt,
    double? ContextConfidence,
    string? RunId,
    int HumanConfirmed,
    int Corrected,
    string ConfirmedByUser,
    string ConfirmedAtUtc);

internal sealed record LegacyEmbeddingRow(
    string SampleId,
    string Model,
    string ModelVersion,
    byte[] Vector,
    string CreatedAt);
