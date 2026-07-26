using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolGuidedVerificationAdapter
{
    public static Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? Create(
        GuidedVerificationService? verifier)
        => verifier is null
            ? null
            : (framePath, importEvent) => VerifyAsync(verifier, framePath, importEvent, CancellationToken.None);

    public static async Task<CodingProtocolVerificationResult?> VerifyAsync(
        GuidedVerificationService verifier,
        string framePath,
        CodingEvent importEvent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(importEvent);

        if (string.IsNullOrWhiteSpace(framePath) || !File.Exists(framePath))
            return null;

        var bytes = await File.ReadAllBytesAsync(framePath, ct).ConfigureAwait(false);
        var groundTruth = ToGroundTruthEntry(importEvent, framePath);
        var result = await verifier.VerifyAsync(bytes, groundTruth, ct).ConfigureAwait(false);

        return ToVerificationResult(result);
    }

    public static CodingProtocolVerificationResult ToVerificationResult(GuidedVerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var confirmationLevel = IsVerifierFallback(result)
            ? "nicht_geprueft"
            : result.ConfirmationLevel;

        return new CodingProtocolVerificationResult(
            ConfirmationLevel: confirmationLevel,
            DamageVisible: result.ProtocolDamageVisible,
            ActualCode: result.ActualVsaCode,
            MeterReading: result.MeterReading,
            Explanation: result.Explanation);
    }

    public static GroundTruthEntry ToGroundTruthEntry(CodingEvent importEvent, string framePath)
    {
        ArgumentNullException.ThrowIfNull(importEvent);

        var entry = importEvent.Entry ?? new ProtocolEntry();
        var start = entry.MeterStart ?? importEvent.MeterAtCapture;
        var end = entry.MeterEnd ?? start;
        var zeit = entry.Zeit ?? importEvent.VideoTimestamp;

        return new GroundTruthEntry
        {
            MeterStart = start,
            MeterEnd = end,
            VsaCode = entry.Code ?? string.Empty,
            Text = entry.Beschreibung ?? string.Empty,
            Quantification = null,
            Characterization = FirstMetaValue(
                entry.CodeMeta,
                "catalog.standardAnnotation",
                "vsa.charakterisierung",
                "Characterization",
                "Charakterisierung"),
            ClockPosition = FirstMetaValue(
                entry.CodeMeta,
                "vsa.uhr.von",
                "ClockPos1",
                "UhrVon",
                "Uhrlage"),
            ConnectionClock = FirstMetaValue(
                entry.CodeMeta,
                "vsa.uhr.bis",
                "ClockPos2",
                "UhrBis"),
            Severity = entry.CodeMeta?.Severity,
            IsStreckenschaden = entry.IsStreckenschaden,
            Zeit = zeit,
            ExtractedFramePath = framePath,
            ExtractedFrameTimeSeconds = zeit.TotalSeconds
        };
    }

    private static string? FirstMetaValue(ProtocolEntryCodeMeta? meta, params string[] keys)
    {
        if (meta is null)
            return null;

        foreach (var key in keys)
        {
            if (!meta.Parameters.TryGetValue(key, out var value))
                continue;

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static bool IsVerifierFallback(GuidedVerificationResult result)
        => result.ActualSeverity == 0
           && result.MeterReading is null
           && string.IsNullOrWhiteSpace(result.ActualVsaCode)
           && !result.ProtocolDamageVisible;
}
