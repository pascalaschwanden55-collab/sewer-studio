using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsKnowledgeRootTests
{
    [Fact]
    public void MigrateLegacyKnowledgeRootPath_uses_last_known_root_when_persisted_path_is_missing()
    {
        var settings = new AppSettings
        {
            LastKnownKnowledgeRoot = @"C:\KI_BRAIN",
            KnowledgeRootPath = null
        };

        var changed = settings.MigrateLegacyKnowledgeRootPath();

        Assert.True(changed);
        Assert.Equal(@"C:\KI_BRAIN", settings.KnowledgeRootPath);
    }

    [Fact]
    public void MigrateLegacyKnowledgeRootPath_keeps_explicit_persisted_path()
    {
        var settings = new AppSettings
        {
            LastKnownKnowledgeRoot = @"C:\OldKnowledge",
            KnowledgeRootPath = @"D:\SewerStudio\Knowledge"
        };

        var changed = settings.MigrateLegacyKnowledgeRootPath();

        Assert.False(changed);
        Assert.Equal(@"D:\SewerStudio\Knowledge", settings.KnowledgeRootPath);
    }

    [Fact]
    public void RecordKnowledgeRootStart_does_not_persist_environment_override()
    {
        var settings = new AppSettings { KnowledgeRootPath = @"D:\StableKnowledge" };

        settings.RecordKnowledgeRootStart(
            @"E:\TemporaryKnowledge",
            17149,
            KnowledgeBasePaths.RootSource.EnvironmentOverride);

        Assert.Equal(@"D:\StableKnowledge", settings.KnowledgeRootPath);
        Assert.Equal(@"E:\TemporaryKnowledge", settings.LastKnownKnowledgeRoot);
        Assert.Equal(17149, settings.LastKnownKnowledgeSampleCount);
    }

    [Fact]
    public void RecordKnowledgeRootStart_persists_default_fallback()
    {
        var settings = new AppSettings();

        settings.RecordKnowledgeRootStart(
            @"C:\Users\Test\AppData\Local\SewerStudio\Knowledge",
            0,
            KnowledgeBasePaths.RootSource.DefaultFallback);

        Assert.Equal(
            @"C:\Users\Test\AppData\Local\SewerStudio\Knowledge",
            settings.KnowledgeRootPath);
    }

    [Fact]
    public void RecordKnowledgeRootStart_keeps_previous_sample_count_when_current_database_is_unreadable()
    {
        var settings = new AppSettings { LastKnownKnowledgeSampleCount = 17149 };

        settings.RecordKnowledgeRootStart(
            @"C:\KI_BRAIN",
            sampleCount: null,
            KnowledgeBasePaths.RootSource.PersistedSettings);

        Assert.Equal(17149, settings.LastKnownKnowledgeSampleCount);
    }
}
