using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Alle Quellpfade der Datensicherung. Wird zur Laufzeit aufgeloest
/// (KnowledgeRoot ueber Env-Var, AppData ueber Resolver) und ist in Tests
/// komplett injizierbar — Tests fassen NIE echte Datenpfade an.
/// </summary>
/// <param name="RepoRoot">Wurzel des Quellcode-Repos (null = nicht gefunden, Komponente wird gemeldet statt zu crashen).</param>
/// <param name="KnowledgeRoot">Aufgeloester KnowledgeRoot (z. B. C:\KI_BRAIN).</param>
/// <param name="LocalSewerStudioDir">%LOCALAPPDATA%\SewerStudio.</param>
/// <param name="RoamingSewerStudioDir">%APPDATA%\SewerStudio.</param>
/// <param name="RoamingAuswertungProDir">%APPDATA%\AuswertungPro (Legacy-Stores).</param>
/// <param name="DesktopDir">Desktop des Nutzers (Startskripte).</param>
/// <param name="AppVersion">App-Version fuer das Manifest.</param>
/// <param name="EnvironmentVariables">Snapshot der SEWERSTUDIO_*/SEWER_*-Umgebungsvariablen.</param>
public sealed record FullBackupSources(
    string? RepoRoot,
    string KnowledgeRoot,
    string LocalSewerStudioDir,
    string RoamingSewerStudioDir,
    string RoamingAuswertungProDir,
    string DesktopDir,
    string AppVersion,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    IReadOnlyList<string>? ProjectRoots = null,
    bool IncludeProjectVideos = false);
