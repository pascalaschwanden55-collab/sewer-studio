using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Projects;

public interface IProjectRepository
{
    Result<Project> Load(string path);
    Result Save(Project project, string path);
}
