using System.Globalization;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingOsdMeterReadWorkflowOutcome
{
    NoFrame,
    NoMeter,
    Accepted,
    Cancelled,
    ErrorLogged
}

public sealed record CodingOsdMeterReadWorkflowRequest(
    byte[] PngBytes,
    double? FrameTimestampSeconds,
    double? LastMeter,
    double? LastTimestampSeconds,
    CancellationToken CancellationToken);

public sealed record CodingOsdMeterReadWorkflowActions(
    Func<byte[], double?, double?, double?, CancellationToken, Task<CodingOsdMeterReadResult>> ReadMeterAsync,
    Action<CodingOsdMeterState> ApplyMeterState,
    Action<string> Trace);

public sealed record CodingOsdMeterReadWorkflowResult(
    CodingOsdMeterReadWorkflowOutcome Outcome,
    double? Meter);

public static class CodingOsdMeterReadWorkflow
{
    public static async Task<CodingOsdMeterReadWorkflowResult> ExecuteAsync(
        CodingOsdMeterReadWorkflowRequest request,
        CodingOsdMeterReadWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.PngBytes);

        if (request.PngBytes.Length == 0)
            return new CodingOsdMeterReadWorkflowResult(CodingOsdMeterReadWorkflowOutcome.NoFrame, null);

        try
        {
            var result = await actions.ReadMeterAsync(
                request.PngBytes,
                request.FrameTimestampSeconds,
                request.LastMeter,
                request.LastTimestampSeconds,
                request.CancellationToken);

            if (!result.Meter.HasValue)
            {
                TraceRejectedResult(result, actions.Trace);
                return new CodingOsdMeterReadWorkflowResult(CodingOsdMeterReadWorkflowOutcome.NoMeter, null);
            }

            var acceptedState = CodingOsdMeterStateWorkflow.FromReadResult(
                result,
                request.FrameTimestampSeconds);
            if (!acceptedState.HasValue)
                return new CodingOsdMeterReadWorkflowResult(CodingOsdMeterReadWorkflowOutcome.NoMeter, null);

            actions.ApplyMeterState(acceptedState.Value);
            return new CodingOsdMeterReadWorkflowResult(
                CodingOsdMeterReadWorkflowOutcome.Accepted,
                acceptedState.Value.Meter);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new CodingOsdMeterReadWorkflowResult(CodingOsdMeterReadWorkflowOutcome.Cancelled, null);
        }
        catch (Exception ex)
        {
            actions.Trace($"[OSD] Frame-Meter nicht lesbar: {ex.Message}");
            return new CodingOsdMeterReadWorkflowResult(CodingOsdMeterReadWorkflowOutcome.ErrorLogged, null);
        }
    }

    private static void TraceRejectedResult(
        CodingOsdMeterReadResult result,
        Action<string> trace)
    {
        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            trace($"[OSD] Frame-Meter nicht lesbar: {result.Error}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.RawReply) || result.Candidate.HasValue)
        {
            trace(
                $"[OSD] Meter verworfen. Raw='{result.RawReply}', " +
                $"Candidate={FormatNullableMeter(result.Candidate)}, " +
                $"Last={FormatNullableMeter(result.RecentMeter)}");
        }
    }

    private static string FormatNullableMeter(double? value)
        => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "null";
}
