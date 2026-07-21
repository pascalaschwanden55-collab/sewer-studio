using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Berechnet eine stabile Inhalts-Signatur eines Projekts. Instabile Meta-Felder
/// (ModifiedAtUtc, Dirty, LastCommittedImportTxId) fliessen NICHT ein, damit die Signatur
/// nur auf echte Datenaenderungen reagiert. Grundlage der U4-Konflikterkennung: der
/// Import vergleicht die Signatur des Live-Projekts vor und nach dem Lauf.
/// </summary>
public interface IProjectContentSignature
{
    /// <summary>Hexadezimaler SHA-256 ueber den normalisierten Projekt-Inhalt.</summary>
    string Compute(Project project);
}
