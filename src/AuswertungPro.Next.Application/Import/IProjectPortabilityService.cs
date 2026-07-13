using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis beim Umstellen eines Projekts auf portable Medienpfade.</summary>
public sealed record ProjectPortabilityResult(
    int RelinkedPaths,
    int FotosCopied,
    int Unresolved,
    IReadOnlyList<string> Messages);

/// <summary>
/// Stellt Medienpfade eines Projekts auf die im Projekt liegenden Kopien um.
/// </summary>
public interface IProjectPortabilityService
{
    ProjectPortabilityResult MakePortable(string projectFolder, Project project, bool dryRun = false);
}
