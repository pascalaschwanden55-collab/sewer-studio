using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace TrainingDataInventory;

internal sealed class TrainingInventoryCommand
{
    public const int SuccessExitCode = 0;
    public const int ErrorExitCode = 1;
    public const int CancelledExitCode = 130;

    private readonly ITrainingDataInventoryService _inventory;
    private readonly TrainingInventoryReportWriter _reportWriter;
    private readonly TrainingInventoryConsole _console;

    public TrainingInventoryCommand(
        ITrainingDataInventoryService inventory,
        TrainingInventoryReportWriter reportWriter,
        TrainingInventoryConsole console)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public async Task<int> RunAsync(
        TrainingInventoryCliOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Billige Zielpruefung zuerst: Ein falsches --out soll nicht erst nach dem Scan auffallen.
        var outputPaths = TrainingInventoryReportOutputPolicy.ValidateTarget(
            options.OutputPath,
            options.KnowledgeRoot,
            options.SearchRoots,
            options.ProtectedRoots);

        _console.WriteStarted(options.ComputeAssetHashes);
        var report = await _inventory
            .InspectAsync(options.CreateRequest(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        TrainingInventoryReportOutputPolicy.EnsureNoSourceCollision(
            outputPaths,
            report.Sources.Select(source => source.Path));
        var written = await _reportWriter
            .WriteAsync(report, outputPaths, cancellationToken)
            .ConfigureAwait(false);

        var successful = TrainingInventoryExitPolicy.IsSuccessful(report);
        _console.WriteCompleted(report, written, successful);
        return successful ? SuccessExitCode : ErrorExitCode;
    }
}
