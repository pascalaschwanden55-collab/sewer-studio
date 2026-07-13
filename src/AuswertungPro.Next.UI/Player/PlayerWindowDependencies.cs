using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerWindowDependencies
{
    private readonly ServiceProvider? _serviceProvider;

    private PlayerWindowDependencies(ServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static PlayerWindowDependencies From(ServiceProvider? serviceProvider)
        => new(serviceProvider);

    public ServiceProvider? LegacyServiceProvider => _serviceProvider;
    public AppSettings? Settings => _serviceProvider?.Settings;
    public ICodeCatalogProvider? CodeCatalog => _serviceProvider?.CodeCatalog;
    public IVsaCodeSelectionCatalog? CodeSelectionCatalog => _serviceProvider?.CodeSelectionCatalog;
    public PipelineConfig? PipelineConfig => _serviceProvider?.PipelineCfg;
    public ProtocolPdfExporter? ProtocolPdfExporter => _serviceProvider?.ProtocolPdfExporter;
    public IProtocolPdfExporter? ProtocolPdfExports => _serviceProvider?.ProtocolPdfExports;
    public ILoggerFactory? LoggerFactory => _serviceProvider?.LoggerFactory;
    public string? LastProjectPath => Settings?.LastProjectPath;
    public bool HasCodeCatalog => CodeCatalog is not null;
}
