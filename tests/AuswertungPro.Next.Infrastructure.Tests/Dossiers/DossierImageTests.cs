using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class ImageSizeReaderTests
{
    [Fact]
    public void Liest_Breite_und_Hoehe_eines_PNG()
    {
        var png = TestImages.Png(width: 716, height: 297);

        Assert.True(ImageSizeReader.TryRead(png, out var width, out var height));
        Assert.Equal(716, width);
        Assert.Equal(297, height);
    }

    [Fact]
    public void Liest_Breite_und_Hoehe_eines_JPEG()
    {
        var jpeg = TestImages.Jpeg(width: 177, height: 213);

        Assert.True(ImageSizeReader.TryRead(jpeg, out var width, out var height));
        Assert.Equal(177, width);
        Assert.Equal(213, height);
    }

    [Fact]
    public void Unbekannte_Bytes_ergeben_kein_Ergebnis_statt_geratener_Masse()
    {
        var muell = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        Assert.False(ImageSizeReader.TryRead(muell, out var width, out var height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }
}

/// <summary>
/// Baut die kleinsten gueltigen Bilddateien, die der Groessenleser verstehen
/// muss. Bewusst von Hand zusammengesetzt: die Testbibliothek hat keine
/// Bildbibliothek, und fuer die Kopfdaten braucht es auch keine.
/// </summary>
internal static class TestImages
{
    /// <summary>PNG-Signatur plus IHDR-Block mit Breite und Hoehe.</summary>
    public static byte[] Png(int width, int height)
    {
        var bytes = new List<byte>
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            (byte)'I', (byte)'H', (byte)'D', (byte)'R'
        };

        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });
        return bytes.ToArray();
    }

    /// <summary>JPEG-Start plus ein SOF0-Segment mit Hoehe und Breite.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        var bytes = new List<byte>
        {
            0xFF, 0xD8,
            // APP0-Segment mit 4 Nutzbytes: wird uebersprungen.
            0xFF, 0xE0, 0x00, 0x06, 1, 2, 3, 4,
            // SOF0: Laenge 17, Genauigkeit 8, dann Hoehe und Breite.
            0xFF, 0xC0, 0x00, 0x11, 0x08
        };

        bytes.Add((byte)(height >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(width & 0xFF));
        bytes.AddRange(new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 });
        return bytes.ToArray();
    }

    private static byte[] BigEndian(int value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
    };
}
