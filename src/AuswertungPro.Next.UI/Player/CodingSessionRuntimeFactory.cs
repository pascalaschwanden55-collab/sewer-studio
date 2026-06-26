using System.ComponentModel;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingSessionRuntime(
    CodingSessionViewModelOwner ViewModelOwner,
    ICodingSessionHost SessionHost,
    ICodingOverlayToolHost OverlayToolHost);

public static class CodingSessionRuntimeFactory
{
    public static CodingSessionRuntime Create(
        PropertyChangedEventHandler propertyChangedHandler,
        Func<IOverlayToolService?> resolveOverlayService)
    {
        ArgumentNullException.ThrowIfNull(propertyChangedHandler);
        ArgumentNullException.ThrowIfNull(resolveOverlayService);

        var viewModelOwner = new CodingSessionViewModelOwner(propertyChangedHandler);

        return new CodingSessionRuntime(
            viewModelOwner,
            new CodingSessionHost(() => viewModelOwner.ViewModel),
            new CodingOverlayToolHost(resolveOverlayService));
    }
}
