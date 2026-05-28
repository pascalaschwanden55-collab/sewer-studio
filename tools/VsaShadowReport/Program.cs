using VsaShadowReport;

var path = ResolvePath(args);
var report = ShadowReportAnalyzer.Analyze(path);
var csvPath = ResolveOptionValue(args, "--csv");

Console.WriteLine("VSA Shadow Report");
Console.WriteLine($"Datei: {report.Path}");
if (!string.IsNullOrWhiteSpace(report.AnalyzedWindow))
{
    Console.WriteLine($"Analysierter Lauf: {report.AnalyzedWindow} UTC ({report.AnalyzedWindowEntries} Logzeilen im Lauf, {report.TotalLogEntries} gesamt)");
}
if (report.LatestWindowIsSmallerThanLargest)
{
    Console.WriteLine($"WARNUNG: Neuester Lauf ist kleiner als ein aelterer Lauf ({report.LargestWindow}: {report.LargestWindowEntries} Logzeilen). Shadow-Log vor dem naechsten App-Lauf loeschen.");
}
Console.WriteLine($"Abweichungen gesamt: {report.TotalDifferences}");
Console.WriteLine($"  expected_drift=true:  {report.ExpectedDifferences}");
Console.WriteLine($"  expected_drift=false: {report.UnexpectedDifferences}");
Console.WriteLine($"    davon v2_ez=null:   {report.UnexpectedMissingV2Ez}");
Console.WriteLine($"    davon EZ ungleich:  {report.UnexpectedDifferentEz}");
Console.WriteLine($"    davon bekannte Nicht-Bewertung: {report.NonAssessableRuleNotFoundCount}");
Console.WriteLine($"    v2 milder:          {report.V2MilderCount}");
Console.WriteLine($"    v2 strenger:        {report.V2StricterCount}");
Console.WriteLine($"    v2 neu bewertet:    {report.V2NewCount}");
Console.WriteLine();

if (report.NoData)
{
    Console.WriteLine("KEINE DATEN: Shadow-Log leer oder nicht vorhanden. App erst mit echten VSA-Projekten laufen lassen.");
    return 2;
}

if (report.Groups.Count > 0)
{
    Console.WriteLine("Gruppen nach Code + Anforderung:");
    foreach (var group in report.Groups)
    {
        var reason = string.IsNullOrWhiteSpace(group.V2Reason)
            ? ""
            : $"  reason={group.V2Reason}";
        Console.WriteLine(
            $"  {group.Code,-8} {group.Requirement,-1}  count={group.Count,5}  expected_drift={group.ExpectedDrift.ToString().ToLowerInvariant()}  v2_missing={group.V2Missing.ToString().ToLowerInvariant()}{reason}");
    }

    Console.WriteLine();
}

if (report.DifferentEzExamples.Count > 0)
{
    Console.WriteLine("Beispiele echte EZ-Differenzen:");
    foreach (var example in report.DifferentEzExamples.Take(20))
    {
        Console.WriteLine(
            $"  {example.Code,-8} {example.Requirement,-1} legacy={FormatEz(example.LegacyEz),-4} v2={FormatEz(example.V2Ez),-4} ch1={Dash(example.Ch1),-2} ch2={Dash(example.Ch2),-2} q1={Dash(example.Q1),-6} q2={Dash(example.Q2),-6} mat={Dash(example.Material),-10} dn={Dash(example.Dn),-5} rule={Dash(example.V2RuleId)} source={Dash(example.V2SourceRef)}");
    }

    Console.WriteLine();
}

if (!string.IsNullOrWhiteSpace(csvPath))
{
    ShadowReportExporter.WriteDifferentEzCsv(report, csvPath);
    Console.WriteLine($"CSV exportiert: {Path.GetFullPath(csvPath)}");
    Console.WriteLine();
}

if (report.IsCutoverSafe)
{
    Console.WriteLine("CUTOVER SICHER: keine unerwarteten VSA-v2-Abweichungen.");
    return 0;
}

var unexpectedCodes = report.Groups
    .Where(group => !group.ExpectedDrift)
    .Select(group => group.V2Missing
        ? $"{group.Code}/{group.Requirement}(v2=null{FormatReason(group.V2Reason)})"
        : $"{group.Code}/{group.Requirement}")
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

static string? ResolveOptionValue(string[] args, string optionName)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(optionName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
    }

    return null;
}

static string FormatReason(string? reason)
    => string.IsNullOrWhiteSpace(reason) ? "" : $", {reason}";

static string FormatEz(int? ez)
    => ez?.ToString() ?? "null";

static string Dash(string? value)
    => string.IsNullOrWhiteSpace(value) ? "-" : value;
