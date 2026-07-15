namespace AuswertungPro.Next.Application.Import;

/// <summary>Ersetzt eine PDF erst nach erfolgreicher Sicherheits- und Lesbarkeitspruefung.</summary>
public interface IAtomicPdfFileReplacer
{
    void ReplaceValidated(string generatedPdfPath, string targetPdfPath);
}
