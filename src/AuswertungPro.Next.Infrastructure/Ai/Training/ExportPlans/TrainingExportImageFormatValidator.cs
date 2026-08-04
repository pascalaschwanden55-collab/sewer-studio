using System.Buffers.Binary;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Kleine, paketfreie Vorpruefung fuer den lokalen Fallback. Der Sidecar dekodiert
/// Bilder zusaetzlich vollstaendig mit Pillow; lokal werden Signatur, Dateiende,
/// Format/Endung und Pixelgrenze geprueft.
/// </summary>
internal static class TrainingExportImageFormatValidator
{
    private const long MaximumPixels = 50_000_000;

    public static void Validate(byte[] bytes, string targetFileName)
        => ValidateCore(bytes, targetFileName);

    public static void Validate(
        byte[] bytes,
        string targetFileName,
        int expectedWidth,
        int expectedHeight)
    {
        var image = ValidateCore(bytes, targetFileName);
        if (image.Width != expectedWidth || image.Height != expectedHeight)
        {
            throw Error(
                $"Bildabmessungen {image.Width}x{image.Height} passen nicht zu " +
                $"{expectedWidth}x{expectedHeight}.");
        }
    }

    private static ImageInfo ValidateCore(
        byte[] bytes,
        string targetFileName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFileName);
        var image = ReadImageInfo(bytes)
                    ?? throw Error("Originalbild hat kein unterstuetztes oder vollstaendiges Bildformat.");
        var extension = Path.GetExtension(targetFileName).ToLowerInvariant();
        var extensionMatches = image.Format switch
        {
            ImageFormat.Png => extension == ".png",
            ImageFormat.Jpeg => extension is ".jpg" or ".jpeg",
            ImageFormat.Bmp => extension == ".bmp",
            ImageFormat.WebP => extension == ".webp",
            _ => false
        };
        if (!extensionMatches)
            throw Error("Dateiendung passt nicht zum Bildformat.");
        if (image.Width <= 0
            || image.Height <= 0
            || (long)image.Width * image.Height > MaximumPixels)
        {
            throw Error("Bildabmessungen sind ungueltig oder groesser als 50 Millionen Pixel.");
        }

        return image;
    }

    private static ImageInfo? ReadImageInfo(ReadOnlySpan<byte> bytes)
    {
        if (TryReadPng(bytes, out var info)
            || TryReadJpeg(bytes, out info)
            || TryReadBmp(bytes, out info)
            || TryReadWebP(bytes, out info))
        {
            return info;
        }

        return null;
    }

    private static bool TryReadPng(ReadOnlySpan<byte> bytes, out ImageInfo info)
    {
        info = default;
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        ReadOnlySpan<byte> ihdr = "IHDR"u8;
        ReadOnlySpan<byte> end = [0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130];
        if (bytes.Length < 45
            || !bytes[..8].SequenceEqual(signature)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[8..12]) != 13
            || !bytes[12..16].SequenceEqual(ihdr)
            || !bytes[^12..].SequenceEqual(end))
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..20]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..24]);
        if (width > int.MaxValue || height > int.MaxValue)
            return false;
        info = new ImageInfo(ImageFormat.Png, (int)width, (int)height);
        return true;
    }

    private static bool TryReadJpeg(ReadOnlySpan<byte> bytes, out ImageInfo info)
    {
        info = default;
        if (bytes.Length < 12
            || bytes[0] != 0xff
            || bytes[1] != 0xd8
            || bytes[^2] != 0xff
            || bytes[^1] != 0xd9)
        {
            return false;
        }

        var offset = 2;
        while (offset + 3 < bytes.Length - 2)
        {
            if (bytes[offset] != 0xff)
            {
                offset++;
                continue;
            }
            while (offset < bytes.Length && bytes[offset] == 0xff)
                offset++;
            if (offset >= bytes.Length)
                return false;
            var marker = bytes[offset++];
            if (marker is 0x01 or >= 0xd0 and <= 0xd9)
                continue;
            if (offset + 2 > bytes.Length)
                return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..(offset + 2)]);
            if (length < 2 || offset + length > bytes.Length)
                return false;
            if (IsStartOfFrame(marker))
            {
                if (length < 7)
                    return false;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..(offset + 5)]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..(offset + 7)]);
                info = new ImageInfo(ImageFormat.Jpeg, width, height);
                return true;
            }
            if (marker == 0xda)
                return false;
            offset += length;
        }

        return false;
    }

    private static bool IsStartOfFrame(byte marker)
        => marker is >= 0xc0 and <= 0xcf
           && marker is not (0xc4 or 0xc8 or 0xcc);

    private static bool TryReadBmp(ReadOnlySpan<byte> bytes, out ImageInfo info)
    {
        info = default;
        if (bytes.Length < 26 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            return false;
        var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[2..6]);
        if (declaredSize > bytes.Length || declaredSize < 26)
            return false;
        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..22]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..26]);
        if (height == int.MinValue)
            return false;
        info = new ImageInfo(ImageFormat.Bmp, width, Math.Abs(height));
        return true;
    }

    private static bool TryReadWebP(ReadOnlySpan<byte> bytes, out ImageInfo info)
    {
        info = default;
        if (bytes.Length < 30
            || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes[8..12].SequenceEqual("WEBP"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]) + 8 != bytes.Length)
        {
            return false;
        }

        var chunk = bytes[12..16];
        if (chunk.SequenceEqual("VP8X"u8))
        {
            var width = 1 + ReadUInt24LittleEndian(bytes[24..27]);
            var height = 1 + ReadUInt24LittleEndian(bytes[27..30]);
            info = new ImageInfo(ImageFormat.WebP, width, height);
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
        {
            var width = 1 + bytes[21] + ((bytes[22] & 0x3f) << 8);
            var height = 1 + (bytes[22] >> 6) + (bytes[23] << 2) + ((bytes[24] & 0x0f) << 10);
            info = new ImageInfo(ImageFormat.WebP, width, height);
            return true;
        }
        if (chunk.SequenceEqual("VP8 "u8)
            && bytes[23] == 0x9d
            && bytes[24] == 0x01
            && bytes[25] == 0x2a)
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3fff;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3fff;
            info = new ImageInfo(ImageFormat.WebP, width, height);
            return true;
        }
        return false;
    }

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes)
        => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private static TrainingExportPlanException Error(string message)
        => new($"Lokaler Exportkonflikt: {message}");

    private enum ImageFormat
    {
        Png,
        Jpeg,
        Bmp,
        WebP
    }

    private readonly record struct ImageInfo(ImageFormat Format, int Width, int Height);
}
