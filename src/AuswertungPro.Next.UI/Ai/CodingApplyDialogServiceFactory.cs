using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingApplyDialogServiceFactory
{
    public static CodingApplyDialogService Create()
        => new(
            (message, title) => DialogHost.Current.ConfirmWarn(message, title),
            (message, title) => DialogHost.Current.ConfirmCancel(message, title));
}
