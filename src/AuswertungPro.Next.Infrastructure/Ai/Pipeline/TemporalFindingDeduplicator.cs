using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Uhrlage in den Dedup-Schluessel aufnehmen (Default). Im Klassifikator-Regime
    /// (Ganzbild-Code) fuehrt das zu Duplikaten — derselbe Befund splittet sich
    /// ueber die Masken-Uhrlagen (Pilot 2026-06-10: 12x BDD statt 1) — dort false.
    /// </summary>
    public bool ClockInKey { get; init; } = true;

    /// <summary>
    /// BBox-IoU-Mindestwert, ab dem zwei gleichcodierte Befunde im selben Frame als
    /// derselbe Schaden gelten und verschmolzen werden (F12). Liegt die Ueberlappung
    /// darunter, bleiben beide eigenstaendig — zwei reale getrennte Schaeden gleichen
    /// Codes/Uhrlage im selben Bild verschmelzen dann nicht mehr. Konservativer
    /// Default 0.3. Befunde ohne BBox verschmelzen weiterhin wie bisher
    /// (Bestandsschutz); 0 deaktiviert die raeumliche Trennung komplett.
    /// </summary>
    public double SameFrameMergeMinIoU { get; init; } = 0.3;
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
            var candidateKey = key;
            var disambiguation = 0;

            while (true)
            {
                if (!currentMap.TryGetValue(candidateKey, out var existing))
                {
                    currentMap[candidateKey] = finding;
                    break;
                }

                // F12: Raeumlich getrennte Befunde gleichen Schluessels (beide mit BBox,
                // IoU unter Schwelle) werden NICHT verschmolzen — sie sind getrennte
                // Schaeden im selben Bild und bekommen einen eigenen Schluessel. Die
                // zeitliche Fortschreibung laeuft ueber diesen Schluessel unveraendert mit.
                if (AreSpatiallySeparate(existing, finding))
                {
                    candidateKey = $"{key}#{++disambiguation}";
                    continue;
                }

                // Bei Schluessel-Kollision im selben Frame (Ueberlappung oder fehlende
                // Geometrie) gewinnt der dominante Befund (groesste Ausdehnung) — er
                // traegt die repraesentative Quantifizierung. Bisheriges Verhalten.
                if ((finding.ExtentPercent ?? 0) > (existing.ExtentPercent ?? 0))
                    currentMap[candidateKey] = finding;
                break;
            }
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
        => ResolveMeterRange(vsaCode, meterStart, meterStart, observedMeterEnd, minStretchLengthMeters).End;

    public static (double Start, double End) ResolveMeterRange(
        string? vsaCode,
        double firstObservedMeter,
        double observedMeterMin,
        double observedMeterMax,
        double minStretchLengthMeters = 1.0)
    {
        var start = Math.Min(observedMeterMin, observedMeterMax);
        var end = Math.Max(observedMeterMin, observedMeterMax);

        return VsaCodeResolver.IsStreckenschadenCode(vsaCode ?? string.Empty)
            && end - start >= minStretchLengthMeters
            ? (start, end)
            : (firstObservedMeter, firstObservedMeter);
    }

    private bool ShouldStartNewFinding(ActiveFindingState active, double meter)
        => _options.MeterMergeGapMaxMeters is { } maxGap
            && active.DistanceToObservedRange(meter) > maxGap;

    private string BuildFindingKey(EnhancedFinding finding)
    {
        var label = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(finding.Label)
            ?? (_options.NormalizeFallbackLabels
                ? NormalizeFindingLabel(finding.Label.Trim())
                : finding.Label.Trim());
        var clock = _options.ClockInKey ? VsaCodeResolver.NormalizeClock(finding.PositionClock) : null;
        return string.IsNullOrWhiteSpace(clock) ? label : $"{label}|{clock}";
    }

    /// <summary>
    /// F12: true, wenn beide Befunde eine brauchbare BBox tragen und deren IoU UNTER der
    /// konfigurierten Schwelle liegt — dann handelt es sich um zwei raeumlich getrennte
    /// Schaeden, die nicht verschmolzen werden duerfen. Ohne Geometrie: false, d. h. das
    /// bisherige Verschmelzen bleibt (Bestandsschutz).
    /// </summary>
    private bool AreSpatiallySeparate(EnhancedFinding a, EnhancedFinding b)
    {
        if (_options.SameFrameMergeMinIoU <= 0)
            return false;
        if (!TryGetBBox(a, out var boxA) || !TryGetBBox(b, out var boxB))
            return false;
        return ComputeIoU(boxA, boxB) < _options.SameFrameMergeMinIoU;
    }

    private static bool TryGetBBox(
        EnhancedFinding finding,
        out (double X1, double Y1, double X2, double Y2) bbox)
    {
        bbox = default;
        if (finding.BboxX1 is not { } x1 || finding.BboxY1 is not { } y1
            || finding.BboxX2 is not { } x2 || finding.BboxY2 is not { } y2)
            return false;
        if (x2 <= x1 || y2 <= y1)
            return false; // degenerierte Box — keine brauchbare Geometrie
        bbox = (x1, y1, x2, y2);
        return true;
    }

    /// <summary>Intersection-over-Union zweier normalisierter BBoxen [x1,y1,x2,y2].</summary>
    internal static double ComputeIoU(
        (double X1, double Y1, double X2, double Y2) a,
        (double X1, double Y1, double X2, double Y2) b)
    {
        var interW = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
        var interH = Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1);
        if (interW <= 0 || interH <= 0)
            return 0.0;

        var intersection = interW * interH;
        var union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - intersection;
        return union <= 0 ? 0.0 : intersection / union;
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

    // NormalizeClock-Duplikat entfernt — kanonische Implementierung: VsaCodeResolver.NormalizeClock

    private sealed class ActiveFindingState
    {
        private readonly bool _normalizeOutputClock;
        private readonly double _minStretchLengthMeters;

        public string Name { get; }
        public double MeterStart { get; }
        public double MeterEnd { get; private set; }
        public double ObservedMeterMin { get; private set; }
        public double ObservedMeterMax { get; private set; }
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
            ObservedMeterMin = start;
            ObservedMeterMax = start;
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
            ObservedMeterMin = Math.Min(ObservedMeterMin, meter);
            ObservedMeterMax = Math.Max(ObservedMeterMax, meter);
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

        public double DistanceToObservedRange(double meter)
        {
            if (meter < ObservedMeterMin)
                return ObservedMeterMin - meter;
            if (meter > ObservedMeterMax)
                return meter - ObservedMeterMax;

            return 0.0;
        }

        public RawVideoDetection ToDetection()
        {
            var (meterStart, meterEnd) = ResolveMeterRange(
                VsaCodeHint,
                MeterStart,
                ObservedMeterMin,
                ObservedMeterMax,
                _minStretchLengthMeters);

            return new(Name, meterStart, meterEnd, SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null,
                MeterSource: MeterSource,
                IsMeterEstimated: IsMeterEstimated,
                SeverityLevel: MaxSeverity);
        }

        private string? NormalizeStoredClock(string? clock) =>
            _normalizeOutputClock ? VsaCodeResolver.NormalizeClock(clock) : clock;

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
