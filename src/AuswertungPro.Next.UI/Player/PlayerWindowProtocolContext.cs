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
        Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        HaltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        HaltungRecord = haltungRecord;
    }

    private readonly Action<ProtocolEntry>? _onEntryCreated;

    public PlayerWindowDependencies Dependencies { get; }

    public string? HaltungId { get; }

    public HaltungRecord? HaltungRecord { get; }

    public bool HasHaltungRecord => HaltungRecord is not null;

    public ServiceProvider? LegacyServiceProvider => Dependencies.LegacyServiceProvider;

    public AppSettings? Settings => Dependencies.Settings;

    public ICodeCatalogProvider? CodeCatalog => Dependencies.CodeCatalog;

    public IVsaCodeSelectionCatalog? CodeSelectionCatalog => Dependencies.CodeSelectionCatalog;

    public PipelineConfig? PipelineConfig => Dependencies.PipelineConfig;

    public ProtocolPdfExporter? ProtocolPdfExporter => Dependencies.ProtocolPdfExporter;

    public ILoggerFactory? LoggerFactory => Dependencies.LoggerFactory;

    public string? LastProjectPath => Dependencies.LastProjectPath;

    public bool HasCodeCatalog => Dependencies.HasCodeCatalog;

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
