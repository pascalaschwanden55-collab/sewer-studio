namespace AuswertungPro.Next.UI;

internal static class DialogHost
{
    private static readonly Lazy<IDialogService> Fallback = new(() => new DialogService());

    public static IDialogService Current
        => Fallback.Value;
}
