using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Logs Accept/Reject events to the ValidationLog SQLite table.
/// Used by WeightLearningService and FeedbackIngestionService for self-improvement.
/// </summary>
public sealed class ValidationLogger
{
    private readonly SqliteConnection _conn;

    public ValidationLogger(SqliteConnection connection)
    {
        _conn = connection;
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
}
