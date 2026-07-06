using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Xunit;

public sealed class ModernizerRecordFieldUpdaterTests
{
    [Fact]
    public void ForceSetUsesRecordSetterAndUpdatesMetadata()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.Link, "old.mp4", FieldSource.Manual, userEdited: false);
        var report = new ModernizeReport();

        var updated = ModernizerRecordFieldUpdater.ForceSet(record, FieldKeys.Link, "new.mp4", report);

        Assert.True(updated);
        Assert.Equal("new.mp4", record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(FieldSource.Legacy, record.FieldMeta[FieldKeys.Link].Source);
        Assert.False(record.FieldMeta[FieldKeys.Link].UserEdited);
        Assert.Equal(1, report.RelinkedPaths);
        Assert.Equal(0, report.UnresolvedPaths);
    }

    [Fact]
    public void ForceSetDoesNotOverwriteUserEditedField()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.Link, "manual.mp4", FieldSource.Manual, userEdited: true);
        var report = new ModernizeReport();

        var updated = ModernizerRecordFieldUpdater.ForceSet(record, FieldKeys.Link, "modern.mp4", report);

        Assert.False(updated);
        Assert.Equal("manual.mp4", record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(0, report.RelinkedPaths);
        Assert.Equal(1, report.UnresolvedPaths);
        Assert.Contains(report.Messages, message => message.Contains("benutzerbearbeitet", StringComparison.OrdinalIgnoreCase));
    }
}
