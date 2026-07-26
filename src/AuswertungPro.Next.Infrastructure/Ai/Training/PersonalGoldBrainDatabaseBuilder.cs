using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Backup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Erstellt aus einem konsistenten SQLite-Schnappschuss eine Gold-only-Datenbank.
/// Alte Protokolle, Gewichte und Pruefentscheidungen werden nicht uebernommen.
/// </summary>
internal sealed class PersonalGoldBrainDatabaseBuilder(
    ISqliteSnapshotCopier sqliteSnapshots)
{
    private readonly ISqliteSnapshotCopier _sqliteSnapshots =
        sqliteSnapshots ?? throw new ArgumentNullException(nameof(sqliteSnapshots));

    public async Task<PersonalGoldBrainDatabaseResult> BuildAsync(
        string sourcePath,
        string targetPath,
        IReadOnlyList<TrainingSample> selected,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(selected);

        await _sqliteSnapshots
            .CreateVerifiedSnapshotAsync(sourcePath, targetPath, null, cancellationToken)
            .ConfigureAwait(false);

        var ids = selected
            .Select(sample => sample.SampleId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Count != selected.Count)
            throw new InvalidDataException("Leere oder doppelte Goldsample-ID.");

        using var connection = Open(targetPath, readOnly: false);
        var sourceSamples = Count(connection, "Samples");
        using (var transaction = connection.BeginTransaction())
        {
            Execute(connection, transaction, "CREATE TEMP TABLE KeepGoldSamples (SampleId TEXT PRIMARY KEY);");
            foreach (var id in ids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO KeepGoldSamples (SampleId) VALUES ($id);";
                insert.Parameters.AddWithValue("$id", id);
                insert.ExecuteNonQuery();
            }

            var matchedSamples = ScalarInt(
                connection,
                transaction,
                "SELECT COUNT(*) FROM Samples WHERE SampleId IN (SELECT SampleId FROM KeepGoldSamples);");
            var matchedEmbeddings = ScalarInt(
                connection,
                transaction,
                "SELECT COUNT(*) FROM Embeddings WHERE SampleId IN (SELECT SampleId FROM KeepGoldSamples);");
            if (matchedSamples != selected.Count || matchedEmbeddings != selected.Count)
            {
                throw new InvalidDataException(
                    $"Goldbestand ist in SQLite unvollstaendig: " +
                    $"Samples={matchedSamples}/{selected.Count}, " +
                    $"Embeddings={matchedEmbeddings}/{selected.Count}.");
            }

            Execute(
                connection,
                transaction,
                "DELETE FROM Embeddings WHERE SampleId NOT IN (SELECT SampleId FROM KeepGoldSamples);");
            Execute(
                connection,
                transaction,
                "DELETE FROM Samples WHERE SampleId NOT IN (SELECT SampleId FROM KeepGoldSamples);");
            foreach (var sample in selected)
            {
                using var updatePath = connection.CreateCommand();
                updatePath.Transaction = transaction;
                updatePath.CommandText =
                    "UPDATE Samples SET FramePath = $framePath WHERE SampleId = $sampleId;";
                updatePath.Parameters.AddWithValue("$framePath", sample.FramePath);
                updatePath.Parameters.AddWithValue("$sampleId", sample.SampleId);
                if (updatePath.ExecuteNonQuery() != 1)
                {
                    throw new InvalidDataException(
                        $"Goldbildpfad konnte in SQLite nicht aktualisiert werden: {sample.SampleId}");
                }
            }
            DeleteAllIfPresent(connection, transaction, "ValidationLog");
            DeleteAllIfPresent(connection, transaction, "CategoryWeights");
            DeleteAllIfPresent(connection, transaction, "SanierungDecisionLog");
            DeleteAllIfPresent(connection, transaction, "TrainingRuns");
            DeleteAllIfPresent(connection, transaction, "Versions");
            DeleteAllIfPresent(connection, transaction, "Embeddings_orphan");
            transaction.Commit();
        }

        using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            vacuum.ExecuteNonQuery();
        }

        ValidateSelectedRows(connection, selected);
        ValidateIntegrity(connection);
        return new PersonalGoldBrainDatabaseResult(
            sourceSamples,
            Count(connection, "Samples"),
            Count(connection, "Embeddings"));
    }

    public PersonalGoldBrainDatabaseResult Inspect(
        string path,
        IReadOnlyList<TrainingSample> selected)
    {
        using var connection = Open(path, readOnly: true);
        ValidateSelectedRows(connection, selected);
        ValidateIntegrity(connection);
        return new PersonalGoldBrainDatabaseResult(
            Count(connection, "Samples"),
            Count(connection, "Samples"),
            Count(connection, "Embeddings"));
    }

    private static void ValidateSelectedRows(
        SqliteConnection connection,
        IReadOnlyList<TrainingSample> selected)
    {
        var expected = selected
            .ToDictionary(
                sample => sample.SampleId,
                sample => sample,
                StringComparer.OrdinalIgnoreCase);
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd, IsStreck, " +
                "HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc, SourceType, " +
                "FramePath, QualityGateLevel " +
                "FROM Samples;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sampleId = reader.GetString(0);
                if (!expected.TryGetValue(sampleId, out var expectedSample))
                    throw new InvalidDataException($"Fremdes Sample in Gold-Datenbank: {sampleId}");

                EnsureEqual(sampleId, "CaseId", expectedSample.CaseId, reader.GetString(1));
                EnsureEqual(sampleId, "VsaCode", expectedSample.Code, reader.GetString(2));
                EnsureEqual(
                    sampleId,
                    "Beschreibung",
                    expectedSample.Beschreibung,
                    reader.GetString(3));
                EnsureEqual(sampleId, "MeterStart", expectedSample.MeterStart, reader.GetDouble(4));
                EnsureEqual(sampleId, "MeterEnd", expectedSample.MeterEnd, reader.GetDouble(5));
                EnsureEqual(
                    sampleId,
                    "IsStreck",
                    expectedSample.IsStreckenschaden,
                    reader.GetInt32(6) == 1);
                EnsureEqual(
                    sampleId,
                    "HumanConfirmed",
                    expectedSample.HumanConfirmed,
                    ReadNullableBool(reader, 7));
                EnsureEqual(
                    sampleId,
                    "Corrected",
                    expectedSample.Corrected,
                    ReadNullableBool(reader, 8));
                EnsureEqual(
                    sampleId,
                    "ConfirmedByUser",
                    expectedSample.ConfirmedByUser,
                    ReadNullableString(reader, 9));
                EnsureEqual(
                    sampleId,
                    "ConfirmedAtUtc",
                    expectedSample.ConfirmedAtUtc,
                    ReadNullableUtc(reader, 10, sampleId));
                EnsureEqual(
                    sampleId,
                    "SourceType",
                    expectedSample.SourceType,
                    ReadNullableString(reader, 11));
                EnsurePathEqual(
                    sampleId,
                    "FramePath",
                    expectedSample.FramePath,
                    ReadNullableString(reader, 12));
                EnsureOptionalTextEqual(
                    sampleId,
                    "QualityGateLevel",
                    expectedSample.QualityGateLevel,
                    ReadNullableString(reader, 13));

                actual.Add(sampleId);
            }
        }

        if (!actual.SetEquals(expected.Keys))
            throw new InvalidDataException("SQLite enthaelt nicht exakt den ausgewaehlten Goldbestand.");
        if (Count(connection, "Embeddings") != expected.Count)
            throw new InvalidDataException("Nicht jedes Goldsample besitzt genau eine Einbettung.");
        if (CountIfPresent(connection, "ValidationLog") != 0
            || CountIfPresent(connection, "CategoryWeights") != 0
            || CountIfPresent(connection, "SanierungDecisionLog") != 0
            || CountIfPresent(connection, "TrainingRuns") != 0
            || CountIfPresent(connection, "Versions") != 0
            || CountIfPresent(connection, "Embeddings_orphan") != 0)
        {
            throw new InvalidDataException("Die neue Gold-Datenbank enthaelt noch alte Laufzeitdaten.");
        }
    }

    private static bool? ReadNullableBool(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal) == 1;

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTime? ReadNullableUtc(
        SqliteDataReader reader,
        int ordinal,
        string sampleId)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var raw = reader.GetString(ordinal);
        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            throw new InvalidDataException(
                $"SQLite-Konflikt fuer Sample '{sampleId}', Feld 'ConfirmedAtUtc': " +
                "Zeitwert ist ungueltig.");
        }

        return parsed.ToUniversalTime();
    }

    private static void EnsureEqual<T>(
        string sampleId,
        string field,
        T expected,
        T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidDataException(
                $"SQLite-Konflikt fuer Sample '{sampleId}', Feld '{field}'.");
        }
    }

    private static void EnsurePathEqual(
        string sampleId,
        string field,
        string? expected,
        string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrWhiteSpace(actual)
            || !string.Equals(
                Path.GetFullPath(expected),
                Path.GetFullPath(actual),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite-Konflikt fuer Sample '{sampleId}', Feld '{field}'.");
        }
    }

    private static void EnsureOptionalTextEqual(
        string sampleId,
        string field,
        string? expected,
        string? actual)
    {
        var normalizedExpected = string.IsNullOrEmpty(expected) ? null : expected;
        var normalizedActual = string.IsNullOrEmpty(actual) ? null : actual;
        EnsureEqual(sampleId, field, normalizedExpected, normalizedActual);
    }

    private static void ValidateIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(command.ExecuteScalar());
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"SQLite-Inhaltspruefung fehlgeschlagen: {result ?? "keine Antwort"}");
    }

    private static SqliteConnection Open(string path, bool readOnly)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var timeout = connection.CreateCommand();
        timeout.CommandText = "PRAGMA busy_timeout=30000;";
        timeout.ExecuteNonQuery();
        return connection;
    }

    private static int Count(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CountIfPresent(SqliteConnection connection, string table)
        => TableExists(connection, table) ? Count(connection, table) : 0;

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static void DeleteAllIfPresent(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table)
    {
        if (TableExists(connection, table))
            Execute(connection, transaction, $"DELETE FROM \"{table}\";");
    }

    private static int ScalarInt(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

internal sealed record PersonalGoldBrainDatabaseResult(
    int SourceSamples,
    int Samples,
    int Embeddings);
