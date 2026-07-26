using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPreviewCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_record_or_service_provider()
    {
        var doc = new ProtocolDocument();

        var withoutRecord = CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                HasHaltungRecord: false,
                HasLegacyServiceProvider: true,
                Document: doc),
            Actions(_ => throw new InvalidOperationException("No action should run.")));
        var withoutProvider = CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                HasHaltungRecord: true,
                HasLegacyServiceProvider: false,
                Document: doc),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingProtocolPreviewCommandWorkflowOutcome.NoRecord, withoutRecord.Outcome);
        Assert.Equal(CodingProtocolPreviewCommandWorkflowOutcome.NoServiceProvider, withoutProvider.Outcome);
        Assert.False(withoutRecord.Completed);
        Assert.False(withoutProvider.Completed);
    }

    [Fact]
    public void Execute_stops_when_preview_was_not_opened()
    {
        var calls = new List<string>();
        var doc = new ProtocolDocument();

        var result = CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                HasHaltungRecord: true,
                HasLegacyServiceProvider: true,
                Document: doc),
            Actions(
                calls.Add,
                showPreview: () =>
                {
                    calls.Add("preview");
                    return false;
                }));

        Assert.Equal(["preview"], calls);
        Assert.Equal(CodingProtocolPreviewCommandWorkflowOutcome.NotOpened, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_syncs_current_protocol_and_offers_pdf_after_preview()
    {
        var calls = new List<string>();
        var originalDoc = new ProtocolDocument { HaltungId = "original" };
        var currentDoc = new ProtocolDocument { HaltungId = "current" };

        var result = CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                HasHaltungRecord: true,
                HasLegacyServiceProvider: true,
                Document: originalDoc),
            Actions(
                calls.Add,
                getCurrentProtocol: () => currentDoc));

        Assert.Equal(["preview", "sync:current", "pdf:current"], calls);
        Assert.Equal(CodingProtocolPreviewCommandWorkflowOutcome.Opened, result.Outcome);
        Assert.True(result.Completed);
    }

    [Fact]
    public void Execute_offers_original_document_when_current_protocol_is_missing()
    {
        var calls = new List<string>();
        var originalDoc = new ProtocolDocument { HaltungId = "original" };

        var result = CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                HasHaltungRecord: true,
                HasLegacyServiceProvider: true,
                Document: originalDoc),
            Actions(
                calls.Add,
                getCurrentProtocol: () => null));

        Assert.Equal(["preview", "pdf:original"], calls);
        Assert.Equal(CodingProtocolPreviewCommandWorkflowOutcome.Opened, result.Outcome);
        Assert.True(result.Completed);
    }

    private static CodingProtocolPreviewCommandActions Actions(
        Action<string> calls,
        Func<bool>? showPreview = null,
        Func<ProtocolDocument?>? getCurrentProtocol = null)
        => new(
            ShowPreview: showPreview ?? (() =>
            {
                calls("preview");
                return true;
            }),
            GetCurrentProtocol: getCurrentProtocol ?? (() => new ProtocolDocument { HaltungId = "current" }),
            SyncPrimaryDamages: document => calls($"sync:{DocumentName(document)}"),
            OfferPdfExport: document => calls($"pdf:{DocumentName(document)}"));

    private static string DocumentName(ProtocolDocument document)
        => document.HaltungId ?? "current";
}
