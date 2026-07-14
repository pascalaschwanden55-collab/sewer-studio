namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erkennt das Herstellerformat und die massgeblichen Dateien eines Kanal-Exportordners.
/// </summary>
public interface IKanalExportDetectionService
{
    KanalExportDetection Detect(string sourceFolder);
}
