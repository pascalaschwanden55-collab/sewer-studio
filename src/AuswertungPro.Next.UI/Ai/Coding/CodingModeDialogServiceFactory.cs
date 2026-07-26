using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingModeDialogServiceFactory
{
    public static CodingModeDialogService Create()
        => new(
            (message, title) => DialogHost.Current.Info(message, title),
            (message, title) => DialogHost.Current.Warn(message, title));
}
