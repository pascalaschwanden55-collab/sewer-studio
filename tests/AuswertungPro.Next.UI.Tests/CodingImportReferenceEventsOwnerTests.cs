using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceEventsOwnerTests
{
    [Fact]
    public void Owner_exposes_stable_import_event_collection()
    {
        var owner = new CodingImportReferenceEventsOwner();

        var first = owner.Events;
        first.Add(new CodingEvent());
        var second = owner.Events;

        Assert.Same(first, second);
        Assert.Single(second);
    }
}
