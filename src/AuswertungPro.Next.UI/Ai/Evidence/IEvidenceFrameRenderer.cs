namespace AuswertungPro.Next.UI.Ai.Evidence;

public interface IEvidenceFrameRenderer
{
    bool SaveAnnotatedFrame(
        string sourceImagePath,
        string outputImagePath,
        EvidenceFrameAnnotation annotation);
}
