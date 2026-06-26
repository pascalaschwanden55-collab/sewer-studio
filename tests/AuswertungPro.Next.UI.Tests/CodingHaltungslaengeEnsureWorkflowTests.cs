using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingHaltungslaengeEnsureWorkflowTests
{
    [Fact]
    public void Ensure_creates_service_and_delegates_record_and_overlay_length()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var service = new CodingHaltungslaengeEnsureService(
            tryEnsureFromKnownSources: (actualRecord, overlayLength) =>
            {
                Assert.Same(record, actualRecord);
                Assert.Equal(12.5, overlayLength);
                calls.Add("ensure");
                return true;
            },
            askForLength: () => throw new InvalidOperationException("Prompt must not open."));

        CodingHaltungslaengeEnsureWorkflow.Ensure(
            record,
            overlayPipeLengthMeters: 12.5,
            new CodingHaltungslaengeEnsureWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "ensure"], calls);
    }
}
