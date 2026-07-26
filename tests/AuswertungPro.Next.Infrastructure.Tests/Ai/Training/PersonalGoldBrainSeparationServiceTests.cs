using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class PersonalGoldBrainSeparationServiceTests
{
    [Fact]
    public async Task SeparateAsync_uebernimmt_nur_persoenliches_Gold_und_archiviert_den_Altstand()
    {
        using var fixture = await Fixture.CreateAsync();

        var result = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));

        Assert.True(result.Success, result.Error);
        Assert.False(result.DryRun);
        Assert.Equal(2, result.SourceSamples);
        Assert.Equal(1, result.PersonalGoldSamples);
        Assert.Equal(1, result.FullGoldSamples);
        Assert.Equal(3, result.SourceKnowledgeSamples);
        Assert.Equal(1, result.ActiveKnowledgeSamples);
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.True(Directory.Exists(fixture.LocalArchiveRoot));
        Assert.True(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalMirrorRoot));

        var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!;
        var active = Assert.Single(activeSamples);
        Assert.Equal("gold-1", active.SampleId);
        Assert.StartsWith(
            Path.Combine(fixture.KnowledgeRoot, "gold_frames"),
            active.FramePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(active.FramePath));
        Assert.False(File.Exists(Path.Combine(fixture.KnowledgeRoot, "old-runtime.json")));

        Assert.Equal(1, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "Samples"));
        Assert.Equal(1, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "Embeddings"));
        Assert.Equal(0, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "ValidationLog"));
        Assert.Equal(3, CountRows(Path.Combine(fixture.LocalArchiveRoot, "KnowledgeBase.db"), "Samples"));
        Assert.True(File.Exists(Path.Combine(
            fixture.LocalArchiveRoot,
            PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName)));
        Assert.True(File.Exists(Path.Combine(
            fixture.ExternalArchiveRoot,
            PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName)));
        Assert.True(File.Exists(Path.Combine(
            fixture.LocalArchiveRoot,
            "external_context",
            "protocol_training.json")));
        Assert.True(File.Exists(Path.Combine(
            fixture.ExternalArchiveRoot,
            "external_context",
            "protocol_training.json")));
        Assert.False(File.Exists(fixture.LegacyProtocolTrainingPath));
        Assert.Single(Directory.GetFiles(
            Path.GetDirectoryName(fixture.LegacyProtocolTrainingPath)!,
            "protocol_training.json.disconnected_*"));

        Assert.Empty(JsonSerializer.Deserialize<List<object>>(
            await File.ReadAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "teacher_annotations.json")))!);
        Assert.True(File.Exists(result.ReceiptPath));
        Assert.True(File.Exists(Path.Combine(
            fixture.KnowledgeRoot,
            "training",
            "gold_standard",
            "gold_brain_files_v1.json")));
    }

    [Fact]
    public async Task SeparateAsync_Prueflauf_aendert_keinen_Ordner()
    {
        using var fixture = await Fixture.CreateAsync();
        var sourceHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(Path.Combine(fixture.KnowledgeRoot, "training_samples.json"))));

        var result = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: true));

        Assert.True(result.Success, result.Error);
        Assert.True(result.DryRun);
        Assert.Equal(1, result.PersonalGoldSamples);
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.True(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.False(Directory.Exists(fixture.LocalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.True(File.Exists(fixture.LegacyProtocolTrainingPath));
        var currentHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(Path.Combine(fixture.KnowledgeRoot, "training_samples.json"))));
        Assert.Equal(sourceHash, currentHash);
    }

    [Fact]
    public async Task SeparateAsync_Prueflauf_mit_offenem_Journal_meldet_nur_und_rollt_nicht_zurueck()
    {
        using var fixture = await Fixture.CreateAsync();
        var interruptedService = new PersonalGoldBrainSeparationService(
            sqliteSnapshots: null,
            commitStepObserver: step =>
            {
                if (step == PersonalGoldBrainCommitStep.ExternalMirrorMoved)
                    throw new IOException("Simulierter Prozessabbruch.");
            });
        var interrupted = await interruptedService.SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.False(interrupted.Success);
        Assert.True(File.Exists(fixture.CommitJournalPath));
        Assert.True(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.True(Directory.Exists(fixture.StagingRoot));

        var dryRun = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: true));

        Assert.False(dryRun.Success);
        Assert.Contains("Commit-Journal", dryRun.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prueflauf", dryRun.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(fixture.CommitJournalPath));
        Assert.True(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.True(Directory.Exists(fixture.StagingRoot));
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.False(Directory.Exists(fixture.LocalArchiveRoot));
    }

    [Fact]
    public async Task SeparateAsync_blockiert_Protokollpfad_innerhalb_des_aktiven_Wissensordners_vor_dem_Commit()
    {
        using var fixture = await Fixture.CreateAsync();
        var overlappingProtocolPath = Path.Combine(
            fixture.KnowledgeRoot,
            "protocol_training.json");
        await File.WriteAllTextAsync(
            overlappingProtocolPath,
            "{\"Samples\":[{\"Code\":\"ALT\"}]}");
        var request = fixture.CreateRequest(dryRun: false) with
        {
            LegacyProtocolTrainingPath = overlappingProtocolPath
        };

        var result = await new PersonalGoldBrainSeparationService().SeparateAsync(request);

        Assert.False(result.Success);
        Assert.Contains("ueberlapp", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.CommitJournalPath));
        Assert.False(Directory.Exists(fixture.StagingRoot));
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.True(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.False(Directory.Exists(fixture.LocalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.True(File.Exists(overlappingProtocolPath));
    }

    [Theory]
    [InlineData("CaseId", "andere-haltung")]
    [InlineData("VsaCode", "BCA")]
    [InlineData("Beschreibung", "anderer Text")]
    [InlineData("MeterStart", 1.25)]
    [InlineData("MeterEnd", 2.50)]
    [InlineData("IsStreck", 1)]
    [InlineData("SourceType", "TeacherAnnotation")]
    [InlineData("HumanConfirmed", 0)]
    [InlineData("Corrected", 1)]
    [InlineData("ConfirmedByUser", "AnderePerson")]
    [InlineData("ConfirmedAtUtc", "2026-07-24T09:01:00.0000000Z")]
    [InlineData("QualityGateLevel", "Red")]
    public async Task SeparateAsync_stoppt_bei_fachlichem_JSON_SQLite_Konflikt(
        string column,
        object conflictingValue)
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.SetKnowledgeSampleValueInSourceAndMirror(
            "gold-1",
            column,
            conflictingValue);

        var result = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));

        Assert.False(result.Success);
        Assert.Contains(column, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.CommitJournalPath));
        Assert.False(Directory.Exists(fixture.StagingRoot));
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.True(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.False(Directory.Exists(fixture.LocalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalArchiveRoot));
    }

    [Theory]
    [InlineData((int)PersonalGoldBrainCommitStep.ExternalMirrorMoved)]
    [InlineData((int)PersonalGoldBrainCommitStep.LocalKnowledgeMoved)]
    [InlineData((int)PersonalGoldBrainCommitStep.ActiveKnowledgePublished)]
    public async Task SeparateAsync_nach_Prozessabbruch_rollt_zurueck_und_startet_neu(
        int interruptedAfterValue)
    {
        using var fixture = await Fixture.CreateAsync();
        var interruptedAfter = (PersonalGoldBrainCommitStep)interruptedAfterValue;
        var interruptedService = new PersonalGoldBrainSeparationService(
            sqliteSnapshots: null,
            commitStepObserver: step =>
            {
                if (step == interruptedAfter)
                    throw new IOException("Simulierter Prozessabbruch.");
            });

        var interrupted = await interruptedService.SeparateAsync(
            fixture.CreateRequest(dryRun: false));

        Assert.False(interrupted.Success);
        Assert.True(File.Exists(fixture.CommitJournalPath));

        var restartRequest = fixture.CreateRestartRequest();
        var restarted = await new PersonalGoldBrainSeparationService().SeparateAsync(
            restartRequest);

        Assert.True(restarted.Success, restarted.Error);
        Assert.False(File.Exists(fixture.CommitJournalPath));
        Assert.True(Directory.Exists(fixture.KnowledgeRoot));
        Assert.True(Directory.Exists(restartRequest.LocalArchiveRoot));
        Assert.True(Directory.Exists(restartRequest.ExternalArchiveRoot));
        Assert.False(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.False(Directory.Exists(fixture.StagingRoot));
        Assert.True(File.Exists(Path.Combine(
            restartRequest.LocalArchiveRoot,
            "old-runtime.json")));
        var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!;
        Assert.Equal("gold-1", Assert.Single(activeSamples).SampleId);
    }

    [Fact]
    public async Task SeparateAsync_unklarer_Journalzustand_bleibt_fail_closed()
    {
        using var fixture = await Fixture.CreateAsync();
        var interruptedService = new PersonalGoldBrainSeparationService(
            sqliteSnapshots: null,
            commitStepObserver: step =>
            {
                if (step == PersonalGoldBrainCommitStep.ExternalMirrorMoved)
                    throw new IOException("Simulierter Prozessabbruch.");
            });
        var interrupted = await interruptedService.SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.False(interrupted.Success);
        Directory.CreateDirectory(fixture.ExternalMirrorRoot);
        await File.WriteAllTextAsync(
            Path.Combine(fixture.ExternalMirrorRoot, "unklar.txt"),
            "nicht automatisch veraendern");

        var restarted = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));

        Assert.False(restarted.Success);
        Assert.Contains("unklar", restarted.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(fixture.CommitJournalPath));
        Assert.True(Directory.Exists(fixture.ExternalMirrorRoot));
        Assert.True(Directory.Exists(fixture.ExternalArchiveRoot));
        Assert.True(Directory.Exists(fixture.StagingRoot));
    }

    [Fact]
    public async Task SeparateAsync_fremdes_Staging_wird_beim_Rollback_nicht_geloescht()
    {
        using var fixture = await Fixture.CreateAsync();
        var interruptedService = new PersonalGoldBrainSeparationService(
            sqliteSnapshots: null,
            commitStepObserver: step =>
            {
                if (step == PersonalGoldBrainCommitStep.ExternalMirrorMoved)
                    throw new IOException("Simulierter Prozessabbruch.");
            });
        var interrupted = await interruptedService.SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.False(interrupted.Success);
        await File.WriteAllTextAsync(
            Path.Combine(
                fixture.StagingRoot,
                PersonalGoldBrainSeparationService.CommitOwnerMarkerFileName),
            "fremde-transaktion");

        var restarted = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));

        Assert.False(restarted.Success);
        Assert.Contains("Besitzmarker", restarted.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(fixture.CommitJournalPath));
        Assert.True(Directory.Exists(fixture.StagingRoot));
        Assert.True(File.Exists(Path.Combine(
            fixture.StagingRoot,
            PersonalGoldBrainSeparationService.CommitOwnerMarkerFileName)));
    }

    [Theory]
    [InlineData("KI_BRAIN.gold-staging_20260724_120000")]
    [InlineData("KI_BRAIN_ALT")]
    [InlineData("elements-archive")]
    public async Task MutationPathGuard_blockiert_fake_Junction_ohne_Linkrecht(
        string junctionName)
    {
        using var fixture = await Fixture.CreateAsync();
        var safetyRoot = Path.GetDirectoryName(fixture.KnowledgeRoot)!;
        var junction = Path.Combine(safetyRoot, junctionName);
        var target = Path.Combine(junction, "mutation.tmp");

        var error = Assert.Throws<InvalidDataException>(() =>
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                safetyRoot,
                target,
                path => string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(junction),
                    StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : null));

        Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecoverAsync_holt_nur_db_ManualCoding_mit_Bild_aus_Altarchiv_nach()
    {
        using var fixture = await Fixture.CreateAsync();
        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);

        var result = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.True(result.Success, result.Error);
        Assert.False(result.DryRun);
        Assert.Equal(1, result.ExistingPersonalGoldSamples);
        Assert.Equal(1, result.DatabaseOnlyCandidates);
        Assert.Equal(1, result.RecoveredSamples);
        Assert.Equal(2, result.ActivePersonalGoldSamples);
        Assert.True(File.Exists(result.ReceiptPath));

        var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!;
        Assert.Equal(2, activeSamples.Count);
        var recovered = Assert.Single(activeSamples, sample => sample.SampleId == "db-only-1");
        Assert.True(ManualGoldTrainingPolicy.IsManuallyConfirmed(recovered, "Besitzer"));
        Assert.StartsWith(
            Path.Combine(fixture.KnowledgeRoot, "gold_frames"),
            recovered.FramePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(recovered.FramePath));
        Assert.Equal(2, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "Samples"));
        Assert.Equal(2, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "Embeddings"));
    }

    [Fact]
    public async Task RecoverAsync_remappt_absoluten_alten_Bildpfad_in_das_verschobene_Lokalarchiv()
    {
        using var fixture = await Fixture.CreateAsync();
        var oldAbsolutePath = fixture.DatabaseOnlyFramePath;
        Assert.StartsWith(
            fixture.KnowledgeRoot,
            oldAbsolutePath,
            StringComparison.OrdinalIgnoreCase);

        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);
        Assert.False(File.Exists(oldAbsolutePath));
        var archivedSource = Path.Combine(
            fixture.LocalArchiveRoot,
            Path.GetRelativePath(fixture.KnowledgeRoot, oldAbsolutePath));
        Assert.True(File.Exists(archivedSource));

        var result = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.RecoveredSamples);
        var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!;
        var recovered = Assert.Single(
            activeSamples,
            sample => sample.SampleId == "db-only-1");
        Assert.True(File.Exists(recovered.FramePath));
    }

    [Fact]
    public async Task RecoverAsync_laesst_absoluten_Bildpfad_ausserhalb_des_frueheren_Roots_unveraendert()
    {
        using var fixture = await Fixture.CreateAsync();
        var externalSource = fixture.MoveDatabaseOnlyFrameOutsideKnowledgeRoot();

        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);
        Assert.True(File.Exists(externalSource));

        var result = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.RecoveredSamples);
        Assert.True(File.Exists(externalSource));
        var activeSamples = JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!;
        var recovered = Assert.Single(
            activeSamples,
            sample => sample.SampleId == "db-only-1");
        Assert.NotEqual(
            Path.GetFullPath(externalSource),
            Path.GetFullPath(recovered.FramePath));
        Assert.True(File.Exists(recovered.FramePath));
    }

    [Fact]
    public async Task RecoverAsync_Prueflauf_findet_db_ManualCoding_ohne_Aenderung()
    {
        using var fixture = await Fixture.CreateAsync();

        var result = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: true, inspectSourceBeforeSeparation: true));

        Assert.True(result.Success, result.Error);
        Assert.True(result.DryRun);
        Assert.Equal(1, result.ExistingPersonalGoldSamples);
        Assert.Equal(1, result.DatabaseOnlyCandidates);
        Assert.Equal(0, result.RecoveredSamples);
        Assert.Equal(2, result.ActivePersonalGoldSamples);
        Assert.Equal(2, JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!.Count);
        Assert.Equal(3, CountRows(Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"), "Samples"));
    }

    [Fact]
    public async Task RecoverAsync_Absturz_nach_Datenbankcommit_wird_beim_Neustart_sicher_geheilt()
    {
        using var fixture = await Fixture.CreateAsync();
        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);

        var interruptedService = new PersonalGoldArchiveRecoveryService(
            frameStore: null,
            sqliteSnapshots: null,
            transactionStepObserver: step =>
            {
                if (step == PersonalGoldArchiveRecoveryStep.DatabaseImported)
                    throw new IOException("Simulierter Stromausfall.");
            });
        var interrupted = await interruptedService.RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.False(interrupted.Success);
        Assert.True(File.Exists(fixture.RecoveryJournalPath));
        Assert.Equal(2, CountRows(
            Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"),
            "Samples"));
        Assert.Single(JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!);

        var restarted = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.True(restarted.Success, restarted.Error);
        Assert.Equal(1, restarted.RecoveredSamples);
        Assert.False(File.Exists(fixture.RecoveryJournalPath));
        Assert.Equal(2, CountRows(
            Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"),
            "Samples"));
        Assert.Equal(2, JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!.Count);

        var repeated = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.True(repeated.Success, repeated.Error);
        Assert.Equal(0, repeated.DatabaseOnlyCandidates);
        Assert.Equal(0, repeated.RecoveredSamples);
        Assert.Equal(2, CountRows(
            Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"),
            "Samples"));
        Assert.Equal(2, JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!.Count);
    }

    [Fact]
    public async Task RecoverAsync_offene_Transaktion_loescht_keine_fremden_Artefakte()
    {
        using var fixture = await Fixture.CreateAsync();
        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);
        var interrupted = await CreateInterruptedRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));
        Assert.False(interrupted.Success);

        var auditDirectory = ReadRecoveryAuditDirectory(fixture.RecoveryJournalPath);
        var foreignPath = Path.Combine(auditDirectory, "fremde-datei.txt");
        await File.WriteAllTextAsync(foreignPath, "nicht von der Transaktion");

        var restarted = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));

        Assert.False(restarted.Success);
        Assert.Contains("fremdes Artefakt", restarted.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(foreignPath));
        Assert.True(File.Exists(fixture.RecoveryJournalPath));
        Assert.Equal(2, CountRows(
            Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"),
            "Samples"));
        Assert.Single(JsonSerializer.Deserialize<List<TrainingSample>>(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json")))!);
    }

    [JunctionFact]
    public async Task RecoverAsync_offene_Transaktion_folgt_keiner_Junction_im_Pruefpfad()
    {
        using var fixture = await Fixture.CreateAsync();
        var separation = await new PersonalGoldBrainSeparationService().SeparateAsync(
            fixture.CreateRequest(dryRun: false));
        Assert.True(separation.Success, separation.Error);
        var interrupted = await CreateInterruptedRecoveryService().RecoverAsync(
            fixture.CreateRecoveryRequest(dryRun: false));
        Assert.False(interrupted.Success);

        var auditDirectory = ReadRecoveryAuditDirectory(fixture.RecoveryJournalPath);
        var outside = Path.Combine(fixture.TestRoot, "fremdes-junction-ziel");
        var sentinel = Path.Combine(outside, "behalten.txt");
        var link = Path.Combine(auditDirectory, "fremde-junction");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(sentinel, "behalten");
        JunctionTestSupport.CreateDirectoryLink(link, outside);
        try
        {
            var restarted = await new PersonalGoldArchiveRecoveryService().RecoverAsync(
                fixture.CreateRecoveryRequest(dryRun: false));

            Assert.False(restarted.Success);
            Assert.Contains("Verknuepfung", restarted.Error, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(sentinel));
            Assert.True(Directory.Exists(link));
            Assert.True(File.Exists(fixture.RecoveryJournalPath));
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
        }
    }

    private static PersonalGoldArchiveRecoveryService CreateInterruptedRecoveryService()
        => new(
            frameStore: null,
            sqliteSnapshots: null,
            transactionStepObserver: step =>
            {
                if (step == PersonalGoldArchiveRecoveryStep.DatabaseImported)
                    throw new IOException("Simulierter Stromausfall.");
            });

    private static string ReadRecoveryAuditDirectory(string journalPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
        return document.RootElement
                   .GetProperty("AuditDirectory")
                   .GetString()
               ?? throw new InvalidDataException("Recovery-Journal besitzt keinen Pruefpfad.");
    }

    private static int CountRows(string databasePath, string table)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root;

        private Fixture(string root)
        {
            _root = root;
            KnowledgeRoot = Path.Combine(root, "KI_BRAIN");
            LocalArchiveRoot = Path.Combine(root, "KI_BRAIN_ALT");
            ExternalMirrorRoot = Path.Combine(root, "elements-brain");
            ExternalArchiveRoot = Path.Combine(root, "elements-archive");
            LegacyProtocolTrainingPath = Path.Combine(root, "appdata", "protocol_training.json");
        }

        public string KnowledgeRoot { get; }
        public string TestRoot => _root;
        public string LocalArchiveRoot { get; }
        public string ExternalMirrorRoot { get; }
        public string ExternalArchiveRoot { get; }
        public string LegacyProtocolTrainingPath { get; }
        public string DatabaseOnlyFramePath =>
            Path.Combine(KnowledgeRoot, "frames", "db_only.jpg");
        public string CommitJournalPath =>
            PersonalGoldBrainSeparationService.ResolveCommitJournalPath(KnowledgeRoot);
        public string RecoveryJournalPath =>
            PersonalGoldArchiveRecoveryService.ResolveTransactionJournalPath(KnowledgeRoot);
        public string StagingRoot =>
            KnowledgeRoot + ".gold-staging_20260724_120000";

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture(Path.Combine(
                Path.GetTempPath(),
                "sewer-gold-brain-separation-tests",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.Combine(fixture.KnowledgeRoot, "gold_frames"));
            Directory.CreateDirectory(Path.Combine(fixture.KnowledgeRoot, "eval_set"));
            Directory.CreateDirectory(Path.Combine(fixture.KnowledgeRoot, "training", "gold_inbox", "BAB - Riss"));
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.LegacyProtocolTrainingPath)!);

            var goldFrame = Path.Combine(fixture.KnowledgeRoot, "gold_frames", "gold_a.jpg");
            var oldFrame = Path.Combine(fixture.KnowledgeRoot, "gold_frames", "gold_old.jpg");
            var databaseOnlyFrame = fixture.DatabaseOnlyFramePath;
            Directory.CreateDirectory(Path.GetDirectoryName(databaseOnlyFrame)!);
            await File.WriteAllBytesAsync(goldFrame, [1, 2, 3, 4]);
            await File.WriteAllBytesAsync(oldFrame, [5, 6, 7, 8]);
            await File.WriteAllBytesAsync(databaseOnlyFrame, [9, 10, 11, 12]);
            var gold = PersonalGold("gold-1", goldFrame);
            var databaseOnly = PersonalGold("db-only-1", databaseOnlyFrame);
            databaseOnly.CaseId = "db-only";
            databaseOnly.BboxXCenter = null;
            databaseOnly.BboxYCenter = null;
            databaseOnly.BboxWidth = null;
            databaseOnly.BboxHeight = null;
            databaseOnly.SamMaskRle = null;
            databaseOnly.SamMaskImageWidth = null;
            databaseOnly.SamMaskImageHeight = null;
            var old = new TrainingSample
            {
                SampleId = "old-1",
                CaseId = "alt",
                Code = "BAB",
                FramePath = oldFrame,
                Status = TrainingSampleStatus.Approved,
                SourceType = SourceTypeNames.TeacherAnnotation
            };
            await File.WriteAllBytesAsync(
                Path.Combine(fixture.KnowledgeRoot, "training_samples.json"),
                JsonSerializer.SerializeToUtf8Bytes(
                    new List<TrainingSample> { gold, old },
                    JsonDefaults.Indented));
            await File.WriteAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "teacher_annotations.json"),
                "[{\"annotationId\":\"old\"}]");
            await File.WriteAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "yolo_class_map.json"),
                "{}");
            await File.WriteAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "classes.txt"),
                "BAB");
            await File.WriteAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "eval_set", "_manifest.json"),
                "{\"frozen\":true}");
            await File.WriteAllTextAsync(
                Path.Combine(fixture.KnowledgeRoot, "old-runtime.json"),
                "{\"old\":true}");
            await File.WriteAllTextAsync(
                fixture.LegacyProtocolTrainingPath,
                "{\"Samples\":[{\"Code\":\"ALT\"}]}");
            CreateDatabase(
                Path.Combine(fixture.KnowledgeRoot, "KnowledgeBase.db"),
                gold,
                old,
                databaseOnly);
            SqliteConnection.ClearAllPools();

            await CopyCriticalMirrorAsync(fixture);
            return fixture;
        }

        public void SetKnowledgeSampleValueInSourceAndMirror(
            string sampleId,
            string column,
            object value)
        {
            var allowedColumns = new HashSet<string>(
                [
                    "CaseId",
                    "VsaCode",
                    "Beschreibung",
                    "MeterStart",
                    "MeterEnd",
                    "IsStreck",
                    "SourceType",
                    "HumanConfirmed",
                    "Corrected",
                    "ConfirmedByUser",
                    "ConfirmedAtUtc",
                    "QualityGateLevel",
                    "FramePath"
                ],
                StringComparer.Ordinal);
            if (!allowedColumns.Contains(column))
                throw new ArgumentOutOfRangeException(nameof(column), column, null);

            foreach (var databasePath in new[]
                     {
                         Path.Combine(KnowledgeRoot, "KnowledgeBase.db"),
                         Path.Combine(ExternalMirrorRoot, "KnowledgeBase.db")
                     })
            {
                using var connection = new SqliteConnection(
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = databasePath,
                        Mode = SqliteOpenMode.ReadWrite,
                        Pooling = false
                    }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    $"UPDATE Samples SET \"{column}\" = $value WHERE SampleId = $id;";
                command.Parameters.AddWithValue("$value", value);
                command.Parameters.AddWithValue("$id", sampleId);
                Assert.Equal(1, command.ExecuteNonQuery());
            }

            SqliteConnection.ClearAllPools();
        }

        public string MoveDatabaseOnlyFrameOutsideKnowledgeRoot()
        {
            var externalPath = Path.Combine(_root, "external-gold", "db_only.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(externalPath)!);
            File.Move(DatabaseOnlyFramePath, externalPath);
            SetKnowledgeSampleValueInSourceAndMirror(
                "db-only-1",
                "FramePath",
                externalPath);
            return externalPath;
        }

        public PersonalGoldBrainSeparationRequest CreateRequest(bool dryRun)
            => new(
                KnowledgeRoot,
                LocalArchiveRoot,
                ExternalMirrorRoot,
                ExternalArchiveRoot,
                LegacyProtocolTrainingPath,
                "Besitzer",
                new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
                ["BAB", "BAF"],
                DryRun: dryRun);

        public PersonalGoldBrainSeparationRequest CreateRestartRequest()
            => new(
                KnowledgeRoot,
                LocalArchiveRoot + "_restart",
                ExternalMirrorRoot,
                ExternalArchiveRoot + "_restart",
                LegacyProtocolTrainingPath,
                "Besitzer",
                new DateTimeOffset(2026, 7, 24, 12, 1, 0, TimeSpan.Zero),
                ["BAB", "BAF"],
                DryRun: false);

        public PersonalGoldArchiveRecoveryRequest CreateRecoveryRequest(
            bool dryRun,
            bool inspectSourceBeforeSeparation = false)
            => new(
                KnowledgeRoot,
                inspectSourceBeforeSeparation ? KnowledgeRoot : LocalArchiveRoot,
                "Besitzer",
                new DateTimeOffset(2026, 7, 24, 13, 0, 0, TimeSpan.Zero),
                ["BAB", "BAF"],
                DryRun: dryRun);

        private static TrainingSample PersonalGold(string sampleId, string framePath)
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
                ConfirmedByUser = "Besitzer",
                ConfirmedAtUtc = new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc),
                MatchLevel = MatchLevelNames.ReviewApproved,
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

        private static void CreateDatabase(
            string databasePath,
            params TrainingSample[] samples)
        {
            using var context = new KnowledgeBaseContext(databasePath);
            foreach (var sample in samples)
            {
                using (var command = context.Connection.CreateCommand())
                {
                    command.CommandText = """
                        INSERT INTO Samples(
                            SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                            IsStreck, FramePath, ExportedUtc, VersionId, SourceType,
                            HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc)
                        VALUES(
                            $id, $case, $code, $text, 0, 0,
                            0, $frame, $utc, 'test', $source,
                            $confirmed, $corrected, $user, $confirmedUtc);
                        """;
                    command.Parameters.AddWithValue("$id", sample.SampleId);
                    command.Parameters.AddWithValue("$case", sample.CaseId);
                    command.Parameters.AddWithValue("$code", sample.Code);
                    command.Parameters.AddWithValue("$text", sample.Beschreibung);
                    command.Parameters.AddWithValue("$frame", sample.FramePath);
                    command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("$source", sample.SourceType ?? "");
                    command.Parameters.AddWithValue(
                        "$confirmed",
                        sample.HumanConfirmed.HasValue ? sample.HumanConfirmed.Value ? 1 : 0 : DBNull.Value);
                    command.Parameters.AddWithValue(
                        "$corrected",
                        sample.Corrected.HasValue ? sample.Corrected.Value ? 1 : 0 : DBNull.Value);
                    command.Parameters.AddWithValue("$user", (object?)sample.ConfirmedByUser ?? DBNull.Value);
                    command.Parameters.AddWithValue(
                        "$confirmedUtc",
                        sample.ConfirmedAtUtc?.ToString("O") ?? (object)DBNull.Value);
                    command.ExecuteNonQuery();
                }

                using var embedding = context.Connection.CreateCommand();
                embedding.CommandText = """
                    INSERT INTO Embeddings(SampleId, Model, Vector, CreatedAt)
                    VALUES($id, 'test', $vector, $utc);
                    """;
                embedding.Parameters.AddWithValue("$id", sample.SampleId);
                embedding.Parameters.AddWithValue("$vector", new byte[] { 1, 2, 3, 4 });
                embedding.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
                embedding.ExecuteNonQuery();
            }

            using var validation = context.Connection.CreateCommand();
            validation.CommandText = """
                INSERT INTO ValidationLog(
                    LogId, VsaCode, SuggestedCode, FinalCode, WasCorrect,
                    EvidenceJson, CreatedUtc)
                VALUES('old-log', 'BAB', 'BAB', 'BAB', 1, '{}', $utc);
                """;
            validation.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
            validation.ExecuteNonQuery();
        }

        private static async Task CopyCriticalMirrorAsync(Fixture fixture)
        {
            Directory.CreateDirectory(Path.Combine(fixture.ExternalMirrorRoot, "gold_frames"));
            Directory.CreateDirectory(Path.Combine(fixture.ExternalMirrorRoot, "eval_set"));
            foreach (var relativePath in new[]
                     {
                         "KnowledgeBase.db",
                         "training_samples.json",
                         "teacher_annotations.json",
                         "yolo_class_map.json",
                         Path.Combine("gold_frames", "gold_a.jpg"),
                         Path.Combine("eval_set", "_manifest.json")
                     })
            {
                var target = Path.Combine(fixture.ExternalMirrorRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(fixture.KnowledgeRoot, relativePath), target);
            }

            await File.WriteAllTextAsync(
                Path.Combine(
                    fixture.ExternalMirrorRoot,
                    KnowledgeRealtimeMirrorService.MarkerFileName),
                $"{KnowledgeMirrorMarker.Header}{Environment.NewLine}" +
                $"Source={fixture.KnowledgeRoot}{Environment.NewLine}" +
                $"Target={fixture.ExternalMirrorRoot}{Environment.NewLine}",
                new UTF8Encoding(false));
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(_root))
                return;

            foreach (var directory in Directory.EnumerateDirectories(
                         _root,
                         "*",
                         SearchOption.AllDirectories)
                     .Prepend(_root)
                     .OrderByDescending(path => path.Length))
            {
                try
                {
                    File.SetAttributes(
                        directory,
                        File.GetAttributes(directory) & ~FileAttributes.ReadOnly);
                }
                catch
                {
                    // Nur Testaufraeumen.
                }
            }

            Directory.Delete(_root, recursive: true);
        }
    }
}
