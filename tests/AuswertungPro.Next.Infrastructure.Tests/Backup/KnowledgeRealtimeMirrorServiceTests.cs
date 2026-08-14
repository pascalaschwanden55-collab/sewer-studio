using System.Collections.Concurrent;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class KnowledgeRealtimeMirrorServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-knowledge-mirror-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _createdLinks = new();

    [Fact]
    public async Task SynchronizeNowAsync_spiegelt_alle_Dateien_und_SQLite_konsistent()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(Path.Combine(source, "gold_frames"));
        await File.WriteAllTextAsync(Path.Combine(source, "gold_frames", "gold_1.jpg"), "gold");

        var databasePath = Path.Combine(source, "KnowledgeBase.db");
        await using var liveConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await liveConnection.OpenAsync();
        await using (var command = liveConnection.CreateCommand())
        {
            command.CommandText =
                "PRAGMA journal_mode=WAL;" +
                "CREATE TABLE samples(id INTEGER PRIMARY KEY, code TEXT NOT NULL);" +
                "INSERT INTO samples(code) VALUES ('BAB');";
            await command.ExecuteNonQueryAsync();
        }

        using var service = CreateService(source, target);
        await service.SynchronizeNowAsync();

        Assert.Equal(
            "gold",
            await File.ReadAllTextAsync(Path.Combine(target, "gold_frames", "gold_1.jpg")));
        Assert.True(File.Exists(Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName)));
        Assert.False(File.Exists(Path.Combine(target, "KnowledgeBase.db-wal")));
        Assert.False(File.Exists(Path.Combine(target, "KnowledgeBase.db-shm")));

        await using var mirroredConnection =
            new SqliteConnection(
                $"Data Source={Path.Combine(target, "KnowledgeBase.db")};Mode=ReadOnly;Pooling=False");
        await mirroredConnection.OpenAsync();
        await using var read = mirroredConnection.CreateCommand();
        read.CommandText = "SELECT code FROM samples WHERE id = 1;";
        Assert.Equal("BAB", Convert.ToString(await read.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task SynchronizeNowAsync_entfernt_verwaiste_Zieldatei_nur_mit_Marker()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "aktuell.txt"), "aktuell");

        using var service = CreateService(source, target);
        await service.SynchronizeNowAsync();
        await File.WriteAllTextAsync(Path.Combine(target, "verwaist.txt"), "alt");

        await service.SynchronizeNowAsync();

        Assert.False(File.Exists(Path.Combine(target, "verwaist.txt")));
        Assert.True(File.Exists(Path.Combine(target, "aktuell.txt")));
    }

    [Fact]
    public async Task SynchronizeNowAsync_uebernimmt_den_belegten_alten_E_Brain_Spiegel()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "training_samples.json"), "[]");
        await File.WriteAllTextAsync(
            Path.Combine(target, "_spiegel_log.txt"),
            $"Quelle : {source}{Path.DirectorySeparatorChar}{Environment.NewLine}" +
            $"Ziel : {target}{Path.DirectorySeparatorChar}{Environment.NewLine}");
        await File.WriteAllTextAsync(Path.Combine(target, "manifest.json"), "bestehend");

        using var service = CreateService(source, target);
        await service.SynchronizeNowAsync();

        Assert.True(File.Exists(Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName)));
        Assert.Equal("bestehend", await File.ReadAllTextAsync(Path.Combine(target, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(target, "_spiegel_log.txt")));
        Assert.True(File.Exists(Path.Combine(target, "training_samples.json")));
    }

    [Fact]
    public async Task SynchronizeNowAsync_uebernimmt_keinen_legacy_log_mit_nur_eingebetteten_Pfaden()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "gold.txt"), "gold");
        var foreign = Path.Combine(target, "privat.txt");
        await File.WriteAllTextAsync(foreign, "nicht loeschen");
        await File.WriteAllTextAsync(
            Path.Combine(target, "_spiegel_log.txt"),
            $"Kommentar : alte Quelle {source}{Path.DirectorySeparatorChar}{Environment.NewLine}" +
            $"Archiv : altes Ziel {target}{Path.DirectorySeparatorChar}{Environment.NewLine}");

        using var service = CreateService(source, target);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SynchronizeNowAsync());

        Assert.Contains("KI-Spiegel-Marker", error.Message);
        Assert.Equal("nicht loeschen", await File.ReadAllTextAsync(foreign));
        Assert.False(File.Exists(Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName)));
    }

    [Fact]
    public async Task SynchronizeNowAsync_fremder_Zielordner_ohne_Marker_bleibt_unveraendert()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "gold.txt"), "gold");
        var foreign = Path.Combine(target, "privat.txt");
        await File.WriteAllTextAsync(foreign, "nicht löschen");

        using var service = CreateService(source, target);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SynchronizeNowAsync());
        Assert.Contains("keinen gültigen KI-Spiegel-Marker", error.Message);
        Assert.Equal("nicht löschen", await File.ReadAllTextAsync(foreign));
        Assert.False(File.Exists(Path.Combine(target, "gold.txt")));
    }

    [Fact]
    public async Task SynchronizeNowAsync_Marker_mit_nur_aehnlichem_Quellpfad_wird_abgelehnt()
    {
        var source = Path.Combine(_root, "brain");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "gold.txt"), "gold");
        await File.WriteAllTextAsync(
            Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName),
            $"SewerStudio KI_BRAIN Echtzeit-Spiegel{Environment.NewLine}" +
            $"Source={source}-alt{Environment.NewLine}" +
            $"Target={target}{Environment.NewLine}");

        using var service = CreateService(source, target);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SynchronizeNowAsync());
        Assert.Contains("anderen Quelle", error.Message);
        Assert.False(File.Exists(Path.Combine(target, "gold.txt")));
    }

    [Fact]
    public async Task SynchronizeNowAsync_Marker_mit_falschem_Zielpfad_wird_abgelehnt()
    {
        var source = Path.Combine(_root, "brain-target-marker");
        var target = Path.Combine(_root, "target-marker");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "gold.txt"), "gold");
        await File.WriteAllTextAsync(
            Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName),
            $"SewerStudio KI_BRAIN Echtzeit-Spiegel{Environment.NewLine}" +
            $"Source={source}{Environment.NewLine}" +
            $"Target={target}-alt{Environment.NewLine}");

        using var service = CreateService(source, target);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SynchronizeNowAsync());
        Assert.Contains("anderen Ziel", error.Message);
        Assert.False(File.Exists(Path.Combine(target, "gold.txt")));
    }

    [JunctionFact]
    public async Task SynchronizeNowAsync_Zielroot_als_Junction_schreibt_nichts_nach_aussen()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target-link");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(Path.Combine(source, "gold.txt"), "gold");
        CreateDirectoryLinkOrSkip(target, foreign);

        using var service = CreateService(source, target);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SynchronizeNowAsync());
        Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(foreign));
    }

    [Fact]
    public async Task Start_spiegelt_Erstellen_Aendern_und_Loeschen_laufend()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);

        using var service = CreateService(source, target, TimeSpan.FromMilliseconds(40));
        service.Start();
        await WaitUntilAsync(
            () => File.Exists(Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName)));

        var sourceFile = Path.Combine(source, "training_samples.json");
        var targetFile = Path.Combine(target, "training_samples.json");
        await File.WriteAllTextAsync(sourceFile, "eins");
        await WaitUntilAsync(
            () => File.Exists(targetFile) && File.ReadAllText(targetFile) == "eins");

        await File.WriteAllTextAsync(sourceFile, "zwei-und-neu");
        await WaitUntilAsync(
            () => File.Exists(targetFile) && File.ReadAllText(targetFile) == "zwei-und-neu");

        File.Delete(sourceFile);
        await WaitUntilAsync(() => !File.Exists(targetFile));
    }

    [Fact]
    public async Task Start_reservierte_Quelldatei_ueberschreibt_den_Zielmarker_nicht()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);

        using var service = CreateService(source, target, TimeSpan.FromMilliseconds(40));
        service.Start();
        var targetMarker = Path.Combine(target, KnowledgeRealtimeMirrorService.MarkerFileName);
        await WaitUntilAsync(() => File.Exists(targetMarker));
        var trustedMarker = await File.ReadAllTextAsync(targetMarker);

        await File.WriteAllTextAsync(
            Path.Combine(source, KnowledgeRealtimeMirrorService.MarkerFileName),
            "fremder Quellinhalt");
        await Task.Delay(300);

        Assert.Equal(trustedMarker, await File.ReadAllTextAsync(targetMarker));
    }

    [Fact]
    public void ReadSourceAttributes_Zugriffsfehler_ist_nicht_Datei_fehlt()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            KnowledgeRealtimeMirrorService.ReadSourceAttributes(
                Path.Combine(_root, "gesperrt.txt"),
                _ => throw new UnauthorizedAccessException("gesperrt")));

        Assert.Contains("nicht sicher geprueft", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [JunctionFact]
    public async Task Start_Junction_im_Ziel_blockiert_inkrementelles_Schreiben_nach_aussen()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        var foreign = Path.Combine(_root, "fremd");
        var sourceFolder = Path.Combine(source, "training");
        var targetFolder = Path.Combine(target, "training");
        var sourceFile = Path.Combine(sourceFolder, "sample.json");
        var targetFile = Path.Combine(targetFolder, "sample.json");
        var foreignFile = Path.Combine(foreign, "sample.json");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(sourceFile, "quelle-alt");
        await File.WriteAllTextAsync(foreignFile, "fremd");

        var logger = new RecordingLogger();
        using var service = CreateService(source, target, TimeSpan.FromMilliseconds(40), logger);
        service.Start();
        await WaitUntilAsync(() => File.Exists(targetFile));

        File.Delete(targetFile);
        Directory.Delete(targetFolder);
        CreateDirectoryLinkOrSkip(targetFolder, foreign);
        await File.WriteAllTextAsync(sourceFile, "quelle-neu-und-laenger");

        await WaitUntilAsync(() =>
            Blockiert(logger)
            || File.ReadAllText(foreignFile) == "quelle-neu-und-laenger");

        Assert.Equal("fremd", await File.ReadAllTextAsync(foreignFile));
        Assert.True(Blockiert(logger));
    }

    [JunctionFact]
    public async Task Start_Junction_im_Ziel_blockiert_inkrementelles_Loeschen_nach_aussen()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        var foreign = Path.Combine(_root, "fremd");
        var sourceFolder = Path.Combine(source, "training");
        var targetFolder = Path.Combine(target, "training");
        var sourceFile = Path.Combine(sourceFolder, "sample.json");
        var targetFile = Path.Combine(targetFolder, "sample.json");
        var foreignFile = Path.Combine(foreign, "sample.json");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(sourceFile, "quelle");
        await File.WriteAllTextAsync(foreignFile, "fremd");

        var logger = new RecordingLogger();
        using var service = CreateService(source, target, TimeSpan.FromMilliseconds(40), logger);
        service.Start();
        await WaitUntilAsync(() => File.Exists(targetFile));

        File.Delete(targetFile);
        Directory.Delete(targetFolder);
        CreateDirectoryLinkOrSkip(targetFolder, foreign);
        File.Delete(sourceFile);

        await WaitUntilAsync(() =>
            Blockiert(logger)
            || !File.Exists(foreignFile));

        Assert.True(File.Exists(foreignFile));
        Assert.Equal("fremd", await File.ReadAllTextAsync(foreignFile));
        Assert.True(Blockiert(logger));
    }

    /// <summary>
    /// Der Wächter hat die Verknüpfung gemeldet — auf einem von zwei Wegen.
    ///
    /// Eine Änderung an der Quelldatei meldet Windows manchmal als Ereignis am
    /// enthaltenden Ordner. Dann verlangt der Dienst einen Vollabgleich
    /// (<c>QueuePath</c>), und dieser scheitert an der Verknüpfung. Sonst greift der
    /// inkrementelle Weg. Gemessen über acht Läufe: 7x Vollabgleich, 1x inkrementell —
    /// und in allen acht blieb die fremde Datei unberührt.
    ///
    /// Beide Meldungen belegen dasselbe: Es wurde nichts nach aussen geschrieben, und
    /// der Dienst hat es gesagt. Nur einen der beiden Wege zu erwarten, machte den Test
    /// unzuverlässig, ohne mehr zu prüfen.
    /// </summary>
    private static bool Blockiert(RecordingLogger logger)
        => logger.Contains("erneut versucht")
           || logger.Contains("Vollabgleich fehlgeschlagen");

    [Fact]
    public async Task Start_Elements_zuerst_fehlt_holt_nach_Wiederanschliessen_Alles_nach()
    {
        var source = Path.Combine(_root, "source");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "goldsample.jpg"), "gold");
        string? connectedTarget = null;

        using var service = new KnowledgeRealtimeMirrorService(
            source,
            () => connectedTarget,
            TimeSpan.FromMilliseconds(40),
            NullLogger.Instance);
        service.Start();
        await Task.Delay(150);
        Assert.False(Directory.Exists(target));

        connectedTarget = target;

        await WaitUntilAsync(
            () => File.Exists(Path.Combine(target, "goldsample.jpg")));
        Assert.Equal("gold", await File.ReadAllTextAsync(Path.Combine(target, "goldsample.jpg")));
    }

    private static KnowledgeRealtimeMirrorService CreateService(
        string source,
        string target,
        TimeSpan? interval = null,
        ILogger? logger = null)
        => new(
            source,
            () => target,
            interval ?? TimeSpan.FromMilliseconds(100),
            logger ?? NullLogger.Instance);

    private void CreateDirectoryLinkOrSkip(string link, string target)
    {
        JunctionTestSupport.CreateDirectoryLink(link, target);
        _createdLinks.Add(link);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < timeout)
        {
            if (condition())
                return;

            await Task.Delay(30);
        }

        Assert.Fail("Der erwartete Spiegelzustand wurde nicht rechtzeitig erreicht.");
    }

    public void Dispose()
    {
        foreach (var link in _createdLinks.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Testaufraeumen.
            }
        }

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _messages.Enqueue(formatter(state, exception));

        public bool Contains(string value)
            => _messages.Any(message =>
                message.Contains(value, StringComparison.OrdinalIgnoreCase));

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
