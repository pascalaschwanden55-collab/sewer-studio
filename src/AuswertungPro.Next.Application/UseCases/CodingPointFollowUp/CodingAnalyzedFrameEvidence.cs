using System.Globalization;
using System.Security.Cryptography;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Bild und bereits aufgeloeste Position eines einzelnen Analyseaufrufs.</summary>
public sealed record CodingAnalyzedFrameEvidence(byte[] ImageBytes, TimeSpan CaptureTime, double Meter, bool MeterFromOsd)
{
    public CodingMeterSource MeterSource { get; init; } = MeterFromOsd ? CodingMeterSource.SameFrameOsd : CodingMeterSource.Unknown;
    public bool HasSameFrameOsd => MeterFromOsd && MeterSource == CodingMeterSource.SameFrameOsd;

    public static CodingAnalyzedFrameEvidence FromResolution(byte[] bytes, TimeSpan time, CodingMeterResolution resolution)
        => new(bytes, time, resolution.Meter, resolution.IsOsd) { MeterSource = resolution.Source };

    public void AttachPhoto(ProtocolEntry entry, Action<ProtocolEntry, byte[]> attach)
    {
        WriteAnalysisMetadata(entry);
        attach(entry, ImageBytes);
        if (entry.FotoPaths.Count > 0)
            WritePhotoMetadata(entry, ImageBytes, CaptureTime.TotalSeconds, "analyzed_frame");
    }

    public void WriteAnalysisMetadata(ProtocolEntry entry)
    {
        var values = Parameters(entry);
        values["ai.frame.sha256"] = Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();
        values["ai.frame.time_seconds"] = CaptureTime.TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
        values["ai.frame.meter"] = Meter.ToString("R", CultureInfo.InvariantCulture);
        values["ai.frame.meter_source"] = MeterSource switch
        {
            CodingMeterSource.SameFrameOsd => "frame_osd",
            CodingMeterSource.RecentOsd => "recent_osd",
            CodingMeterSource.VideoEstimate => "video_estimate",
            CodingMeterSource.SessionFallback => "session_fallback",
            _ => "resolved_fallback"
        };
    }

    public static void WritePhotoMetadata(ProtocolEntry entry, byte[] imageBytes, double? seconds, string source)
    {
        var values = Parameters(entry);
        values["ai.photo.sha256"] = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        values["ai.photo.source"] = source;
        if (seconds is { } time && double.IsFinite(time) && time >= 0)
            values["ai.photo.time_seconds"] = time.ToString("R", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> Parameters(ProtocolEntry entry)
    {
        entry.CodeMeta ??= new() { Code = entry.Code };
        return entry.CodeMeta.Parameters;
    }
}
