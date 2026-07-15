namespace AuswertungPro.Next.UI.Ai;

public interface IEvidenceFrameRenderer
{
    bool SaveAnnotatedFrame(
        string sourceImagePath,
        string outputImagePath,
        EvidenceFrameAnnotation annotation);
}
