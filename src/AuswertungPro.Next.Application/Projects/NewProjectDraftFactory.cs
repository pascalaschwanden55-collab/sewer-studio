using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Erzeugt ein Draft-Projekt für die Neuanlage. Der Auftraggeber ist fast immer „Abwasser Uri" und
/// wird daher vorbelegt (frei änderbar). Reiner Helfer, damit der Default unit-testbar ist.
/// </summary>
public static class NewProjectDraftFactory
{
    public const string DefaultAuftraggeber = "Abwasser Uri";

    public static Project Create()
    {
        var project = new Project { Name = string.Empty };
        project.Metadata["Auftraggeber"] = DefaultAuftraggeber;
        return project;
    }
}
