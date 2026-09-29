using System.Diagnostics;
using System.Security.Cryptography;

namespace AuswertungPro.Next.Application.UseCases.CodingReplay;

/// <summary>Nur Laufzeiteingaben. Sollcodes und Fachurteile gelangen nicht zum Analysator.</summary>
public sealed record CodingReplayFrame(
    string Id, string ImageSha256, double TimestampSeconds,
    int? DiameterMm, double? ReachLengthM);

public sealed record CodingReplayEvent(string Code, double? Meter, string? QualityGate);

public sealed record CodingReplayObservation(
    string Outcome, IReadOnlyList<CodingReplayEvent> Events,
    IReadOnlyDictionary<string, string> Trace, string? TechnicalError = null);

public sealed record CodingReplayResult(
    string Id, string Status, CodingReplayObservation? Observation,
    string? Error, double ElapsedMilliseconds);

public interface ICodingReplayAnalyzer
{
    Task<CodingReplayObservation> AnalyzeAsync(CodingReplayFrame frame, byte[] image, CancellationToken ct);
}

public sealed record CodingReplayActions(
    Func<string, CancellationToken, Task<byte[]>> ReadImage,
    Func<CodingReplayResult, CancellationToken, Task> RecordResult);

/// <summary>
/// Serieller, abbrechbarer Bildvergleich. Quelldateien und Ausgaben gehoeren dem Host.
/// Ein Fehler ist nie ein erfolgreicher negativer Befund. Schreiben eines Ergebnisses
/// darf nicht still scheitern: Ohne Pruefspur wird der ganze Lauf angehalten.
/// </summary>
public sealed class CodingReplayUseCase(ICodingReplayAnalyzer analyzer)
{
    public async Task<IReadOnlyList<CodingReplayResult>> RunAsync(
        IReadOnlyList<CodingReplayFrame> frames, CodingReplayActions actions,
        TimeSpan timeoutPerFrame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(actions);
        if (frames.Count == 0 || timeoutPerFrame <= TimeSpan.Zero || timeoutPerFrame > TimeSpan.FromMinutes(30))
            throw new ArgumentException("Bildliste oder Zeitlimit ist ungültig.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in frames)
        {
            if (string.IsNullOrWhiteSpace(frame.Id) || !ids.Add(frame.Id)
                || frame.ImageSha256.Length != 64 || !frame.ImageSha256.All(Uri.IsHexDigit)
                || !double.IsFinite(frame.TimestampSeconds) || frame.TimestampSeconds < 0)
                throw new ArgumentException("Doppelte Kennung oder ungültiger Bildnachweis.");
        }

        var results = new List<CodingReplayResult>();
        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            CodingReplayResult result;
            if (frame.DiameterMm is not > 0 || frame.ReachLengthM is not > 0
                || !double.IsFinite(frame.ReachLengthM.Value))
            {
                result = new(frame.Id, "context_missing", null,
                    "Durchmesser oder Haltungslaenge fehlt. Kein Ersatzwert erfunden.", 0);
            }
            else
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(timeoutPerFrame);
                try
                {
                    var bytes = await actions.ReadImage(frame.Id, deadline.Token).ConfigureAwait(false);
                    if (bytes.Length == 0 || !Convert.ToHexString(SHA256.HashData(bytes))
                        .Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Bild stimmt nicht mit der gebundenen Prüfsumme überein.");
                    var observation = await analyzer.AnalyzeAsync(frame, bytes, deadline.Token).ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    result = new(frame.Id, observation.TechnicalError is null ? "measured" : "technical_error",
                        observation, observation.TechnicalError, watch.Elapsed.TotalMilliseconds);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                {
                    result = new(frame.Id, "timeout", null, "Zeitlimit erreicht.", watch.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    result = new(frame.Id, "technical_error", null, ex.Message, watch.Elapsed.TotalMilliseconds);
                }
            }
            ct.ThrowIfCancellationRequested();
            await actions.RecordResult(result, ct).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }
}
