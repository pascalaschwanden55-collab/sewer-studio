using System;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Liest Breite und Hoehe aus den Kopfdaten einer PNG- oder JPEG-Datei.
///
/// Die Infrastructure-Schicht ist reines net10.0 — es gibt hier weder WPF noch
/// System.Drawing. Fuer das Seitenverhaeltnis eines Bildes im Word reichen die
/// Kopfdaten aber vollstaendig aus.
///
/// Bei allem, was nicht sicher erkannt wird, wird NICHT geraten: ein falsches
/// Seitenverhaeltnis wuerde das Logo im fertigen Dossier verzerren.
/// </summary>
public static class ImageSizeReader
{
    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static bool TryRead(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (TryReadPng(bytes, out width, out height))
            return true;

        return TryReadJpeg(bytes, out width, out height);
    }

    private static bool TryReadPng(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        // Signatur (8) + Blocklaenge (4) + "IHDR" (4) + Breite (4) + Hoehe (4)
        if (bytes.Length < 24)
            return false;

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (bytes[i] != PngSignature[i])
                return false;
        }

        if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
            return false;

        width = ReadInt32(bytes, 16);
        height = ReadInt32(bytes, 20);
        return width > 0 && height > 0;
    }

    private static bool TryReadJpeg(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return false;

        var index = 2;
        while (index + 9 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
                return false;

            var marker = bytes[index + 1];
            var segmentLength = ReadUInt16(bytes, index + 2);
            if (segmentLength < 2)
                return false;

            if (IsStartOfFrame(marker))
            {
                height = ReadUInt16(bytes, index + 5);
                width = ReadUInt16(bytes, index + 7);
                return width > 0 && height > 0;
            }

            index += 2 + segmentLength;
        }

        return false;
    }

    /// <summary>
    /// Alle Bildanfangs-Marker. C4, C8 und CC sind ausgenommen: das sind
    /// Huffman-Tabellen und Erweiterungen, keine Bildmasse.
    /// </summary>
    private static bool IsStartOfFrame(byte marker)
        => marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16)
           | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static int ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
        => (bytes[offset] << 8) | bytes[offset + 1];
}
