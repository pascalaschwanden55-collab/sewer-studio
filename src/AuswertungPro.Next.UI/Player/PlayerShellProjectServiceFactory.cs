namespace AuswertungPro.Next.UI.Player;

public static class PlayerShellProjectServiceFactory
{
    public static PlayerShellProjectService Create()
        => new(() => App.Current?.MainWindow?.DataContext);
}
