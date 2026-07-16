using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

internal interface IDataPageWindowLauncher
{
    void ShowPlayer(DataPageVideoPlaybackRequest request);
    void ShowProtocol(DataPageProtocolWindowRequest request);
    void ShowSanierung(SanierungsmassnahmenViewModel viewModel);
    PipelineResult? ShowVideoAnalysis(
        PipelineRequest request,
        IVideoAnalysisPipelineService pipeline);
    DataPageMediaSearchResult? ShowMediaSearch(
        IReadOnlyList<HaltungRecord> records,
        string? initialFolder);
    void ShowHydraulik(DataPageHydraulikPanelRequest request);
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

    public void ShowSanierung(SanierungsmassnahmenViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var window = new Views.Windows.SanierungsmassnahmenWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    public PipelineResult? ShowVideoAnalysis(
        PipelineRequest request,
        IVideoAnalysisPipelineService pipeline)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pipeline);

        var window = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    public DataPageMediaSearchResult? ShowMediaSearch(
        IReadOnlyList<HaltungRecord> records,
        string? initialFolder)
    {
        ArgumentNullException.ThrowIfNull(records);

        var window = new MediaSearchWindow(
            records.ToList(),
            initialFolder,
            _services.Dialogs,
            _services.Settings,
            _services.BatchMediaSearch)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true
            ? new DataPageMediaSearchResult(
                window.Applied,
                window.AppliedVideoCount,
                window.AppliedPdfCount,
                window.AppliedFotoCount)
            : null;
    }

    public void ShowHydraulik(DataPageHydraulikPanelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewModel = new HydraulikPanelViewModel(_services.Settings);
        viewModel.LoadFromRecord(
            request.DnMillimeters,
            request.Material,
            request.WasserstandMillimeters);

        var window = new HydraulikPanelWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }
}
