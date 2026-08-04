using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Rekonstruiert die PDF-DCT-Farbkanaele und wendet danach die PDF-Decode-Werte an.
/// Das ist besonders fuer Adobe-YCCK/DeviceCMYK noetig: Ein normales Laden
/// des JPEGs verwirft die PDF-Farbregel und zeigt das Foto mit falschen Farben.
/// </summary>
internal sealed class TrainingPdfJpegColorNormalizer
    : ITrainingPdfJpegColorNormalizer
{
    // CMYK-Quelle und BGRA-Ziel liegen waehrend der Umwandlung gleichzeitig
    // im Speicher. 12 MP halten den Spitzenbedarf auch bei grossen Fotos begrenzt.
    private const long MaximumPixels = 12_000_000;

    public bool TryNormalizeToRgbPng(
        TrainingPdfJpegColorNormalizationRequest request,
        out byte[] pngBytes)
    {
        pngBytes = [];
        if (!IsValidRequest(request, out var componentCount))
            return false;

        try
        {
            using var input = new MemoryStream(
                request.JpegBytes,
                writable: false);
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
                    request.ColorModel,
                    componentCount,
                    out var components,
                    out var componentStride))
            {
                return false;
            }

            var outputStride = checked(request.PixelWidth * 4);
            var output = new byte[checked(outputStride * request.PixelHeight)];
            ConvertToBgra(
                request,
                components,
                componentStride,
                output,
                outputStride);

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

    private static bool IsValidRequest(
        TrainingPdfJpegColorNormalizationRequest? request,
        out int componentCount)
    {
        componentCount = request?.ColorModel switch
        {
            TrainingPdfJpegColorModel.Gray => 1,
            TrainingPdfJpegColorModel.Rgb => 3,
            TrainingPdfJpegColorModel.Cmyk => 4,
            _ => 0,
        };
        if (request is null
            || componentCount == 0
            || (request.InvertSourceSamples
                && request.ColorModel != TrainingPdfJpegColorModel.Cmyk)
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

        return true;
    }

    private static bool TryReadColorComponents(
        BitmapSource frame,
        TrainingPdfJpegColorModel colorModel,
        int componentCount,
        out byte[] components,
        out int stride)
    {
        components = [];
        stride = 0;
        PixelFormat targetFormat;
        switch (colorModel)
        {
            case TrainingPdfJpegColorModel.Gray:
                if (frame.Format == PixelFormats.Cmyk32)
                    return false;
                targetFormat = PixelFormats.Gray8;
                break;
            case TrainingPdfJpegColorModel.Rgb:
                if (frame.Format == PixelFormats.Cmyk32)
                    return false;
                targetFormat = PixelFormats.Bgr24;
                break;
            case TrainingPdfJpegColorModel.Cmyk:
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
        TrainingPdfJpegColorNormalizationRequest request,
        byte[] source,
        int sourceStride,
        byte[] target,
        int targetStride)
    {
        var sourceComponents = request.ColorModel switch
        {
            TrainingPdfJpegColorModel.Gray => 1,
            TrainingPdfJpegColorModel.Rgb => 3,
            TrainingPdfJpegColorModel.Cmyk => 4,
            _ => throw new InvalidDataException(
                "Unbekanntes PDF-JPEG-Farbmodell."),
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

                switch (request.ColorModel)
                {
                    case TrainingPdfJpegColorModel.Gray:
                        red = green = blue = Decode(
                            source[sourceOffset],
                            request.Decode,
                            componentIndex: 0);
                        break;
                    case TrainingPdfJpegColorModel.Rgb:
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
                    case TrainingPdfJpegColorModel.Cmyk:
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
                        throw new InvalidDataException(
                            "Unbekanntes PDF-JPEG-Farbmodell.");
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
        var value = minimum
                    + normalizedSample * (maximum - minimum);
        return Math.Clamp((double)value, 0d, 1d);
    }

    private static byte ToByte(double value)
        => (byte)Math.Clamp(
            (int)Math.Round(
                value * byte.MaxValue,
                MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);
}
