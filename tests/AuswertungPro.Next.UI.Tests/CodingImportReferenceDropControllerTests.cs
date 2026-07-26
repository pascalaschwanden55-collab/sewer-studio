using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceDropControllerTests
{
    [Fact]
    public void Execute_KopiertImportereignisMitNeuenIdsInCodingSession()
    {
        var original = Event(8, "BCA");
        original.Overlay = new OverlayGeometry
        {
            GeometryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ToolType = OverlayToolType.Rectangle
        };
        var importEvents = new ObservableCollection<CodingEvent> { original };
        ProtocolEntry? addedEntry = null;
        OverlayGeometry? addedOverlay = null;
        var controller = new CodingImportReferenceDropController();

        var result = controller.Execute(
            new CodingImportReferenceDropRequest(
                original,
                TargetIsCoding: true,
                new ObservableCollection<CodingEvent>(),
                importEvents),
            new CodingImportReferenceDropActions(
                (entry, overlay) =>
                {
                    addedEntry = entry;
                    addedOverlay = overlay;
                },
                RemoveSessionEvent: null));

        Assert.Equal(CodingImportReferenceDropOutcome.CopiedToCoding, result.Outcome);
        Assert.True(result.Applied);
        Assert.Same(original, Assert.Single(importEvents));
        Assert.NotNull(addedEntry);
        Assert.NotEqual(original.Entry.EntryId, addedEntry!.EntryId);
        Assert.NotNull(addedOverlay);
        Assert.NotEqual(original.Overlay.GeometryId, addedOverlay!.GeometryId);
    }

    [Fact]
    public void Execute_IgnoriertImportNachCodingOhneSession()
    {
        var original = Event(8, "BCA");
        var importEvents = new ObservableCollection<CodingEvent> { original };
        var controller = new CodingImportReferenceDropController();

        var result = controller.Execute(
            new CodingImportReferenceDropRequest(
                original,
                TargetIsCoding: true,
                new ObservableCollection<CodingEvent>(),
                importEvents),
            new CodingImportReferenceDropActions(null, null));

        Assert.Equal(CodingImportReferenceDropOutcome.MissingSession, result.Outcome);
        Assert.False(result.Applied);
        Assert.Same(original, Assert.Single(importEvents));
    }

    [Fact]
    public void Execute_VerschiebtCodingEreignisSortiertNachImportUndEntferntEsAusSession()
    {
        var moved = Event(8, "BCA");
        var codingEvents = new ObservableCollection<CodingEvent> { moved };
        var importEvents = new ObservableCollection<CodingEvent>
        {
            Event(4, "BBA"),
            Event(12, "BDA")
        };
        Guid? removedId = null;
        var controller = new CodingImportReferenceDropController();

        var result = controller.Execute(
            new CodingImportReferenceDropRequest(
                moved,
                TargetIsCoding: false,
                codingEvents,
                importEvents),
            new CodingImportReferenceDropActions(
                AddSessionEvent: null,
                RemoveSessionEvent: id => removedId = id));

        Assert.Equal(CodingImportReferenceDropOutcome.MovedToImport, result.Outcome);
        Assert.True(result.Applied);
        Assert.Empty(codingEvents);
        Assert.Equal([4d, 8d, 12d], importEvents.Select(item => item.MeterAtCapture));
        Assert.Equal(moved.EventId, removedId);
    }

    private static CodingEvent Event(double meter, string code)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry { Code = code }
        };
}
