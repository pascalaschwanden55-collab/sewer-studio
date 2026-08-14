using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Backup;
using AuswertungPro.Next.UI.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Vervollstaendigter KI-Wissen-Export: kompletter training/-Subbaum samt
/// Eval-Set-Root im Archiv und die Wissensdatenbank als gepruefter
/// SQLite-Online-Snapshot statt Rohkopie.
/// </summary>
public sealed class KnowledgeBackupTrainingExportTests
{
    [Fact]
    public async Task ExportAsync_sichert_den_Trainingsbaum_und_den_Eval_Set_Root()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        await WriteKnowledgeFileAsync(locations, "training/export_registry_v1.json", "{}");
        await WriteKnowledgeFileAsync(locations, "training/gold_standard/gold_brain_files_v1.json", "{}");
        await WriteKnowledgeFileAsync(locations, "training/gold_standard/gold_brain_separation_v1.json", "{}");
        await WriteKnowledgeFileAsync(locations, "training/gold_migrations/mig.json", "{}");
        await WriteKnowledgeFileAsync(locations, "training/datasets/ds1/manifest.json", "{}");
        await WriteKnowledgeFileAsync(locations, "training/reports/bericht.txt", "bericht");
        await WriteKnowledgeFileAsync(locations, "eval_set/_manifest.json", "{}");
        await WriteKnowledgeFileAsync(locations, "eval_set/schutz-set/_manifest.json", "{}");

        var zipPath = Path.Combine(temp.Path, "wissen.zip");
        var service = CreateService(locations);
        var result = await service.ExportAsync(zipPath);

        Assert.True(result.Success, result.Error);
        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains("knowledge/training/export_registry_v1.json", names);
        Assert.Contains("knowledge/training/gold_standard/gold_brain_files_v1.json", names);
        Assert.Contains("knowledge/training/gold_standard/gold_brain_separation_v1.json", names);
        Assert.Contains("knowledge/training/gold_migrations/mig.json", names);
        Assert.Contains("knowledge/training/datasets/ds1/manifest.json", names);
        Assert.Contains("knowledge/training/reports/bericht.txt", names);
        Assert.Contains("knowledge/eval_set/_manifest.json", names);
        Assert.Contains("knowledge/eval_set/schutz-set/_manifest.json", names);
    }

    [Fact]
    public async Task ExportAsync_schreibt_KnowledgeBase_als_geprueften_Snapshot_ohne_WAL_Begleiter()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        Directory.CreateDirectory(locations.KnowledgeRoot);
        var dbPath = Path.Combine(locations.KnowledgeRoot, "KnowledgeBase.db");

        // Laufende Datenbank im WAL-Modus: wie im Produktivbetrieb geoeffnet.
        using var liveConnection = CreateLiveKnowledgeDatabase(dbPath, rowCount: 3);

        var zipPath = Path.Combine(temp.Path, "wissen.zip");
        var service = CreateService(locations);
        var result = await service.ExportAsync(zipPath);

        Assert.True(result.Success, result.Error);
        using var archive = ZipFile.OpenRead(zipPath);
        var dbEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "knowledge/KnowledgeBase.db");
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName == "knowledge/KnowledgeBase.db-wal");
        Assert.DoesNotContain(
            archive.Entries,
            entry => entry.FullName == "knowledge/KnowledgeBase.db-shm");

        var extractedPath = Path.Combine(temp.Path, "extracted.db");
        await using (var source = dbEntry.Open())
        await using (var target = new FileStream(extractedPath, FileMode.CreateNew, FileAccess.Write))
            await source.CopyToAsync(target);

        AssertIntegrityAndRows(extractedPath, expectedRows: 3);
    }

    [Fact]
    public async Task ExportAsync_bricht_bei_Checkpoint_Fehler_mit_klarer_Meldung_ab()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        var zipPath = Path.Combine(temp.Path, "wissen.zip");
        var service = new KnowledgeBackupTransferService(
            locations,
            flushPendingSettings: () => { },
            flushSqliteWal: _ => throw new UserFacingException(
                "SQLite WAL-Checkpoint fehlgeschlagen; der Export wurde abgebrochen. " +
                "Technischer Hinweis: simuliert"));

        var result = await service.ExportAsync(zipPath);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("WAL-Checkpoint", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(zipPath));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".wissen.zip.*.tmp"));
    }

    [Fact]
    public async Task ImportAsync_stellt_Trainingsdateien_wieder_her_und_entfernt_veraltete_WAL_Begleiter()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        Directory.CreateDirectory(locations.KnowledgeRoot);

        // Archiv im neuen Format: Snapshot-Datenbank ohne WAL-/SHM-Begleiter.
        var sourceDbPath = Path.Combine(temp.Path, "quelle.db");
        CreateKnowledgeDatabase(sourceDbPath, rowCount: 2);
        var zipPath = Path.Combine(temp.Path, "wissen.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(sourceDbPath, "knowledge/KnowledgeBase.db");
            var registryEntry = archive.CreateEntry("knowledge/training/export_registry_v1.json");
            await using var stream = registryEntry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("{\"approved\":[]}");
        }

        // Veralteter lokaler WAL-Rest aus der bisher laufenden Datenbank.
        var staleWalPath = Path.Combine(locations.KnowledgeRoot, "KnowledgeBase.db-wal");
        await File.WriteAllBytesAsync(staleWalPath, [7, 7, 7]);

        var service = CreateService(locations);
        var result = await service.ImportAsync(zipPath);

        Assert.True(result.Success, result.Error);
        Assert.False(File.Exists(staleWalPath));
        Assert.Equal(
            "{\"approved\":[]}",
            await File.ReadAllTextAsync(
                Path.Combine(locations.KnowledgeRoot, "training", "export_registry_v1.json")));
        AssertIntegrityAndRows(
            Path.Combine(locations.KnowledgeRoot, "KnowledgeBase.db"),
            expectedRows: 2);
    }

    private static KnowledgeBackupTransferService CreateService(KnowledgeBackupLocations locations)
        => new(
            locations,
            flushPendingSettings: () => { },
            flushSqliteWal: _ => { });

    private static async Task WriteKnowledgeFileAsync(
        KnowledgeBackupLocations locations,
        string relativePath,
        string content)
    {
        var path = Path.Combine(
            locations.KnowledgeRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static SqliteConnection CreateLiveKnowledgeDatabase(string path, int rowCount)
    {
        var connection = OpenKnowledgeDatabase(path);
        using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            wal.ExecuteNonQuery();
        }

        CreateSchemaAndRows(connection, rowCount);
        return connection;
    }

    private static void CreateKnowledgeDatabase(string path, int rowCount)
    {
        using var connection = OpenKnowledgeDatabase(path);
        CreateSchemaAndRows(connection, rowCount);
    }

    private static SqliteConnection OpenKnowledgeDatabase(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void CreateSchemaAndRows(SqliteConnection connection, int rowCount)
    {
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE Samples (Id TEXT PRIMARY KEY, Code TEXT);";
            create.ExecuteNonQuery();
        }

        for (var index = 0; index < rowCount; index++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO Samples (Id, Code) VALUES ($id, 'BAB');";
            insert.Parameters.AddWithValue("$id", $"sample-{index}");
            insert.ExecuteNonQuery();
        }
    }

    private static void AssertIntegrityAndRows(string path, int expectedRows)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", Convert.ToString(integrity.ExecuteScalar()));
        }

        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Samples;";
        Assert.Equal(expectedRows, Convert.ToInt32(count.ExecuteScalar()));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-knowledge-backup-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public KnowledgeBackupLocations CreateLocations()
            => new(
                KnowledgeRoot: System.IO.Path.Combine(Path, "knowledge"),
                RoamingAuswertungPro: System.IO.Path.Combine(Path, "roaming-ap"),
                RoamingSewerStudio: System.IO.Path.Combine(Path, "roaming-ss"),
                LocalSewerStudio: System.IO.Path.Combine(Path, "local-ss"),
                TrainingCenterStatePath: System.IO.Path.Combine(Path, "training-center", "training_center.json"),
                TempRoot: System.IO.Path.Combine(Path, "temp"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
