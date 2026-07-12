using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public sealed record KnowledgeBaseHealthResult(bool IsHealthy, string? Error)
{
    public static readonly KnowledgeBaseHealthResult Ok = new(true, null);
}

/// <summary>Prueft eine vorhandene SQLite-KB schreibgeschuetzt vor der normalen Nutzung.</summary>
public static class KnowledgeBaseHealthChecker
{
    public static KnowledgeBaseHealthResult Check(string dbPath)
    {
        if (!File.Exists(dbPath))
            return KnowledgeBaseHealthResult.Ok;

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            using var reader = command.ExecuteReader();
            var messages = new List<string>();
            while (reader.Read())
                messages.Add(reader.GetString(0));

            return messages.Count == 1 && string.Equals(messages[0], "ok", StringComparison.OrdinalIgnoreCase)
                ? KnowledgeBaseHealthResult.Ok
                : new(false, messages.Count == 0 ? "SQLite lieferte kein Pruefergebnis." : string.Join("; ", messages));
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new(false, ex.Message);
        }
    }
}
