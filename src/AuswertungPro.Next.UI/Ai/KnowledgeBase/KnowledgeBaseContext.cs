// AuswertungPro – KI Videoanalyse Modul
using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// SQLite-Datenbankkontext für die KI-Wissensdatenbank.
/// Verwaltet Verbindung und Schema-Migration.
///
/// Tabellen:
/// - Samples:    Approved Training Samples (Code, Text, Meter, CaseId, ...)
/// - Embeddings: Vektor-Embeddings pro Sample (als BLOB)
/// - Versions:   Export-Versionen (Version-ID, Timestamp, Anzahl)
/// </summary>
public sealed class KnowledgeBaseContext : IDisposable
{
    public static string DefaultDbPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuswertungPro", "KiVideoanalyse", "KnowledgeBase.db");

    private readonly SqliteConnection _connection;

    public KnowledgeBaseContext(string? dbPath = null)
    {
        var path = dbPath ?? DefaultDbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _connection = new SqliteConnection($"Data Source={path}");
        _connection.Open();
        EnsureSchema();
    }

    /// <summary>Gibt eine offene Verbindung zurück (für Kommandos).</summary>
    public SqliteConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();

    // ── Schema ────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Samples (
                SampleId     TEXT    PRIMARY KEY,
                CaseId       TEXT    NOT NULL,
                VsaCode      TEXT    NOT NULL,
                Beschreibung TEXT    NOT NULL DEFAULT '',
                MeterStart   REAL    NOT NULL DEFAULT 0,
                MeterEnd     REAL    NOT NULL DEFAULT 0,
                IsStreck     INTEGER NOT NULL DEFAULT 0,
                FramePath    TEXT    NOT NULL DEFAULT '',
                ExportedUtc  TEXT    NOT NULL,
                VersionId    TEXT    NOT NULL
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Embeddings (
                SampleId  TEXT PRIMARY KEY,
                Model     TEXT NOT NULL,
                Vector    BLOB NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Versions (
                VersionId   TEXT PRIMARY KEY,
                CreatedAt   TEXT NOT NULL,
                SampleCount INTEGER NOT NULL DEFAULT 0,
                Notes       TEXT NOT NULL DEFAULT ''
            );
            """);

        // Index für schnelle Code-Suche
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_samples_code
                ON Samples(VsaCode);
            """);
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
