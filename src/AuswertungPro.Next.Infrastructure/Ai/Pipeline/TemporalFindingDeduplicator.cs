using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

internal sealed class TemporalDedupOptions
{
    public int DedupWindowFrames { get; init; } = 3;
    public bool NormalizeFallbackLabels { get; init; } = true;
    public bool NormalizeOutputClock { get; init; }
    public double MinStretchLengthMeters { get; init; } = 1.0;
    public double? MeterMergeGapMaxMeters { get; init; }
}

internal sealed class TemporalFindingDeduplicator
{
    private readonly TemporalDedupOptions _options;
    private readonly Dictionary<string, ActiveFindingState> _active = new(StringComparer.OrdinalIgnoreCase);

    public TemporalFindingDeduplicator(TemporalDedupOptions options)
    {
        _options = options;
    }

    public int ActiveCount => _active.Count;

    public IReadOnlyList<RawVideoDetection> Update(
        IReadOnlyList<EnhancedFinding> current,
        double meter,
        EvidenceVector? evidence = null,
        string? meterSource = null,
        bool isMeterEstimated = false)
    {
        var completed = new List<RawVideoDetection>();
        var currentMap = new Dictionary<string, EnhancedFinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in current)
        {
            var key = BuildFindingKey(finding);
            if (!currentMap.ContainsKey(key))
                currentMap[key] = finding;
        }

        foreach (var key in _active.Keys.ToList())
        {
            if (currentMap.TryGetValue(key, out var finding))
            {
                var active = _active[key];
                if (ShouldStartNewFinding(active, meter))
                {
                    completed.Add(active.ToDetection());
                    _active.Remove(key);
                    continue;
                }

                active.Update(
                    meter,
                    finding.Severity,
                    finding.VsaCodeHint,
                    finding.PositionClock,
                    finding.ExtentPercent,
                    finding.HeightMm,
                    finding.WidthMm,
                    finding.IntrusionPercent,
                    finding.CrossSectionReductionPercent,
                    finding.DiameterReductionMm,
                    meterSource,
                    isMeterEstimated,
                    evidence);
            }
            else
            {
                _active[key].MissedFrames++;
                if (_active[key].MissedFrames >= _options.DedupWindowFrames)
                {
                    completed.Add(_active[key].ToDetection());
                    _active.Remove(key);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (_active.ContainsKey(pair.Key))
                continue;

            var finding = pair.Value;
            _active[pair.Key] = new ActiveFindingState(
                finding.Label.Trim(),
                meter,
                finding.Severity,
                finding.VsaCodeHint,
                finding.PositionClock,
                finding.ExtentPercent,
                finding.HeightMm,
                finding.WidthMm,
                finding.IntrusionPercent,
                finding.CrossSectionReductionPercent,
                finding.DiameterReductionMm,
                meterSource,
                isMeterEstimated,
                _options.NormalizeOutputClock,
                _options.MinStretchLengthMeters,
                evidence);
        }

        return completed;
    }

    public IReadOnlyList<RawVideoDetection> AdvanceAll()
    {
        var completed = new List<RawVideoDetection>();

        foreach (var key in _active.Keys.ToList())
        {
            _active[key].MissedFrames++;
            if (_active[key].MissedFrames >= _options.DedupWindowFrames)
            {
                completed.Add(_active[key].ToDetection());
                _active.Remove(key);
            }
        }

        return completed;
    }

    public IReadOnlyList<RawVideoDetection> Flush()
    {
        var completed = _active.Values.Select(active => active.ToDetection()).ToList();
        _active.Clear();
        return completed;
    }

    public static double ResolveMeterEnd(
        string? vsaCode,
        double meterStart,
        double observedMeterEnd,
        double minStretchLengthMeters = 1.0)
        => VsaCodeResolver.IsStreckenschadenCode(vsaCode ?? string.Empty)
            && observedMeterEnd - meterStart >= minStretchLengthMeters
            ? observedMeterEnd
            : meterStart;

    private bool ShouldStartNewFinding(ActiveFindingState active, double meter)
        => _options.MeterMergeGapMaxMeters is { } maxGap
            && meter - active.MeterEnd > maxGap;

    private string BuildFindingKey(EnhancedFinding finding)
    {
        var label = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(finding.Label)
            ?? (_options.NormalizeFallbackLabels
                ? NormalizeFindingLabel(finding.Label.Trim())
                : finding.Label.Trim());
        var clock = NormalizeClock(finding.PositionClock);
        return string.IsNullOrWhiteSpace(clock) ? label : $"{label}|{clock}";
    }

    private static string NormalizeFindingLabel(string label)
    {
        var lower = label.ToLowerInvariant();

        if (lower.Contains("crack") || lower.Contains("fracture") || lower.Contains("riss"))
            return "crack";
        if (lower.Contains("break") || lower.Contains("bruch") || lower.Contains("collapse") || lower.Contains("einsturz"))
            return "break";
        if (lower.Contains("deform") || lower.Contains("verform") || lower.Contains("dent") || lower.Contains("oval"))
            return "deformation";
        if (lower.Contains("root") || lower.Contains("wurzel"))
            return "roots";
        if (lower.Contains("corros") || lower.Contains("erosion") || lower.Contains("surface damage") || lower.Contains("abplatz"))
            return "corrosion";
        if (lower.Contains("deposit") || lower.Contains("sediment") || lower.Contains("buildup")
            || lower.Contains("ablagerung") || lower.Contains("inkrust"))
            return "deposit";
        if (lower.Contains("infiltrat") || lower.Contains("ingress") || lower.Contains("leak")
            || lower.Contains("undicht") || lower.Contains("fremdwasser"))
            return "infiltration";
        if (lower.Contains("displace") || lower.Contains("offset") || lower.Contains("versatz") || lower.Contains("joint"))
            return "displacement";
        if (lower.Contains("obstacle") || lower.Contains("blockage") || lower.Contains("obstruct") || lower.Contains("hindernis"))
            return "obstacle";
        if (lower.Contains("connection") || lower.Contains("anschluss") || lower.Contains("intrud") || lower.Contains("protrud"))
            return "connection";

        return lower;
    }

    private static string? NormalizeClock(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim().ToLowerInvariant();
        if (text.Contains("oben") || text.Contains("scheitel") || text.Contains("krone"))
            return "12:00";
        if (text.Contains("unten") || text.Contains("sohle"))
            return "6:00";
        if (text.Contains("rechts")) return "3:00";
        if (text.Contains("links")) return "9:00";

        var match = Regex.Match(raw, @"\b(1[0-2]|0?[1-9])\b");
        if (match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            && hour >= 1
            && hour <= 12)
        {
            return $"{hour}:00";
        }

        return raw.Trim();
    }

    private sealed class ActiveFindingState
    {
        private readonly bool _normalizeOutputClock;
        private readonly double _minStretchLengthMeters;

        public string Name { get; }
        public double MeterStart { get; }
        public double MeterEnd { get; private set; }
        public int MaxSeverity { get; private set; }
        public string? VsaCodeHint { get; private set; }
        public string? PositionClock { get; private set; }
        public int? ExtentPercent { get; private set; }
        public int? HeightMm { get; private set; }
        public int? WidthMm { get; private set; }
        public int? IntrusionPercent { get; private set; }
        public int? CrossSectionReductionPercent { get; private set; }
        public int? DiameterReductionMm { get; private set; }
        public string? MeterSource { get; private set; }
        public bool IsMeterEstimated { get; private set; }
        public EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        public ActiveFindingState(
            string name,
            double start,
            int severity,
            string? hint,
            string? clock,
            int? extent,
            int? height,
            int? width,
            int? intrusion,
            int? crossSection,
            int? diameterReduction,
            string? meterSource,
            bool isMeterEstimated,
            bool normalizeOutputClock,
            double minStretchLengthMeters,
            EvidenceVector? evidence = null)
        {
            _normalizeOutputClock = normalizeOutputClock;
            _minStretchLengthMeters = minStretchLengthMeters;
            Name = name;
            MeterStart = start;
            MeterEnd = start;
            MaxSeverity = severity;
            VsaCodeHint = hint;
            PositionClock = NormalizeStoredClock(clock);
            ExtentPercent = extent is null ? null : Math.Clamp(extent.Value, 1, 100);
            HeightMm = height;
            WidthMm = width;
            IntrusionPercent = intrusion;
            CrossSectionReductionPercent = crossSection;
            DiameterReductionMm = diameterReduction;
            MeterSource = meterSource;
            IsMeterEstimated = isMeterEstimated;
            Evidence = evidence;
        }

        public void Update(
            double meter,
            int severity,
            string? hint,
            string? clock,
            int? extent,
            int? height,
            int? width,
            int? intrusion,
            int? crossSection,
            int? diameterReduction,
            string? meterSource,
            bool isMeterEstimated,
            EvidenceVector? evidence = null)
        {
            MeterEnd = meter;
            MissedFrames = 0;
            FrameCount++;
            if (severity > MaxSeverity) MaxSeverity = severity;
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
            if (!string.IsNullOrWhiteSpace(clock)) PositionClock = NormalizeStoredClock(clock);
            if (extent is { } e) ExtentPercent = Math.Max(ExtentPercent ?? 0, Math.Clamp(e, 1, 100));
            if (height is { } h) HeightMm = Math.Max(HeightMm ?? 0, h);
            if (width is { } w) WidthMm = Math.Max(WidthMm ?? 0, w);
            if (intrusion is { } ip) IntrusionPercent = Math.Max(IntrusionPercent ?? 0, ip);
            if (crossSection is { } csr) CrossSectionReductionPercent = Math.Max(CrossSectionReductionPercent ?? 0, csr);
            if (diameterReduction is { } dr) DiameterReductionMm = Math.Max(DiameterReductionMm ?? 0, dr);
            if (!string.IsNullOrWhiteSpace(meterSource)) MeterSource = meterSource;
            IsMeterEstimated |= isMeterEstimated;
            if (evidence is not null)
            {
                Evidence = Evidence is null ? evidence : MergeEvidence(Evidence, evidence);
            }
        }

        public RawVideoDetection ToDetection() =>
            new(Name, MeterStart, ResolveMeterEnd(VsaCodeHint, MeterStart, MeterEnd, _minStretchLengthMeters), SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null,
                MeterSource: MeterSource,
                IsMeterEstimated: IsMeterEstimated);

        private string? NormalizeStoredClock(string? clock) =>
            _normalizeOutputClock ? NormalizeClock(clock) : clock;

        private static string SeverityLabel(int severity) => severity >= 4 ? "high" : severity == 3 ? "mid" : "low";

        private static EvidenceVector MergeEvidence(EvidenceVector a, EvidenceVector b) =>
            new(
                YoloConf: Max(a.YoloConf, b.YoloConf),
                DinoConf: Max(a.DinoConf, b.DinoConf),
                SamMaskStability: Max(a.SamMaskStability, b.SamMaskStability),
                QwenVisionConf: Max(a.QwenVisionConf, b.QwenVisionConf),
                LlmCodeConf: Max(a.LlmCodeConf, b.LlmCodeConf),
                KbSimilarity: Max(a.KbSimilarity, b.KbSimilarity),
                KbCodeAgreement: a.KbCodeAgreement ?? b.KbCodeAgreement,
                PlausibilityScore: Max(a.PlausibilityScore, b.PlausibilityScore),
                DamageCategory: a.DamageCategory ?? b.DamageCategory,
                FrameCount: (a.FrameCount ?? 0) + (b.FrameCount ?? 0));

        private static double? Max(double? a, double? b) =>
            a.HasValue && b.HasValue ? Math.Max(a.Value, b.Value) : a ?? b;
    }
}
