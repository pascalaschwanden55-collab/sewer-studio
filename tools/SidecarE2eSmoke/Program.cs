using System.Text.Json;
using SidecarE2eSmoke;

SidecarSmokeOptions options;
try
{
    options = SidecarSmokeOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    SidecarSmokeOptions.PrintHelp();
    return 2;
}

if (options.ShowHelp)
{
    SidecarSmokeOptions.PrintHelp();
    return 0;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    SidecarSmokeOptions.PrintHelp();
    return 2;
}

using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSec));
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    timeoutCts.Cancel();
};
Console.CancelKeyPress += cancelHandler;

SidecarSmokeReport report;
try
{
    report = await new SidecarSmokeRunner().RunAsync(options, timeoutCts.Token);
}
catch (OperationCanceledException)
{
    report = SidecarSmokeReport.Failed(options, "Der Lauf wurde abgebrochen oder hat das Zeitlimit erreicht.");
}
catch (Exception ex)
{
    report = SidecarSmokeReport.Failed(options, ex.ToString());
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

var reportPath = options.ResolveReportPath();
if (reportPath is not null)
{
    var directory = Path.GetDirectoryName(reportPath);
    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    await File.WriteAllTextAsync(
        reportPath,
        JsonSerializer.Serialize(report, SidecarSmokeJson.Options));
    Console.WriteLine($"Report: {reportPath}");
}

if (report.GoldenValidation is { Success: false } golden)
{
    foreach (var failure in golden.Failures)
        Console.Error.WriteLine($"Vertrag: {failure}");
}

if (!string.IsNullOrWhiteSpace(report.Error))
    Console.Error.WriteLine(report.Error);

Console.WriteLine(report.Success ? "E2E smoke PASS" : "E2E smoke FAIL");
return report.Success ? 0 : 1;
