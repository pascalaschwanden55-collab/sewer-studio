using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPrimaryDamageSyncWorkflowTests
{
    [Fact]
    public void Sync_creates_synchronizer_and_delegates_record_and_document()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var document = new ProtocolDocument();
        var timestamp = new DateTime(2026, 6, 24, 9, 15, 0, DateTimeKind.Utc);
        var synchronizer = new CodingPrimaryDamageSynchronizer(
            actualDocument =>
            {
                Assert.Same(document, actualDocument);
                calls.Add("build");
                return "1.23m BAJ Riss";
            },
            () => timestamp);

        CodingPrimaryDamageSyncWorkflow.Sync(
            record,
            document,
            new CodingPrimaryDamageSyncWorkflowActions(
                CreateSynchronizer: () =>
                {
                    calls.Add("service");
                    return synchronizer;
                }));

        Assert.Equal(["service", "build"], calls);
        Assert.Equal("1.23m BAJ Riss", record.GetFieldValue("Primaere_Schaeden"));
        Assert.Equal(timestamp, record.ModifiedAtUtc);
    }
}
