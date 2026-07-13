namespace AuswertungPro.Next.UI.DataPage;

internal interface IDataPageWindowLauncher
{
    void ShowPlayer(DataPageVideoPlaybackRequest request);
    void ShowProtocol(DataPageProtocolWindowRequest request);
}

internal sealed class DataPageWindowLauncher : IDataPageWindowLauncher
{
    private readonly ServiceProvider _services;

    public DataPageWindowLauncher(ServiceProvider services)
        => _services = services ?? throw new ArgumentNullException(nameof(services));

    public void ShowPlayer(DataPageVideoPlaybackRequest request)
    {
        var window = new Views.Windows.PlayerWindow(
            request.Path,
            request.Options,
            damageOverlay: request.DamageOverlay,
            serviceProvider: _services,
            haltungId: request.Record.Id.ToString(),
            haltungRecord: request.Record)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.Show();
    }

    public void ShowProtocol(DataPageProtocolWindowRequest request)
    {
        var window = new Views.ProtocolObservationsWindow(
            request.Record,
            request.Project,
            _services,
            request.ResolvedVideoPath,
            request.ProjectFolder,
            request.MarkDirty)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }
}
