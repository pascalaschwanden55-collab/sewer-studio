using System.Text;

namespace VsaShadowReport;

public static class ShadowReportExporter
{
    public static void WriteDifferentEzCsv(ShadowReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var sb = new StringBuilder();
        sb.AppendLine("code;requirement;legacy_ez;v2_ez;ch1;ch2;q1;q2;material;dn;v2_rule_id;v2_source_ref");
        foreach (var item in report.DifferentEzExamples)
        {
            sb.AppendLine(string.Join(";",
                Csv(item.Code),
                Csv(item.Requirement),
                Csv(item.LegacyEz?.ToString()),
                Csv(item.V2Ez?.ToString()),
                Csv(item.Ch1),
                Csv(item.Ch2),
                Csv(item.Q1),
                Csv(item.Q2),
                Csv(item.Material),
                Csv(item.Dn),
                Csv(item.V2RuleId),
                Csv(item.V2SourceRef)));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
