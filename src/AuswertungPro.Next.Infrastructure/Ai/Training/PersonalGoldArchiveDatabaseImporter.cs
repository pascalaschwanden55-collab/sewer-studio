using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal static class PersonalGoldArchiveDatabaseImporter
{
    public static void Import(
        string activeDatabasePath,
        IReadOnlyList<LegacyPersonalGoldCandidate> candidates,
        IReadOnlyDictionary<string, string> targetPaths)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = activeDatabasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString());
        connection.Open();
        using var timeout = connection.CreateCommand();
        timeout.CommandText = "PRAGMA busy_timeout=30000;";
        timeout.ExecuteNonQuery();

        var sampleColumns = ReadColumns(connection, "Samples");
        var embeddingColumns = ReadColumns(connection, "Embeddings");
        using var transaction = connection.BeginTransaction();
        foreach (var candidate in candidates)
        {
            var row = candidate.Knowledge;
            using (var sample = connection.CreateCommand())
            {
                sample.Transaction = transaction;
                PrepareInsert(
                    sample,
                    "Samples",
                    sampleColumns,
                    [
                        new("SampleId", row.SampleId),
                        new("CaseId", row.CaseId),
                        new("VsaCode", row.VsaCode),
                        new("Beschreibung", row.Beschreibung),
                        new("MeterStart", row.MeterStart),
                        new("MeterEnd", row.MeterEnd),
                        new("IsStreck", row.IsStreck),
                        new("FramePath", targetPaths[row.SampleId]),
                        new("ExportedUtc", row.ExportedUtc),
                        new("VersionId", row.VersionId),
                        new("SourceType", row.SourceType),
                        new("Rohrmaterial", row.Rohrmaterial),
                        new("NennweiteMm", row.NennweiteMm),
                        new("IsKorrigiert", row.IsKorrigiert),
                        new("QualityGateLevel", row.QualityGateLevel),
                        new("ContextSource", row.ContextSource),
                        new("ContextUpdatedAt", row.ContextUpdatedAt),
                        new("ContextConfidence", row.ContextConfidence),
                        new("RunId", row.RunId),
                        new("HumanConfirmed", row.HumanConfirmed),
                        new("Corrected", row.Corrected),
                        new("ConfirmedByUser", row.ConfirmedByUser),
                        new("ConfirmedAtUtc", row.ConfirmedAtUtc)
                    ]);
                sample.ExecuteNonQuery();
            }

            var embeddingRow = candidate.Embedding;
            using var embedding = connection.CreateCommand();
            embedding.Transaction = transaction;
            PrepareInsert(
                embedding,
                "Embeddings",
                embeddingColumns,
                [
                    new("SampleId", embeddingRow.SampleId),
                    new("Model", embeddingRow.Model),
                    new("ModelVersion", embeddingRow.ModelVersion),
                    new("Vector", embeddingRow.Vector),
                    new("CreatedAt", embeddingRow.CreatedAt)
                ]);
            embedding.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void PrepareInsert(
        SqliteCommand command,
        string table,
        IReadOnlySet<string> availableColumns,
        IReadOnlyList<ColumnValue> values)
    {
        var selected = values
            .Where(value => availableColumns.Contains(value.Column))
            .ToArray();
        command.CommandText =
            $"INSERT INTO \"{table}\" (" +
            string.Join(", ", selected.Select(value => $"\"{value.Column}\"")) +
            ") VALUES (" +
            string.Join(", ", selected.Select((_, index) => $"$p{index}")) +
            ");";
        for (var index = 0; index < selected.Length; index++)
            command.Parameters.AddWithValue($"$p{index}", selected[index].Value ?? DBNull.Value);
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

    private sealed record ColumnValue(string Column, object? Value);
}
