namespace AuswertungPro.Next.Application.Map;

public interface IKatasterXtfPathResolver
{
    string Resolve(string? explicitPath, string? directoryPath);

    string? TryFindKatasterXtf(string? directoryPath);
}
