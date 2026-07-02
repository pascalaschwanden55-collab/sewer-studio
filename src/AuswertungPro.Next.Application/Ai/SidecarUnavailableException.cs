using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Fachliche Ausnahme fuer einen nicht erreichbaren oder fehlerhaft antwortenden Vision-Sidecar.
/// </summary>
public sealed class SidecarUnavailableException : Exception
{
    public SidecarUnavailableException(string message)
        : base(message)
    {
    }

    public SidecarUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
