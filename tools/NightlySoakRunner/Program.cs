using NightlySoakRunner;

NightlySoakOptions options;
try
{
    options = NightlySoakOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    NightlySoakOptions.PrintHelp();
    return 2;
}

if (options.ShowHelp)
{
    NightlySoakOptions.PrintHelp();
    return 0;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    NightlySoakOptions.PrintHelp();
    return 2;
}

using var cancel = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancel.Cancel();
};
Console.CancelKeyPress += cancelHandler;

try
{
    await using var session = await SidecarRunSession.StartAsync(options, cancel.Token);
    var runner = new NightlySoakRunService(
        new SidecarPipelineProbe(options),
        new ProcessResourceSampler());
    var result = await runner.RunAsync(options, cancel.Token);
    Console.WriteLine(result.Message);
    Console.WriteLine($"CSV: {result.CsvPath}");
    return result.Success ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Der Nachtlauf wurde abgebrochen.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
