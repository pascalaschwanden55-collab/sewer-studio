using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

public interface ISchachtFileTargetResolver
{
    string? ResolvePdfPath(SchachtRecord record, string? projectFilePath);

    string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath);
}
