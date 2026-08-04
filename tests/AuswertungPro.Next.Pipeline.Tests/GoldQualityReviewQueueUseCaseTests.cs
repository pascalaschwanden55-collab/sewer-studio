using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class GoldQualityReviewQueueUseCaseTests
{
    private static readonly string[] TargetCodes = ["BAB", "BAF", "BAI", "BAJ", "BBC", "BBF"];

    [Fact]
    public async Task ExecuteAsync_erstellt_90er_Sitzung_aus_freigegebenem_nicht_geschuetztem_Gold()
    {
        var samples = CreateSamples(perCode: 17);
        var protectedHash = Hash(samples.Single(sample => sample.SampleId == "BAB-00").FramePath);
        var protectedHolding = string.Join(
            '-',
            samples.Single(sample => sample.SampleId == "BAF-00").CaseId.Split('-').Reverse());
        var snapshot = new GoldQualityReviewDataSnapshot(
            samples,
            new HashSet<string>([protectedHash], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>([protectedHolding], StringComparer.OrdinalIgnoreCase),
            new string('b', 64));
        var sessions = new MemorySessionStore();
        var sut = CreateSut(snapshot, samples, sessions);

        var result = await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));

        Assert.Equal(90, result.TotalCount);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(90, result.Items.Count);
        Assert.False(result.Resumed);
        Assert.Equal(15, result.Items.Count(item => item.ExistingCode!.StartsWith("BAB", StringComparison.Ordinal)));
        Assert.Equal(15, result.Items.Count(item => item.ExistingCode!.StartsWith("BAF", StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Items, item => item.ExistingSampleId == "BAB-00");
        Assert.DoesNotContain(result.Items, item => item.ExistingSampleId == "BAF-00");
        Assert.Equal(90, result.Items.Select(item => Hash(item.FramePath)).Distinct().Count());

        var first = result.Items[0];
        Assert.NotNull(first.ExistingBox);
        Assert.NotNull(first.ExistingSegmentation);
        Assert.Equal("1,100", first.ExistingSegmentation!.MaskRle);
        Assert.Equal(9.0, first.ExistingClockPosition);
        Assert.Equal(3, first.ExistingSeverity);
        Assert.Equal(first.ExistingSampleId, sessions.Current!.Entries[0].SampleId);
        Assert.Equal(Hash(first.FramePath), sessions.Current.Entries[0].ImageSha256);
        Assert.Equal(Hash(first.FramePath), first.ExpectedImageSha256);
        Assert.Equal(
            sessions.Current.Entries[0].BaselineConfirmedAtUtc,
            first.ExpectedConfirmedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_setzt_Sitzung_fort_und_zaehlt_nur_erneut_gueltig_bestaetigte_Faelle()
    {
        var samples = CreateSamples(perCode: 15);
        var snapshotProvider = new MutableSnapshotProvider(CreateSnapshot(samples));
        var sessions = new MemorySessionStore();
        var sut = CreateSut(snapshotProvider, samples, sessions);
        var first = await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));
        var completedId = first.Items[0].ExistingSampleId!;
        var stillOpenId = first.Items[1].ExistingSampleId!;
        var externallySavedId = first.Items[2].ExistingSampleId!;

        await sut.MarkCompletedAsync(new GoldQualityReviewCompletionRequest(
            first.SessionId,
            completedId,
            "Besitzer"));
        samples.Single(sample => sample.SampleId == completedId).ConfirmedAtUtc =
            samples.Single(sample => sample.SampleId == completedId).ConfirmedAtUtc!.Value.AddHours(1);
        var invalid = samples.Single(sample => sample.SampleId == stillOpenId);
        invalid.ConfirmedAtUtc = invalid.ConfirmedAtUtc!.Value.AddHours(1);
        invalid.Status = TrainingSampleStatus.Draft;
        invalid.SamMaskRle = null;
        invalid.SamMaskImageWidth = null;
        invalid.SamMaskImageHeight = null;
        samples.Single(sample => sample.SampleId == externallySavedId).ConfirmedAtUtc =
            samples.Single(sample => sample.SampleId == externallySavedId).ConfirmedAtUtc!.Value.AddHours(2);
        snapshotProvider.Current = CreateSnapshot(samples);

        var resumed = await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));

        Assert.True(resumed.Resumed);
        Assert.Equal(1, resumed.CompletedCount);
        Assert.Equal(89, resumed.Items.Count);
        Assert.DoesNotContain(resumed.Items, item => item.ExistingSampleId == completedId);
        var stillOpen = Assert.Single(resumed.Items, item => item.ExistingSampleId == stillOpenId);
        Assert.NotNull(stillOpen.ExistingBox);
        Assert.Null(stillOpen.ExistingSegmentation);
        Assert.Contains(resumed.Items, item => item.ExistingSampleId == externallySavedId);
    }

    [Fact]
    public async Task ExecuteAsync_schreibt_bei_zu_wenig_Faellen_keine_Teilsitzung()
    {
        var samples = CreateSamples(perCode: 15)
            .Where(sample => sample.SampleId != "BBF-14")
            .ToList();
        var sessions = new MemorySessionStore();
        var sut = CreateSut(CreateSnapshot(samples), samples, sessions);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer")));

        Assert.Contains("BBF", error.Message, StringComparison.Ordinal);
        Assert.Null(sessions.Current);
    }

    [Fact]
    public async Task ExecuteAsync_sperrt_Fortsetzung_bei_geaendertem_Schutzfingerprint()
    {
        var samples = CreateSamples(perCode: 15);
        var snapshotProvider = new MutableSnapshotProvider(CreateSnapshot(samples));
        var sessions = new MemorySessionStore();
        var sut = CreateSut(snapshotProvider, samples, sessions);
        await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));
        snapshotProvider.Current = snapshotProvider.Current with { ProtectionFingerprint = new string('c', 64) };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer")));

        Assert.Contains("Schutzstand", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_sperrt_ein_nachtraeglich_veraendertes_Sitzungsmanifest()
    {
        var samples = CreateSamples(perCode: 15);
        var sessions = new MemorySessionStore();
        var sut = CreateSut(CreateSnapshot(samples), samples, sessions);
        await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));
        sessions.Replace(sessions.Current! with
        {
            Entries = sessions.Current!.Entries
                .Select((entry, index) => index == 0
                    ? entry with { BaselineConfirmedAtUtc = entry.BaselineConfirmedAtUtc.AddMinutes(1) }
                    : entry)
                .ToArray(),
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer")));

        Assert.Contains("Sitzungsmanifest", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_bindet_auch_registrierte_Schutzsets_ausserhalb_des_Eval_Wurzelordners()
    {
        var samples = CreateSamples(perCode: 15);
        var snapshotProvider = new MutableSnapshotProvider(CreateSnapshot(samples));
        var protectedRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["release-holdout"] = @"D:\geschuetzt\release-holdout",
        };
        var sut = new GoldQualityReviewQueueUseCase(
            snapshotProvider,
            new RegistryStore(samples, protectedRoots),
            new MemorySessionStore(),
            frameIsReadable: _ => true,
            readImageDimensions: _ => (10, 10),
            computeFileHash: Hash);

        await sut.ExecuteAsync(new GoldQualityReviewQueueRequest("Besitzer"));

        Assert.Equal(protectedRoots, snapshotProvider.LastProtectedSetRootPaths);
    }

    private static GoldQualityReviewQueueUseCase CreateSut(
        GoldQualityReviewDataSnapshot snapshot,
        IReadOnlyList<TrainingSample> samples,
        MemorySessionStore sessions)
        => CreateSut(new MutableSnapshotProvider(snapshot), samples, sessions);

    private static GoldQualityReviewQueueUseCase CreateSut(
        IGoldQualityReviewSnapshotProvider snapshotProvider,
        IReadOnlyList<TrainingSample> samples,
        MemorySessionStore sessions)
        => new(
            snapshotProvider,
            new RegistryStore(samples),
            sessions,
            frameIsReadable: _ => true,
            readImageDimensions: _ => (10, 10),
            computeFileHash: Hash,
            utcNow: () => new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero));

    private static GoldQualityReviewDataSnapshot CreateSnapshot(IReadOnlyList<TrainingSample> samples)
        => new(
            samples,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new string('b', 64));

    private static List<TrainingSample> CreateSamples(int perCode)
    {
        var result = new List<TrainingSample>();
        var index = 0;
        foreach (var mainCode in TargetCodes)
        {
            for (var i = 0; i < perCode; i++)
            {
                var confirmed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMinutes(index++);
                result.Add(new TrainingSample
                {
                    SampleId = $"{mainCode}-{i:00}",
                    CaseId = $"{100000 + index}-{200000 + index}",
                    Code = $"{mainCode}AA",
                    Beschreibung = $"Gepruefter Befund {mainCode} Nummer {i}",
                    MeterStart = i,
                    MeterEnd = i,
                    FramePath = $@"C:\gold\{mainCode}_{i:00}.png",
                    Signature = $"{mainCode}-{i:00}|{mainCode}AA|{i:F1}|{i:F1}",
                    Status = TrainingSampleStatus.Approved,
                    SourceType = SourceTypeNames.ManualCoding,
                    MatchLevel = MatchLevelNames.ReviewApproved,
                    HumanConfirmed = true,
                    Corrected = false,
                    ConfirmedByUser = "Besitzer",
                    ConfirmedAtUtc = confirmed,
                    BboxXCenter = 0.5,
                    BboxYCenter = 0.5,
                    BboxWidth = 1.0,
                    BboxHeight = 1.0,
                    SamMaskRle = "1,100",
                    SamMaskImageWidth = 10,
                    SamMaskImageHeight = 10,
                    SamMaskAreaPixels = 100,
                    CodeMeta = new ProtocolEntryCodeMeta
                    {
                        Code = $"{mainCode}AA",
                        Severity = "3",
                        Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["vsa.uhr.von"] = "9:00",
                        },
                    },
                });
            }
        }

        return result;
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class MutableSnapshotProvider(GoldQualityReviewDataSnapshot current)
        : IGoldQualityReviewSnapshotProvider
    {
        public GoldQualityReviewDataSnapshot Current { get; set; } = current;
        public IReadOnlyDictionary<string, string>? LastProtectedSetRootPaths { get; private set; }

        public Task<GoldQualityReviewDataSnapshot> LoadAsync(
            IReadOnlyDictionary<string, string> protectedSetRootPaths,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProtectedSetRootPaths = protectedSetRootPaths;
            return Task.FromResult(Current);
        }
    }

    private sealed class RegistryStore(
        IReadOnlyList<TrainingSample> samples,
        IReadOnlyDictionary<string, string>? protectedSetRootPaths = null)
        : ITrainingExportRegistryStore
    {
        public TrainingExportRegistryBundle ReadBundle()
        {
            var roles = samples
                .Select(sample => EvalContaminationGuard.NormalizeHaltungKey(sample.CaseId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    key => key,
                    _ => TrainingExportHoldingRole.Train,
                    StringComparer.OrdinalIgnoreCase);
            var snapshot = new TrainingExportRegistrySnapshot(
                TrainingExportRegistrySnapshot.CurrentSchemaVersion,
                new string('a', 64),
                TrainingExportRegistryApprovalStatus.Approved,
                "Besitzer",
                new DateTimeOffset(2026, 8, 2, 20, 0, 0, TimeSpan.Zero),
                roles,
                [])
            {
                ApprovedSampleIds = samples
                    .Select(sample => sample.SampleId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
            };
            return new TrainingExportRegistryBundle(
                snapshot,
                protectedSetRootPaths ?? new Dictionary<string, string>());
        }
    }

    private sealed class MemorySessionStore : IGoldQualityReviewSessionStore
    {
        private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);

        public GoldQualityReviewSession? Current { get; private set; }

        public GoldQualityReviewSession? LoadCurrent(string reviewer)
            => Current;

        public void SaveCurrent(GoldQualityReviewSession session)
            => Current = session;

        public IReadOnlySet<string> LoadCompletedSampleIds(GoldQualityReviewSession session)
            => new HashSet<string>(_completed, StringComparer.OrdinalIgnoreCase);

        public void MarkCompleted(
            GoldQualityReviewSession session,
            string sampleId,
            DateTimeOffset completedUtc)
            => _completed.Add(sampleId);

        public void Replace(GoldQualityReviewSession session)
            => Current = session;
    }
}
