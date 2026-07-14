using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class KnowledgeWalCheckpointTests : IDisposable
{
    private readonly string? _oldEnvironmentRoot;
    private readonly string? _oldSettingsRoot;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "KnowledgeWalCheckpointTests_" + Guid.NewGuid().ToString("N"));

    public KnowledgeWalCheckpointTests()
    {
        _oldEnvironmentRoot = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        _oldSettingsRoot = KnowledgeBasePaths.GetResolution().PersistedSettingsRoot;

        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, null);
        KnowledgeBasePaths.ConfigureSettingsRoot(_tempRoot);
    }

    [Fact]
    public void TryCheckpoint_laesst_eine_vorhandene_Wissensdatenbank_lesbar()
    {
        var dbPath = KnowledgeBasePaths.GetKnowledgeDbPath();
        using (var context = new KnowledgeBaseContext(dbPath))
        {
            using var command = context.Connection.CreateCommand();
            command.CommandText = "PRAGMA wal_autocheckpoint=0;";
            command.ExecuteNonQuery();
        }

        var exception = Record.Exception(KnowledgeWalCheckpoint.TryCheckpoint);

        Assert.Null(exception);
        Assert.True(File.Exists(dbPath));
        using var reopened = new KnowledgeBaseContext(dbPath);
        Assert.Equal(KnowledgeBaseContext.SchemaVersion, ReadUserVersion(reopened));
    }

    [Fact]
    public void Instanzdienst_verwendet_den_vorgegebenen_Datenbankpfad()
    {
        var dbPath = Path.Combine(_tempRoot, "wissen.db");
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(dbPath, "vorhanden");
        string? receivedPath = null;
        var service = new KnowledgeWalCheckpointService(
            dbPath,
            path => receivedPath = path,
            _ => throw new InvalidOperationException("Keine Warnung erwartet."));

        service.TryCheckpoint();

        Assert.Equal(dbPath, receivedPath);
    }

    [Fact]
    public void Instanzdienst_meldet_Fehler_ohne_die_Sicherung_abzubrechen()
    {
        var dbPath = Path.Combine(_tempRoot, "wissen.db");
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(dbPath, "vorhanden");
        string? warning = null;
        var service = new KnowledgeWalCheckpointService(
            dbPath,
            _ => throw new IOException("Datenbank gesperrt"),
            message => warning = message);

        var exception = Record.Exception(service.TryCheckpoint);

        Assert.Null(exception);
        Assert.Contains("WAL-Checkpoint fehlgeschlagen", warning);
        Assert.Contains("Datenbank gesperrt", warning);
    }

    [Fact]
    public void Instanzdienst_ueberspringt_eine_fehlende_Datenbank()
    {
        var calls = 0;
        var service = new KnowledgeWalCheckpointService(
            Path.Combine(_tempRoot, "fehlt.db"),
            _ => calls++,
            _ => throw new InvalidOperationException("Keine Warnung erwartet."));

        service.TryCheckpoint();

        Assert.Equal(0, calls);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            KnowledgeBasePaths.EnvironmentVariableName,
            _oldEnvironmentRoot);
        KnowledgeBasePaths.ConfigureSettingsRoot(_oldSettingsRoot);
        KnowledgeBasePaths.InvalidateCache();

        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }

    private static int ReadUserVersion(KnowledgeBaseContext context)
    {
        using var command = context.Connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
