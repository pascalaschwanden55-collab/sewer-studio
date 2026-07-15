using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Fuehrt SQLite quick_check schreibgeschuetzt aus, ohne eine fehlende Datenbank anzulegen.
/// </summary>
public sealed class KnowledgeBaseHealthInspectionService : IKnowledgeBaseHealthInspector
{
    public KnowledgeBaseHealthInspection Inspect(string dbPath)
    {
        if (!File.Exists(dbPath))
            return new KnowledgeBaseHealthInspection(false, true, null);

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

            return messages.Count == 1
                   && string.Equals(messages[0], "ok", StringComparison.OrdinalIgnoreCase)
                ? new KnowledgeBaseHealthInspection(true, true, null)
                : new KnowledgeBaseHealthInspection(
                    true,
                    false,
                    messages.Count == 0
                        ? "SQLite lieferte kein Pruefergebnis."
                        : string.Join("; ", messages));
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new KnowledgeBaseHealthInspection(true, false, ex.Message);
        }
    }
}
