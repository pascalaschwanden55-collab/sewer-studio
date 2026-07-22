using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert die Kern-Leselogik von <see cref="VideoFrameStream"/> (Deepscan U3): Ein ffmpeg-Haenger
/// (mehrere Frame-Timeouts in Folge) muss als Fehler geworfen werden und darf NICHT wie ein
/// sauberes Videoende aussehen. Getestet ueber den prozessfreien Einstieg
/// <see cref="VideoFrameStream.ReadFramesCoreAsync"/> mit injizierbarem Strom und kurzem Timeout.
/// </summary>
public sealed class VideoFrameStreamTests
{
    // Minimales, gueltiges PNG-Geruest: 8-Byte-Signatur direkt gefolgt vom 12-Byte-IEND-Chunk.
    // Genau das erkennt der Extraktor als einen vollstaendigen Frame.
    private static byte[] MiniPng() => new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,             // PNG-Signatur
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82, // IEND
    };

    [Fact]
    public async Task ReadFramesCore_LiestZweiFrames_ausSauberemStrom()
    {
        var png = MiniPng();
        var data = new byte[png.Length * 2];
        Array.Copy(png, 0, data, 0, png.Length);
        Array.Copy(png, 0, data, png.Length, png.Length);

        using var source = new MemoryStream(data);
        var frames = new List<FrameData>();
        await foreach (var f in VideoFrameStream.ReadFramesCoreAsync(
            source, stepSeconds: 2.0, frameTimeout: TimeSpan.FromSeconds(5),
            maxConsecutiveTimeouts: 3, CancellationToken.None))
        {
            frames.Add(f);
        }

        Assert.Equal(2, frames.Count);
        Assert.Equal(0.0, frames[0].TimestampSeconds);
        Assert.Equal(2.0, frames[1].TimestampSeconds);
    }

    [Fact]
    public async Task ReadFramesCore_EndetStill_beiEofOhneWurf()
    {
        // Sauberes Videoende (ffmpeg fertig) = 0 Bytes -> Sequenz endet normal, KEIN Wurf.
        using var source = new MemoryStream(Array.Empty<byte>());
        var frames = new List<FrameData>();
        await foreach (var f in VideoFrameStream.ReadFramesCoreAsync(
            source, stepSeconds: 1.0, frameTimeout: TimeSpan.FromSeconds(5),
            maxConsecutiveTimeouts: 3, CancellationToken.None))
        {
            frames.Add(f);
        }

        Assert.Empty(frames);
    }

    [Fact]
    public async Task ReadFramesCore_Wirft_wennFfmpegHaengt()
    {
        // Strom, der nie Daten liefert (haengendes ffmpeg): nach 3 Frame-Timeouts muss der
        // Kern werfen, statt still zu enden — sonst ist ein Haenger von einem EOF ununterscheidbar.
        using var source = new HangingStream();

        await Assert.ThrowsAsync<VideoFrameStreamTimeoutException>(async () =>
        {
            await foreach (var _ in VideoFrameStream.ReadFramesCoreAsync(
                source, stepSeconds: 1.0, frameTimeout: TimeSpan.FromMilliseconds(40),
                maxConsecutiveTimeouts: 3, CancellationToken.None))
            {
            }
        });
    }

    /// <summary>Byte-Strom, dessen Leseaufruf bis zur Cancellation blockiert (simuliert haengendes ffmpeg).</summary>
    private sealed class HangingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
