namespace AuswertungPro.Next.Application.Maintenance;

/// <summary>
/// Sucht vom Programm- und Arbeitsordner aus die Wurzel der SewerStudio-Installation.
/// </summary>
public interface IProgramRootLocator
{
    string FindProgramRoot(string appBaseDirectory, string currentDirectory);
}
