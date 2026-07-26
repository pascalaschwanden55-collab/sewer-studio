using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolDialogServiceFactory
{
    public static CodingProtocolDialogService Create()
        => new(
            (message, title) => DialogHost.Current.Confirm(message, title),
            (message, title) => DialogHost.Current.Error(message, title));
}
