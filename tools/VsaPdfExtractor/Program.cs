using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

if (args.Length < 2)
{
    Console.WriteLine("Usage: VsaPdfExtractor <pdfPath> <outputJson>");
    return;
}

var pdfPath = args[0];
var outputPath = args[1];

if (!File.Exists(pdfPath))
{
    Console.WriteLine($"PDF not found: {pdfPath}");
    return;
}

var codeRx = new Regex(@"^(?<code>[A-Z]{3,5})\\s+(.+)$", RegexOptions.Compiled);
var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var dumped = false;

using (var doc = PdfDocument.Open(pdfPath))
{
    foreach (var page in doc.GetPages())
    {
        var text = page.Text;
        if (string.IsNullOrWhiteSpace(text))
            continue;

        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length >= 4)
            .ToList();

        if (!dumped && args.Length >= 3 && args[2].Equals("dump", StringComparison.OrdinalIgnoreCase))
        {
            dumped = true;
            File.WriteAllLines(outputPath, lines.Take(400));
            Console.WriteLine($"Dumped {lines.Count} lines to {outputPath}");
            return;
        }

        foreach (var line in lines)
        {
            var m = codeRx.Match(line);
            if (!m.Success)
                continue;

            var code = m.Groups["code"].Value.Trim();
            var rest = line.Substring(code.Length).Trim();
            if (rest.Length == 0)
                continue;

            if (!map.ContainsKey(code))
                map[code] = rest;
        }
    }
}

var json = JsonSerializer.Serialize(map.OrderBy(k => k.Key)
    .ToDictionary(k => k.Key, v => v.Value), new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outputPath, json);

Console.WriteLine($"Extracted {map.Count} codes -> {outputPath}");
