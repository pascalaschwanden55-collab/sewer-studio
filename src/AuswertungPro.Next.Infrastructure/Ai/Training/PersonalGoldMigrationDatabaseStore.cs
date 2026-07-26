using AuswertungPro.Next.Application.Ai.Training;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Gekapselter SQLite-Pfadzugriff fuer die Goldmigration.</summary>
internal static class PersonalGoldMigrationDatabaseStore
{
    public static Dictionary<string, string> ReadPaths(
        string databasePath,
        IReadOnlyList<TrainingSample> samples)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var connection = Open(databasePath, readOnly: true);
        foreach (var sample in samples)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT FramePath FROM Samples WHERE SampleId = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", sample.SampleId);
            var value = command.ExecuteScalar();
            if (value is string path)
                result.Add(sample.SampleId, path);
        }
        return result;
    }

    public static void UpdatePaths(
        string databasePath,
        IReadOnlyDictionary<string, string> targetPaths,
        IEnumerable<string> indexedSampleIds)
    {
        using var connection = Open(databasePath, readOnly: false);
        using var transaction = connection.BeginTransaction();
        var indexed = indexedSampleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (sampleId, targetPath) in targetPaths)
        {
            if (!indexed.Contains(sampleId))
                continue;
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Samples SET FramePath = $path WHERE SampleId = $id;";
            command.Parameters.AddWithValue("$path", targetPath);
            command.Parameters.AddWithValue("$id", sampleId);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidDataException($"KB-Pfad fuer Sample '{sampleId}' wurde nicht aktualisiert.");
        }
        transaction.Commit();
    }

    private static SqliteConnection Open(string databasePath, bool readOnly)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var timeout = connection.CreateCommand();
        timeout.CommandText = "PRAGMA busy_timeout=10000;";
        timeout.ExecuteNonQuery();
        return connection;
    }
}
