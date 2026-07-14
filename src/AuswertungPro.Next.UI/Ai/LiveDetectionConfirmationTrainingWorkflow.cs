using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionConfirmationTrainingResult(
    bool Saved,
    int SavedCount,
    string? Code);

public static class LiveDetectionConfirmationTrainingWorkflow
{
    public static async Task<LiveDetectionConfirmationTrainingResult> SaveAcceptedAsync(
        IReadOnlyList<LiveFrameFinding>? findings,
        double timestampSec,
        byte[]? preCapturedFrame,
        Func<Task<byte[]?>> captureCurrentFrameAsync,
        Func<byte[], LiveFrameFinding, TimeSpan, Task<TeacherAnnotation>> saveAcceptedAsync)
    {
        if (findings == null || findings.Count == 0)
            return NotSaved();

        ArgumentNullException.ThrowIfNull(captureCurrentFrameAsync);
        ArgumentNullException.ThrowIfNull(saveAcceptedAsync);

        var frameBytes = await ResolveFrameBytesAsync(preCapturedFrame, captureCurrentFrameAsync);
        if (frameBytes == null)
            return NotSaved();

        var videoTimestamp = TimeSpan.FromSeconds(timestampSec);
        foreach (var finding in findings)
            await saveAcceptedAsync(frameBytes, finding, videoTimestamp);

        return new LiveDetectionConfirmationTrainingResult(
            Saved: true,
            findings.Count,
            Code: null);
    }

    public static async Task<LiveDetectionConfirmationTrainingResult> SaveCorrectedAsync(
        IReadOnlyList<LiveFrameFinding>? findings,
        ProtocolEntry? selectedEntry,
        double timestampSec,
        byte[]? preCapturedFrame,
        Func<Task<byte[]?>> captureCurrentFrameAsync,
        Func<byte[], LiveFrameFinding, ProtocolEntry, TimeSpan, Task<TeacherAnnotation>> saveCorrectedAsync)
    {
        if (findings == null || findings.Count == 0 || selectedEntry == null)
            return NotSaved();

        ArgumentNullException.ThrowIfNull(captureCurrentFrameAsync);
        ArgumentNullException.ThrowIfNull(saveCorrectedAsync);

        var frameBytes = await ResolveFrameBytesAsync(preCapturedFrame, captureCurrentFrameAsync);
        if (frameBytes == null)
            return NotSaved();

        await saveCorrectedAsync(
            frameBytes,
            findings[0],
            selectedEntry,
            TimeSpan.FromSeconds(timestampSec));

        return new LiveDetectionConfirmationTrainingResult(
            Saved: true,
            SavedCount: 1,
            selectedEntry.Code);
    }

    private static async Task<byte[]?> ResolveFrameBytesAsync(
        byte[]? preCapturedFrame,
        Func<Task<byte[]?>> captureCurrentFrameAsync)
    {
        if (preCapturedFrame is { Length: > 0 })
            return preCapturedFrame;

        return await captureCurrentFrameAsync();
    }

    private static LiveDetectionConfirmationTrainingResult NotSaved()
        => new(Saved: false, SavedCount: 0, Code: null);
}
