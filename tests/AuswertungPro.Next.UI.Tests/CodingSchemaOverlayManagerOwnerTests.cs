using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayManagerOwnerTests
{
    [Fact]
    public void Owner_delegates_schema_lifecycle_and_drag_state()
    {
        var owner = new CodingSchemaOverlayManagerOwner();
        var schema = new PipeBendSchema();
        var center = new NormalizedPoint(0.5, 0.5);

        owner.Activate(schema);
        owner.Place(center);
        var handleId = owner.HitTest(center, threshold: 0.1);
        var activeBeforeCancel = owner.Active;
        owner.BeginDrag(handleId!);
        owner.UpdateDrag(new NormalizedPoint(0.6, 0.6));
        var dragging = owner.IsDragging;
        owner.EndDrag();
        owner.Cancel();

        Assert.Same(schema, activeBeforeCancel);
        Assert.NotNull(handleId);
        Assert.True(dragging);
        Assert.False(owner.IsDragging);
        Assert.False(owner.IsActive);
        Assert.Null(owner.Active);
    }
}
