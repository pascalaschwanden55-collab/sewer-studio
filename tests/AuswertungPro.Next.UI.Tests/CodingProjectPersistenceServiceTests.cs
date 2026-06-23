using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProjectPersistenceServiceTests
{
    [Fact]
    public void MarkProjectDirty_uses_shell_service_when_available()
    {
        var record = new HaltungRecord();
        var timestamp = new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        var service = new CodingProjectPersistenceService(
            markProjectDirty: actualRecord =>
            {
                Assert.Same(record, actualRecord);
                return true;
            },
            trySaveProjectIfReady: () => throw new InvalidOperationException("Save must not be called."),
            utcNow: () => timestamp);

        service.MarkProjectDirty(record);

        Assert.NotEqual(timestamp, record.ModifiedAtUtc);
    }

    [Fact]
    public void MarkProjectDirty_updates_record_timestamp_when_shell_service_does_not_handle_it()
    {
        var record = new HaltungRecord();
        var timestamp = new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        var service = new CodingProjectPersistenceService(
            markProjectDirty: _ => false,
            trySaveProjectIfReady: () => throw new InvalidOperationException("Save must not be called."),
            utcNow: () => timestamp);

        service.MarkProjectDirty(record);

        Assert.Equal(timestamp, record.ModifiedAtUtc);
    }

    [Fact]
    public void MarkProjectDirty_ignores_missing_record_when_shell_service_does_not_handle_it()
    {
        var service = new CodingProjectPersistenceService(
            markProjectDirty: record =>
            {
                Assert.Null(record);
                return false;
            },
            trySaveProjectIfReady: () => throw new InvalidOperationException("Save must not be called."),
            utcNow: () => throw new InvalidOperationException("Clock must not be read."));

        service.MarkProjectDirty(null);
    }

    [Fact]
    public void TrySaveProjectIfReady_delegates_to_shell_service()
    {
        var saved = false;
        var service = new CodingProjectPersistenceService(
            markProjectDirty: _ => throw new InvalidOperationException("MarkDirty must not be called."),
            trySaveProjectIfReady: () => saved = true,
            utcNow: () => throw new InvalidOperationException("Clock must not be read."));

        service.TrySaveProjectIfReady();

        Assert.True(saved);
    }

    [Fact]
    public void Factory_creates_service()
    {
        var service = CodingProjectPersistenceServiceFactory.Create();

        Assert.NotNull(service);
    }
}
