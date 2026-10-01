using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Baut eine OSD-Meter-Zeitreihe aus einem Video auf.
/// Bewusst nicht versiegelt: Tests haengen eine feste Folge ueber
/// <see cref="BuildTimelineAsync"/> ein (siehe dort).
/// </summary>
public class MeterTimelineService : IDisposable
{
    private readonly AiRuntimeSettings _cfg;
    private readonly OsdMeterDetectionService? _osd;
    private readonly int _concurrency;
    private readonly IDisposable? _ownedResource;
    private bool _disposed;

    public MeterTimelineService(
        AiRuntimeSettings cfg,
        OsdMeterDetectionService? osd = null,
        int concurrency = 1,
        IDisposable? ownedResource = null)
    {
        _cfg = cfg;
        _osd = osd;
        _concurrency = Math.Max(1, concurrency);
        _ownedResource = ownedResource;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ownedResource?.Dispose();
    }

    /// <summary>
    /// Interpoliert den Meterstand zu einem Zeitpunkt. Ehrlichkeit vor Dichte:
    /// Ausserhalb gelesener Klammern und ueber ungefuellten Luecken gibt es keinen
    /// Wert — und ein interpolierter Wert aus geschaetzten Endpunkten bleibt als
    /// geschaetzt gekennzeichnet (<c>IsEstimated</c>). Keine Rand-Extrapolation mehr.
    /// </summary>
    public static (double? Meter, bool IsEstimated) InterpolateMeter(
        IReadOnlyList<FilledMeterReading> timeline,
        double timeSeconds)
    {
        for (var index = 0; index < timeline.Count - 1; index++)
        {
            var links = timeline[index];
            var rechts = timeline[index + 1];
            if (timeSeconds < links.TimeSeconds || timeSeconds > rechts.TimeSeconds)
                continue;

            if (links.Meter is not { } m0 || rechts.Meter is not { } m1)
                return (null, false);

            var spanne = rechts.TimeSeconds - links.TimeSeconds;
            var anteil = spanne <= 0.0 ? 0.0 : (timeSeconds - links.TimeSeconds) / spanne;
            return (m0 + anteil * (m1 - m0), links.IsEstimated || rechts.IsEstimated);
        }

        return (null, false);
    }

    /// <summary>
    /// Bereinigt die rohe Lesefolge: erst Sequenz-Plausibilitaet (unmoegliche Werte
    /// raus), dann kurze Luecken fuellen — mit allen drei Klammern, die aus echten
    /// Fehlern gelernt wurden (Lueckenlaenge, Richtungswechsel, Schaetzkennzeichnung).
    /// Ersetzt die fruehere Glattung, die bei komplett unlesbarem OSD eine Reihe
    /// aus Null-Metern erfand — eine erfundene Null sieht aus wie eine Messung.
    /// </summary>
    public static IReadOnlyList<FilledMeterReading> BereinigeTimeline(
        IReadOnlyList<(double TimeSeconds, double? Meter)> roh)
    {
        var lesungen = roh
            .Select(punkt => new MeterReading(punkt.TimeSeconds, punkt.Meter))
            .ToList();
        var geprueft = MeterSequencePlausibility.Check(lesungen, new MeterPlausibilityOptions());
        return MeterSequenceGapFiller.Fill(geprueft, new MeterGapFillOptions());
    }

    // virtual, damit Tests eine feste Timeline einhaengen koennen (AP-3-Pflichttest:
    // die Quellen-Zuordnung im Generator wird sonst nur durch Lesen belegt).
    public virtual async Task<IReadOnlyList<FilledMeterReading>> BuildTimelineAsync(
        string videoPath,
        double videoDurationSeconds,
        double stepSeconds = 5.0,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled || _osd is null)
            return Array.Empty<FilledMeterReading>();

        var ffmpeg = _cfg.FfmpegPath ?? "ffmpeg";
        var frames = new List<(int Index, FrameData Frame)>();
        var idx = 0;

        await using var stream = VideoFrameStream.Open(
            ffmpeg, videoPath, stepSeconds, videoDurationSeconds, ct);

        try
        {
            await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                frames.Add((idx++, frame));
            }
        }
        catch (VideoFrameStreamTimeoutException)
        {
            // U3: ffmpeg-Haenger — die Meter-Timeline ist ein Hilfssignal; mit den bis hier
            // gelesenen Frames weiterarbeiten statt den Aufrufer mit einem Wurf abzubrechen.
        }

        var results = new ConcurrentDictionary<int, (double Time, double? Meter)>();

        await Parallel.ForEachAsync(frames, new ParallelOptions
        {
            MaxDegreeOfParallelism = _concurrency,
            CancellationToken = ct
        }, async (item, token) =>
        {
            double? meter = null;
            if (item.Frame.PngBytes is { Length: > 0 })
            {
                var base64 = Convert.ToBase64String(item.Frame.PngBytes);
                var result = await _osd.ReadMeterAsync(base64, null, token).ConfigureAwait(false);
                if (result.Source != MeterSource.Unknown)
                    meter = result.Value;
            }

            results[item.Index] = (item.Frame.TimestampSeconds, meter);
        });

        var raw = results.OrderBy(kv => kv.Key)
            .Select(kv => (kv.Value.Time, kv.Value.Meter))
            .ToList();

        return BereinigeTimeline(raw);
    }
}
