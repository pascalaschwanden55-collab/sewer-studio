using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis der Fotozuordnung zu Haltungen und Beobachtungen.</summary>
public sealed record ProjectPhotoAssignmentResult(
    int HoldingsMatched,
    int PhotosAssigned,
    int PhotosCopied,
    int UnmatchedFiles,
    IReadOnlyList<string> Messages);

/// <summary>Ordnet Fotos aus einem Quellordner einem bestehenden Projekt zu.</summary>
public interface IProjectPhotoAssignmentService
{
    ProjectPhotoAssignmentResult AssignFromFolder(
        string projectFolder,
        string sourceFolder,
        Project project);
}
