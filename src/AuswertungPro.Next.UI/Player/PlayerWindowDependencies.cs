using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
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
    public ICodingFramePhotoStore CodingFramePhotos
        => _serviceProvider?.CodingFramePhotos ?? CodingAiFramePhotoService.CompatibilityService;
    public ICodingDefectPreviewRenderer CodingDefectPreviews
        => _serviceProvider?.CodingDefectPreviews ?? CodingDefectPreviewService.CompatibilityService;
    public ITeacherAnnotationStore TeacherAnnotations
        => _serviceProvider?.TeacherAnnotations ?? TeacherAnnotationStore.Current;
    public IVsaYoloClassMapStore VsaYoloClasses
        => _serviceProvider?.VsaYoloClasses ?? VsaYoloClassMap.Current;
    public ITrainingSampleStore TrainingSamples
        => _serviceProvider?.TrainingSamples ?? TrainingSamplesStore.Current;
    public IDialogService Dialogs
        => _serviceProvider?.Dialogs ?? DialogHost.Current;
    public ICodeUsageTracker CodeUsage
        => _serviceProvider?.CodeUsage ?? CodeUsageTrackers.Current;
    public ISidecarTelemetryWriter SidecarTelemetry
        => _serviceProvider?.SidecarTelemetry ?? SidecarTelemetryWriter.Current;
    public ILoggerFactory? LoggerFactory => _serviceProvider?.LoggerFactory;
    public string? LastProjectPath => Settings?.LastProjectPath;
    public bool HasCodeCatalog => CodeCatalog is not null;
}
