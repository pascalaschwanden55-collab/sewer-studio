using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

public interface ISchachtProQrImportService
{
    Result<ImportStats> ImportImage(string path, Project project, ImportRunContext? context = null);
}

/// <summary>Plattformadapter: liest QR-Texte aus einer Bilddatei, ohne die Quelle zu veraendern.</summary>
public interface IQrImageReader
{
    IReadOnlyList<string> Read(string path, CancellationToken cancellationToken);
}
