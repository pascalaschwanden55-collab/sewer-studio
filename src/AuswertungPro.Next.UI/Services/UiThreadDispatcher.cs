namespace AuswertungPro.Next.UI.Services;

public sealed class UiThreadDispatcher : IUiThread
{
    public static UiThreadDispatcher Instance { get; } = new();

    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
