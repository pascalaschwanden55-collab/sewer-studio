namespace AuswertungPro.Next.UI;

internal static class DialogHost
{
    private static readonly Lazy<IDialogService> Fallback = new(() => new DialogService());
    private static IDialogService? _current;

    public static void Configure(IDialogService dialogs)
        => _current = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public static IDialogService Current
        => _current ?? Fallback.Value;
}
