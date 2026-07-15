namespace AuswertungPro.Next.UI.Ai;

/// <summary>Kompatible statische Fassade fuer bestehende Aufrufer.</summary>
public static class EvidenceFrameRenderer
{
    private static readonly IEvidenceFrameRenderer DefaultRenderer = new EvidenceFrameImageRenderer();

    public static bool SaveAnnotatedFrame(
        string sourceImagePath,
        string outputImagePath,
        EvidenceFrameAnnotation annotation)
        => DefaultRenderer.SaveAnnotatedFrame(sourceImagePath, outputImagePath, annotation);
}
