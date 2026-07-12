// AuswertungPro – KI Videoanalyse Modul
using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

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
    public const int SchemaVersion = 1;

    public static string DefaultDbPath => KnowledgeBasePaths.GetKnowledgeDbPath();

    private readonly SqliteConnection _connection;

    public KnowledgeBaseContext(string? dbPath = null)
    {
        var path = dbPath ?? DefaultDbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _connection = new SqliteConnection($"Data Source={path}");
        try
        {
            _connection.Open();
            EnsureSchema();
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    /// <summary>Gibt eine offene Verbindung zurück (für Kommandos).</summary>
    public SqliteConnection Connection => _connection;

    public void Dispose() => _connection.Dispose();

    // ── Schema ────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        // WAL-Mode explizit aktivieren: bessere Concurrency + Crash-Safety
        ExecuteNonQuery("PRAGMA journal_mode=WAL;");
        // Konkurrierende Zugriffe (Rebuild im TrainingCenter vs. Retrieval bei der Protokollgenerierung)
        // nicht sofort mit "database is locked" abbrechen, sondern bis zu 3s warten.
        ExecuteNonQuery("PRAGMA busy_timeout=3000;");

        // Vor jeder Schema-Aenderung pruefen: Eine neuere App koennte andere Tabellen
        // erwarten. In diesem Fall niemals versuchen, die Datei rueckwaerts anzupassen.
        var existingVersion = ReadUserVersion();
        if (existingVersion > SchemaVersion)
        {
            throw new InvalidDataException(
                $"Die Wissensdatenbank stammt aus einer neueren Programmversion " +
                $"(Schema {existingVersion}, unterstuetzt bis {SchemaVersion}). Sie wurde nicht veraendert.");
        }

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

        // Migration: SourceType-Spalte hinzufuegen (bestehende DBs upgraden)
        MigrateAddColumn("Samples", "SourceType", "TEXT NOT NULL DEFAULT ''");

        // Migration: QualityGateLevel ("Green"/"Yellow"/"Red") fuer qualitaetsbewusstes Retrieval.
        // Bestands-DBs wurden von aelteren Versionen bereits befuellt; hier nur sicherstellen, dass
        // die Spalte existiert, damit das SELECT in RetrievalService auf frischen DBs nicht bricht.
        MigrateAddColumn("Samples", "QualityGateLevel", "TEXT NOT NULL DEFAULT ''");

        // Migration (Audit Fix #3): Gold-Fund-Metadaten persistieren.
        MigrateAddColumn("Samples", "HumanConfirmed", "INTEGER");
        MigrateAddColumn("Samples", "Corrected", "INTEGER");
        MigrateAddColumn("Samples", "ConfirmedByUser", "TEXT");
        MigrateAddColumn("Samples", "ConfirmedAtUtc", "TEXT");

        // Index für schnelle Code-Suche
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_samples_code
                ON Samples(VsaCode);
            """);

        // Index für CheckModelConsistency (SELECT DISTINCT Model) – vermeidet Full-Scan
        // bei der Modell-Mismatch-Pruefung; additiv, aendert keine bestehende Query.
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_embeddings_model
                ON Embeddings(Model);
            """);

        // QualityGate: Per-category adaptive weights
        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS CategoryWeights (
                Category        TEXT PRIMARY KEY,
                WeightsJson     TEXT NOT NULL DEFAULT '{}',
                ValidationCount INTEGER NOT NULL DEFAULT 0,
                UpdatedUtc      TEXT NOT NULL
            );
            """);

        // QualityGate: Validation log for self-improving loop
        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS ValidationLog (
                LogId         TEXT PRIMARY KEY,
                VsaCode       TEXT NOT NULL DEFAULT '',
                SuggestedCode TEXT NOT NULL DEFAULT '',
                FinalCode     TEXT NOT NULL DEFAULT '',
                WasCorrect    INTEGER NOT NULL DEFAULT 0,
                EvidenceJson  TEXT NOT NULL DEFAULT '{}',
                CreatedUtc    TEXT NOT NULL
            );
            """);
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_validation_code
                ON ValidationLog(VsaCode);
            """);
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_validation_created
                ON ValidationLog(CreatedUtc);
            """);
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_validation_code_created
                ON ValidationLog(VsaCode, CreatedUtc DESC);
            """);
        EnsureUserVersion();
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Fuegt eine Spalte hinzu, falls sie noch nicht existiert.
    /// Sicheres Schema-Upgrade fuer bestehende Datenbanken.
    /// </summary>
    private void MigrateAddColumn(string table, string column, string definition)
    {
        try
        {
            ExecuteNonQuery($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Spalte existiert bereits — Migration nicht noetig
        }
    }

    private void EnsureUserVersion()
    {
        var current = ReadUserVersion();
        if (current > SchemaVersion)
        {
            throw new InvalidDataException(
                $"Die Wissensdatenbank stammt aus einer neueren Programmversion " +
                $"(Schema {current}, unterstuetzt bis {SchemaVersion}). Sie wurde nicht veraendert.");
        }
        if (current < SchemaVersion)
            ExecuteNonQuery($"PRAGMA user_version={SchemaVersion};");
    }

    private int ReadUserVersion()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
