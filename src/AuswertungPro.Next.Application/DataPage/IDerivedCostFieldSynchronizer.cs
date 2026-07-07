using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Zieht die abgeleiteten Kostenfelder aller Haltungen nach der Sanieren-Regel nach
/// (nur <c>Sanieren_JaNein=Ja</c> zaehlt; Nein/leer → Felder leer). Reine Logik, keine I/O.
/// </summary>
public interface IDerivedCostFieldSynchronizer
{
    /// <summary>Synchronisiert alle Records von <paramref name="project"/> gegen <paramref name="store"/>.
    /// Rueckgabe: Anzahl geaenderter Records.</summary>
    int Sync(Project project, ProjectCostStore store);
}
