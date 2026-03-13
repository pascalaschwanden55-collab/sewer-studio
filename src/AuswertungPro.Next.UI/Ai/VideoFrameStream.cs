using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Persistent ffmpeg process that streams all frames as PNG via image2pipe.
/// Instead of spawning one ffmpeg per frame, a single process runs for the
/// entire video and outputs PNG images on stdout.
/// </summary>
public sealed class VideoFrameStream : IAsyncDisposable
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] IendMarker = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
    private const int ReadBufferSize = 64 * 1024; // 64 KB
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(30);
    private const int MaxConsecutiveTimeouts = 3;

    private readonly Process _process;
    private readonly double _stepSeconds;
    private readonly double _duration;

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

        // Drain stderr asynchronously to prevent deadlock.
        // ContinueWith swallows exceptions to avoid UnobservedTaskException.
        _ = process.StandardError.ReadToEndAsync(ct)
            .ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);

        return new VideoFrameStream(process, stepSeconds, duration);
    }

    /// <summary>
    /// Reads frames from the ffmpeg stdout stream. Each yielded FrameData contains
    /// the timestamp and PNG bytes for one frame.
    /// </summary>
    public async IAsyncEnumerable<FrameData> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var stdout = _process.StandardOutput.BaseStream;
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
                readCts.CancelAfter(FrameTimeout);
                bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, readCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                consecutiveTimeouts++;
                if (consecutiveTimeouts >= MaxConsecutiveTimeouts)
                    break; // ffmpeg likely hung — abort

                // Frame timeout — skip and continue reading
                if (accumulator.Length > 0)
                {
                    frameIndex++;
                    accumulator.SetLength(0);
                }
                continue;
            }

            if (bytesRead == 0)
                break; // EOF — ffmpeg finished

            consecutiveTimeouts = 0;
            accumulator.Write(buffer, 0, bytesRead);

            // Try to extract complete PNG images from the accumulator
            while (TryExtractPng(accumulator, out var pngBytes))
            {
                var timestamp = frameIndex * _stepSeconds;
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

    /// <summary>
    /// Extrahiert einen einzelnen Frame an einer bestimmten Zeitposition via ffmpeg -ss seek.
    /// Schneller als Stream fuer gezielte Einzelframes (Protokoll-gesteuert).
    /// </summary>
    public static async Task<FrameData?> ExtractSingleFrameAsync(
        string ffmpegPath, string videoPath, double timeSeconds,
        CancellationToken ct, int scaleWidth = 1280)
    {
        var ts = timeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(ts);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"scale='min({scaleWidth},iw)':-2");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("image2pipe");
        psi.ArgumentList.Add("-vcodec");
        psi.ArgumentList.Add("png");
        psi.ArgumentList.Add("pipe:1");

        using var process = Process.Start(psi);
        if (process is null) return null;

        _ = process.StandardError.ReadToEndAsync(ct)
            .ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);

        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return ms.Length > 0
            ? new FrameData(timeSeconds, ms.ToArray())
            : null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
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
