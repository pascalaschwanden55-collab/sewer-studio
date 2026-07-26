using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Persistent ffmpeg process that streams all frames as PNG via image2pipe.
/// Instead of spawning one ffmpeg per frame, a single process runs for the
/// entire video and outputs PNG images on stdout.
/// </summary>
public sealed class VideoFrameStream : IVideoFrameSource
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] IendMarker = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
    private const int ReadBufferSize = 64 * 1024; // 64 KB
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(30);
    private const int MaxConsecutiveTimeouts = 3;
    // Nach stdout-EOF beendet sich ffmpeg normalerweise sofort — nur so lange auf den
    // ExitCode warten, dann ohne ihn bewerten (kein Haenger durch die Abschlusspruefung).
    private static readonly TimeSpan ProcessExitGracePeriod = TimeSpan.FromSeconds(5);
    private const int StderrTailCapacity = 8 * 1024; // letzte ~8 KB der ffmpeg-Fehlerausgabe
    private const int StderrReasonMaxLength = 300;

    private readonly Process _process;
    private readonly double _stepSeconds;
    private readonly double _duration;
    private readonly object _stderrLock = new();
    private readonly StringBuilder _stderrTail = new();
    private int _framesRead;

    private VideoFrameStream(Process process, double stepSeconds, double duration)
    {
        _process = process;
        _stepSeconds = stepSeconds;
        _duration = duration;
    }

    /// <summary>
    /// Start a persistent ffmpeg process that streams PNG frames from the video.
    /// </summary>
    public static VideoFrameStream Open(
        string ffmpegPath,
        string videoPath,
        double stepSeconds,
        double duration,
        CancellationToken ct,
        int scaleWidth = 1280)
    {
        var vfFilter = $"fps=1/{stepSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},scale='min({scaleWidth},iw)':-2";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add(vfFilter);
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("image2pipe");
        psi.ArgumentList.Add("-vcodec");
        psi.ArgumentList.Add("png");
        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg process.");

        var stream = new VideoFrameStream(process, stepSeconds, duration);

        // stderr in einen begrenzten Tail-Puffer drainen statt verwerfen (F5): verhindert
        // weiterhin den Pipe-Deadlock, macht die ffmpeg-Fehlermeldung aber fuer die
        // Abschlussbewertung (Property Completion) verfuegbar. Lese- und sonstige Fehler
        // werden im Drain selbst geschluckt — kein UnobservedTaskException-Risiko.
        _ = stream.DrainStderrAsync(process.StandardError, ct);

        return stream;
    }

    /// <summary>
    /// Abschlussstatus der Extraktion (F5). Wird gesetzt, sobald
    /// <see cref="ReadFramesAsync"/> vollstaendig enumeriert wurde (Stream-Ende);
    /// null, solange der Stream laeuft oder die Enumeration abgebrochen wurde.
    /// </summary>
    public VideoFrameStreamCompletion? Completion { get; private set; }

    /// <summary>Bisher gelesene Frames des laufenden/beendeten Streams.</summary>
    public int FramesRead => _framesRead;

    /// <summary>Aktueller Inhalt des begrenzten stderr-Tail-Puffers (Thread-sicher gelesen).</summary>
    internal string StderrTail
    {
        get { lock (_stderrLock) return _stderrTail.ToString(); }
    }

    /// <summary>
    /// Reads frames from the ffmpeg stdout stream. Each yielded FrameData contains
    /// the timestamp and PNG bytes for one frame. Nach vollstaendigem Enumerieren ist
    /// <see cref="Completion"/> gesetzt (ExitCode + Framezahl gegen Erwartung, F5).
    /// </summary>
    /// <remarks>
    /// Ein ffmpeg-Haenger (mehrere Frame-Timeouts in Folge) endet NICHT mehr still wie ein
    /// sauberes EOF, sondern wirft <see cref="VideoFrameStreamTimeoutException"/>. Sonst konnte
    /// der Aufrufer einen Haenger nach Frame 50 von 500 nicht von einem vollstaendig gelesenen
    /// Video unterscheiden (stiller Teilerfolg). (Deepscan U3)
    /// </remarks>
    public async IAsyncEnumerable<FrameData> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var frame in ReadFramesCoreAsync(
                _process.StandardOutput.BaseStream, _stepSeconds, FrameTimeout, MaxConsecutiveTimeouts, ct)
            .ConfigureAwait(false))
        {
            _framesRead++;
            yield return frame;
        }

        // F5: Erst nach Stream-Ende Vollstaendigkeit pruefen (ExitCode + Framezahl).
        // Bei Haenger (TimeoutException) oder Abbruch wird dieser Punkt nicht erreicht —
        // Completion bleibt null und der Fehler propagiert wie bisher.
        await FinalizeCompletionAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Wartet nach stdout-EOF kurz auf das Prozessende, wertet den ExitCode aus und
    /// vergleicht die gelesene Framezahl mit der aus der Videodauer erwarteten.
    /// </summary>
    private async Task FinalizeCompletionAsync(CancellationToken ct)
    {
        int? exitCode = null;
        try
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            waitCts.CancelAfter(ProcessExitGracePeriod);
            await _process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
            exitCode = _process.ExitCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Grace-Frist abgelaufen: Prozess lebt trotz EOF noch — ohne ExitCode bewerten.
        }
        catch (InvalidOperationException)
        {
            // Prozess-Handle bereits freigegeben — ohne ExitCode bewerten.
        }

        Completion = EvaluateCompletion(
            _framesRead, ExpectedFrameCount(_duration, _stepSeconds), exitCode, StderrTail);
    }

    /// <summary>
    /// Reine Abschlussbewertung (prozessfrei testbar): Vollstaendig nur bei sauberem
    /// ffmpeg-Ende (Exit 0 bzw. unbekannt) und erreicht erwarteter Framezahl. Ein Frame
    /// Differenz toleriert der fps-Filter je nach Dauer/Rundung — das ist kein Teilverlust.
    /// </summary>
    internal static VideoFrameStreamCompletion EvaluateCompletion(
        int framesRead, int expectedFrames, int? exitCode, string? stderrTail)
    {
        var framesComplete = framesRead >= Math.Max(expectedFrames - 1, 0);

        if (framesComplete && exitCode is null or 0)
            return new VideoFrameStreamCompletion(true, framesRead, expectedFrames, exitCode, null);

        string reason;
        if (exitCode is not null and not 0)
        {
            var tail = CleanStderrTail(stderrTail);
            reason = tail.Length == 0
                ? $"ffmpeg-Exit {exitCode}"
                : $"ffmpeg-Exit {exitCode}: {tail}";
        }
        else
        {
            reason = $"fruehes EOF nach {framesRead} von {expectedFrames} Frames " +
                     $"(ffmpeg-Exit {(exitCode?.ToString() ?? "unbekannt")})";
        }

        return new VideoFrameStreamCompletion(false, framesRead, expectedFrames, exitCode, reason);
    }

    /// <summary>Erwartete Framezahl aus Videodauer und Schrittweite (identisch zur Aufrufer-Rechnung).</summary>
    internal static int ExpectedFrameCount(double duration, double stepSeconds)
        => duration > 0 && stepSeconds > 0 ? (int)Math.Ceiling(duration / stepSeconds) : 0;

    /// <summary>
    /// Verdichtet den stderr-Tail auf eine einzeilige, endbegrenzte Ausgabe — die letzten
    /// Zeichen stehen am naechsten am Abbruchgrund.
    /// </summary>
    internal static string CleanStderrTail(string? stderrTail)
    {
        if (string.IsNullOrWhiteSpace(stderrTail))
            return string.Empty;

        var cleaned = stderrTail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);

        return cleaned.Length <= StderrReasonMaxLength
            ? cleaned
            : cleaned[^StderrReasonMaxLength..];
    }

    /// <summary>
    /// Drainet stderr fortlaufend in den begrenzten Tail-Puffer (nur die letzten
    /// ~8 KB bleiben). Best-effort: Fehler und Abbruch werden geschluckt, damit der
    /// Drain die Extraktion nie gefaehrdet.
    /// </summary>
    private async Task DrainStderrAsync(StreamReader stderr, CancellationToken ct)
    {
        var buffer = new char[2048];
        try
        {
            while (true)
            {
                var read = await stderr.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                    .ConfigureAwait(false);
                if (read <= 0)
                    break;

                lock (_stderrLock)
                {
                    _stderrTail.Append(buffer, 0, read);
                    if (_stderrTail.Length > StderrTailCapacity)
                        _stderrTail.Remove(0, _stderrTail.Length - StderrTailCapacity);
                }
            }
        }
        catch (OperationCanceledException) { /* Abbruch: stderr ist best-effort */ }
        catch (Exception) { /* stderr-Lesen darf die Frame-Extraktion nie gefaehrden */ }
    }

    /// <summary>
    /// Reine, prozessunabhaengige Frame-Leselogik auf einem beliebigen Byte-Strom. Als eigener
    /// Einstieg testbar (injizierbarer Strom + kurzer Timeout), ohne einen echten ffmpeg-Prozess.
    /// </summary>
    internal static async IAsyncEnumerable<FrameData> ReadFramesCoreAsync(
        Stream source,
        double stepSeconds,
        TimeSpan frameTimeout,
        int maxConsecutiveTimeouts,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[ReadBufferSize];
        var accumulator = new MemoryStream();
        int frameIndex = 0;
        int consecutiveTimeouts = 0;

        while (!ct.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(frameTimeout);
                bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                consecutiveTimeouts++;
                if (consecutiveTimeouts >= maxConsecutiveTimeouts)
                    // ffmpeg haengt: als Fehler kennzeichnen, NICHT als normales Ende (U3).
                    throw new VideoFrameStreamTimeoutException(
                        $"ffmpeg lieferte {maxConsecutiveTimeouts}x keine Frame-Daten innerhalb von " +
                        $"je {frameTimeout.TotalSeconds:F0}s — die Frame-Extraktion gilt als haengend.");

                // Einzelner Frame-Timeout — Rest verwerfen und weiterlesen.
                if (accumulator.Length > 0)
                {
                    frameIndex++;
                    accumulator.SetLength(0);
                }
                continue;
            }

            if (bytesRead == 0)
                // EOF — Strom zu Ende. Ob das Ende VORZEITIG kam (Teilvideo), bewertet der
                // Aufrufer ueber Completion/EvaluateCompletion (F5) — der Kern selbst kennt
                // weder Prozess-ExitCode noch die erwartete Framezahl.
                yield break;

            consecutiveTimeouts = 0;
            accumulator.Write(buffer, 0, bytesRead);

            // Try to extract complete PNG images from the accumulator
            while (TryExtractPng(accumulator, out var pngBytes))
            {
                var timestamp = frameIndex * stepSeconds;
                frameIndex++;
                yield return new FrameData(timestamp, pngBytes);
            }

            // Safety: if accumulator grows beyond 50MB without yielding a frame, discard
            if (accumulator.Length > 50 * 1024 * 1024)
            {
                accumulator.SetLength(0);
                frameIndex++;
            }
        }
    }

    /// <summary>
    /// Try to extract a complete PNG from the accumulator buffer.
    /// A PNG starts with the 8-byte signature and ends with the 12-byte IEND chunk.
    /// </summary>
    private static bool TryExtractPng(MemoryStream accumulator, out byte[] pngBytes)
    {
        pngBytes = Array.Empty<byte>();
        var data = accumulator.GetBuffer();
        var length = (int)accumulator.Length;

        if (length < PngSignature.Length + IendMarker.Length)
            return false;

        // Find PNG start
        int pngStart = IndexOf(data, 0, length, PngSignature);
        if (pngStart < 0)
        {
            // No PNG signature found — discard all data
            accumulator.SetLength(0);
            return false;
        }

        // Find IEND marker after the PNG signature
        int iendPos = IndexOf(data, pngStart + PngSignature.Length, length, IendMarker);
        if (iendPos < 0)
            return false; // Incomplete PNG — wait for more data

        int pngEnd = iendPos + IendMarker.Length;
        int pngLength = pngEnd - pngStart;

        pngBytes = new byte[pngLength];
        Array.Copy(data, pngStart, pngBytes, 0, pngLength);

        // Remove extracted PNG from accumulator, keep remaining bytes
        int remaining = length - pngEnd;
        if (remaining > 0)
        {
            var temp = new byte[remaining];
            Array.Copy(data, pngEnd, temp, 0, remaining);
            accumulator.SetLength(0);
            accumulator.Write(temp, 0, remaining);
        }
        else
        {
            accumulator.SetLength(0);
        }

        return true;
    }

    /// <summary>
    /// Boyer-Moore-like search for a byte pattern in a buffer region.
    /// </summary>
    private static int IndexOf(byte[] data, int offset, int length, byte[] pattern)
    {
        int end = length - pattern.Length;
        for (int i = offset; i <= end; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                // Ganzer Prozessbaum (wie ExternalProcessRunner.TryKill), damit kein ffmpeg-Kind
                // als Zombie zurueckbleibt.
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch { /* ignore cleanup errors */ }
        finally
        {
            _process.Dispose();
        }
    }
}

/// <summary>
/// A single extracted video frame with its timestamp and PNG data.
/// </summary>
public readonly record struct FrameData(double TimestampSeconds, byte[] PngBytes);

/// <summary>
/// Abschlussstatus der ffmpeg-Frame-Extraktion (F5). <see cref="IsComplete"/> ist nur true,
/// wenn ffmpeg sauber endete und die erwartete Framezahl erreicht wurde. Bei false traegt
/// <see cref="Reason"/> den Grund (fruehes EOF oder ffmpeg-Fehler inkl. stderr-Auszug).
/// </summary>
public sealed record VideoFrameStreamCompletion(
    bool IsComplete,
    int FramesRead,
    int ExpectedFrames,
    int? ExitCode,
    string? Reason);

/// <summary>
/// Interne Abstraktion der Frame-Quelle fuer <c>VideoFullAnalysisService</c> (Test-Seam
/// fuer F4/F5): Der Frame-Loop kann damit ohne echtes ffmpeg gefaked werden.
/// Produktiv ist die einzige Implementierung <see cref="VideoFrameStream"/>.
/// </summary>
internal interface IVideoFrameSource : IAsyncDisposable
{
    IAsyncEnumerable<FrameData> ReadFramesAsync(CancellationToken ct);

    /// <summary>Abschlussstatus nach vollstaendig enumeriertem Stream; null, solange er laeuft.</summary>
    VideoFrameStreamCompletion? Completion { get; }
}

/// <summary>
/// Wird geworfen, wenn die ffmpeg-Frame-Extraktion haengt (mehrere Frame-Timeouts in Folge).
/// Bewusst eine eigene Klasse, damit die Analyse-Wege sie gezielt fangen und den Lauf als
/// fehlgeschlagen statt still als Teilerfolg behandeln koennen (Deepscan U3). Erbt NICHT von
/// <see cref="InvalidOperationException"/>, damit sie nicht mit anderen Health-Check-Wuerfen
/// verwechselt wird.
/// </summary>
public sealed class VideoFrameStreamTimeoutException : Exception
{
    public VideoFrameStreamTimeoutException(string message) : base(message) { }
}
