using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerPlaybackDialogServiceFactory
{
    public static PlayerPlaybackDialogService Create()
        => new((message, title) => DialogHost.Current.Info(message, title));
}
