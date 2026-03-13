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

            var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
            const double minLength = 3.0; // Kanäle; Schächte: 0.5
            var rb = ComputeRandbedingungen(record);

            var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
            var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
            var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

            ApplyRecordFields(record, d, s, b);

            results.Add(d);
            results.Add(s);
            results.Add(b);
        }

        project.Metadata["VSA_Diag"] =
            $"Records={project.Data.Count}; UnknownCodes={unknownCodeCount}; Table={tableResult.Value.SourceName}";

        return Result<IReadOnlyList<VsaConditionResult>>.Success(results);
    }

    public Result<bool> EvaluateRecord(HaltungRecord record)
    {
        if (record is null)
            return Result<bool>.Fail("VSA_RECORD_NULL", "Record is null.");

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

        var assessmentLength = ParseDouble(record.GetFieldValue("Haltungslaenge_m"));
        const double minLength = 3.0;
        var rb = ComputeRandbedingungen(record);

        var d = ComputeForRequirement(VsaRequirement.Dichtheit, classified, assessmentLength, minLength, rb);
        var s = ComputeForRequirement(VsaRequirement.Standsicherheit, classified, assessmentLength, minLength, rb);
        var b = ComputeForRequirement(VsaRequirement.Betriebssicherheit, classified, assessmentLength, minLength, rb);

        ApplyRecordFields(record, d, s, b);

        return Result<bool>.Success(true);
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
            sb.AppendLine($"Dringlichkeit: {MapDringlichkeit(worstDz)}");
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

    // ── Tabelle laden ────────────────────────────────────────────────────

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

    // ── Feststellungen auflösen / klassifizieren ─────────────────────────

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

    // ── Kernberechnung: Zustandsnote + Dringlichkeitszahl ────────────────

    /// <summary>
    /// Berechnet ZN und DZ für eine Anforderung gemäss VSA Richtlinie 2023.
    /// ZN = EZ_min + 0.4 - A  (Kap. 5.2, Formel 1)
    /// DZ = ZN × 100 × B1 × B2 × B3 × B4  (Kap. 5.3, Formel 2)
    /// </summary>
    private static VsaConditionResult ComputeForRequirement(
        VsaRequirement requirement,
        IReadOnlyList<ClassifiedFinding> classified,
        double assessmentLength,
        double minLength,
        double randbedingungen)
    {
        // EZ-Werte mit Längenfaktoren sammeln
        var entries = new List<(int EZ, double LF)>();
        foreach (var c in classified)
        {
            int? ez = requirement switch
            {
                VsaRequirement.Dichtheit => c.Classification.EZD,
                VsaRequirement.Standsicherheit => c.Classification.EZS,
                _ => c.Classification.EZB
            };
            if (ez is null) continue;
            entries.Add((ez.Value, ComputeLengthFactor(c.Finding, minLength)));
        }

        if (entries.Count == 0)
        {
            // Unterscheide: keine Findings vs. nur unbekannte Codes
            var hasUnknown = classified.Any(c => c.IsUnknown);
            if (hasUnknown)
            {
                // Unbekannte Codes vorhanden, aber keine klassifizierbaren EZ-Werte
                // → Bewertung nicht möglich (n/a), NICHT "Leitung i.O."
                var na = new VsaConditionResult
                {
                    Requirement = requirement,
                    Zustandsnote = null,
                    WorstEinzelzustand = null,
                    Abminderung = null,
                    Dringlichkeitszahl = null
                };
                na.Notes.Add("Nur unbekannte Schadenscodes – Bewertung nicht möglich.");
                return na;
            }

            // Tatsächlich keine Schadenscodes für diese Anforderung → Leitung i.O. (ZN = 4.0)
            var dzOk = Math.Round(4.0 * 100.0 * randbedingungen, 2, MidpointRounding.AwayFromZero);
            var ok = new VsaConditionResult
            {
                Requirement = requirement,
                Zustandsnote = 4.00,
                WorstEinzelzustand = 4,
                Abminderung = 0,
                Dringlichkeitszahl = dzOk
            };
            ok.Notes.Add("Keine Schadenscodes vorhanden – Leitung i.O.");
            return ok;
        }

        // EZ_min = schlechtester Einzelzustand (0 = schlecht, 4 = gut)
        var ezMin = entries.Min(e => e.EZ);

        double zn;
        double abminderung = 0;

        if (ezMin == 4)
        {
            // Bestmöglicher Zustand – keine Abminderung
            zn = 4.00;
        }
        else
        {
            // ZN_start = EZ_min + 0.4
            var znStart = ezMin + 0.4;

            // Abminderung A = 0.4 × Σ((4 - EZ_i) × LF_i) / ((4 - EZ_min) × LA)
            if (assessmentLength > 0)
            {
                var sumNumerator = entries.Sum(e => (4.0 - e.EZ) * e.LF);
                var denominator = (4.0 - ezMin) * assessmentLength;
                if (denominator > 0)
                {
                    abminderung = 0.4 * sumNumerator / denominator;
                    abminderung = Math.Min(abminderung, 0.8); // A ≤ 0.8
                }
            }

            zn = Math.Max(znStart - abminderung, 0); // ZN ≥ 0
        }

        zn = Math.Round(zn, 2, MidpointRounding.AwayFromZero);
        zn = Math.Min(zn, 4.00); // sicherheitshalber kappen
        abminderung = Math.Round(abminderung, 2, MidpointRounding.AwayFromZero);

        // DZ = ZN × 100 × Π(B_j)
        var dz = Math.Round(zn * 100.0 * randbedingungen, 2, MidpointRounding.AwayFromZero);

        var result = new VsaConditionResult
        {
            Requirement = requirement,
            Zustandsnote = zn,
            WorstEinzelzustand = ezMin,
            Abminderung = abminderung,
            Dringlichkeitszahl = dz
        };

        result.Notes.Add($"Beiträge={entries.Count}; A={abminderung:F2}; RB={randbedingungen:F4}");
        return result;
    }

    // ── Record-Felder setzen ─────────────────────────────────────────────

    private static void ApplyRecordFields(
        HaltungRecord record,
        VsaConditionResult dResult,
        VsaConditionResult sResult,
        VsaConditionResult bResult)
    {
        record.SetFieldValue("VSA_Zustandsnote_D", FmtNote(dResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_S", FmtNote(sResult.Zustandsnote), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("VSA_Zustandsnote_B", FmtNote(bResult.Zustandsnote), FieldSource.Legacy, userEdited: false);

        // Gesamt: schlechteste (=niedrigste) ZN über D/S/B
        var allZn = new[] { dResult.Zustandsnote, sResult.Zustandsnote, bResult.Zustandsnote }
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        var worstZn = allZn.Count > 0 ? (double?)allZn.Min() : null;

        record.SetFieldValue("Zustandsklasse", MapZustandsklasse(worstZn), FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Pruefungsresultat", BuildPruefungsresultat(worstZn), FieldSource.Legacy, userEdited: false);
    }

    private static void AppendRequirementSection(StringBuilder sb, VsaConditionResult result)
    {
        sb.AppendLine($"Anforderung {result.Requirement}:");
        sb.AppendLine($"  EZmin: {FmtEz(result.WorstEinzelzustand)}");
        sb.AppendLine($"  Abminderung A: {FmtNote(result.Abminderung)}");
        sb.AppendLine($"  Zustandsnote: {FmtNote(result.Zustandsnote)}");
        sb.AppendLine($"  Dringlichkeitszahl: {FmtNote(result.Dringlichkeitszahl)}");
        sb.AppendLine($"  Dringlichkeit: {MapDringlichkeit(result.Dringlichkeitszahl)}");
        if (result.Notes.Count > 0)
            sb.AppendLine($"  Hinweise: {string.Join("; ", result.Notes)}");
    }

    // ── Randbedingungen (VSA Richtlinie 2023, Kap. 5.3, Tabellen 3-6) ──

    /// <summary>Berechnet Π(B_j) = B1 × B2 × B3 × B4.</summary>
    private static double ComputeRandbedingungen(HaltungRecord record)
    {
        var b1 = ComputeB1(record.GetFieldValue("Gewaesserschutz"));
        var b2 = ComputeB2(record.GetFieldValue("Nutzungsart"));
        var b3 = ComputeB3(record.GetFieldValue("Grundwasserspiegel"));
        var b4 = ComputeB4(record.GetFieldValue("FunktionHierarchisch"));
        return b1 * b2 * b3 * b4;
    }

    // Tabelle 3: Gewässer-/Grundwasserschutz
    private static double ComputeB1(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "S"  => 0.90,
        "AU" => 0.95,
        "ZU" => 0.95,
        "AO" => 0.95,
        _    => 1.00
    };

    // Tabelle 4: Nutzungsart
    private static double ComputeB2(string? value) => value?.Trim() switch
    {
        "Bachwasser"        => 1.10,
        "Industrieabwasser" => 0.90,
        "Schmutzwasser" or "Schmutzabwasser" => 0.95,
        "Mischabwasser"     => 1.00,
        "Regenwasser" or "Meteorwasser"      => 1.05,
        _                   => 1.00
    };

    // Tabelle 5: Grundwasserspiegel
    // GW oberhalb → Infiltrationsrisiko hoeher → DZ soll sinken (dringender) → Faktor < 1.0
    // GW unterhalb → geringeres Risiko → DZ soll steigen (weniger dringend) → Faktor > 1.0
    private static double ComputeB3(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "oberhalb"  => 0.90,
        "unterhalb" => 1.10,
        _           => 1.00 // unbekannt
    };

    // Tabelle 6: Funktionale Hierarchie (PAA gemäss VSA-DSS)
    private static double ComputeB4(string? value) => value?.Trim() switch
    {
        "PAA.Hauptsammelkanal"          => 0.95,
        "PAA.Hauptsammelkanal_regional" => 0.90,
        "PAA.Liegenschaftsentwaesserung" or "PAA.Liegenschaftsentwässerung" => 1.10,
        "PAA.Sammelkanal"               => 1.00,
        "PAA.Sanierungsleitung"         => 1.00,
        "PAA.Strassenentwaesserung" or "PAA.Strassenentwässerung" => 1.00,
        "PAA.Gewaesser" or "PAA.Gewässer" => 1.00,
        _                               => 1.00
    };

    // ── Längenfaktor ─────────────────────────────────────────────────────

    /// <summary>
    /// LF_i: Längenfaktor pro Feststellung.
    /// Punktfeststellungen: minLength (3.0m Kanäle, 0.5m Schächte).
    /// Streckenfeststellungen: tatsächliche Länge wenn > minLength.
    /// </summary>
    private static double ComputeLengthFactor(VsaFinding finding, double minLength)
    {
        double? actualLength = null;
        if (finding.SchadenlageAnfang.HasValue && finding.SchadenlageEnde.HasValue)
            actualLength = Math.Abs(finding.SchadenlageEnde.Value - finding.SchadenlageAnfang.Value);
        else if (finding.MeterStart.HasValue && finding.MeterEnd.HasValue)
            actualLength = Math.Abs(finding.MeterEnd.Value - finding.MeterStart.Value);

        return actualLength.HasValue && actualLength.Value > minLength
            ? actualLength.Value
            : minLength;
    }

    // ── Mappings ─────────────────────────────────────────────────────────

    /// <summary>ZN (0=schlecht, 4=gut) → Prüfungsresultat.</summary>
    private static string BuildPruefungsresultat(double? note)
    {
        if (note is null)
            return "n/a";

        // ZN 0 = schlechtester Zustand, ZN 4 = bester Zustand
        if (note.Value >= 3.0)
            return "i.O.";
        if (note.Value >= 1.5)
            return "beobachten";
        return "Sanierungsbedarf";
    }

    /// <summary>DZ → Dringlichkeitsstufe (VSA Richtlinie, Tabelle 7).</summary>
    private static string MapDringlichkeit(double? dz)
    {
        if (dz is null) return "n/a";
        return dz.Value switch
        {
            < 50  => "Sofort",
            < 150 => "Kurzfristig (3J)",
            < 250 => "Mittelfristig (8J)",
            < 350 => "Langfristig",
            _     => "Keine"
        };
    }

    private static string MapZustandsklasse(double? note)
    {
        if (note is null)
            return "n/a";

        var value = (int)Math.Clamp(
            Math.Round(note.Value, MidpointRounding.AwayFromZero),
            min: 0,
            max: 4);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // ── Parse-Hilfen ─────────────────────────────────────────────────────

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

    // ── Interne Records ──────────────────────────────────────────────────

    private sealed record ClassifiedFinding(
        VsaFinding Finding,
        VsaClassificationResult Classification,
        bool IsUnknown);

    private sealed record LoadedTable(
        VsaClassificationTable Table,
        string SourceName);
}
