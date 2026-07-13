using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerWindowProtocolContext
{
    private PlayerWindowProtocolContext(
        PlayerWindowDependencies dependencies,
        string? haltungId,
        Action<ProtocolEntry>? onEntryCreated,
        HaltungRecord? haltungRecord)
    {
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        HaltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        HaltungRecord = haltungRecord;
    }

    private readonly PlayerWindowDependencies _dependencies;
    private readonly Action<ProtocolEntry>? _onEntryCreated;

    public string? HaltungId { get; }

    public HaltungRecord? HaltungRecord { get; }

    public bool HasHaltungRecord => HaltungRecord is not null;

    public ServiceProvider? LegacyServiceProvider => _dependencies.LegacyServiceProvider;

    public AppSettings? Settings => _dependencies.Settings;

    public ICodeCatalogProvider? CodeCatalog => _dependencies.CodeCatalog;

    public IVsaCodeSelectionCatalog? CodeSelectionCatalog => _dependencies.CodeSelectionCatalog;

    public PipelineConfig? PipelineConfig => _dependencies.PipelineConfig;

    public ProtocolPdfExporter? ProtocolPdfExporter => _dependencies.ProtocolPdfExporter;

    public IProtocolPdfExporter? ProtocolPdfExports => _dependencies.ProtocolPdfExports;

    public ILoggerFactory? LoggerFactory => _dependencies.LoggerFactory;

    public string? LastProjectPath => _dependencies.LastProjectPath;

    public bool HasCodeCatalog => _dependencies.HasCodeCatalog;

    public static PlayerWindowProtocolContext From(
        ServiceProvider? serviceProvider,
        string? haltungId,
        Action<ProtocolEntry>? onEntryCreated,
        HaltungRecord? haltungRecord)
        => new(
            PlayerWindowDependencies.From(serviceProvider),
            haltungId,
            onEntryCreated,
            haltungRecord);

    public void NotifyEntryCreated(ProtocolEntry entry)
    {
        _onEntryCreated?.Invoke(entry);
    }
}
