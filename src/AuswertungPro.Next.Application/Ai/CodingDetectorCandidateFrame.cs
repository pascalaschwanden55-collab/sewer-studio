using System.Security.Cryptography;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Ausdruecklicher Entwicklungsbeleg fuer ein einzelnes Bild, keine Modellfreigabe.
/// Der Messhost bindet die Antwort an vorher gewaehlte, tatsaechlich gehashte Gewichte.
/// Technisch fehlgeschlagene Antworten duerfen diesen Beleg nicht erzeugen.
/// </summary>
public sealed record CodingDetectorCandidateFrame(
    string CandidateId,
    string ExpectedWeightSha256,
    string ActualWeightSha256,
    string ImageSha256,
    IReadOnlyList<YoloDetectionDto> Detections,
    double InferenceTimeMs = 0)
{
    public string? ValidateForImage(ReadOnlySpan<byte> image)
    {
        if (string.IsNullOrWhiteSpace(CandidateId) || CandidateId.Length > 200)
            return "Kandidatenkennung fehlt oder ist ungueltig.";
        if (!CodingLocalizedDetection.IsSha256(ExpectedWeightSha256)
            || !string.Equals(ExpectedWeightSha256, ActualWeightSha256, StringComparison.OrdinalIgnoreCase))
            return "Kandidatengewichte stimmen nicht mit dem vorher gewaehlten SHA-256 ueberein.";
        if (!CodingLocalizedDetection.IsSha256(ImageSha256)
            || !string.Equals(ImageSha256, Convert.ToHexString(SHA256.HashData(image)), StringComparison.OrdinalIgnoreCase))
            return "Kandidatenantwort gehoert nicht zum analysierten Bild (SHA-256).";
        if (Detections is null || Detections.Any(d => d is null)
            || !double.IsFinite(InferenceTimeMs) || InferenceTimeMs < 0)
            return "Kandidatenantwort ist unvollstaendig oder technisch ungueltig.";
        return null;
    }
}
