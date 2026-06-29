using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;

var root = FindSolutionRoot(Environment.CurrentDirectory);
var markdownPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(root, "docs", "vsa-zustandsklassifizierung-2023-schwellen.md");
var outputDir = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data");

var result = MarkdownRuleParser.Parse(markdownPath);
Directory.CreateDirectory(outputDir);

WriteRuleSet(result.ChannelRules, "channel", "vsa_zustandsklassifizierung_2023_channels.json");
WriteRuleSet(result.ManholeRules, "manhole", "vsa_zustandsklassifizierung_2023_manholes.json");

Console.WriteLine($"Kanal-Regeln:  {result.ChannelRules.Count}");
Console.WriteLine($"Schacht-Regeln: {result.ManholeRules.Count}");

void WriteRuleSet(List<VsaClassificationRule> rules, string assetKind, string fileName)
{
    var ruleSet = new VsaClassificationRuleSet
    {
        SchemaVersion = 2,
        Source = "VSA_Rili_ Zustandsbeurteilung von Entwaesserungsanlagen.pdf / docs/vsa-zustandsklassifizierung-2023-schwellen.md",
        AssetKind = assetKind,
        Rules = rules
    };

    var json = JsonSerializer.Serialize(ruleSet, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    File.WriteAllText(Path.Combine(outputDir, fileName), json);
}

static string FindSolutionRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
            return current.FullName;
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("AuswertungPro.sln wurde nicht gefunden.");
}

// Ergebnis-Record wird von MarkdownRuleParser.cs genutzt und hier deklariert,
// damit er im selben Compilation-Unit sichtbar ist.
internal sealed record ParseResult(
    List<VsaClassificationRule> ChannelRules,
    List<VsaClassificationRule> ManholeRules);
