using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionDialogServiceFactory
{
    public static LiveDetectionDialogService Create()
        => new(
            (message, title) => DialogHost.Current.Warn(message, title),
            (message, title) => DialogHost.Current.Info(message, title));
}
