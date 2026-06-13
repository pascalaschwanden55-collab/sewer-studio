namespace AuswertungPro.Next.UI;

internal static class DialogHost
{
    private static readonly Lazy<IDialogService> Fallback = new(() => new DialogService());

    public static IDialogService Current
    {
        get
        {
            try
            {
                return App.Services is ServiceProvider sp ? sp.Dialogs : Fallback.Value;
            }
            catch
            {
                return Fallback.Value;
            }
        }
    }
}
