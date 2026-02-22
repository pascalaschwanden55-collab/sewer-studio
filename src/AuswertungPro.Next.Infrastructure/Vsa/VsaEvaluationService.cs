using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;
using VsaFinding = AuswertungPro.Next.Domain.Models.VsaFinding;

namespace AuswertungPro.Next.Infrastructure.Vsa;

public sealed class VsaEvaluationService : IVsaEvaluationService
{
    private static readonly Regex LeadingTokenRegex = new(@"^[A-Za-z0-9]+", RegexOptions.Compiled);

    private readonly string _channelsTablePath;
    private readonly string _manholesTablePath;

    public VsaEvaluationService(string channelsTablePath, string manholesTablePath)
    {
        _channelsTablePath = channelsTablePath;
        _manholesTablePath = manholesTablePath;
    }

    public Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project)
    {
        if (project is null)
            return Result<IReadOnlyList<VsaConditionResult>>.Fail("VSA_PROJECT_NULL", "Project is null.");

        var tableResult = LoadClassificationTable();
        if (!tableResult.Ok || tableResult.Value is null)
            return Result<IReadOnlyList<VsaConditionResult>>.Fail(
                tableResult.ErrorCode ?? "VSA_TABLE_LOAD_FAILED",
                tableResult.ErrorMessage ?? "Classification table could not be loaded.");

        var table = tableResult.Value.Table;
        var knownCodes = new HashSet<string>(
            table.Rules.Select(r => NormalizeCode(r.Code)).Where(c => c.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var results = new List<VsaConditionResult>(project.Data.Count * 3);
        var unknownCodeCount = 0;

        foreach (var record in project.Data)
        {
            var findings = ResolveFindings(record, knownCodes);
            var classified = ClassifyFindings(findings, table, out var unknownForRecord);
            unknownCodeCount += unknownForRecord;

            var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified);
            var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified);
            var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified);

            ApplyRecordFields(record, d, s, b);

            results.Add(d);
            results.Add(s);
            results.Add(b);
        }

        project.Metadata["VSA_Diag"] =
            $"Records={project.Data.Count}; UnknownCodes={unknownCodeCount}; Table={tableResult.Value.SourceName}";

        return Result<IReadOnlyList<VsaConditionResult>>.Success(results);
    }

    public Result<string> Explain(Project project, HaltungRecord record)
    {
        if (project is null)
            return Result<string>.Fail("VSA_PROJECT_NULL", "Project is null.");
        if (record is null)
            return Result<string>.Fail("VSA_RECORD_NULL", "Record is null.");

        var tableResult = LoadClassificationTable();
        if (!tableResult.Ok || tableResult.Value is null)
            return Result<string>.Fail(
                tableResult.ErrorCode ?? "VSA_TABLE_LOAD_FAILED",
                tableResult.ErrorMessage ?? "Classification table could not be loaded.");

        var table = tableResult.Value.Table;
        var knownCodes = new HashSet<string>(
            table.Rules.Select(r => NormalizeCode(r.Code)).Where(c => c.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var findings = ResolveFindings(record, knownCodes);
        var classified = ClassifyFindings(findings, table, out var unknownForRecord);
        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified);
        var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified);

        var sb = new StringBuilder();
        sb.AppendLine("VSA Zustandsklasse - Rechnungsweg");
        sb.AppendLine($"Haltung: {SafeField(record.GetFieldValue("Haltungsname"))}");
        sb.AppendLine($"Klassifikationstabelle: {tableResult.Value.SourceName}");
        sb.AppendLine($"Anzahl Feststellungen: {findings.Count}");
        sb.AppendLine($"Unbekannte Codes: {unknownForRecord}");
        sb.AppendLine();

        AppendRequirementSection(sb, d);
        AppendRequirementSection(sb, s);
        AppendRequirementSection(sb, b);

        if (classified.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Codes:");
            foreach (var item in classified)
            {
                var code = NormalizeCode(item.Finding.KanalSchadencode);
                var marker = item.IsUnknown ? " (unbekannt)" : string.Empty;
                sb.AppendLine(
                    $"- {code}: D={FmtEz(item.Classification.EZD)}, S={FmtEz(item.Classification.EZS)}, B={FmtEz(item.Classification.EZB)}{marker}");
            }
        }

        return Result<string>.Success(sb.ToString());
    }

    private Result<LoadedTable> LoadClassificationTable()
    {
        var candidates = new[] { _channelsTablePath, _manholesTablePath };
        foreach (var path in candidates)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            try
            {
                var table = VsaClassificationTable.LoadFromFile(path);
                return Result<LoadedTable>.Success(new LoadedTable(table, Path.GetFileName(path)));
            }
            catch (Exception ex)
            {
                return Result<LoadedTable>.Fail("VSA_TABLE_PARSE_FAILED", $"Cannot read table '{path}': {ex.Message}");
            }
        }

        return Result<LoadedTable>.Fail(
            "VSA_TABLE_MISSING",
            $"Classification table not found. Expected '{_channelsTablePath}' or '{_manholesTablePath}'.");
    }

    private static List<VsaFinding> ResolveFindings(HaltungRecord record, IReadOnlySet<string> knownCodes)
    {
        if (record.VsaFindings is { Count: > 0 })
        {
            return record.VsaFindings
                .Where(f => !string.IsNullOrWhiteSpace(f.KanalSchadencode))
                .Select(f => f)
                .ToList();
        }

        return ParseFindingsFromPrimaryDamage(record.GetFieldValue("Primaere_Schaeden"), knownCodes);
    }

    private static List<ClassifiedFinding> ClassifyFindings(
        IEnumerable<VsaFinding> findings,
        VsaClassificationTable table,
        out int unknownCodeCount)
    {
        var list = new List<ClassifiedFinding>();
        unknownCodeCount = 0;

        foreach (var finding in findings)
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            if (code.Length == 0)
                continue;

            var rule = table.Find(code);
            if (rule is null)
            {
                unknownCodeCount++;
                list.Add(new ClassifiedFinding(finding, new VsaClassificationResult(null, null, null), true));
                continue;
            }

            list.Add(new ClassifiedFinding(
                finding,
                new VsaClassificationResult(rule.EZD, rule.EZS, rule.EZB),
                false));
        }

        return list;
    }

    private static VsaConditionResult ComputeForRequirement(VsaRequirement requirement, IReadOnlyList<ClassifiedFinding> classified)
    {
        var values = requirement switch
        {
            VsaRequirement.Dichtheit => classified.Select(c => c.Classification.EZD).Where(v => v is not null).Select(v => v!.Value).ToList(),
            VsaRequirement.Standsicherheit => classified.Select(c => c.Classification.EZS).Where(v => v is not null).Select(v => v!.Value).ToList(),
            _ => classified.Select(c => c.Classification.EZB).Where(v => v is not null).Select(v => v!.Value).ToList()
        };

        var worst = values.Count == 0 ? (int?)null : values.Max();
        var note = worst is null ? (double?)null : worst.Value;
        var dz = worst is null ? (double?)null : Math.Round(worst.Value * 25.0, 2, MidpointRounding.AwayFromZero);

        var result = new VsaConditionResult
        {
            Requirement = requirement,
            Zustandsnote = note,
            WorstEinzelzustand = worst,
            Abminderung = null,
            Dringlichkeitszahl = dz
        };

        result.Notes.Add($"Contributions={values.Count}");
        if (values.Count == 0)
            result.Notes.Add("Keine klassifizierbaren Codes vorhanden.");

        return result;
    }

    private static void ApplyRecordFields(
        HaltungRecord record,
        VsaConditionResult dResult,
        VsaConditionResult sResult,
        VsaConditionResult bResult)
    {
        var dNote = dResult.Zustandsnote;
        record.SetFieldValue("VSA_Zustandsnote_D", FmtNote(dNote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_S", FmtNote(sResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_B", FmtNote(bResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Zustandsklasse", MapZustandsklasse(dNote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Pruefungsresultat", BuildPruefungsresultat(dNote), FieldSource.Legacy, userEdited: false);
    }

    private static void AppendRequirementSection(StringBuilder sb, VsaConditionResult result)
    {
        sb.AppendLine($"Anforderung {result.Requirement}:");
        sb.AppendLine($"  EZmin: {FmtEz(result.WorstEinzelzustand)}");
        sb.AppendLine($"  Zustandsnote: {FmtNote(result.Zustandsnote)}");
        sb.AppendLine($"  Dringlichkeitszahl: {FmtNote(result.Dringlichkeitszahl)}");
        if (result.Notes.Count > 0)
            sb.AppendLine($"  Hinweise: {string.Join("; ", result.Notes)}");
    }

    private static string BuildPruefungsresultat(double? note)
    {
        if (note is null)
            return "n/a";

        if (note.Value <= 1.0)
            return "i.O.";
        if (note.Value <= 2.0)
            return "beobachten";
        return "Sanierungsbedarf";
    }

    private static string MapZustandsklasse(double? note)
    {
        if (note is null)
            return "n/a";

        var value = (int)Math.Clamp(
            Math.Round(note.Value, MidpointRounding.AwayFromZero),
            min: 0,
            max: 5);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static List<VsaFinding> ParseFindingsFromPrimaryDamage(string? raw, IReadOnlySet<string> knownCodes)
    {
        var findings = new List<VsaFinding>();
        if (string.IsNullOrWhiteSpace(raw))
            return findings;

        var lines = raw.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var match = LeadingTokenRegex.Match(line);
            if (!match.Success)
                continue;

            var code = NormalizeCode(match.Value);
            if (code.Length < 3)
                continue;

            if (knownCodes.Count > 0 && !knownCodes.Contains(code) && code.Length > 4)
                continue;

            findings.Add(new VsaFinding
            {
                KanalSchadencode = code,
                Raw = line
            });
        }

        return findings;
    }

    private static string FmtEz(int? ez)
        => ez is null ? "n/a" : ez.Value.ToString(CultureInfo.InvariantCulture);

    private static string FmtNote(double? value)
        => value is null ? "n/a" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string SafeField(string? s)
        => string.IsNullOrWhiteSpace(s) ? "n/a" : s.Trim();

    internal static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        var cutChars = new[] { ' ', '@', '(' };
        var idx = s.IndexOfAny(cutChars);
        if (idx >= 0)
            s = s.Substring(0, idx);

        s = Regex.Replace(s, @"[^A-Za-z0-9]+", string.Empty);
        return s.ToUpperInvariant();
    }

    private sealed record ClassifiedFinding(
        VsaFinding Finding,
        VsaClassificationResult Classification,
        bool IsUnknown);

    private sealed record LoadedTable(
        VsaClassificationTable Table,
        string SourceName);
}
