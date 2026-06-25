using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfExportCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_exporter_or_record()
    {
        var doc = new ProtocolDocument();

        var withoutExporter = CodingProtocolPdfExportCommandWorkflow.Execute(
            new CodingProtocolPdfExportCommandRequest(
                HasProtocolPdfExporter: false,
                HasHaltungRecord: true,
                Document: doc),
            Actions(_ => throw new InvalidOperationException("No action should run.")));
        var withoutRecord = CodingProtocolPdfExportCommandWorkflow.Execute(
            new CodingProtocolPdfExportCommandRequest(
                HasProtocolPdfExporter: true,
                HasHaltungRecord: false,
                Document: doc),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingProtocolPdfExportCommandWorkflowOutcome.NoExporter, withoutExporter.Outcome);
        Assert.Equal(CodingProtocolPdfExportCommandWorkflowOutcome.NoRecord, withoutRecord.Outcome);
        Assert.False(withoutExporter.Completed);
        Assert.False(withoutRecord.Completed);
    }

    [Fact]
    public void Execute_stops_when_export_is_declined_or_fails()
    {
        var calls = new List<string>();

        var result = CodingProtocolPdfExportCommandWorkflow.Execute(
            new CodingProtocolPdfExportCommandRequest(
                HasProtocolPdfExporter: true,
                HasHaltungRecord: true,
                Document: new ProtocolDocument()),
            Actions(
                calls.Add,
                offerPdfExport: () =>
                {
                    calls.Add("offer");
                    return false;
                }));

        Assert.Equal(["offer"], calls);
        Assert.Equal(CodingProtocolPdfExportCommandWorkflowOutcome.NotExported, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_shows_overlay_after_successful_export()
    {
        var calls = new List<string>();

        var result = CodingProtocolPdfExportCommandWorkflow.Execute(
            new CodingProtocolPdfExportCommandRequest(
                HasProtocolPdfExporter: true,
                HasHaltungRecord: true,
                Document: new ProtocolDocument()),
            Actions(calls.Add));

        Assert.Equal(["offer", "overlay:PDF-Protokoll erstellt:4"], calls);
        Assert.Equal(CodingProtocolPdfExportCommandWorkflowOutcome.Exported, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingProtocolPdfExportCommandActions Actions(
        Action<string> calls,
        Func<bool>? offerPdfExport = null)
        => new(
            OfferPdfExport: offerPdfExport ?? (() =>
            {
                calls("offer");
                return true;
            }),
            ShowOverlay: (message, duration) => calls($"overlay:{message}:{duration.TotalSeconds:0}"));
}
