using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.GoldQualityReview;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.GoldQualityReview;

public sealed class GoldQualityReviewSessionFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "gold-quality-review-session-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveCurrent_schreibt_Manifest_atomar_und_LoadCurrent_liest_es_streng()
    {
        var store = new GoldQualityReviewSessionFileStore(_root);
        var session = CreateSession();

        store.SaveCurrent(session);

        var loaded = Assert.IsType<GoldQualityReviewSession>(store.LoadCurrent("besitzer"));
        AssertSessionEqual(session, loaded);
        Assert.True(File.Exists(store.GetCurrentPath("Besitzer")));
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(store.GetCurrentPath("Besitzer"))!,
            "*.tmp"));
    }

    [Fact]
    public void SaveCurrent_ueberschreibt_keine_bestehende_Sitzung()
    {
        var store = new GoldQualityReviewSessionFileStore(_root);
        var session = CreateSession();
        store.SaveCurrent(session);

        var error = Assert.Throws<InvalidOperationException>(() => store.SaveCurrent(session));

        Assert.Contains("nicht ueberschrieben", error.Message, StringComparison.OrdinalIgnoreCase);
        AssertSessionEqual(session, Assert.IsType<GoldQualityReviewSession>(store.LoadCurrent("Besitzer")));
    }

    [Fact]
    public void LoadCurrent_blockiert_unbekannte_oder_beschaedigte_Felder()
    {
        var store = new GoldQualityReviewSessionFileStore(_root);
        store.SaveCurrent(CreateSession());
        var path = store.GetCurrentPath("Besitzer");
        var json = File.ReadAllText(path)
            .Replace("\"schema_version\"", "\"unbekannt\": true, \"schema_version\"", StringComparison.Ordinal);
        File.WriteAllText(path, json);

        var error = Assert.Throws<InvalidDataException>(() => store.LoadCurrent("Besitzer"));

        Assert.Contains("nicht sicher lesbar", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkCompleted_schreibt_einen_unveraenderlichen_idempotenten_Abschlussbeleg()
    {
        var store = new GoldQualityReviewSessionFileStore(_root);
        var session = CreateSession();
        store.SaveCurrent(session);

        store.MarkCompleted(
            session,
            "sample-1",
            new DateTimeOffset(2026, 8, 3, 13, 0, 0, TimeSpan.Zero));
        store.MarkCompleted(
            session,
            "sample-1",
            new DateTimeOffset(2026, 8, 3, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal(["sample-1"], store.LoadCompletedSampleIds(session));
        Assert.True(File.Exists(store.GetCompletionPath(session, "sample-1")));
    }

    [JunctionFact]
    public void SaveCurrent_blockiert_KnowledgeRoot_als_Verzeichnisverknuepfung()
    {
        Directory.CreateDirectory(_root);
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var linkedKnowledgeRoot = Path.Combine(_root, "knowledge-link");
        JunctionTestSupport.CreateDirectoryLink(linkedKnowledgeRoot, target);
        try
        {
            var store = new GoldQualityReviewSessionFileStore(linkedKnowledgeRoot);

            var error = Assert.Throws<InvalidDataException>(() => store.SaveCurrent(CreateSession()));

            Assert.Contains("Verknuepfte Pfade", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        }
        finally
        {
            if (Directory.Exists(linkedKnowledgeRoot))
                Directory.Delete(linkedKnowledgeRoot);
        }
    }

    [Fact]
    public void SaveCurrent_prueft_die_vollstaendige_Pfadkette_vor_dem_Schreiben()
    {
        var simulatedLink = Path.Combine(_root, "simulierte-verknuepfung");
        var store = new GoldQualityReviewSessionFileStore(
            _root,
            _ => simulatedLink);

        var error = Assert.Throws<InvalidDataException>(() => store.SaveCurrent(CreateSession()));

        Assert.Contains(simulatedLink, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(_root, "training", "gold_quality_reviews")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private static GoldQualityReviewSession CreateSession()
        => new(
            GoldQualityReviewSession.CurrentSchemaVersion,
            "session-123",
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            "Besitzer",
            new string('a', 64),
            new string('b', 64),
            ["BAB"],
            SamplesPerMainCode: 1,
            [new GoldQualityReviewSessionEntry(
                "sample-1",
                "BAB",
                new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                new string('c', 64))]);

    private static void AssertSessionEqual(
        GoldQualityReviewSession expected,
        GoldQualityReviewSession actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
        Assert.Equal(expected.Reviewer, actual.Reviewer);
        Assert.Equal(expected.RegistryHash, actual.RegistryHash);
        Assert.Equal(expected.ProtectionFingerprint, actual.ProtectionFingerprint);
        Assert.Equal(expected.MainCodes, actual.MainCodes);
        Assert.Equal(expected.SamplesPerMainCode, actual.SamplesPerMainCode);
        Assert.Equal(expected.Entries, actual.Entries);
    }
}
