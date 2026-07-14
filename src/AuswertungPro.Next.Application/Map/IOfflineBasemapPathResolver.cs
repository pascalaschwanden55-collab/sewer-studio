namespace AuswertungPro.Next.Application.Map;

public interface IOfflineBasemapPathResolver
{
    string? Resolve(string? configuredPath);
}
