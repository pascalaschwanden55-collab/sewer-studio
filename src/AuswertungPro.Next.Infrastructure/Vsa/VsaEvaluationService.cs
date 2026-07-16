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

/// <summary>
/// VSA Zustandsbeurteilung gemäss VSA Richtlinie 2023.
/// Berechnet Zustandsnote (ZN), Abminderung (A) und Dringlichkeitszahl (DZ)
/// pro Anforderung (Dichtheit, Standsicherheit, Betriebssicherheit).
/// </summary>
public sealed class VsaEvaluationService : IVsaEvaluationService
{
    private static readonly HashSet<string> ExpectedShadowDriftCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BAA",
        "BAB",
        "BAC",
        "BAF",
        "BBA",
        "BDD"
    };

    private readonly string _channelsTablePath;
    private readonly string _manholesTablePath;
    private readonly bool _shadowModeEnabled;
    private readonly string? _shadowLogPath;
    private readonly bool _useV2Engine;
    private readonly string _v2ChannelsTablePath;
    private readonly string _v2ManholesTablePath;
    private readonly IVsaShadowTelemetryWriter _shadowTelemetry;

    public VsaEvaluationService(
        string channelsTablePath,
        string manholesTablePath,
        bool shadowModeEnabled = true,
        string? shadowLogPath = null,
        bool useV2Engine = true,
        string? v2ChannelsTablePath = null,
        string? v2ManholesTablePath = null)
        : this(
            channelsTablePath,
            manholesTablePath,
            new VsaShadowTelemetryFileWriter(),
            shadowModeEnabled,
            shadowLogPath,
            useV2Engine,
            v2ChannelsTablePath,
            v2ManholesTablePath)
    {
    }

    public VsaEvaluationService(
        string channelsTablePath,
        string manholesTablePath,
        IVsaShadowTelemetryWriter shadowTelemetry,
        bool shadowModeEnabled = true,
        string? shadowLogPath = null,
        bool useV2Engine = true,
        string? v2ChannelsTablePath = null,
        string? v2ManholesTablePath = null)
    {
        _channelsTablePath = channelsTablePath;
        _manholesTablePath = manholesTablePath;
        _shadowTelemetry = shadowTelemetry ?? throw new ArgumentNullException(nameof(shadowTelemetry));
        _shadowModeEnabled = shadowModeEnabled;
        _shadowLogPath = shadowLogPath;
        _useV2Engine = useV2Engine;
        _v2ChannelsTablePath = v2ChannelsTablePath
            ?? Path.Combine(Path.GetDirectoryName(channelsTablePath) ?? "", "vsa_zustandsklassifizierung_2023_channels.json");
        _v2ManholesTablePath = v2ManholesTablePath
            ?? Path.Combine(Path.GetDirectoryName(manholesTablePath) ?? "", "vsa_zustandsklassifizierung_2023_manholes.json");
    }

    public Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project)
    {
        if (project is null)
            return Result<IReadOnlyList<VsaConditionResult>>.Fail("VSA_PROJECT_NULL", "Project is null.");

        if (_useV2Engine)
            return EvaluateWithV2(project);

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
        var shadowSelector = TryLoadShadowSelector();

        foreach (var record in project.Data)
        {
            var findings = ResolveFindings(record, knownCodes);
            var classified = ClassifyFindings(findings, table, out var unknownForRecord);
            unknownCodeCount += unknownForRecord;
            WriteShadowDiffs(record, classified, shadowSelector);

            var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
            const double minLength = 3.0; // Kanäle; Schächte: 0.5
            var rb = ComputeRandbedingungen(record);

            var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
            var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
            var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

            ApplyRecordFields(record, d, s, b, classified.Any(c => c.Approximated));

            results.Add(d);
            results.Add(s);
            results.Add(b);
        }

        project.Metadata["VSA_Diag"] =
            $"Records={project.Data.Count}; UnknownCodes={unknownCodeCount}; Table={tableResult.Value.SourceName}";
        project.Metadata["VSA_Table"] = tableResult.Value.SourceName;

        return Result<IReadOnlyList<VsaConditionResult>>.Success(results);
    }

    public Result<bool> EvaluateRecord(HaltungRecord record)
    {
        if (record is null)
            return Result<bool>.Fail("VSA_RECORD_NULL", "Record is null.");

        if (_useV2Engine)
            return EvaluateRecordWithV2(record);

        var tableResult = LoadClassificationTable();
        if (!tableResult.Ok || tableResult.Value is null)
            return Result<bool>.Fail(
                tableResult.ErrorCode ?? "VSA_TABLE_LOAD_FAILED",
                tableResult.ErrorMessage ?? "Classification table could not be loaded.");

        var table = tableResult.Value.Table;
        var knownCodes = new HashSet<string>(
            table.Rules.Select(r => NormalizeCode(r.Code)).Where(c => c.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var findings = ResolveFindings(record, knownCodes);
        var classified = ClassifyFindings(findings, table, out _);
        WriteShadowDiffs(record, classified, TryLoadShadowSelector());

        var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
        const double minLength = 3.0;
        var rb = ComputeRandbedingungen(record);

        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
        var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

        ApplyRecordFields(record, d, s, b, classified.Any(c => c.Approximated));

        return Result<bool>.Success(true);
    }

    private Result<IReadOnlyList<VsaConditionResult>> EvaluateWithV2(Project project)
    {
        var modelResult = LoadV2ClassificationModel();
        if (!modelResult.Ok || modelResult.Value is null)
            return Result<IReadOnlyList<VsaConditionResult>>.Fail(
                modelResult.ErrorCode ?? "VSA_V2_TABLE_LOAD_FAILED",
                modelResult.ErrorMessage ?? "VSA-v2 classification model could not be loaded.");

        var model = modelResult.Value;
        var results = new List<VsaConditionResult>(project.Data.Count * 3);
        var unknownCodeCount = 0;

        foreach (var record in project.Data)
        {
            var findings = ResolveFindings(record, model.KnownCodes);
            var classified = ClassifyFindingsV2(findings, model.Selector, record, model.KnownCodes, model.ApproxEz, out var unknownForRecord);
            unknownCodeCount += unknownForRecord;

            var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
            const double minLength = 3.0;
            var rb = ComputeRandbedingungen(record);

            var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
            var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
            var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

            ApplyRecordFields(record, d, s, b, classified.Any(c => c.Approximated));

            results.Add(d);
            results.Add(s);
            results.Add(b);
        }

        project.Metadata["VSA_Diag"] =
            $"Records={project.Data.Count}; UnknownCodes={unknownCodeCount}; Table={model.SourceName}";
        project.Metadata["VSA_Table"] = model.SourceName;

        return Result<IReadOnlyList<VsaConditionResult>>.Success(results);
    }

    private Result<bool> EvaluateRecordWithV2(HaltungRecord record)
    {
        var modelResult = LoadV2ClassificationModel();
        if (!modelResult.Ok || modelResult.Value is null)
            return Result<bool>.Fail(
                modelResult.ErrorCode ?? "VSA_V2_TABLE_LOAD_FAILED",
                modelResult.ErrorMessage ?? "VSA-v2 classification model could not be loaded.");

        var model = modelResult.Value;
        var findings = ResolveFindings(record, model.KnownCodes);
        var classified = ClassifyFindingsV2(findings, model.Selector, record, model.KnownCodes, model.ApproxEz, out _);

        var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
        const double minLength = 3.0;
        var rb = ComputeRandbedingungen(record);

        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
        var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

        ApplyRecordFields(record, d, s, b, classified.Any(c => c.Approximated));

        return Result<bool>.Success(true);
    }

    private VsaClassificationRuleSelector? TryLoadShadowSelector()
    {
        if (!_shadowModeEnabled)
            return null;

        if (!File.Exists(_v2ChannelsTablePath) || !File.Exists(_v2ManholesTablePath))
            return null;

        try
        {
            return VsaClassificationRuleSelector.Load(_v2ChannelsTablePath, _v2ManholesTablePath);
        }
        catch
        {
            return null;
        }
    }

    private void WriteShadowDiffs(
        HaltungRecord record,
        IReadOnlyList<ClassifiedFinding> classified,
        VsaClassificationRuleSelector? selector)
    {
        if (selector is null || classified.Count == 0)
            return;

        foreach (var item in classified)
        {
            var rawCode = NormalizeCode(item.Finding.KanalSchadencode);
            if (rawCode.Length < 3)
                continue;

            var baseCode = rawCode[..3];
            var ch1 = rawCode.Length >= 4 ? rawCode.Substring(3, 1) : null;
            var ch2 = rawCode.Length >= 5 ? rawCode.Substring(4, 1) : null;
            var q1 = item.Finding.Quantifizierung1;
            var q2 = item.Finding.Quantifizierung2;
            var material = record.GetFieldValue("Rohrmaterial");
            var dn = record.GetFieldValue("DN_mm");
            var outcome = selector.Classify(new VsaClassificationRequest(
                Code: baseCode,
                Ch1: ch1,
                Ch2: ch2,
                Q1: q1,
                Q2: q2,
                Material: material,
                AssetKind: baseCode.StartsWith('D') ? "manhole" : "channel"));

            WriteRequirementDiff(rawCode, baseCode, "D", item.Classification.EZD, outcome.D, ResolveV2Reason(outcome, "D"), ch1, ch2, q1, q2, material, dn);
            WriteRequirementDiff(rawCode, baseCode, "S", item.Classification.EZS, outcome.S, ResolveV2Reason(outcome, "S"), ch1, ch2, q1, q2, material, dn);
            WriteRequirementDiff(rawCode, baseCode, "B", item.Classification.EZB, outcome.B, ResolveV2Reason(outcome, "B"), ch1, ch2, q1, q2, material, dn);
        }
    }

    private void WriteRequirementDiff(
        string code,
        string baseCode,
        string requirement,
        int? legacyEz,
        VsaRequirementOutcome? v2Outcome,
        string? v2Reason,
        string? ch1,
        string? ch2,
        string? q1,
        string? q2,
        string? material,
        string? dn)
    {
        var v2Ez = v2Outcome?.Ez;
        if (legacyEz == v2Ez)
            return;

        try
        {
            _shadowTelemetry.Write(new VsaShadowTelemetryEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                Code: code,
                BaseCode: baseCode,
                Requirement: requirement,
                LegacyEz: legacyEz,
                V2Ez: v2Ez,
                ExpectedDrift: ExpectedShadowDriftCodes.Contains(baseCode),
                V2Reason: v2Reason,
                Ch1: ch1,
                Ch2: ch2,
                Q1: q1,
                Q2: q2,
                Material: material,
                Dn: dn,
                V2RuleId: v2Outcome?.RuleId,
                V2SourceRef: v2Outcome?.SourceRef),
                _shadowLogPath);
        }
        catch
        {
            // Auch ein ersetzter Schreiber darf die produktive Auswertung nie beeinflussen.
        }
    }

    private static string? ResolveV2Reason(VsaClassificationOutcome outcome, string requirement)
        => outcome.Diagnostics
            .FirstOrDefault(d => d.Requirement.Equals(requirement, StringComparison.OrdinalIgnoreCase))
            ?.Reason;

    public Result<string> Explain(Project project, HaltungRecord record)
    {
        if (project is null)
            return Result<string>.Fail("VSA_PROJECT_NULL", "Project is null.");
        if (record is null)
            return Result<string>.Fail("VSA_RECORD_NULL", "Record is null.");

        if (_useV2Engine)
            return ExplainWithV2(project, record);

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

        var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
        const double minLength = 3.0;
        var rb = ComputeRandbedingungen(record);

        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
        var bResult = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

        var sb = new StringBuilder();
        sb.AppendLine("VSA Zustandsbeurteilung - Rechnungsweg (VSA Richtlinie 2023)");
        sb.AppendLine($"Haltung: {SafeField(record.GetFieldValue("Haltungsname"))}");
        sb.AppendLine($"Klassifikationstabelle: {tableResult.Value.SourceName}");
        sb.AppendLine($"Haltungslänge: {assessmentLength:F1} m");
        sb.AppendLine($"Anzahl Feststellungen: {findings.Count}");
        sb.AppendLine($"Unbekannte Codes: {unknownForRecord}");
        sb.AppendLine($"Randbedingungen: B1×B2×B3×B4 = {rb:F4}");
        sb.AppendLine();

        AppendRequirementSection(sb, d);
        AppendRequirementSection(sb, s);
        AppendRequirementSection(sb, bResult);

        // Gesamt-Zustandsnote (schlechteste über D/S/B)
        var allZn = new[] { d.Zustandsnote, s.Zustandsnote, bResult.Zustandsnote }
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        if (allZn.Count > 0)
        {
            var worstZn = allZn.Min();
            var allDz = new[] { d.Dringlichkeitszahl, s.Dringlichkeitszahl, bResult.Dringlichkeitszahl }
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var worstDz = allDz.Count > 0 ? (double?)allDz.Min() : null;
            sb.AppendLine();
            sb.AppendLine($"Gesamt-Zustandsnote (min D/S/B): {FmtNote(worstZn)}");
            sb.AppendLine($"Gesamt-Dringlichkeitszahl: {FmtNote(worstDz)}");
            sb.AppendLine($"Dringlichkeit: {VsaConditionScorer.MapDringlichkeit(worstDz)}");
        }

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

    // -- Erklaerung v2 --

    private Result<string> ExplainWithV2(Project project, HaltungRecord record)
    {
        var modelResult = LoadV2ClassificationModel();
        if (!modelResult.Ok || modelResult.Value is null)
            return Result<string>.Fail(
                modelResult.ErrorCode ?? "VSA_V2_TABLE_LOAD_FAILED",
                modelResult.ErrorMessage ?? "VSA-v2 classification model could not be loaded.");

        var model = modelResult.Value;
        var findings = ResolveFindings(record, model.KnownCodes);
        var classified = ClassifyFindingsV2(findings, model.Selector, record, model.KnownCodes, model.ApproxEz, out var unknownForRecord);

        var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
        const double minLength = 3.0;
        var rb = ComputeRandbedingungen(record);

        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
        var bResult = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

        var sb = new StringBuilder();
        sb.AppendLine("VSA Zustandsbeurteilung - Rechnungsweg (VSA Richtlinie 2023)");
        sb.AppendLine($"Haltung: {SafeField(record.GetFieldValue("Haltungsname"))}");
        sb.AppendLine($"Klassifikationstabelle: {model.SourceName}");
        sb.AppendLine($"Haltungslaenge: {assessmentLength:F1} m");
        sb.AppendLine($"Anzahl Feststellungen: {findings.Count}");
        sb.AppendLine($"Unbekannte Codes: {unknownForRecord}");
        sb.AppendLine($"Randbedingungen: B1xB2xB3xB4 = {rb:F4}");
        sb.AppendLine();

        AppendRequirementSection(sb, d);
        AppendRequirementSection(sb, s);
        AppendRequirementSection(sb, bResult);

        var allZn = new[] { d.Zustandsnote, s.Zustandsnote, bResult.Zustandsnote }
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        if (allZn.Count > 0)
        {
            var worstZn = allZn.Min();
            var allDz = new[] { d.Dringlichkeitszahl, s.Dringlichkeitszahl, bResult.Dringlichkeitszahl }
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var worstDz = allDz.Count > 0 ? (double?)allDz.Min() : null;
            sb.AppendLine();
            sb.AppendLine($"Gesamt-Zustandsnote (min D/S/B): {FmtNote(worstZn)}");
            sb.AppendLine($"Gesamt-Dringlichkeitszahl: {FmtNote(worstDz)}");
            sb.AppendLine($"Dringlichkeit: {VsaConditionScorer.MapDringlichkeit(worstDz)}");
        }

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

    // -- Tabelle laden --

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

    private Result<LoadedV2Model> LoadV2ClassificationModel()
    {
        if (!File.Exists(_v2ChannelsTablePath) || !File.Exists(_v2ManholesTablePath))
        {
            return Result<LoadedV2Model>.Fail(
                "VSA_V2_TABLE_MISSING",
                $"VSA-v2 classification tables not found. Expected '{_v2ChannelsTablePath}' and '{_v2ManholesTablePath}'.");
        }

        try
        {
            var channels = VsaClassificationRuleSet.LoadFromFile(_v2ChannelsTablePath);
            var manholes = VsaClassificationRuleSet.LoadFromFile(_v2ManholesTablePath);
            var selector = new VsaClassificationRuleSelector(channels, manholes);
            var knownCodes = BuildKnownV2Codes(channels, manholes);
            var approxEz = BuildApproxEz(channels, manholes);
            return Result<LoadedV2Model>.Success(new LoadedV2Model(
                selector,
                knownCodes,
                approxEz,
                Path.GetFileName(_v2ChannelsTablePath)));
        }
        catch (Exception ex)
        {
            return Result<LoadedV2Model>.Fail(
                "VSA_V2_TABLE_PARSE_FAILED",
                $"Cannot read VSA-v2 classification tables: {ex.Message}");
        }
    }

    private static HashSet<string> BuildKnownV2Codes(params VsaClassificationRuleSet[] ruleSets)
    {
        var knownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ruleSet in ruleSets)
        {
            foreach (var rule in ruleSet.Rules)
                AddKnownCode(knownCodes, rule.Code);
            foreach (var item in ruleSet.NonAssessableCodes)
                AddKnownCode(knownCodes, item.Code);
            foreach (var item in ruleSet.NonAssessableRequirements)
                AddKnownCode(knownCodes, item.Code);
        }

        return knownCodes;
    }

    private static void AddKnownCode(HashSet<string> knownCodes, string? code)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length > 0)
            knownCodes.Add(normalized);
    }

    // Naeherungstabelle: Basiscode -> EZ (wenn kein Messwert vorhanden).
    private static IReadOnlyDictionary<string, int> BuildApproxEz(params VsaClassificationRuleSet[] ruleSets)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ruleSet in ruleSets)
            foreach (var item in ruleSet.ApproximateEzWhenUnquantified)
            {
                var code = NormalizeCode(item.Code);
                if (code.Length > 0 && item.Ez is >= 0 and <= 4)
                    map[code] = item.Ez;
            }
        return map;
    }

    // -- Feststellungen aufloesen / klassifizieren --

    internal static List<VsaFinding> ResolveFindings(HaltungRecord record, IReadOnlySet<string> knownCodes)
    {
        if (record.VsaFindings is { Count: > 0 })
        {
            var primaryDamageText = record.GetFieldValue("Primaere_Schaeden");
            return EnrichFindingsFromPrimaryDamage(record.VsaFindings, primaryDamageText)
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

            // Classify() berücksichtigt Q1/Q2-Quantifizierung für dynamische EZ-Werte.
            // Fällt automatisch auf statische Defaults zurück wenn Q1/Q2 fehlen.
            var classification = table.Classify(code, finding.Quantifizierung1, finding.Quantifizierung2);
            if (classification is null)
            {
                unknownCodeCount++;
                list.Add(new ClassifiedFinding(finding, new VsaClassificationResult(null, null, null), true));
                continue;
            }

            list.Add(new ClassifiedFinding(finding, classification, false));
        }

        return list;
    }

    // ── Kernberechnung: Zustandsnote + Dringlichkeitszahl ────────────────

    private static List<ClassifiedFinding> ClassifyFindingsV2(
        IEnumerable<VsaFinding> findings,
        VsaClassificationRuleSelector selector,
        HaltungRecord record,
        IReadOnlySet<string> knownCodes,
        IReadOnlyDictionary<string, int> approxEz,
        out int unknownCodeCount)
    {
        var list = new List<ClassifiedFinding>();
        unknownCodeCount = 0;

        foreach (var finding in findings)
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            if (code.Length == 0)
                continue;

            var baseCode = code.Length >= 3 ? code[..3] : code;
            var ch1 = code.Length >= 4 ? code.Substring(3, 1) : null;
            var ch2 = code.Length >= 5 ? code.Substring(4, 1) : null;
            var outcome = selector.Classify(new VsaClassificationRequest(
                Code: baseCode,
                Ch1: ch1,
                Ch2: ch2,
                Q1: finding.Quantifizierung1,
                Q2: finding.Quantifizierung2,
                Material: record.GetFieldValue("Rohrmaterial"),
                AssetKind: baseCode.StartsWith('D') ? "manhole" : "channel"));

            var classification = new VsaClassificationResult(
                outcome.D?.Ez,
                outcome.S?.Ez,
                outcome.B?.Ez);

            var isKnown = knownCodes.Contains(code) || knownCodes.Contains(baseCode);
            var isUnknown = !isKnown
                            && classification.EZD is null
                            && classification.EZS is null
                            && classification.EZB is null;
            if (isUnknown)
                unknownCodeCount++;

            // Bestandsaufnahme-/Beobachtungscodes (nonAssessable in der Klassifizierungstabelle):
            // bekannt, kein EZ-Wert, und alle Diagnostics sagen "rule-not-found" (keine Regel im
            // Regelwerk vorhanden). Solche Codes sind fachlich transparent – Bestandsaufnahme,
            // kein Schaden. Sie werden herausgefiltert, damit eine Haltung mit ausschliesslich
            // Bestandsaufnahme-Codes Zustandsklasse 4 ("Leitung i.O.") bekommt.
            // Abgrenzung zu echten Schadenscodes mit fehlender Quantifizierung (z.B. BAA ohne Q1):
            // Diese haben Diagnostics wie "quantification-missing" oder "ch1-missing" und werden
            // NICHT gefiltert.
            if (isKnown
                && classification.EZD is null
                && classification.EZS is null
                && classification.EZB is null
                && outcome.Diagnostics.Count > 0
                && outcome.Diagnostics.All(d => d.Reason.Equals("rule-not-found", StringComparison.OrdinalIgnoreCase)))
                continue;

            // Naeherung: Schadencode ohne benotbaren Messwert -> Standard-Schaetzwert je Code.
            // Greift nur, wenn die Regeln keinen EZ liefern konnten – echte Messwerte haben Vorrang.
            var approximated = false;
            if (classification.EZD is null
                && classification.EZS is null
                && classification.EZB is null
                && approxEz.TryGetValue(baseCode, out var fallbackEz))
            {
                classification = baseCode.StartsWith("BB", StringComparison.Ordinal)
                    ? classification with { EZB = fallbackEz }   // betriebliche Schaeden
                    : classification with { EZS = fallbackEz };  // strukturelle Schaeden
                approximated = true;
            }

            list.Add(new ClassifiedFinding(finding, classification, isUnknown, approximated));
        }

        return list;
    }

    /// <summary>
    /// Berechnet ZN und DZ fuer eine Anforderung gemaess VSA Richtlinie 2023.
    /// ZN = EZ_min + 0.4 - A  (Kap. 5.2, Formel 1)
    /// DZ = ZN x 100 x B1 x B2 x B3 x B4  (Kap. 5.3, Formel 2)
    /// </summary>
    private static VsaConditionResult ComputeForRequirement(
        VsaRequirement requirement,
        IReadOnlyList<ClassifiedFinding> classified,
        double assessmentLength,
        double minLength,
        double randbedingungen)
        => VsaConditionScorer.ComputeForRequirement(requirement, classified, assessmentLength, minLength, randbedingungen);

    // ── Record-Felder setzen ─────────────────────────────────────────────

    private static void ApplyRecordFields(
        HaltungRecord record,
        VsaConditionResult dResult,
        VsaConditionResult sResult,
        VsaConditionResult bResult,
        bool approximated = false)
    {
        record.SetFieldValue("VSA_Zustandsnote_D", FmtNote(dResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_S", FmtNote(sResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_B", FmtNote(bResult.Zustandsnote), FieldSource.Legacy, userEdited: false);

        // Gesamt: schlechteste (=niedrigste) ZN ueber D/S/B
        var allZn = new[] { dResult.Zustandsnote, sResult.Zustandsnote, bResult.Zustandsnote }
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        var worstZn = allZn.Count > 0 ? (double?)allZn.Min() : null;

        record.SetFieldValue("Zustandsklasse", VsaConditionScorer.MapZustandsklasse(worstZn), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Pruefungsresultat", VsaConditionScorer.BuildPruefungsresultat(worstZn), FieldSource.Legacy, userEdited: false);

        // Markierung, wenn die Note (teilweise) auf Naeherungswerten beruht (fehlende Messwerte).
        record.SetFieldValue("VSA_Geschaetzt", approximated ? "ja" : "", FieldSource.Legacy, userEdited: false);
    }

    private static void AppendRequirementSection(StringBuilder sb, VsaConditionResult result)
        => VsaConditionScorer.AppendRequirementSection(sb, result);

    // ── Randbedingungen / Laengenfaktor / Mappings: delegiert an VsaConditionScorer ──

    /// <summary>Berechnet Π(B_j) = B1 × B2 × B3 × B4.</summary>
    private static double ComputeRandbedingungen(HaltungRecord record)
        => VsaConditionScorer.ComputeRandbedingungen(record);

    // ── Parse-Hilfen: delegiert an PrimaryDamageParser ───────────────────

    private static List<VsaFinding> ParseFindingsFromPrimaryDamage(string? raw, IReadOnlySet<string> knownCodes)
        => PrimaryDamageParser.ParseFindingsFromPrimaryDamage(raw, knownCodes);

    private static IEnumerable<VsaFinding> EnrichFindingsFromPrimaryDamage(
        IEnumerable<VsaFinding> findings,
        string? primaryDamageText)
        => PrimaryDamageParser.EnrichFindingsFromPrimaryDamage(findings, primaryDamageText);

    internal static string? ExtractQuantValue(string? text)
        => PrimaryDamageParser.ExtractQuantValue(text);

    private static double ParseDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string FmtEz(int? ez)
        => ez is null ? "n/a" : ez.Value.ToString(CultureInfo.InvariantCulture);

    private static string FmtNote(double? value)
        => value is null ? "n/a" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string SafeField(string? s)
        => string.IsNullOrWhiteSpace(s) ? "n/a" : s.Trim();

    internal static string NormalizeCode(string? raw)
        => VsaConditionScorer.NormalizeCode(raw);


    // ── Interne Records ──────────────────────────────────────────────────

    private sealed record LoadedTable(
        VsaClassificationTable Table,
        string SourceName);

    private sealed record LoadedV2Model(
        VsaClassificationRuleSelector Selector,
        IReadOnlySet<string> KnownCodes,
        IReadOnlyDictionary<string, int> ApproxEz,
        string SourceName);
}

/// <summary>Klassifiziertes Befundobjekt: Feststellung + Klassifikationsergebnis.</summary>
internal sealed record ClassifiedFinding(
    VsaFinding Finding,
    VsaClassificationResult Classification,
    bool IsUnknown,
    bool Approximated = false);
