using System;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingOverlayServiceOwner
{
    public IOverlayToolService? Service { get; private set; }

    public bool HasService => Service is not null;

    public void Set(IOverlayToolService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        Service = service;
    }

    public void Clear()
        => Service = null;
}
