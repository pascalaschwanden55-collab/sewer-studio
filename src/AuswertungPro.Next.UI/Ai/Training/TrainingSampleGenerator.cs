using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Training.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Erstellt TrainingSamples aus einem TrainingCase (Video + ProtocolDocument).
///
/// Features (Phase 2 + 2.5):
/// - Range Sampling: Streckenschaden → N Samples entlang MeterStart-End
/// - OSD Mismatch: Δm > threshold → HasOsdMismatch = true
/// - Dedup: Signature Code + gerundete MeterRange → skip duplicates
/// </summary>
public sealed class TrainingSampleGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AiRuntimeConfig _cfg;
    private readonly MeterTimelineService _meterTimeline;
    private readonly TrainingCenterSettings _settings;

    public TrainingSampleGenerator(
        AiRuntimeConfig cfg,
        MeterTimelineService meterTimeline,
        TrainingCenterSettings? settings = null)
    {
        _cfg = cfg;
        _meterTimeline = meterTimeline;
        _settings = settings ?? new TrainingCenterSettings();
    }

    public async Task<List<TrainingSample>> GenerateAsync(
        TrainingCase tc,
        IReadOnlyCollection<string>? existingSignatures = null,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        var result = new List<TrainingSample>();

        if (!File.Exists(tc.VideoPath) || !File.Exists(tc.ProtocolPath))
            return result;

        var doc = LoadProtocol(tc.ProtocolPath);
        if (doc is null) return result;

        var entries = doc.Current.Entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        if (entries.Count == 0) return result;

        var (duration, _) = await GetDurationAsync(_cfg.FfmpegPath ?? "ffmpeg", tc.VideoPath, ct)
            .ConfigureAwait(false);
        if (duration <= 0) return result;

        // OSD-Zeitreihe aufbauen (optional, nur wenn AI enabled)
        var timeline = await _meterTimeline.BuildTimelineAsync(
            tc.VideoPath, duration, stepSeconds: 5.0, ct).ConfigureAwait(false);

        // Maximaler Meterstand als Referenz für Zeitschätzung
        var maxMeter = entries
            .Select(e => e.MeterEnd ?? e.MeterStart ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        if (maxMeter <= 0) maxMeter = duration;

        var seen = new HashSet<string>(
            existingSignatures ?? Array.Empty<string>(),
            StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var meterStart = entry.MeterStart ?? 0;
            var meterEnd = entry.MeterEnd ?? meterStart;
            var samplePoints = GetSamplePoints(entry, meterStart, meterEnd, duration, maxMeter);

            foreach (var (meter, t, frameIndex) in samplePoints)
            {
                ct.ThrowIfCancellationRequested();

                var sig = BuildSignature(entry.Code, meter, meterEnd);
                if (seen.Contains(sig)) continue;
                seen.Add(sig);

                var safeCase = Regex.Replace(tc.CaseId, @"[^\w\-]", "_");
                var sampleId = $"{safeCase}_{entry.Code}_{meter:F2}_{Guid.NewGuid():N}";

                var framePath = await FrameStore.ExtractAndStoreAsync(
                    _cfg.FfmpegPath ?? "ffmpeg",
                    tc.VideoPath, t, sampleId, framesDir, ct).ConfigureAwait(false);

                // OSD Mismatch prüfen
                double? detectedMeter = null;
                var meterSource = "linear";
                var hasOsdMismatch = false;
                double? odsDelta = null;

                if (timeline.Count > 0)
                {
                    detectedMeter = MeterTimelineService.InterpolateMeter(timeline, t);
                    if (detectedMeter.HasValue)
                    {
                        meterSource = "osd";
                        odsDelta = Math.Abs(detectedMeter.Value - meter);
                        hasOsdMismatch = odsDelta.Value > _settings.OsdMismatchThresholdMeters;
                    }
                }

                result.Add(new TrainingSample
                {
                    SampleId = sampleId,
                    CaseId = tc.CaseId,
                    Code = entry.Code,
                    Beschreibung = entry.Beschreibung,
                    MeterStart = meterStart,
                    MeterEnd = meterEnd,
                    IsStreckenschaden = entry.IsStreckenschaden,
                    TimeSeconds = t,
                    DetectedMeter = detectedMeter,
                    MeterSource = meterSource,
                    FramePath = framePath ?? "",
                    Status = TrainingSampleStatus.New,
                    TruthMeterCenter = meter,
                    OdsDeltaMeters = odsDelta,
                    HasOsdMismatch = hasOsdMismatch,
                    Signature = sig,
                    FrameIndex = frameIndex
                });
            }
        }

        return result;
    }

    // ── Hilfsmethoden ──────────────────────────────────────────────────────

    private List<(double Meter, double Time, int FrameIndex)> GetSamplePoints(
        ProtocolEntry entry,
        double meterStart, double meterEnd,
        double duration, double maxMeter)
    {
        var points = new List<(double Meter, double Time, int FrameIndex)>();

        // Falls Zeit direkt im Eintrag gesetzt → nutzen
        if (entry.Zeit.HasValue)
        {
            var t = entry.Zeit.Value.TotalSeconds;
            points.Add((meterStart, Math.Clamp(t, 0, duration - 0.1), 0));
            return points;
        }

        var rangeLength = meterEnd - meterStart;
        if (entry.IsStreckenschaden
            && rangeLength >= _settings.MinRangeLengthForSampling
            && _settings.RangeSampleCount > 1)
        {
            // Range Sampling: N gleichmäßige Punkte entlang MeterStart–MeterEnd
            for (var i = 0; i < _settings.RangeSampleCount; i++)
            {
                var frac = (double)i / (_settings.RangeSampleCount - 1);
                var m = meterStart + frac * rangeLength;
                var t = EstimateTime(m, maxMeter, duration);
                points.Add((m, t, i));
            }
        }
        else
        {
            points.Add((meterStart, EstimateTime(meterStart, maxMeter, duration), 0));
        }

        return points;
    }

    private static double EstimateTime(double meter, double maxMeter, double duration)
    {
        if (maxMeter <= 0) return 0;
        return Math.Clamp(meter / maxMeter * duration, 0, duration - 0.1);
    }

    private static string BuildSignature(string code, double meterCenter, double meterEnd)
    {
        var rc = Math.Round(meterCenter, 1);
        var re = Math.Round(meterEnd, 1);
        return $"{code}|{rc:F1}|{re:F1}";
    }

    private static ProtocolDocument? LoadProtocol(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // JSON: direkt als ProtocolDocument deserialisieren
        if (ext == ".json")
        {
            try
            {
                var json = File.ReadAllText(path);
                var doc  = JsonSerializer.Deserialize<ProtocolDocument>(json, JsonOpts);
                if (doc?.Current?.Entries?.Count > 0)
                    return doc;
            }
            catch { /* weiter zum PDF-Fallback */ }
        }

        // PDF (und JSON-Fallback falls ProtocolDocument-Deserialisierung fehlschlägt):
        // PdfProtocolExtractor → GroundTruthEntry → ProtocolEntry
        if (ext is ".pdf" or ".json")
        {
            try
            {
                var extractor = new PdfProtocolExtractor();
                var entries   = extractor.ExtractAsync(path).GetAwaiter().GetResult();
                if (entries.Count == 0) return null;

                var protocolEntries = entries
                    .Select(e => new ProtocolEntry
                    {
                        Code             = e.VsaCode,
                        Beschreibung     = e.Text,
                        MeterStart       = e.MeterStart,
                        MeterEnd         = e.MeterEnd,
                        IsStreckenschaden = e.IsStreckenschaden,
                        Zeit             = e.Zeit,
                        Source           = ProtocolEntrySource.Imported
                    })
                    .ToList();

                return new ProtocolDocument
                {
                    Current = new ProtocolRevision { Entries = protocolEntries }
                };
            }
            catch { }
        }

        return null;
    }

    private static async Task<(double duration, string? error)> GetDurationAsync(
        string ffmpegPath, string videoPath, CancellationToken ct)
    {
        // ffprobe zuerst
        var dir = Path.GetDirectoryName(ffmpegPath) ?? "";
        var ffprobe = string.IsNullOrWhiteSpace(dir)
            ? "ffprobe"
            : Path.Combine(dir, "ffprobe.exe");
        if (!File.Exists(ffprobe)) ffprobe = "ffprobe";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is not null)
            {
                var output = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                await p.WaitForExitAsync(ct).ConfigureAwait(false);
                if (double.TryParse(output.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var d) && d > 0)
                    return (d, null);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* ffprobe nicht verfügbar, weiter */ }

        // ffmpeg fallback: Duration aus stderr parsen
        try
        {
            var psi2 = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p2 = Process.Start(psi2);
            if (p2 is not null)
            {
                var stderr = await p2.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await p2.WaitForExitAsync(ct).ConfigureAwait(false);
                var m = Regex.Match(stderr, @"Duration:\s*(\d+):(\d{2}):(\d{2}\.?\d*)");
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, out var hh)
                    && int.TryParse(m.Groups[2].Value, out var mm)
                    && double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var ss))
                {
                    return (hh * 3600 + mm * 60 + ss, null);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* ignore */ }

        return (0, "Videodauer konnte nicht ermittelt werden.");
    }
}
