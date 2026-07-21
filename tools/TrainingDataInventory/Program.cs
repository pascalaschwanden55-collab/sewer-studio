using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;
using TrainingDataInventory;

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += cancelHandler;

try
{
    var options = TrainingInventoryCliOptions.Parse(
        args,
        TimeProvider.System.GetUtcNow());
    var output = new TrainingInventoryConsole(Console.Out, Console.Error);
    if (options.ShowHelp)
    {
        output.WriteHelp();
        return TrainingInventoryCommand.SuccessExitCode;
    }

    var command = new TrainingInventoryCommand(
        new TrainingDataInventoryService(),
        new TrainingInventoryReportWriter(),
        output);
    return await command.RunAsync(options, cancellation.Token);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Abgebrochen. Quelldateien wurden nicht veraendert.");
    return TrainingInventoryCommand.CancelledExitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FEHLER: " + ex.Message);
    return TrainingInventoryCommand.ErrorExitCode;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
