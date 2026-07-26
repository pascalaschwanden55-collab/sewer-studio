using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class PersonalGoldFrameMigrationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewer-personal-gold-migration-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigrateAsync_uebernimmt_nur_persoenliches_Gold_und_aktualisiert_JSON_und_KB()
    {
        Directory.CreateDirectory(_root);
        var sourceDir = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var personalFrame = Path.Combine(sourceDir, "riss.jpg");
        var foreignFrame = Path.Combine(sourceDir, "fremd.jpg");
        await File.WriteAllBytesAsync(personalFrame, [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(foreignFrame, [5, 6, 7, 8]);
        var personal = PersonalGold("gold-1", "Besitzer", personalFrame);
        var foreign = PersonalGold("gold-2", "Andere Person", foreignFrame);
        var samplesPath = Path.Combine(_root, "training_samples.json");
        var originalBytes = JsonSerializer.SerializeToUtf8Bytes(
            new List<TrainingSample> { personal, foreign },
            JsonDefaults.Indented);
        await File.WriteAllBytesAsync(samplesPath, originalBytes);
        var databasePath = Path.Combine(_root, "KnowledgeBase.db");
        CreateDatabaseSample(databasePath, personal);

        var result = await new PersonalGoldFrameMigrationService(
            codeLabelLookup: code => code == "BAB" ? "Riss" : null).MigrateAsync(
            new PersonalGoldFrameMigrationRequest(
                _root,
                "Besitzer",
                ["BAB", "BAF"],
                new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero)));

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.SelectedSamples);
        Assert.Equal(1, result.MigratedSamples);
        Assert.Equal(1, result.UniqueGoldFrames);
        Assert.Equal(1, result.FullGoldSamples);
        Assert.True(File.Exists(personalFrame));
        Assert.True(File.Exists(foreignFrame));
        Assert.True(File.Exists(result.InventoryPath));
        Assert.True(File.Exists(Path.Combine(result.AuditDirectory!, "training_samples.before.json")));
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(Path.Combine(result.AuditDirectory!, "training_samples.before.json")));

        var saved = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(samplesPath))!;
        var savedPersonal = saved.Single(sample => sample.SampleId == "gold-1");
        Assert.StartsWith(
            Path.Combine(_root, "gold_frames", "BAB - Riss"),
            savedPersonal.FramePath);
        Assert.True(File.Exists(savedPersonal.FramePath));
        Assert.Equal(foreignFrame, saved.Single(sample => sample.SampleId == "gold-2").FramePath);
        Assert.Equal(savedPersonal.FramePath, ReadDatabaseFramePath(databasePath, "gold-1"));

        var bab = Assert.Single(result.MainCodes, code => code.MainCode == "BAB");
        Assert.Equal(1, bab.FullGoldSamples);
        Assert.Equal(29, bab.NeededForMinimum);
        Assert.Equal("needs_more", bab.Status);
        var baf = Assert.Single(result.MainCodes, code => code.MainCode == "BAF");
        Assert.Equal(0, baf.FullGoldSamples);
        Assert.Equal("missing", baf.Status);
    }

    [Fact]
    public async Task MigrateAsync_bei_fehlendem_Bild_laesst_JSON_und_KB_unveraendert()
    {
        Directory.CreateDirectory(_root);
        var missingFrame = Path.Combine(_root, "fehlt.jpg");
        var sample = PersonalGold("gold-1", "Besitzer", missingFrame);
        var samplesPath = Path.Combine(_root, "training_samples.json");
        var originalBytes = JsonSerializer.SerializeToUtf8Bytes(
            new List<TrainingSample> { sample },
            JsonDefaults.Indented);
        await File.WriteAllBytesAsync(samplesPath, originalBytes);
        var databasePath = Path.Combine(_root, "KnowledgeBase.db");
        CreateDatabaseSample(databasePath, sample);

        var result = await new PersonalGoldFrameMigrationService().MigrateAsync(
            new PersonalGoldFrameMigrationRequest(
                _root,
                "Besitzer",
                ["BAB"],
                new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero)));

        Assert.False(result.Success);
        Assert.Contains("Goldbild", result.Error);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(samplesPath));
        Assert.Equal(missingFrame, ReadDatabaseFramePath(databasePath, "gold-1"));
        Assert.False(Directory.Exists(Path.Combine(_root, "gold_frames")));
    }

    [Fact]
    public async Task MigrateAsync_Prueflauf_schreibt_keine_Datei_und_aendert_keinen_Pfad()
    {
        Directory.CreateDirectory(_root);
        var framePath = Path.Combine(_root, "riss.jpg");
        await File.WriteAllBytesAsync(framePath, [1, 2, 3, 4]);
        var sample = PersonalGold("gold-1", "Besitzer", framePath);
        var samplesPath = Path.Combine(_root, "training_samples.json");
        var originalBytes = JsonSerializer.SerializeToUtf8Bytes(
            new List<TrainingSample> { sample },
            JsonDefaults.Indented);
        await File.WriteAllBytesAsync(samplesPath, originalBytes);
        var databasePath = Path.Combine(_root, "KnowledgeBase.db");
        CreateDatabaseSample(databasePath, sample);

        var result = await new PersonalGoldFrameMigrationService().MigrateAsync(
            new PersonalGoldFrameMigrationRequest(
                _root,
                "Besitzer",
                ["BAB"],
                new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero),
                DryRun: true));

        Assert.True(result.Success, result.Error);
        Assert.True(result.DryRun);
        Assert.Equal(1, result.SelectedSamples);
        Assert.Equal(0, result.MigratedSamples);
        Assert.Null(result.InventoryPath);
        Assert.Null(result.AuditDirectory);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(samplesPath));
        Assert.Equal(framePath, ReadDatabaseFramePath(databasePath, "gold-1"));
        Assert.False(Directory.Exists(Path.Combine(_root, "gold_frames")));
    }

    private static TrainingSample PersonalGold(string sampleId, string user, string framePath)
        => new()
        {
            SampleId = sampleId,
            CaseId = "100-200",
            Code = "BABBB",
            Beschreibung = "Riss quer im Scheitel",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = user,
            ConfirmedAtUtc = new DateTime(2026, 7, 23, 9, 0, 0, DateTimeKind.Utc),
            MatchLevel = MatchLevelNames.ReviewApproved,
            QualityGateLevel = "Green",
            FramePath = framePath,
            KbIndexState = KbIndexState.Indexed,
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.2,
            SamMaskRle = "0,4050,1,3949",
            SamMaskImageWidth = 100,
            SamMaskImageHeight = 80
        };

    private static void CreateDatabaseSample(string databasePath, TrainingSample sample)
    {
        using var context = new KnowledgeBaseContext(databasePath);
        using var command = context.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Samples(
                SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                IsStreck, FramePath, ExportedUtc, VersionId, SourceType,
                QualityGateLevel, HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc)
            VALUES(
                $id, $case, $code, $text, 0, 0,
                0, $frame, $utc, 'test', $source,
                'Green', 1, 0, $user, $utc);
            """;
        command.Parameters.AddWithValue("$id", sample.SampleId);
        command.Parameters.AddWithValue("$case", sample.CaseId);
        command.Parameters.AddWithValue("$code", sample.Code);
        command.Parameters.AddWithValue("$text", sample.Beschreibung);
        command.Parameters.AddWithValue("$frame", sample.FramePath);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$source", sample.SourceType!);
        command.Parameters.AddWithValue("$user", sample.ConfirmedByUser!);
        command.ExecuteNonQuery();
    }

    private static string ReadDatabaseFramePath(string databasePath, string sampleId)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FramePath FROM Samples WHERE SampleId = $id;";
        command.Parameters.AddWithValue("$id", sampleId);
        return Assert.IsType<string>(command.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
