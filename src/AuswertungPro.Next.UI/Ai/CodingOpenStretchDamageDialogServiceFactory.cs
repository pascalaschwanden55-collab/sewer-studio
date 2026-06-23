using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOpenStretchDamageDialogServiceFactory
{
    public static CodingOpenStretchDamageDialogService Create()
        => new((message, title) => DialogHost.Current.ConfirmCancel(message, title));
}
