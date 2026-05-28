using System.Text;
using System.Text.Json;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public static class DiagnosticReportReader
{
    public static DiagnosticReport Read(string outputDir, int maxEntries = 5000)
    {
        var entriesPath = FindLatest(outputDir, "entries.csv");
        var haltungenPath = FindLatest(outputDir, "haltungen.json");

        var haltungen = haltungenPath is null
            ? []
            : ReadJsonArray(haltungenPath);
        var entries = entriesPath is null
            ? []
            : ReadCsv(entriesPath, maxEntries);

        return new DiagnosticReport(
            OutputDir: outputDir,
            EntriesCsvPath: entriesPath,
            HaltungenJsonPath: haltungenPath,
            HasEntriesCsv: entriesPath is not null,
            HasHaltungenJson: haltungenPath is not null,
            Haltungen: haltungen,
            Entries: entries);
    }

    private static string? FindLatest(string outputDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir))
            return null;

        var direct = Path.Combine(outputDir, fileName);
        if (File.Exists(direct))
            return direct;

        return Directory.EnumerateFiles(outputDir, fileName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static IReadOnlyList<JsonElement> ReadJsonArray(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return doc.RootElement.EnumerateArray()
            .Select(e => e.Clone())
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(string path, int maxEntries)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
            return [];

        var headers = ParseCsvLine(headerLine);
        var rows = new List<IReadOnlyDictionary<string, string>>();
        string? line;
        while (rows.Count < maxEntries && (line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = i < values.Count ? values[i] : "";
            rows.Add(row);
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }
}
