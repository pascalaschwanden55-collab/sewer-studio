using System;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.QualityGate;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

/// <summary>
/// Logs Accept/Reject events and coordinates the persistent feedback-learning checkpoint.
/// The checkpoint lives in SQLite, so the 25-event threshold survives service recreation
/// and application restarts.
/// </summary>
public sealed class ValidationLogger
{
    private readonly SqliteConnection _conn;

    public ValidationLogger(SqliteConnection connection)
    {
        _conn = connection ?? throw new ArgumentNullException(nameof(connection));
        EnsureLearningStateSchema();
    }

    /// <summary>Log a validation event (user accepted or rejected an AI suggestion).</summary>
    public void Log(string vsaCode, string suggestedCode, string finalCode,
        bool wasCorrect, EvidenceVector? evidence)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ValidationLog (LogId, VsaCode, SuggestedCode, FinalCode, WasCorrect, EvidenceJson, CreatedUtc)
            VALUES (@id, @vsa, @suggested, @final, @correct, @evidence, @utc)
            """;
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@vsa", vsaCode ?? "");
        cmd.Parameters.AddWithValue("@suggested", suggestedCode ?? "");
        cmd.Parameters.AddWithValue("@final", finalCode ?? "");
        cmd.Parameters.AddWithValue("@correct", wasCorrect ? 1 : 0);
        cmd.Parameters.AddWithValue("@evidence", evidence is not null
            ? JsonSerializer.Serialize(evidence) : "{}");
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Total number of validation entries.</summary>
    public int GetTotalCount()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ValidationLog";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Count of validation entries for a specific damage category/code prefix.</summary>
    public int GetCountForCategory(string category)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ValidationLog WHERE VsaCode LIKE @cat || '%'";
        cmd.Parameters.AddWithValue("@cat", category ?? "");
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Claims the next relearn batch atomically. Only one connection/process can claim a
    /// given validation range. A stale Running claim is released after <paramref name="staleAfter"/>.
    /// </summary>
    public bool TryClaimRelearnBatch(
        int interval,
        out int claimedValidationCount,
        TimeSpan? staleAfter = null)
    {
        if (interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval));

        claimedValidationCount = GetTotalCount();
        var now = DateTime.UtcNow;
        var staleCutoff = now - (staleAfter ?? TimeSpan.FromMinutes(10));

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE FeedbackLearningState
            SET Status = 'Running',
                ClaimedValidationCount = @total,
                UpdatedUtc = @utc,
                LastError = NULL
            WHERE StateId = 1
              AND (@total - LastCompletedValidationCount) >= @interval
              AND (Status <> 'Running' OR UpdatedUtc <= @stale)
            """;
        cmd.Parameters.AddWithValue("@total", claimedValidationCount);
        cmd.Parameters.AddWithValue("@interval", interval);
        cmd.Parameters.AddWithValue("@utc", now.ToString("o"));
        cmd.Parameters.AddWithValue("@stale", staleCutoff.ToString("o"));

        return cmd.ExecuteNonQuery() == 1;
    }

    public void CompleteRelearnBatch(int claimedValidationCount, string? activeWeightVersion)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE FeedbackLearningState
            SET LastCompletedValidationCount = MAX(LastCompletedValidationCount, @claimed),
                ClaimedValidationCount = NULL,
                Status = 'Idle',
                UpdatedUtc = @utc,
                LastError = NULL,
                ActiveWeightVersion = @version
            WHERE StateId = 1
            """;
        cmd.Parameters.AddWithValue("@claimed", claimedValidationCount);
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@version", (object?)activeWeightVersion ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void FailRelearnBatch(string error)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE FeedbackLearningState
            SET ClaimedValidationCount = NULL,
                Status = 'Failed',
                UpdatedUtc = @utc,
                LastError = @error
            WHERE StateId = 1
            """;
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@error", error ?? "Unbekannter Weight-Learning-Fehler");
        cmd.ExecuteNonQuery();
    }

    public FeedbackLearningState GetLearningState()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT LastCompletedValidationCount, ClaimedValidationCount, Status,
                   UpdatedUtc, LastError, ActiveWeightVersion
            FROM FeedbackLearningState
            WHERE StateId = 1
            """;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return FeedbackLearningState.Empty;

        var updated = DateTime.TryParse(reader.GetString(3), out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.MinValue;

        return new FeedbackLearningState(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.GetString(2),
            updated,
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private void EnsureLearningStateSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS FeedbackLearningState (
                StateId INTEGER PRIMARY KEY CHECK (StateId = 1),
                LastCompletedValidationCount INTEGER NOT NULL DEFAULT 0,
                ClaimedValidationCount INTEGER NULL,
                Status TEXT NOT NULL DEFAULT 'Idle',
                UpdatedUtc TEXT NOT NULL,
                LastError TEXT NULL,
                ActiveWeightVersion TEXT NULL
            );

            INSERT OR IGNORE INTO FeedbackLearningState
                (StateId, LastCompletedValidationCount, ClaimedValidationCount, Status, UpdatedUtc, LastError, ActiveWeightVersion)
            VALUES
                (1, 0, NULL, 'Idle', @utc, NULL, NULL);
            """;
        cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }
}

public sealed record FeedbackLearningState(
    int LastCompletedValidationCount,
    int? ClaimedValidationCount,
    string Status,
    DateTime UpdatedUtc,
    string? LastError,
    string? ActiveWeightVersion)
{
    public static FeedbackLearningState Empty { get; } =
        new(0, null, "Idle", DateTime.MinValue, null, null);
}
