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
    public static string DefaultDbPath => KnowledgeRoot.GetKnowledgeDbPath();

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
        // WAL-Mode explizit aktivieren: bessere Concurrency + Crash-Safety
        ExecuteNonQuery("PRAGMA journal_mode=WAL;");

        // Phase 2.1: FK-Constraints zur Laufzeit aktivieren — verhindert
        // Orphan-Embeddings ohne zugehoeriges Sample. SQLite ignoriert FK-
        // Definitionen sonst (Default ist OFF).
        ExecuteNonQuery("PRAGMA foreign_keys=ON;");

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

        // Phase 2.1: Embeddings-Tabelle mit FK + ModelVersion.
        // - FK auf Samples(SampleId) ON DELETE CASCADE: orphane Embeddings
        //   gibt es nicht mehr; beim Sample-Delete verschwindet das Embedding.
        // - ModelVersion: erlaubt Embedding-Migration bei Modell-Upgrade
        //   (z.B. nomic-embed-text v1 -> v1.5) zu erkennen. Default '' fuer
        //   bestehende Daten, neue Inserts koennen explizit setzen.
        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS Embeddings (
                SampleId     TEXT PRIMARY KEY,
                Model        TEXT NOT NULL,
                ModelVersion TEXT NOT NULL DEFAULT '',
                Vector       BLOB NOT NULL,
                CreatedAt    TEXT NOT NULL,
                FOREIGN KEY (SampleId) REFERENCES Samples(SampleId) ON DELETE CASCADE
            );
            """);

        // Migration fuer existierende DBs (vor Phase 2.1):
        //  a) ModelVersion-Spalte ergaenzen (idempotent via MigrateAddColumn).
        //  b) Wenn Embeddings-Tabelle keinen FK hat, defensive Migration:
        //     Orphans archivieren -> Tabelle umbauen mit FK -> Daten kopieren.
        MigrateAddColumn("Embeddings", "ModelVersion", "TEXT NOT NULL DEFAULT ''");
        MigrateEmbeddingsAddForeignKeyIfMissing();

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

        // Migration: Video-Selbsttraining — Kontextfelder fuer KB-Anreicherung
        MigrateAddColumn("Samples", "Rohrmaterial", "TEXT DEFAULT NULL");
        MigrateAddColumn("Samples", "NennweiteMm", "INTEGER DEFAULT NULL");
        MigrateAddColumn("Samples", "IsKorrigiert", "INTEGER NOT NULL DEFAULT 0");
        MigrateAddColumn("Samples", "QualityGateLevel", "TEXT DEFAULT NULL");

        // Index für schnelle Code-Suche
        ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_samples_code
                ON Samples(VsaCode);
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

    /// <summary>
    /// Phase 2.1: Defensive FK-Migration der Embeddings-Tabelle.
    /// Wenn die Tabelle aus einer Pre-2.1-DB stammt und keinen FK auf Samples
    /// hat, wird ein Tabellenumbau durchgefuehrt:
    ///  1) Orphan-Embeddings (SampleId ohne Sample) werden in Embeddings_orphan
    ///     archiviert (NICHT geloescht — kein Datenverlust).
    ///  2) Neue Embeddings-Tabelle mit FK + ModelVersion wird angelegt.
    ///  3) Saubere Daten werden kopiert. Orphans bleiben ausserhalb.
    ///  4) Alte Tabelle wird verworfen.
    /// Alles in einer Transaction — bei Fehler bleibt der Pre-Stand erhalten.
    /// </summary>
    private void MigrateEmbeddingsAddForeignKeyIfMissing()
    {
        if (HasForeignKey("Embeddings", "Samples")) return;

        // Embeddings-Tabelle hat keinen FK -> Umbau.
        // CREATE TABLE IF NOT EXISTS oben erzeugt nichts, weil sie schon existiert.
        // Daher hier echte Migration.

        long orphanCount;
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM Embeddings
                WHERE SampleId NOT IN (SELECT SampleId FROM Samples)
                """;
            orphanCount = Convert.ToInt64(cmd.ExecuteScalar());
        }

        using var tx = _connection.BeginTransaction();
        try
        {
            // 1) Orphans archivieren (idempotent via IF NOT EXISTS)
            ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS Embeddings_orphan (
                    SampleId     TEXT,
                    Model        TEXT,
                    ModelVersion TEXT NOT NULL DEFAULT '',
                    Vector       BLOB,
                    CreatedAt    TEXT,
                    ArchivedAt   TEXT NOT NULL
                );
                """);

            if (orphanCount > 0)
            {
                using var archive = _connection.CreateCommand();
                archive.CommandText = """
                    INSERT INTO Embeddings_orphan(SampleId, Model, ModelVersion, Vector, CreatedAt, ArchivedAt)
                    SELECT SampleId, Model,
                           COALESCE(ModelVersion, ''),
                           Vector, CreatedAt, $now
                    FROM Embeddings
                    WHERE SampleId NOT IN (SELECT SampleId FROM Samples)
                    """;
                archive.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                archive.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine(
                    $"[KnowledgeBaseContext] Phase 2.1 FK-Migration: {orphanCount} Orphan-Embeddings nach Embeddings_orphan archiviert.");
            }

            // 2) Tabelle umbauen: alt umbenennen, neu anlegen, kopieren, alt droppen
            ExecuteNonQuery("ALTER TABLE Embeddings RENAME TO Embeddings_old;");
            ExecuteNonQuery("""
                CREATE TABLE Embeddings (
                    SampleId     TEXT PRIMARY KEY,
                    Model        TEXT NOT NULL,
                    ModelVersion TEXT NOT NULL DEFAULT '',
                    Vector       BLOB NOT NULL,
                    CreatedAt    TEXT NOT NULL,
                    FOREIGN KEY (SampleId) REFERENCES Samples(SampleId) ON DELETE CASCADE
                );
                """);
            ExecuteNonQuery("""
                INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                SELECT SampleId, Model, COALESCE(ModelVersion, ''), Vector, CreatedAt
                FROM Embeddings_old
                WHERE SampleId IN (SELECT SampleId FROM Samples)
                """);
            ExecuteNonQuery("DROP TABLE Embeddings_old;");

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Prueft ob die Tabelle einen FK auf die referenzierte Tabelle hat.
    /// Nutzt SQLite-PRAGMA foreign_key_list.
    /// </summary>
    private bool HasForeignKey(string table, string referencedTable)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // Spalte 2 (Index 2) ist die referenzierte Tabelle
            var refTable = reader.IsDBNull(2) ? null : reader.GetString(2);
            if (string.Equals(refTable, referencedTable, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
