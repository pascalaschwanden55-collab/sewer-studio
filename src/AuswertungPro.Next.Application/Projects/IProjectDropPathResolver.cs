namespace AuswertungPro.Next.Application.Projects;

public interface IProjectDropPathResolver
{
    string? ResolveProjectFile(string path);
}
