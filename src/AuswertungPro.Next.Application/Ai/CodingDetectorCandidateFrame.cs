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
            return "Kandidatenkennung fehlt oder ist ungültig.";
        if (!CodingLocalizedDetection.IsSha256(ExpectedWeightSha256)
            || !string.Equals(ExpectedWeightSha256, ActualWeightSha256, StringComparison.OrdinalIgnoreCase))
            return "Kandidatengewichte stimmen nicht mit dem vorher gewählten SHA-256 überein.";
        if (!CodingLocalizedDetection.IsSha256(ImageSha256)
            || !string.Equals(ImageSha256, Convert.ToHexString(SHA256.HashData(image)), StringComparison.OrdinalIgnoreCase))
            return "Kandidatenantwort gehört nicht zum analysierten Bild (SHA-256).";
        if (Detections is null || Detections.Any(d => d is null)
            || !double.IsFinite(InferenceTimeMs) || InferenceTimeMs < 0)
            return "Kandidatenantwort ist unvollständig oder technisch ungültig.";
        return null;
    }
}
