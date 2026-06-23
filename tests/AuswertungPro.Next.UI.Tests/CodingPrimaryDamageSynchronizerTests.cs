using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPrimaryDamageSynchronizerTests
{
    [Fact]
    public void Sync_writes_primary_damage_text_and_modified_timestamp()
    {
        var record = new HaltungRecord();
        var doc = new ProtocolDocument();
        var timestamp = new DateTime(2026, 6, 23, 12, 30, 0, DateTimeKind.Utc);
        var synchronizer = new CodingPrimaryDamageSynchronizer(
            _ => "1.23m BAJ Riss",
            () => timestamp);

        synchronizer.Sync(record, doc);

        Assert.Equal("1.23m BAJ Riss", record.GetFieldValue("Primaere_Schaeden"));
        Assert.Equal(timestamp, record.ModifiedAtUtc);
        Assert.True(record.FieldMeta.TryGetValue("Primaere_Schaeden", out var meta));
        Assert.Equal(FieldSource.Manual, meta.Source);
        Assert.True(meta.UserEdited);
    }

    [Fact]
    public void Factory_creates_synchronizer()
    {
        var synchronizer = CodingPrimaryDamageSynchronizerFactory.Create();

        Assert.NotNull(synchronizer);
    }
}
