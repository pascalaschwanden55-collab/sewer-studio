namespace AuswertungPro.Next.Application.Ai;

public enum CodingDetectionSource { Dino, Yolo, YoloAndDino }

/// <summary>Herkunft einer SAM-Eingabebox; Modellwerte bleiben getrennte Belege.</summary>
public sealed record CodingLocalizedDetection(
    CodingDetectionSource Source,
    string Label,
    double X1, double Y1, double X2, double Y2,
    double? YoloConfidence = null,
    double? DinoConfidence = null,
    string? YoloArtifactSha256 = null,
    string? VsaMainCode = null,
    bool RequiresReview = false,
    string? YoloModelName = null,
    bool DevelopmentCandidate = false)
{
    /// <summary>Echter Modell-/Maskenfehler, getrennt von der normalen Kandidaten-Pruefpflicht.</summary>
    public bool HasTechnicalFailure { get; init; }
    public bool HasYolo => Source is CodingDetectionSource.Yolo or CodingDetectionSource.YoloAndDino;
    public double Confidence => YoloConfidence ?? DinoConfidence ?? 0;
    public string SourceName => Source switch
    {
        CodingDetectionSource.Yolo => "YOLO",
        CodingDetectionSource.Dino => "DINO",
        CodingDetectionSource.YoloAndDino => "YOLO + DINO",
        _ => "unbekannt"
    };

    public bool HasValidEvidence =>
        !string.IsNullOrWhiteSpace(Label)
        && double.IsFinite(X1) && double.IsFinite(Y1) && double.IsFinite(X2) && double.IsFinite(Y2)
        && X2 > X1 && Y2 > Y1
        && (!DevelopmentCandidate || RequiresReview)
        && (Source switch
        {
            CodingDetectionSource.Dino => ValidConfidence(DinoConfidence) && YoloConfidence is null,
            CodingDetectionSource.Yolo => ValidYolo() && DinoConfidence is null,
            CodingDetectionSource.YoloAndDino => ValidYolo() && ValidConfidence(DinoConfidence),
            _ => false
        });

    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool ValidConfidence(double? value) => value.HasValue && double.IsFinite(value.Value) && value is >= 0 and <= 1;
    private bool ValidYolo() => ValidConfidence(YoloConfidence) && IsSha256(YoloArtifactSha256)
        && X1 >= 0 && Y1 >= 0 && !string.IsNullOrWhiteSpace(VsaMainCode);
}
