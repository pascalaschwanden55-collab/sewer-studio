using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingImportReferenceEventsOwner
{
    public ObservableCollection<CodingEvent> Events { get; } = new();
}
