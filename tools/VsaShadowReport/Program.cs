using VsaShadowReport;

var path = ResolvePath(args);
var report = ShadowReportAnalyzer.Analyze(path);

Console.WriteLine("VSA Shadow Report");
Console.WriteLine($"Datei: {report.Path}");
Console.WriteLine($"Abweichungen gesamt: {report.TotalDifferences}");
Console.WriteLine($"  expected_drift=true:  {report.ExpectedDifferences}");
Console.WriteLine($"  expected_drift=false: {report.UnexpectedDifferences}");
Console.WriteLine();

if (report.Groups.Count > 0)
{
    Console.WriteLine("Gruppen nach Code + Anforderung:");
    foreach (var group in report.Groups)
    {
        Console.WriteLine(
            $"  {group.Code,-8} {group.Requirement,-1}  count={group.Count,5}  expected_drift={group.ExpectedDrift.ToString().ToLowerInvariant()}");
    }

    Console.WriteLine();
}

if (report.IsCutoverSafe)
{
    Console.WriteLine("CUTOVER SICHER: keine unerwarteten VSA-v2-Abweichungen.");
    return 0;
}

var unexpectedCodes = report.Groups
    .Where(group => !group.ExpectedDrift)
    .Select(group => $"{group.Code}/{group.Requirement}")
    .Distinct(StringComparer.OrdinalIgnoreCase);
Console.WriteLine($"NICHT SICHER: {report.UnexpectedDifferences} unerwartete Abweichungen, siehe Codes: {string.Join(", ", unexpectedCodes)}");
return 1;

static string ResolvePath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals("--file", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return Path.GetFullPath(args[i + 1]);
    }

    var overrideDir = Environment.GetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR");
    var root = !string.IsNullOrWhiteSpace(overrideDir)
        ? overrideDir
        : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    return Path.Combine(root, "SewerStudio", "Telemetry", "vsa_shadow.jsonl");
}
