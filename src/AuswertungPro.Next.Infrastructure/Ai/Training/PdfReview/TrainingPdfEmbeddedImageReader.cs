using System.Buffers.Binary;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

/// <summary>
/// Liest ein einzelnes PDF-Foto fail-closed. JPEG-Farbraum, PDF-Decode-Regel
/// und deklarierte Abmessungen werden vor der Trainingsablage geprueft.
/// </summary>
internal sealed class TrainingPdfEmbeddedImageReader
{
    internal const long MaximumPhotoPixels = 50_000_000;

    private readonly ITrainingPdfJpegColorNormalizer? _jpegColorNormalizer;

    public TrainingPdfEmbeddedImageReader(
        ITrainingPdfJpegColorNormalizer? jpegColorNormalizer)
    {
        _jpegColorNormalizer = jpegColorNormalizer;
    }

    public bool TryRead(
        IPdfImage image,
        out byte[] bytes,
        out string extension,
        out long pixelCount)
    {
        pixelCount = (long)image.WidthInSamples * image.HeightInSamples;
        if (image.WidthInSamples <= 0
            || image.HeightInSamples <= 0
            || pixelCount > MaximumPhotoPixels)
        {
            return Fail(
                out bytes,
                out extension,
                out pixelCount);
        }

        bytes = image.RawBytes.ToArray();
        extension = ".jpg";
        if (HasCompleteJpegEnvelope(bytes))
        {
            var isValidatedJpeg = false;
            try
            {
                TrainingExportImageFormatValidator.Validate(
                    bytes,
                    "photo.jpg",
                    image.WidthInSamples,
                    image.HeightInSamples);
                isValidatedJpeg = true;
            }
            catch (TrainingExportPlanException)
            {
                // Bei einem nur scheinbaren JPEG darf PdfPig noch versuchen,
                // den PDF-Bildstrom sicher als PNG zu dekodieren.
            }

            if (isValidatedJpeg)
                return TryReadValidatedJpeg(
                    image,
                    ref bytes,
                    ref extension,
                    out pixelCount);
        }

        return TryReadPdfPigPng(
            image,
            out bytes,
            out extension,
            out pixelCount);
    }

    private bool TryReadValidatedJpeg(
        IPdfImage image,
        ref byte[] bytes,
        ref string extension,
        out long pixelCount)
    {
        pixelCount = (long)image.WidthInSamples * image.HeightInSamples;
        try
        {
            if (TryCreateJpegColorNormalizationRequest(
                    image,
                    bytes,
                    out var normalizationRequest))
            {
                if (normalizationRequest is null
                    || _jpegColorNormalizer is null
                    || !_jpegColorNormalizer.TryNormalizeToRgbPng(
                        normalizationRequest,
                        out var normalizedPng))
                {
                    return Fail(
                        out bytes,
                        out extension,
                        out pixelCount);
                }

                TrainingExportImageFormatValidator.Validate(
                    normalizedPng,
                    "photo.png",
                    image.WidthInSamples,
                    image.HeightInSamples);
                bytes = normalizedPng;
                extension = ".png";
            }

            return true;
        }
        catch (TrainingExportPlanException)
        {
            // Ein validiertes JPEG mit unbekannter oder fehlerhafter
            // PDF-Farbregel darf nie ungefiltert ins Training gelangen.
            return Fail(
                out bytes,
                out extension,
                out pixelCount);
        }
    }

    private static bool TryReadPdfPigPng(
        IPdfImage image,
        out byte[] bytes,
        out string extension,
        out long pixelCount)
    {
        if (!image.TryGetPng(out var png))
        {
            return Fail(
                out bytes,
                out extension,
                out pixelCount);
        }

        bytes = png;
        extension = ".png";
        pixelCount = (long)image.WidthInSamples * image.HeightInSamples;
        try
        {
            TrainingExportImageFormatValidator.Validate(
                bytes,
                "photo.png",
                image.WidthInSamples,
                image.HeightInSamples);
            return true;
        }
        catch (TrainingExportPlanException)
        {
            return Fail(
                out bytes,
                out extension,
                out pixelCount);
        }
    }

    private static bool TryCreateJpegColorNormalizationRequest(
        IPdfImage image,
        byte[] jpegBytes,
        out TrainingPdfJpegColorNormalizationRequest? request)
    {
        request = null;
        if (!TryResolveJpegColorModel(
                image.ColorSpaceDetails,
                out var colorModel,
                out var componentCount))
        {
            // Bei einem unbekannten Farbraum ist nicht beweisbar, dass ein
            // normaler JPEG-Decoder dieselben Farben wie das PDF erzeugt.
            return true;
        }

        var declaredDecode = image.Decode;
        var hasDeclaredDecode = declaredDecode.Count > 0;
        var invertSourceSamples = false;
        if (colorModel == TrainingPdfJpegColorModel.Cmyk
            && !TryResolveCmykSourceInversion(
                jpegBytes,
                out invertSourceSamples))
        {
            return true;
        }

        var requiresNormalization =
            colorModel == TrainingPdfJpegColorModel.Cmyk
            || hasDeclaredDecode
               && !IsIdentityDecode(declaredDecode, componentCount);
        if (!requiresNormalization)
            return false;

        var effectiveDecode = hasDeclaredDecode
            ? declaredDecode.ToArray()
            : BuildIdentityDecode(componentCount);
        request = new TrainingPdfJpegColorNormalizationRequest(
            jpegBytes,
            image.WidthInSamples,
            image.HeightInSamples,
            image.BitsPerComponent,
            colorModel,
            effectiveDecode,
            invertSourceSamples);
        return true;
    }

    private static bool TryResolveCmykSourceInversion(
        ReadOnlySpan<byte> jpegBytes,
        out bool invertSourceSamples)
    {
        invertSourceSamples = false;
        if (!TryReadAdobeJpegColorTransform(
                jpegBytes,
                out var adobeTransform))
        {
            return false;
        }

        switch (adobeTransform)
        {
            case 0:
            case 2:
                // WIC liefert bereits benutzer-normalisierte CMYK-Komponenten.
                // Erst Quellsamples rekonstruieren, danach PDF-/Decode anwenden.
                invertSourceSamples = true;
                return true;
            case null:
                // Ohne Adobe-Marker ist die Polung der vier JPEG-Kanaele
                // nicht beweisbar. Das Foto wird deshalb fail-closed ausgelassen.
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
            if (segmentLength < 2
                || offset + segmentLength > jpegBytes.Length)
            {
                return false;
            }

            var payload = jpegBytes[
                (offset + 2)..(offset + segmentLength)];
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

    private static bool TryResolveJpegColorModel(
        ColorSpaceDetails? details,
        out TrainingPdfJpegColorModel colorModel,
        out int componentCount)
    {
        colorModel = default;
        componentCount = details?.NumberOfColorComponents ?? 0;
        if (details is null)
            return false;

        if (componentCount == 4
            && details.Type == ColorSpace.DeviceCMYK)
        {
            colorModel = TrainingPdfJpegColorModel.Cmyk;
            return true;
        }

        if (componentCount == 3
            && details.Type == ColorSpace.DeviceRGB)
        {
            colorModel = TrainingPdfJpegColorModel.Rgb;
            return true;
        }

        if (componentCount == 1
            && details.Type == ColorSpace.DeviceGray)
        {
            colorModel = TrainingPdfJpegColorModel.Gray;
            return true;
        }

        return false;
    }

    private static bool IsIdentityDecode(
        IReadOnlyList<decimal> decode,
        int componentCount)
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

    private static IReadOnlyList<decimal> BuildIdentityDecode(
        int componentCount)
    {
        var decode = new decimal[componentCount * 2];
        for (var index = 0; index < decode.Length; index += 2)
        {
            decode[index] = 0m;
            decode[index + 1] = 1m;
        }

        return decode;
    }

    private static bool HasCompleteJpegEnvelope(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 4
           && bytes[0] == 0xff
           && bytes[1] == 0xd8
           && bytes[^2] == 0xff
           && bytes[^1] == 0xd9;

    private static bool Fail(
        out byte[] bytes,
        out string extension,
        out long pixelCount)
    {
        bytes = [];
        extension = string.Empty;
        pixelCount = 0;
        return false;
    }
}
