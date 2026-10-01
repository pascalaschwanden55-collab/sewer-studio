// Spiegelung der App-Logik fuer den Gold-Bildbeleg (CMYK-/Decode-Normalisierung):
// - TrainingPdfJpegColorNormalizer (UI/Services) — Wandlung JPEG -> RGB-PNG
// - TrainingPdfEmbeddedImageReader (Infrastructure/Ai/Training/PdfReview) — Normalisierungsentscheid
// SYNCHRONISATIONSPFLICHT: Bei Aenderungen an den genannten Klassen diese
// Spiegelung angleichen. Die Byte-Gleichwertigkeit ist empirisch abgesichert:
// Gold-PNGs des Imports matchen die hier erzeugten Hashes.
using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;

namespace PdfImageAnalyzer;

internal static class PdfJpegColorNormalizer
{
    internal enum ColorModel
    {
        Gray,
        Rgb,
        Cmyk,
    }

    internal sealed record Request(
        byte[] JpegBytes,
        int PixelWidth,
        int PixelHeight,
        int BitsPerComponent,
        ColorModel Model,
        IReadOnlyList<decimal> Decode,
        bool InvertSourceSamples);

    private const long MaximumPixels = 12_000_000;

    // -- Normalisierungsentscheid (Spiegel von TryCreateJpegColorNormalizationRequest) --
    // Rueckgabe true + request=null: Normalisierung noetig, aber nicht beweisbar
    // (unbekannter Farbraum oder unloesbare Inversion) -> fail-closed.
    internal static bool TryCreateRequest(
        IPdfImage image,
        byte[] jpegBytes,
        out Request? request)
    {
        request = null;
        if (!TryResolveColorModel(image.ColorSpaceDetails, out var colorModel, out var componentCount))
            return true;

        var declaredDecode = image.Decode;
        var hasDeclaredDecode = declaredDecode.Count > 0;
        var invertSourceSamples = false;
        if (colorModel == ColorModel.Cmyk
            && !TryResolveCmykSourceInversion(jpegBytes, out invertSourceSamples))
        {
            return true;
        }

        var requiresNormalization =
            colorModel == ColorModel.Cmyk
            || hasDeclaredDecode && !IsIdentityDecode(declaredDecode, componentCount);
        if (!requiresNormalization)
            return false;

        var effectiveDecode = hasDeclaredDecode
            ? declaredDecode.ToArray()
            : BuildIdentityDecode(componentCount);
        request = new Request(
            jpegBytes,
            (int)image.WidthInSamples,
            (int)image.HeightInSamples,
            image.BitsPerComponent,
            colorModel,
            effectiveDecode,
            invertSourceSamples);
        return true;
    }

    // -- Wandlung (Spiegel von TrainingPdfJpegColorNormalizer) --
    internal static bool TryNormalizeToRgbPng(Request request, out byte[] pngBytes)
    {
        pngBytes = [];
        var componentCount = request.Model switch
        {
            ColorModel.Gray => 1,
            ColorModel.Rgb => 3,
            ColorModel.Cmyk => 4,
            _ => 0,
        };
        if (componentCount == 0
            || (request.InvertSourceSamples && request.Model != ColorModel.Cmyk)
            || request.BitsPerComponent != 8
            || request.PixelWidth <= 0
            || request.PixelHeight <= 0
            || (long)request.PixelWidth * request.PixelHeight > MaximumPixels
            || request.Decode.Count != componentCount * 2
            || request.JpegBytes.Length < 4
            || request.JpegBytes[0] != 0xff
            || request.JpegBytes[1] != 0xd8
            || request.JpegBytes[^2] != 0xff
            || request.JpegBytes[^1] != 0xd9)
        {
            return false;
        }

        try
        {
            using var input = new MemoryStream(request.JpegBytes, writable: false);
            var decoder = BitmapDecoder.Create(
                input,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                return false;

            var frame = decoder.Frames[0];
            if (frame.PixelWidth != request.PixelWidth
                || frame.PixelHeight != request.PixelHeight)
            {
                return false;
            }

            if (!TryReadColorComponents(
                    frame,
                    request.Model,
                    componentCount,
                    out var components,
                    out var componentStride))
            {
                return false;
            }

            var outputStride = checked(request.PixelWidth * 4);
            var output = new byte[checked(outputStride * request.PixelHeight)];
            ConvertToBgra(request, components, componentStride, output, outputStride);

            var bitmap = BitmapSource.Create(
                request.PixelWidth,
                request.PixelHeight,
                96,
                96,
                PixelFormats.Bgra32,
                palette: null,
                output,
                outputStride);
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var result = new MemoryStream();
            encoder.Save(result);
            pngBytes = result.ToArray();
            return pngBytes.Length > 0;
        }
        catch (Exception ex) when (
            ex is ArgumentException
                or FileFormatException
                or FormatException
                or InvalidOperationException
                or IOException
                or NotSupportedException)
        {
            pngBytes = [];
            return false;
        }
    }

    private static bool TryReadColorComponents(
        BitmapSource frame,
        ColorModel colorModel,
        int componentCount,
        out byte[] components,
        out int stride)
    {
        components = [];
        stride = 0;
        PixelFormat targetFormat;
        switch (colorModel)
        {
            case ColorModel.Gray:
                if (frame.Format == PixelFormats.Cmyk32)
                    return false;
                targetFormat = PixelFormats.Gray8;
                break;
            case ColorModel.Rgb:
                if (frame.Format == PixelFormats.Cmyk32)
                    return false;
                targetFormat = PixelFormats.Bgr24;
                break;
            case ColorModel.Cmyk:
                if (frame.Format != PixelFormats.Cmyk32)
                    return false;
                targetFormat = PixelFormats.Cmyk32;
                break;
            default:
                return false;
        }

        var source = frame.Format == targetFormat
            ? frame
            : new FormatConvertedBitmap(
                frame,
                targetFormat,
                destinationPalette: null,
                alphaThreshold: 0);
        stride = checked(source.PixelWidth * componentCount);
        components = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(components, stride, offset: 0);
        return true;
    }

    private static void ConvertToBgra(
        Request request,
        byte[] source,
        int sourceStride,
        byte[] target,
        int targetStride)
    {
        var sourceComponents = request.Model switch
        {
            ColorModel.Gray => 1,
            ColorModel.Rgb => 3,
            ColorModel.Cmyk => 4,
            _ => throw new InvalidDataException("Unbekanntes PDF-JPEG-Farbmodell."),
        };

        for (var y = 0; y < request.PixelHeight; y++)
        {
            var sourceRow = y * sourceStride;
            var targetRow = y * targetStride;
            for (var x = 0; x < request.PixelWidth; x++)
            {
                var sourceOffset = sourceRow + x * sourceComponents;
                var targetOffset = targetRow + x * 4;
                double red;
                double green;
                double blue;

                switch (request.Model)
                {
                    case ColorModel.Gray:
                        red = green = blue = Decode(
                            source[sourceOffset],
                            request.Decode,
                            componentIndex: 0);
                        break;
                    case ColorModel.Rgb:
                        // Bgr24 speichert die drei Bytes in der Reihenfolge B, G, R.
                        blue = Decode(
                            source[sourceOffset],
                            request.Decode,
                            componentIndex: 2);
                        green = Decode(
                            source[sourceOffset + 1],
                            request.Decode,
                            componentIndex: 1);
                        red = Decode(
                            source[sourceOffset + 2],
                            request.Decode,
                            componentIndex: 0);
                        break;
                    case ColorModel.Cmyk:
                        var cyan = Decode(
                            source[sourceOffset],
                            request.Decode,
                            componentIndex: 0,
                            invertSourceSample: request.InvertSourceSamples);
                        var magenta = Decode(
                            source[sourceOffset + 1],
                            request.Decode,
                            componentIndex: 1,
                            invertSourceSample: request.InvertSourceSamples);
                        var yellow = Decode(
                            source[sourceOffset + 2],
                            request.Decode,
                            componentIndex: 2,
                            invertSourceSample: request.InvertSourceSamples);
                        var black = Decode(
                            source[sourceOffset + 3],
                            request.Decode,
                            componentIndex: 3,
                            invertSourceSample: request.InvertSourceSamples);
                        red = (1d - cyan) * (1d - black);
                        green = (1d - magenta) * (1d - black);
                        blue = (1d - yellow) * (1d - black);
                        break;
                    default:
                        throw new InvalidDataException("Unbekanntes PDF-JPEG-Farbmodell.");
                }

                target[targetOffset] = ToByte(blue);
                target[targetOffset + 1] = ToByte(green);
                target[targetOffset + 2] = ToByte(red);
                target[targetOffset + 3] = byte.MaxValue;
            }
        }
    }

    private static double Decode(
        byte sample,
        IReadOnlyList<decimal> decode,
        int componentIndex,
        bool invertSourceSample = false)
    {
        var pairIndex = componentIndex * 2;
        var minimum = decode[pairIndex];
        var maximum = decode[pairIndex + 1];
        var normalizedSample = invertSourceSample
            ? 1m - sample / 255m
            : sample / 255m;
        var value = minimum + normalizedSample * (maximum - minimum);
        return Math.Clamp((double)value, 0d, 1d);
    }

    private static byte ToByte(double value)
        => (byte)Math.Clamp(
            (int)Math.Round(
                value * byte.MaxValue,
                MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);

    // -- Farbmodell/Decode-Helfer (Spiegel von TrainingPdfEmbeddedImageReader) --
    private static bool TryResolveColorModel(
        ColorSpaceDetails? details,
        out ColorModel colorModel,
        out int componentCount)
    {
        colorModel = default;
        componentCount = details?.NumberOfColorComponents ?? 0;
        if (details is null)
            return false;

        if (componentCount == 4 && details.Type == ColorSpace.DeviceCMYK)
        {
            colorModel = ColorModel.Cmyk;
            return true;
        }

        if (componentCount == 3 && details.Type == ColorSpace.DeviceRGB)
        {
            colorModel = ColorModel.Rgb;
            return true;
        }

        if (componentCount == 1 && details.Type == ColorSpace.DeviceGray)
        {
            colorModel = ColorModel.Gray;
            return true;
        }

        return false;
    }

    private static bool IsIdentityDecode(IReadOnlyList<decimal> decode, int componentCount)
    {
        if (decode.Count != componentCount * 2)
            return false;

        for (var index = 0; index < decode.Count; index += 2)
        {
            if (decode[index] != 0m || decode[index + 1] != 1m)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<decimal> BuildIdentityDecode(int componentCount)
    {
        var decode = new decimal[componentCount * 2];
        for (var index = 0; index < componentCount; index += 1)
        {
            decode[index * 2] = 0m;
            decode[index * 2 + 1] = 1m;
        }

        return decode;
    }

    private static bool TryResolveCmykSourceInversion(
        ReadOnlySpan<byte> jpegBytes,
        out bool invertSourceSamples)
    {
        invertSourceSamples = false;
        if (!TryReadAdobeJpegColorTransform(jpegBytes, out var adobeTransform))
            return false;

        switch (adobeTransform)
        {
            case 0:
            case 2:
                invertSourceSamples = true;
                return true;
            case null:
                return false;
            default:
                return false;
        }
    }

    private static bool TryReadAdobeJpegColorTransform(
        ReadOnlySpan<byte> jpegBytes,
        out byte? transform)
    {
        transform = null;
        if (!HasCompleteJpegEnvelope(jpegBytes))
            return false;

        var offset = 2;
        while (offset < jpegBytes.Length)
        {
            if (jpegBytes[offset] != 0xff)
                return false;

            while (offset < jpegBytes.Length && jpegBytes[offset] == 0xff)
                offset++;
            if (offset >= jpegBytes.Length)
                return false;

            var marker = jpegBytes[offset++];
            if (marker is 0xd9 or 0xda)
                return true;
            if (marker is 0x01 or >= 0xd0 and <= 0xd7)
                continue;
            if (offset + 2 > jpegBytes.Length)
                return false;

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(
                jpegBytes[offset..(offset + 2)]);
            if (segmentLength < 2 || offset + segmentLength > jpegBytes.Length)
                return false;

            var payload = jpegBytes[(offset + 2)..(offset + segmentLength)];
            if (marker == 0xee
                && payload.Length >= 12
                && payload[..5].SequenceEqual("Adobe"u8))
            {
                var current = payload[11];
                if (transform.HasValue && transform.Value != current)
                    return false;
                transform = current;
            }

            offset += segmentLength;
        }

        return false;
    }

    internal static bool HasCompleteJpegEnvelope(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 4
           && bytes[0] == 0xff
           && bytes[1] == 0xd8
           && bytes[^2] == 0xff
           && bytes[^1] == 0xd9;
}
