using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using AuswertungPro.Next.UI.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.Monitoring;

/// <summary>
/// Version tracking for model + weights + calibration state.
/// Supports rollback by restoring a previous weights snapshot.
/// </summary>
public sealed class ModelRegistryService
{
    private readonly SqliteConnection _conn;

    public ModelRegistryService(SqliteConnection connection)
    {
        _conn = connection;
        EnsureSchema();
    }

    /// <summary>Register a new model version snapshot.</summary>
    public string RegisterVersion(
        string modelVersion,
        IReadOnlyList<CategoryWeights> weights,
        double eceBefore,
        double eceAfter,
        string? notes = null)
    {
        var versionId = Guid.NewGuid().ToString();
        var weightsJson = JsonSerializer.Serialize(weights);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ModelVersions (VersionId, ModelVersion, WeightsSnapshot, EceBefore, EceAfter, Notes, CreatedUtc)
            VALUES (@id, @model, @weights, @eceBefore, @eceAfter, @notes, @utc)
            """;
        cmd.Parameters.AddWithValue("@id", versionId);
        cmd.Parameters.AddWithValue("@model", modelVersion);
        cmd.Parameters.AddWithValue("@weights", weightsJson);
        cmd.Parameters.AddWithValue("@eceBefore", eceBefore);
        cmd.Parameters.AddWithValue("@eceAfter", eceAfter);
        cmd.Parameters.AddWithValue("@notes", notes ?? "");
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        return versionId;
    }

    /// <summary>Get all registered versions, newest first.</summary>
    public IReadOnlyList<ModelVersionInfo> GetVersions()
    {
        var list = new List<ModelVersionInfo>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT VersionId, ModelVersion, EceBefore, EceAfter, Notes, CreatedUtc FROM ModelVersions ORDER BY CreatedUtc DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ModelVersionInfo(
                VersionId: reader.GetString(0),
                ModelVersion: reader.GetString(1),
                EceBefore: reader.GetDouble(2),
                EceAfter: reader.GetDouble(3),
                Notes: reader.GetString(4),
                CreatedUtc: DateTime.Parse(reader.GetString(5))));
        }
        return list;
    }

    /// <summary>Rollback to a previous version by restoring its weights.</summary>
    public IReadOnlyList<CategoryWeights>? RestoreWeights(string versionId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT WeightsSnapshot FROM ModelVersions WHERE VersionId = @id";
        cmd.Parameters.AddWithValue("@id", versionId);
        var json = cmd.ExecuteScalar() as string;
        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<List<CategoryWeights>>(json);
        }
        catch
        {
            return null;
        }
    }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ModelVersions (
                VersionId      TEXT PRIMARY KEY,
                ModelVersion   TEXT NOT NULL DEFAULT '',
                WeightsSnapshot TEXT NOT NULL DEFAULT '[]',
                EceBefore      REAL NOT NULL DEFAULT 0,
                EceAfter       REAL NOT NULL DEFAULT 0,
                Notes          TEXT NOT NULL DEFAULT '',
                CreatedUtc     TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }
}

public sealed record ModelVersionInfo(
    string VersionId,
    string ModelVersion,
    double EceBefore,
    double EceAfter,
    string Notes,
    DateTime CreatedUtc
);
